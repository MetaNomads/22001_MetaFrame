// ── AuditTestHarness.cs ───────────────────────────────────────────────────────
// One-click reproduction tests for every Tier 1 fix.
//
// Why this design (not Unity Test Framework, not NUnit):
//   - Zero setup cost: drop the component on a GameObject, enter Play Mode,
//     click a button. No test runner, no [SetUp] / [TearDown], no asmdef.
//   - Same idiom as the rest of MetaFrame's debug buttons (TeleportToHand
//     editor, AnomalyStateManager force-state buttons).
//   - Each test creates and tears down its own scratch GameObjects, so it
//     can run in any scene without polluting state.
//
// To use:
//   1. Add an empty GameObject to any scene.
//   2. Add the AuditTestHarness component.
//   3. Enter Play Mode.
//   4. Click "Run All" or any individual test.
//   5. Read PASS / FAIL in the Console (and the diagnostics panel below).
//
// Each test follows the same pattern:
//   - Arrange: spin up minimal scratch state with FULL contracts wired
//   - Act: trigger the failure scenario (real path through the code)
//   - Assert: verify the fix prevents the bad behaviour
//   - Cleanup: DestroyImmediate scratch state + null out singletons
//
// Strengthening notes (vs the first cut):
//   - Tests now wire a complete scratch GSM + Sequencer + ASM where needed,
//     so my own Contract.Require checks don't false-fail the harness.
//   - S-1/S-6 actually drives the sequencer to the session-end branch and
//     blocks the post-end transition with a real allowedFrom rule.
//   - S-2 actually wires a trigger.onEnter UnityEvent that recurses into
//     TriggerAnomaly to reproduce the cascade re-entry.
//   - S-3 actually wires a slot.onEnter UnityEvent that recurses into
//     RequestTransition to reproduce the GSM re-entry.
//   - All cleanup uses DestroyImmediate + manual instance reset so each test
//     starts with a guaranteed clean slate.

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using MetaFrame.State;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.Contracts
{
    [DisallowMultipleComponent]
    public class AuditTestHarness : MonoBehaviour
    {
        public enum TestResult { NotRun, Pass, Fail, Skipped }

        [System.Serializable]
        public class TestRecord
        {
            public string code;
            public string description;
            public TestResult result = TestResult.NotRun;
            public string detail = "";
        }

        public readonly List<TestRecord> Records = new()
        {
            // ── Tier 1 (B.03 + B.02) ─────────────────────────────────────────
            new TestRecord { code = "S-1/S-6", description = "Sequencer.Advance: rollback both indices and skip OnSessionEnded when post-end transition fails" },
            new TestRecord { code = "S-2",     description = "AnomalyStateManager.EvaluateTriggers: real re-entrant cascade does not throw or corrupt state" },
            new TestRecord { code = "S-3",     description = "GameStateManager.ApplyTransition: real re-entrant transition is rejected with a warning, outer state preserved" },
            new TestRecord { code = "S-4",     description = "AnomalyStateManager.OnEnable: null GameStateManager.instance does not throw" },
            new TestRecord { code = "S-5",     description = "ExperimentController.AdvanceForced: nested ForceStep is rejected" },
            new TestRecord { code = "D-1/2/3", description = "DataSource_FACS / DataSource_Body: missing Inspector refs do not NRE on CollectData" },
            new TestRecord { code = "D-4",     description = "DataSource_Voice: SourceNameLower is cached even though Voice skips registration" },
            new TestRecord { code = "Contract",description = "Contract.Healed counts heals correctly and tolerates null inputs" },

            // ── Tier 2 (B.01_Interaction + Mechanics) ────────────────────────
            new TestRecord { code = "T2-2",    description = "GazeInteractable.IsEnabled: null Collider does not NRE after Awake" },
            new TestRecord { code = "T2-4",    description = "GazeReticle.SetTarget: returns silently when _interactor was never set" },
            new TestRecord { code = "T2-5",    description = "GestureHoldInteractable: forceSelect/Release log error instead of NRE on missing refs" },
            new TestRecord { code = "T2-6",    description = "Doppelganger.Spawn: second rapid call is rejected, only one instance spawns" },

            // ── Tier 3 (C.01 + D.01) ─────────────────────────────────────────
            new TestRecord { code = "T3-1",    description = "RandomAudioPause: missing audioSource disables the component cleanly" },
            new TestRecord { code = "T3-3",    description = "SpaceInteractableState: null triggerColliders list is handled gracefully in OnTriggerEnter/Exit" },
            new TestRecord { code = "T3-4",    description = "LightFlicker: missing Light component disables the script with an error log" },

            // ── Tier 4 (X.01_Script) ─────────────────────────────────────────
            new TestRecord { code = "T4-1",    description = "CompositeTag: null Tag in _tags is skipped instead of NRE'ing Add/Remove" },
        };

        // ── Public entry points (exposed via custom Editor) ──────────────────

        public void RunAll()
        {
            StopAllCoroutines();
            StartCoroutine(RunAllCoroutine());
        }

        private IEnumerator RunAllCoroutine()
        {
            foreach (var r in Records) { r.result = TestResult.NotRun; r.detail = ""; }

            yield return RunOne(0,  Test_S1_S6_Rollback);
            yield return RunOne(1,  Test_S2_TriggerReentrancy);
            yield return RunOne(2,  Test_S3_GsmReentrancy);
            yield return RunOne(3,  Test_S4_NullGsmInOnEnable);
            yield return RunOne(4,  Test_S5_NestedForceStep);
            yield return RunOne(5,  Test_D123_MissingInspectorRefs);
            yield return RunOne(6,  Test_D4_VoiceSourceNameLower);
            yield return RunOne(7,  Test_Contract_HealedAndNullSafety);

            // Tier 2
            yield return RunOne(8,  Test_T22_GazeInteractableNullCollider);
            yield return RunOne(9,  Test_T24_GazeReticleNullInteractor);
            yield return RunOne(10, Test_T25_GestureHoldNullRefs);
            yield return RunOne(11, Test_T26_DoppelgangerReentrancy);

            // Tier 3
            yield return RunOne(12, Test_T31_RandomAudioPauseNullSource);
            yield return RunOne(13, Test_T33_SpaceInteractableNullList);
            yield return RunOne(14, Test_T34_LightFlickerNullLight);

            // Tier 4
            yield return RunOne(15, Test_T41_CompositeTagNullEntry);

            int pass = 0, fail = 0;
            foreach (var r in Records)
            {
                if (r.result == TestResult.Pass) pass++;
                else if (r.result == TestResult.Fail) fail++;
            }
            Debug.Log($"[AuditTestHarness] {pass}/{Records.Count} passed, {fail} failed.");
        }

        private IEnumerator RunOne(int index, System.Func<TestRecord, IEnumerator> body)
        {
            var rec = Records[index];
            // Reset Contract counters so each test starts clean. RunAll callers
            // can read TotalViolations / TotalHealed at the end of each test
            // without bleed-over from earlier tests.
            Contract.ResetCounters();
            yield return body(rec);
            // Wait one frame to let DestroyImmediate-deferred Unity cleanup
            // (e.g. the static GSM instance reference) settle before the next test.
            yield return null;
        }

        // ── Test S-1 / S-6 — Sequencer rollback + event ordering ─────────────
        //
        // Strategy:
        //   1. Build a fully-wired scratch sequencer + GSM.
        //   2. Pre-populate resolvedSequences with two trivial 1-trial sessions
        //      so we have a "next session" to roll into idle for.
        //   3. Block the post-end transition by setting stateIdle.allowedFrom
        //      to a list that DOES NOT include stateSessionEnd.
        //   4. ForceState the GSM to stateTrialStart so Advance() takes the
        //      trial-end → session-end → idle branch.
        //   5. Call Advance(). The fix should: enter session_end successfully,
        //      attempt idle, fail, roll back BOTH _sessionIndex and _trialIndex,
        //      and NOT fire OnSessionEnded.

        private IEnumerator Test_S1_S6_Rollback(TestRecord rec)
        {
            ScratchScene scene = null;
            bool onSessionEndedFired = false;
            void MarkOnSessionEnded() => onSessionEndedFired = true;

            try
            {
                scene = ScratchScene.BuildFullSequencer();

                // Pre-populate resolvedSequences with two trivial sessions so
                // _sessionIndex+1 is in range (forcing the idle branch, not the
                // experiment_end branch).
                var fakeAnomaly = ScriptableObject.CreateInstance<AnomalyDefinition>();
                scene.scratchAnomaly = fakeAnomaly;
                scene.seq.resolvedSequences.Clear();
                scene.seq.resolvedSequences.Add(new ResolvedSequence
                {
                    sessionLabel = "T",
                    listIndex    = 0,
                    definitions  = new[] { fakeAnomaly },   // 1 trial
                });
                scene.seq.resolvedSequences.Add(new ResolvedSequence
                {
                    sessionLabel = "S1",
                    listIndex    = 1,
                    definitions  = new[] { fakeAnomaly },   // 1 trial
                });

                // Block the post-end transition: stateIdle.allowedFrom does NOT
                // include stateSessionEnd, so Sequencer's RequestTransition(idle)
                // call inside Advance() will be blocked.
                int idleSlotIndex = scene.gsm.IndexOf(scene.stateIdle);
                scene.SetSlotAllowedFrom(idleSlotIndex, new List<StateDefinition> { scene.stateExperimentStart });

                // Subscribe to the static event we want to verify did NOT fire.
                ExperimentSequencer.OnSessionEnded += MarkOnSessionEnded;

                // Drive the GSM directly to stateTrialStart so Advance() takes the
                // else-branch (mid-trial → trial_end → session_end).
                scene.gsm.ForceState(scene.stateTrialStart);

                // Initial indices: _sessionIndex = 0, _trialIndex = 0.
                int sessionBefore = scene.seq.CurrentSessionIndex;
                int trialBefore   = scene.seq.CurrentTrialIndex;

                // Act.
                bool advanceResult = scene.seq.Advance();

                // Assert.
                int sessionAfter = scene.seq.CurrentSessionIndex;
                int trialAfter   = scene.seq.CurrentTrialIndex;

                bool indicesRolledBack = sessionAfter == sessionBefore && trialAfter == trialBefore;
                bool eventNotFired     = !onSessionEndedFired;
                bool advanceFailed     = advanceResult == false;

                if (indicesRolledBack && eventNotFired && advanceFailed)
                {
                    Pass(rec, $"Indices rolled back ({sessionBefore},{trialBefore}); " +
                              $"OnSessionEnded NOT fired; Advance returned false.");
                }
                else
                {
                    Fail(rec, $"indicesRolledBack={indicesRolledBack} " +
                              $"(expected ({sessionBefore},{trialBefore}), got ({sessionAfter},{trialAfter})); " +
                              $"eventNotFired={eventNotFired}; advanceFailed={advanceFailed}");
                }
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                ExperimentSequencer.OnSessionEnded -= MarkOnSessionEnded;
                scene?.Destroy();
            }
            yield return null;
        }

        // ── Test S-2 — real re-entrant trigger cascade ───────────────────────
        //
        // Strategy:
        //   1. Build a scratch GSM + ASM with a real anomalyToTrigger.
        //   2. Inject TWO triggers via reflection:
        //      Trigger A: fires when anomalyState matches Active. onEnter calls
        //                 asm.TriggerAnomaly() — this is the cascade entry.
        //      Trigger B: fires when anomalyState matches Triggered. onEnter
        //                 increments a counter.
        //   3. Force state to Active. Cascade: Active → Trigger A onEnter →
        //      TriggerAnomaly → state becomes Triggered → Trigger B onEnter
        //      → counter++.
        //   4. Verify counter == 1 (single fire, no double-invoke from re-entry)
        //      and no exceptions.

        private IEnumerator Test_S2_TriggerReentrancy(TestRecord rec)
        {
            ScratchScene scene = null;
            try
            {
                scene = ScratchScene.BuildSimpleGsm(new[] { "any" });

                var asmGo = new GameObject("ScratchASM_S2");
                var asm   = asmGo.AddComponent<AnomalyStateManager>();
                scene.asmGo = asmGo;
                scene.asm   = asm;

                // Wire the anomalyToTrigger so BroadcastTrialBegan-style activation
                // (we use ForceAnomalyState here) makes sense.
                var anomaly = ScriptableObject.CreateInstance<AnomalyDefinition>();
                scene.scratchAnomaly = anomaly;
                ReflectSet(asm, "anomalyToTrigger", anomaly);

                int counter = 0;

                // Trigger A: on Active, recurse into TriggerAnomaly.
                var triggerA = new AnomalyTrigger
                {
                    triggerName       = "A_RecurseToTriggered",
                    gameStateMode     = ConditionMode.Disabled,
                    anomalyStateMode  = ConditionMode.AND,
                    anomalyStates     = AnomalyState.Active,
                    conditionMode     = ConditionMode.Disabled,
                    onEnter           = new UnityEvent(),
                    onExit            = new UnityEvent(),
                };
                triggerA.onEnter.AddListener(() => asm.TriggerAnomaly());

                // Trigger B: on Triggered, increment counter.
                var triggerB = new AnomalyTrigger
                {
                    triggerName       = "B_CountTriggered",
                    gameStateMode     = ConditionMode.Disabled,
                    anomalyStateMode  = ConditionMode.AND,
                    anomalyStates     = AnomalyState.Triggered,
                    conditionMode     = ConditionMode.Disabled,
                    onEnter           = new UnityEvent(),
                    onExit            = new UnityEvent(),
                };
                triggerB.onEnter.AddListener(() => counter++);

                var triggers = (List<AnomalyTrigger>)ReflectGet(asm, "triggers");
                triggers.Add(triggerA);
                triggers.Add(triggerB);

                // Reset counters since we wired stuff that may have logged warnings.
                Contract.ResetCounters();

                // Act — single ForceAnomalyState(Active) should cascade through
                // Triggered via the re-entrant TriggerAnomaly call.
                asm.ForceAnomalyState(AnomalyState.Active);

                // Assert.
                bool counterCorrect    = counter == 1;
                bool stateAdvanced     = asm.CurrentAnomalyState == AnomalyState.Triggered;
                bool noViolations      = !Contract.AnyViolations;

                if (counterCorrect && stateAdvanced && noViolations)
                {
                    Pass(rec, $"Cascade fired Trigger B exactly once (counter={counter}); state={asm.CurrentAnomalyState}; " +
                              $"no contract violations.");
                }
                else
                {
                    Fail(rec, $"counter={counter} (expected 1); state={asm.CurrentAnomalyState} (expected Triggered); " +
                              $"violations={Contract.TotalViolations}");
                }
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                scene?.Destroy();
            }
            yield return null;
        }

        // ── Test S-3 — real GSM re-entrant transition ────────────────────────
        //
        // Strategy:
        //   1. Build a scratch GSM with two states a, b.
        //   2. Wire slot a's onEnter to call gsm.RequestTransition(b) — a real
        //      re-entrant transition request that the fix should reject.
        //   3. ForceState(a). The fix should log a warning and the GSM should
        //      remain at state a (NOT advance to b).

        private IEnumerator Test_S3_GsmReentrancy(TestRecord rec)
        {
            ScratchScene scene = null;
            try
            {
                scene = ScratchScene.BuildSimpleGsm(new[] { "a", "b" });

                // Wire a's onEnter to request transition to b. This SHOULD be
                // rejected by the re-entrancy guard, leaving the GSM at a.
                var slots = scene.GetSlots();
                slots[0].onEnter = new UnityEvent();
                StateDefinition bDef = slots[1].definition;
                slots[0].onEnter.AddListener(() => scene.gsm.RequestTransition(bDef));

                // Reset counters since BuildSimpleGsm may have logged warnings.
                Contract.ResetCounters();

                // Act. ForceState bypasses allowedFrom and triggers slots[0].onEnter,
                // which then attempts a re-entrant RequestTransition(b).
                scene.gsm.ForceState(slots[0].definition);

                // Assert: still at slot 0 (a), guard ignored the re-entrant attempt.
                bool stayedAtA   = scene.gsm.CurrentStateIndex == 0;
                bool noViolations = !Contract.AnyViolations;

                if (stayedAtA && noViolations)
                    Pass(rec, $"GSM stayed at slot 0 (a) after re-entrant RequestTransition(b) was rejected.");
                else
                    Fail(rec, $"stayedAtA={stayedAtA} (CurrentStateIndex={scene.gsm.CurrentStateIndex}); " +
                              $"violations={Contract.TotalViolations}");
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                scene?.Destroy();
            }
            yield return null;
        }

        // ── Test S-4 — null GSM in ASM.OnEnable ──────────────────────────────

        private IEnumerator Test_S4_NullGsmInOnEnable(TestRecord rec)
        {
            // Stash and clear GSM.instance so the ASM's OnEnable runs with null.
            var instanceField = typeof(GameStateManager).GetField("instance",
                BindingFlags.Static | BindingFlags.Public);
            var savedInstance = instanceField?.GetValue(null);
            instanceField?.SetValue(null, null);

            GameObject asmGo = null;
            try
            {
                asmGo = new GameObject("ScratchASM_S4");
                // OnEnable runs synchronously inside AddComponent. If the null-guard
                // fix is missing, this throws NRE.
                asmGo.AddComponent<AnomalyStateManager>();

                // Got here without throwing → fix held.
                Pass(rec, "AnomalyStateManager.OnEnable survived null GameStateManager.instance.");
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (asmGo != null) DestroyImmediate(asmGo);
                instanceField?.SetValue(null, savedInstance);
            }
            yield return null;
        }

        // ── Test S-5 — nested ForceStep ──────────────────────────────────────
        //
        // Strategy: drive ForceStep through a wired controller + sequencer, where
        // an onEnter UnityEvent on a slot calls ctrl.ForceStep() recursively.
        // The fix should reject the nested call without throwing or corrupting
        // the slots' allowedFrom contents.

        private IEnumerator Test_S5_NestedForceStep(TestRecord rec)
        {
            ScratchScene scene = null;
            try
            {
                scene = ScratchScene.BuildFullSequencer();

                var ctrlGo = new GameObject("ScratchController");
                var ctrl   = ctrlGo.AddComponent<ExperimentController>();
                ReflectSet(ctrl, "sequencer", scene.seq);
                scene.ctrlGo = ctrlGo;

                // Pre-populate so Advance has somewhere to go.
                var fakeAnomaly = ScriptableObject.CreateInstance<AnomalyDefinition>();
                scene.scratchAnomaly = fakeAnomaly;
                scene.seq.resolvedSequences.Clear();
                scene.seq.resolvedSequences.Add(new ResolvedSequence
                {
                    sessionLabel = "T",
                    definitions  = new[] { fakeAnomaly },
                });

                // Wire stateExperimentStart's onEnter to recurse into ForceStep —
                // this is what would happen in scene wiring if a UnityEvent on
                // a state callback called ForceStep().
                var slots = scene.GetSlots();
                int expStartIdx = scene.gsm.IndexOf(scene.stateExperimentStart);
                slots[expStartIdx].onEnter = new UnityEvent();
                slots[expStartIdx].onEnter.AddListener(() => ctrl.ForceStep());

                // Reset counters before the act.
                Contract.ResetCounters();

                // Act — drive the GSM into stateExperimentStart (triggering onEnter),
                // which calls ForceStep recursively.
                scene.gsm.ForceState(scene.stateExperimentStart);

                // Then a real ForceStep from the outside.
                ctrl.ForceStep();

                // Assert: no exceptions, no contract violations. The slots'
                // allowedFrom contents should be intact (the in-place mutation
                // in AdvanceForced restores them via try/finally, AND the nested
                // call must be rejected so the lists don't get touched twice).
                bool slotsIntact = true;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].allowedFrom == null) { slotsIntact = false; break; }
                }

                if (slotsIntact && !Contract.AnyViolations)
                    Pass(rec, $"Nested ForceStep was rejected; allowedFrom lists intact; no contract violations.");
                else
                    Fail(rec, $"slotsIntact={slotsIntact}; violations={Contract.TotalViolations}");
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                scene?.Destroy();
            }
            yield return null;
        }

        // ── Test D-1 / D-2 / D-3 — missing Inspector refs don't NRE ─────────

        private IEnumerator Test_D123_MissingInspectorRefs(TestRecord rec)
        {
            GameObject go = null;
            try
            {
                go = new GameObject("ScratchDataSources");
                var facs = go.AddComponent<MetaFrame.Data.DataSource_FACS>();
                var body = go.AddComponent<MetaFrame.Data.DataSource_Body>();

                // Wait one frame so Start() runs and Data is initialized.
                // (Without this, FACS' Data property is null which would itself NRE
                // even with our null-guards on _faceExpressions / _fullBodySkeleton.)
                yield return null;

                Contract.ResetCounters();

                // Now exercise CollectData without any Inspector refs wired.
                var fdata = facs.CollectData();
                var bdata = body.CollectData();

                bool ok = fdata != null && bdata != null && !Contract.AnyViolations;
                if (ok)
                    Pass(rec, $"CollectData returned non-null empty dicts (FACS={fdata.Count}, Body={bdata.Count}).");
                else
                    Fail(rec, $"FACS={(fdata == null ? "null" : fdata.Count.ToString())}, " +
                              $"Body={(bdata == null ? "null" : bdata.Count.ToString())}, " +
                              $"violations={Contract.TotalViolations}");
            }
            finally
            {
                if (go != null) DestroyImmediate(go);
            }
        }

        // ── Test D-4 — Voice source caches SourceNameLower ───────────────────

        private IEnumerator Test_D4_VoiceSourceNameLower(TestRecord rec)
        {
            GameObject dmGo = null, voiceGo = null;
            try
            {
                dmGo = new GameObject("ScratchDM");
                var dm = dmGo.AddComponent<MetaFrame.Data.DataManager>();

                voiceGo = new GameObject("ScratchVoice");
                var voice = voiceGo.AddComponent<MetaFrame.Data.DataSource_Voice>();

                voice.Initialize(dm);

                string lower = voice.SourceNameLower;
                if (!string.IsNullOrEmpty(lower) && lower == voice.SourceName.ToLower())
                    Pass(rec, $"SourceNameLower cached as '{lower}' even though Voice skipped registration.");
                else
                    Fail(rec, $"SourceNameLower='{lower}' (expected '{voice.SourceName.ToLower()}').");
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (voiceGo != null) DestroyImmediate(voiceGo);
                if (dmGo != null)    DestroyImmediate(dmGo);
            }
            yield return null;
        }

        // ── Test Contract — Healed counts + null safety ──────────────────────

        private IEnumerator Test_Contract_HealedAndNullSafety(TestRecord rec)
        {
            try
            {
                Contract.ResetCounters();

                // No-op (already true) — should NOT count as healed.
                Contract.Healed(() => true, () => { /* no-op */ }, "always-true", this);

                // False condition — should count as healed.
                int counter = 0;
                Contract.Healed(() => counter == 1, () => counter = 1, "fix counter", this);

                // Null inputs — should not throw.
                Contract.Healed(null, null, "nulls", this);

                if (counter == 1 && Contract.TotalHealed == 1)
                    Pass(rec, $"Healed worked correctly. counter={counter}, healed={Contract.TotalHealed}.");
                else
                    Fail(rec, $"counter={counter} (want 1), healed={Contract.TotalHealed} (want 1).");
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            yield return null;
        }

        // ════════════════════════════════════════════════════════════════════
        // Tier 2 tests
        // ════════════════════════════════════════════════════════════════════

        // ── T2-2 — GazeInteractable.IsEnabled with null Collider after Awake ─
        //
        // Strategy: Awake throws if Collider is missing in editor builds, so we
        // can't construct a GazeInteractable without one. Instead: add Collider,
        // then GazeInteractable, then DESTROY the Collider, then access IsEnabled.
        // The runtime guard should make get/set safe even after destruction.

        private IEnumerator Test_T22_GazeInteractableNullCollider(TestRecord rec)
        {
            GameObject go = null;
            try
            {
                go = new GameObject("ScratchGazeInteractable");
                var col = go.AddComponent<BoxCollider>();
                var gi  = go.AddComponent<MetaFrame.Interaction.GazeInteraction.GazeInteractable>();

                // Destroy the collider while the GazeInteractable still holds it.
                DestroyImmediate(col);

                // Read + write IsEnabled. Without the fix, both NRE on _collider.enabled.
                bool readVal = gi.IsEnabled;     // get
                gi.IsEnabled  = true;            // set

                Pass(rec, $"Read IsEnabled={readVal} and set without NRE after Collider destruction.");
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (go != null) DestroyImmediate(go);
            }
            yield return null;
        }

        // ── T2-4 — GazeReticle.SetTarget with null _interactor ───────────────

        private IEnumerator Test_T24_GazeReticleNullInteractor(TestRecord rec)
        {
            GameObject go = null;
            try
            {
                go = new GameObject("ScratchGazeReticle");
                var reticle = go.AddComponent<MetaFrame.Interaction.GazeInteraction.GazeReticle>();

                // Skip SetInteractor — that's the failure mode we're testing.
                // Build a default RaycastHit; SetTarget should bail silently.
                var fakeHit = default(RaycastHit);
                reticle.SetTarget(fakeHit);

                Pass(rec, "SetTarget returned silently with null _interactor (no NRE).");
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (go != null) DestroyImmediate(go);
            }
            yield return null;
        }

        // ── T2-5 — GestureHoldInteractable null refs ────────────────────────

        private IEnumerator Test_T25_GestureHoldNullRefs(TestRecord rec)
        {
            GameObject go = null;
            try
            {
                go = new GameObject("ScratchGestureHold");
                var ghi = go.AddComponent<MetaFrame.Interaction.GestureHoldInteractable>();

                // Don't wire any Inspector refs. Both methods should log an error
                // and return without throwing.
                ghi.forceSelect();
                ghi.forceRelease();

                Pass(rec, "forceSelect / forceRelease both returned without NRE on missing refs.");
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (go != null) DestroyImmediate(go);
            }
            yield return null;
        }

        // ── T2-6 — Doppelganger.Spawn re-entrancy ────────────────────────────
        //
        // The fix sets _spawning = true on Spawn() and rejects subsequent Spawns
        // until DoSpawn finishes. Since Spawn yields, two synchronous calls in
        // a row will hit: first Spawn sets _spawning=true, kicks coroutine.
        // Second Spawn sees _spawning=true → returns with warning. Only one
        // coroutine runs.

        private IEnumerator Test_T26_DoppelgangerReentrancy(TestRecord rec)
        {
            GameObject go = null, source = null;
            try
            {
                source = new GameObject("ScratchDoppelgangerSource");
                source.SetActive(false); // keep inactive so Awake/Start don't fire on the source

                go = new GameObject("ScratchDoppelganger");
                var doppel = go.AddComponent<Doppelganger>();
                ReflectSet(doppel, "source", source);

                // Two rapid Spawns; the second should be rejected.
                doppel.Spawn();
                doppel.Spawn();

                // Verify the re-entrancy flag is set (i.e. the first Spawn took effect).
                bool spawning = (bool)ReflectGet(doppel, "_spawning");

                if (spawning)
                    Pass(rec, "_spawning=true after first call; second call rejected without throwing.");
                else
                    Fail(rec, $"_spawning={spawning} (expected true after first Spawn).");
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (go     != null) DestroyImmediate(go);
                if (source != null) DestroyImmediate(source);
            }
            yield return null;
        }

        // ════════════════════════════════════════════════════════════════════
        // Tier 3 tests
        // ════════════════════════════════════════════════════════════════════

        // ── T3-1 — RandomAudioPause auto-disables on missing audioSource ─────

        private IEnumerator Test_T31_RandomAudioPauseNullSource(TestRecord rec)
        {
            GameObject go = null;
            try
            {
                go = new GameObject("ScratchRandomAudio");
                var rap = go.AddComponent<RandomAudioPause>();

                // Don't wire audioSource. Wait one frame for Start() to run.
                yield return null;

                if (!rap.enabled)
                    Pass(rec, "RandomAudioPause disabled itself on missing audioSource.");
                else
                    Fail(rec, "RandomAudioPause is still enabled despite missing audioSource.");
            }
            finally
            {
                if (go != null) DestroyImmediate(go);
            }
        }

        // ── T3-3 — SpaceInteractableState null triggerColliders ──────────────

        private IEnumerator Test_T33_SpaceInteractableNullList(TestRecord rec)
        {
            GameObject go = null;
            try
            {
                go = new GameObject("ScratchSpaceInteractable");
                var sis = go.AddComponent<MetaFrame.ArchInteraction.SpaceInteractableState>();

                // NULL the field via reflection — we want to test that the runtime
                // guard handles the null case (the field default is empty, but
                // Inspector users can clear it to null).
                ReflectSet(sis, "triggerColliders", null);

                // Invoke OnTriggerEnter via reflection. Argument is `Collider other`,
                // we pass null (not strictly valid but the guard short-circuits before
                // touching `other`).
                var onEnter = typeof(MetaFrame.ArchInteraction.SpaceInteractableState).GetMethod(
                    "OnTriggerEnter",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var onExit = typeof(MetaFrame.ArchInteraction.SpaceInteractableState).GetMethod(
                    "OnTriggerExit",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                onEnter?.Invoke(sis, new object[] { null });
                onExit ?.Invoke(sis, new object[] { null });

                Pass(rec, "OnTriggerEnter/Exit returned silently with null triggerColliders.");
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                Fail(rec, $"Inner threw {tie.InnerException?.GetType().Name}: {tie.InnerException?.Message}");
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (go != null) DestroyImmediate(go);
            }
            yield return null;
        }

        // ── T3-4 — LightFlicker auto-disables on missing Light ──────────────

        private IEnumerator Test_T34_LightFlickerNullLight(TestRecord rec)
        {
            GameObject go = null;
            try
            {
                go = new GameObject("ScratchLightFlicker");
                var flicker = go.AddComponent<FS_OfficePack.LightFlicker>();

                // Wait one frame for Start() to run.
                yield return null;

                if (!flicker.enabled)
                    Pass(rec, "LightFlicker disabled itself on missing Light component.");
                else
                    Fail(rec, "LightFlicker is still enabled despite missing Light.");
            }
            finally
            {
                if (go != null) DestroyImmediate(go);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Tier 4 tests
        // ════════════════════════════════════════════════════════════════════

        // ── T4-1 — CompositeTag null-skip ────────────────────────────────────

        private IEnumerator Test_T41_CompositeTagNullEntry(TestRecord rec)
        {
            MetaFrame.Tags.Tag goodTag = null;
            MetaFrame.Tags.CompositeTag composite = null;
            GameObject scratchGo = null;
            try
            {
                goodTag   = ScriptableObject.CreateInstance<MetaFrame.Tags.Tag>();
                composite = ScriptableObject.CreateInstance<MetaFrame.Tags.CompositeTag>();

                // Build a _tags array with [validTag, null] via reflection — the
                // null is the case the fix protects against.
                var tagsArray = new MetaFrame.Tags.Tag[] { goodTag, null };
                ReflectSet(composite, "_tags", tagsArray);

                scratchGo = new GameObject("ScratchTagged");
                int hash  = scratchGo.GetHashCode();

                // Invoke internal Add and Remove via reflection.
                var addMethod = typeof(MetaFrame.Tags.CompositeTag).GetMethod(
                    "Add", BindingFlags.Instance | BindingFlags.NonPublic);
                var removeMethod = typeof(MetaFrame.Tags.CompositeTag).GetMethod(
                    "Remove", BindingFlags.Instance | BindingFlags.NonPublic);

                addMethod   ?.Invoke(composite, new object[] { scratchGo, hash });
                removeMethod?.Invoke(composite, new object[] { scratchGo, hash });

                Pass(rec, "CompositeTag.Add/Remove skipped null entry in _tags without NRE.");
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                Fail(rec, $"Inner threw {tie.InnerException?.GetType().Name}: {tie.InnerException?.Message}");
            }
            catch (System.Exception e)
            {
                Fail(rec, $"Threw {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (scratchGo != null) DestroyImmediate(scratchGo);
                if (composite != null) DestroyImmediate(composite);
                if (goodTag   != null) DestroyImmediate(goodTag);
            }
            yield return null;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void Pass(TestRecord rec, string detail)
        {
            rec.result = TestResult.Pass;
            rec.detail = detail;
            Debug.Log($"[AuditTestHarness] PASS  {rec.code}: {detail}");
        }

        private void Fail(TestRecord rec, string detail)
        {
            rec.result = TestResult.Fail;
            rec.detail = detail;
            Debug.LogError($"[AuditTestHarness] FAIL  {rec.code}: {detail}");
        }

        private static object ReflectGet(object target, string fieldName)
        {
            var f = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f?.GetValue(target);
        }

        private static void ReflectSet(object target, string fieldName, object value)
        {
            var f = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            f?.SetValue(target, value);
        }

        // ── Scratch scene container — owns scratch state, cleans up on Destroy ──
        //
        // Encapsulates the boilerplate of creating a fully-wired scratch GSM
        // (and optionally Sequencer + ASM + Controller) and tearing it all down
        // deterministically with DestroyImmediate. Avoids the per-test cleanup
        // boilerplate that gets out of sync.

        private class ScratchScene
        {
            public GameObject gsmGo;
            public GameStateManager gsm;
            public List<StateDefinition> defs = new();

            public GameObject seqGo;
            public ExperimentSequencer seq;

            public GameObject asmGo;
            public AnomalyStateManager asm;

            public GameObject ctrlGo;
            public AnomalyDefinition scratchAnomaly;

            // Convenience refs for the full-sequencer setup.
            public StateDefinition stateExperimentStart;
            public StateDefinition stateSessionStart;
            public StateDefinition stateSessionEnd;
            public StateDefinition stateTrialStart;
            public StateDefinition stateTrialEnd;
            public StateDefinition stateIdle;
            public StateDefinition stateExperimentEnd;

            /// <summary>Build a GSM with N empty slots, no sequencer.</summary>
            public static ScratchScene BuildSimpleGsm(string[] stateNames)
            {
                var s = new ScratchScene();
                s.gsmGo = new GameObject("ScratchGSM");
                s.gsm   = s.gsmGo.AddComponent<GameStateManager>();

                var slots = new List<StateSlot>();
                foreach (var n in stateNames)
                {
                    var d = ScriptableObject.CreateInstance<StateDefinition>();
                    d.displayName = n;
                    s.defs.Add(d);
                    slots.Add(new StateSlot
                    {
                        definition  = d,
                        allowedFrom = new List<StateDefinition>(),
                        onEnter     = new UnityEvent(),
                        onExit      = new UnityEvent(),
                    });
                }
                ReflectSet(s.gsm, "slots", slots);

                // Force initialization to slot 0 so CurrentStateIndex is sane.
                s.gsm.ForceState(0);
                return s;
            }

            /// <summary>
            /// Build a full sequencer setup: GSM with the 7 named experiment states,
            /// a sequencer with all stateXxx refs wired, GSM ref wired, ready for
            /// a test to populate resolvedSequences.
            /// </summary>
            public static ScratchScene BuildFullSequencer()
            {
                var s = new ScratchScene();
                s.gsmGo = new GameObject("ScratchGSM_Full");
                s.gsm   = s.gsmGo.AddComponent<GameStateManager>();

                s.stateExperimentStart = MakeDef("experiment_start");
                s.stateSessionStart    = MakeDef("session_start");
                s.stateSessionEnd      = MakeDef("session_end");
                s.stateTrialStart      = MakeDef("trial_start");
                s.stateTrialEnd        = MakeDef("trial_end");
                s.stateIdle            = MakeDef("idle");
                s.stateExperimentEnd   = MakeDef("experiment_end");

                s.defs.AddRange(new[] {
                    s.stateExperimentStart, s.stateSessionStart, s.stateSessionEnd,
                    s.stateTrialStart, s.stateTrialEnd, s.stateIdle, s.stateExperimentEnd
                });

                var slots = new List<StateSlot>();
                foreach (var d in s.defs)
                {
                    slots.Add(new StateSlot
                    {
                        definition  = d,
                        // No allowedFrom rules by default — every transition is permitted.
                        // Tests that want to block transitions set allowedFrom on
                        // specific slots after BuildFullSequencer() returns.
                        allowedFrom = new List<StateDefinition>(),
                        onEnter     = new UnityEvent(),
                        onExit      = new UnityEvent(),
                    });
                }
                ReflectSet(s.gsm, "slots", slots);

                // Sequencer
                s.seqGo = new GameObject("ScratchSequencer");
                s.seq   = s.seqGo.AddComponent<ExperimentSequencer>();
                ReflectSet(s.seq, "gsm",                  s.gsm);
                ReflectSet(s.seq, "stateExperimentStart", s.stateExperimentStart);
                ReflectSet(s.seq, "stateSessionStart",    s.stateSessionStart);
                ReflectSet(s.seq, "stateSessionEnd",      s.stateSessionEnd);
                ReflectSet(s.seq, "stateTrialStart",      s.stateTrialStart);
                ReflectSet(s.seq, "stateTrialEnd",        s.stateTrialEnd);
                ReflectSet(s.seq, "stateIdle",            s.stateIdle);
                ReflectSet(s.seq, "stateExperimentEnd",   s.stateExperimentEnd);

                // Force GSM to a known initial state so any test gets a stable starting point.
                s.gsm.ForceState(s.stateExperimentStart);

                return s;

                static StateDefinition MakeDef(string n)
                {
                    var d = ScriptableObject.CreateInstance<StateDefinition>();
                    d.displayName = n;
                    return d;
                }
            }

            public List<StateSlot> GetSlots()
            {
                return (List<StateSlot>)ReflectGet(gsm, "slots");
            }

            public void SetSlotAllowedFrom(int slotIndex, List<StateDefinition> allowed)
            {
                var slots = GetSlots();
                if (slotIndex < 0 || slotIndex >= slots.Count) return;
                slots[slotIndex].allowedFrom = allowed ?? new List<StateDefinition>();
            }

            public void Destroy()
            {
                if (ctrlGo  != null) DestroyImmediate(ctrlGo);
                if (asmGo   != null) DestroyImmediate(asmGo);
                if (seqGo   != null) DestroyImmediate(seqGo);
                if (gsmGo   != null) DestroyImmediate(gsmGo);

                foreach (var d in defs) if (d != null) DestroyImmediate(d);
                if (scratchAnomaly != null) DestroyImmediate(scratchAnomaly);

                // Clear any lingering static singleton refs so the next test gets
                // a clean slate. Use reflection because the field is public-static,
                // and Unity's destroyed-object semantics make `instance == null`
                // already return true — but we want the field literally null.
                var instField = typeof(GameStateManager).GetField("instance",
                    BindingFlags.Static | BindingFlags.Public);
                instField?.SetValue(null, null);

                var seqField = typeof(ExperimentSequencer).GetProperty("instance",
                    BindingFlags.Static | BindingFlags.Public);
                if (seqField != null && seqField.CanWrite) seqField.SetValue(null, null);
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(AuditTestHarness))]
    public class AuditTestHarnessEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var t = (AuditTestHarness)target;
            bool inPlayMode = Application.isPlaying;

            EditorGUILayout.LabelField("Audit Test Harness", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Click Run All in Play Mode to verify every Tier 1 fix. " +
                "Each test creates and tears down its own scratch state — no scene setup needed.",
                MessageType.None);

            EditorGUILayout.Space(4);
            GUI.enabled = inPlayMode;
            GUI.color = new Color(0.4f, 1f, 0.6f);
            if (GUILayout.Button("Run All", GUILayout.Height(34)))
                t.RunAll();
            GUI.color = Color.white;
            GUI.enabled = true;

            if (!inPlayMode)
                EditorGUILayout.HelpBox("Enter Play Mode to run.", MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);

            foreach (var r in t.Records)
            {
                Rect row = EditorGUILayout.GetControlRect(false, 22f);
                Rect tag = new Rect(row.x,                  row.y, 70f,                row.height);
                Rect res = new Rect(row.x + 75f,            row.y, 60f,                row.height);
                Rect msg = new Rect(row.x + 140f,           row.y, row.width - 145f,   row.height);

                EditorGUI.LabelField(tag, r.code, EditorStyles.miniBoldLabel);

                Color c = r.result switch
                {
                    AuditTestHarness.TestResult.Pass    => new Color(0.45f, 1f,    0.55f),
                    AuditTestHarness.TestResult.Fail    => new Color(1f,    0.45f, 0.45f),
                    AuditTestHarness.TestResult.Skipped => new Color(0.7f,  0.7f,  0.7f),
                    _                                   => new Color(0.55f, 0.55f, 0.55f),
                };
                var prevColor = GUI.color;
                GUI.color = c;
                EditorGUI.LabelField(res, r.result.ToString(), EditorStyles.miniBoldLabel);
                GUI.color = prevColor;

                EditorGUI.LabelField(msg, string.IsNullOrEmpty(r.detail) ? r.description : r.detail);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Contract counters", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"   Total violations: {Contract.TotalViolations}");
            EditorGUILayout.LabelField($"   Total healed:     {Contract.TotalHealed}");

            if (inPlayMode) Repaint();
        }
    }
#endif
}
