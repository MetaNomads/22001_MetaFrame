// LslExperimentRouter.cs
// Bridges LSLService inbound messages to the existing ExperimentSequencer /
// ExperimentController, and pushes state updates back to the LSL host.
//
// One-of-a-kind component: attach this to ANY persistent GameObject (a
// scene-level controller, a "[LSLBridge]" empty, etc.). It does no networking
// itself — it piggybacks on the static LSLService listener (already bound on
// UDP 12345) and subscribes to LSLService.OnHostMessage on the main thread,
// emitting via LSLService.SendToHost(). LSLService gates inbound messages by
// the locked LSL host IP, so we can trust everything we see here.
//
// Message protocol (must match LSL-side experiment_controller.py):
//   inbound:
//     SUBJECT_ID:<int>:<seq>          — set/confirm subject ID
//     SUBJECT_ID_OVERRIDE:<int>:<seq> — explicit override after SUBJECT_ID_REJECT
//     CMD:STEP:<seq>                  — sequencer.Step()
//     CMD:FORCE_STEP:<seq>            — sequencer.ForceStep()
//     CMD:SESSION:<int>:<seq>         — JumpToSession(N)
//     STATE_REQ                       — push current state back (no seq)
//     DISCOVER / CONNECT              — host handshake; respond with READY:<state>
//
//   outbound:
//     READY:no_subject                — host locked, no subject yet
//     READY:subject=<id>              — host re-locked, subject is <id>
//     SUBJECT_ID_ACK:<id>:<seq>       — accepted (or already-set with same id)
//     SUBJECT_ID_REJECT:<reason>:<seq>
//     CMD_ACK:<seq>                   — received, dispatching
//     CMD_DONE:<seq>:<result>         — done; result = ok|blocked|error:<msg>
//     CMD_REJECT:<seq>:<reason>       — rejected before dispatch
//     STATE:session=<n>,session_label=<lbl>,trial=<k>,total=<t>,violation=<v>,gsm=<g>
//
// Reliability:
//   - We dedupe inbound CMD by <seq>: a repeated CMD with the same seq replays
//     the cached response without re-dispatching. This makes the LSL-host retry
//     loop safe.
//   - We push STATE on every transition (no polling). If LSL misses a packet,
//     it can request STATE_REQ to re-seed.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetaFrame.LSL
{
    [DefaultExecutionOrder(-100)]   // before recorders so subscriptions are live early
    public class LslExperimentRouter : MonoBehaviour
    {
        [SerializeField] private MetaFrame.State.ExperimentController _controller;
        [SerializeField] private MetaFrame.State.ExperimentSequencer  _sequencer;

        // Cache of <seq> → outbound response, so duplicate inbound commands
        // (LSL host UDP retry) reproduce the exact prior reply instead of
        // re-dispatching the action. Bounded to keep memory in check.
        private readonly Dictionary<string, string> _seenCmdResponses = new();
        private readonly Queue<string>              _seenCmdOrder     = new();
        private const int SEEN_CMD_MAX = 256;

        // Cached state — used to answer STATE_REQ without recomputing from scratch.
        private string _lastStateMsg = null;

        // Latest violation/stimulus label from OnTrialBegan, surfaced in STATE.
        private string _lastViolationLabel = "—";

        void Reset()
        {
            // Auto-wire if the user hasn't dragged refs in — common-case scenes
            // have one of each in the hierarchy.
#if UNITY_2023_1_OR_NEWER
            if (_controller == null) _controller = UnityEngine.Object.FindFirstObjectByType<MetaFrame.State.ExperimentController>();
            if (_sequencer  == null) _sequencer  = UnityEngine.Object.FindFirstObjectByType<MetaFrame.State.ExperimentSequencer>();
#else
            if (_controller == null) _controller = UnityEngine.Object.FindObjectOfType<MetaFrame.State.ExperimentController>();
            if (_sequencer  == null) _sequencer  = UnityEngine.Object.FindObjectOfType<MetaFrame.State.ExperimentSequencer>();
#endif
        }

        void Awake()
        {
            // Late-bind in case Reset didn't run (e.g. component added at runtime).
            if (_controller == null || _sequencer == null) Reset();
        }

        void OnEnable()
        {
            // We piggyback on the existing LSLService.cs UDP listener (port 12345)
            // rather than running a parallel SyncBridge listener — only one process
            // can bind a UDP port at a time, so two listeners on 12345 would silently
            // fail. LSLService already locks the LSL host IP via DISCOVER/CONNECT;
            // it now also forwards SUBJECT_ID:, CMD:, and STATE_REQ on this event,
            // and fires OnHostConnected once after handshake so we can push READY.
            MetaFrame.Data.LSLService.OnHostMessage += HandleHostMessage;
            MetaFrame.Data.LSLService.OnHostConnected += SendReady;

            MetaFrame.State.ExperimentSequencer.OnSubjectIdConfirmed += OnSubjectIdConfirmed;
            MetaFrame.State.ExperimentSequencer.OnExperimentBegan    += OnExperimentBegan;
            MetaFrame.State.ExperimentSequencer.OnSessionBegan       += OnSessionBegan;
            MetaFrame.State.ExperimentSequencer.OnSessionEnded       += OnSessionEnded;
            MetaFrame.State.ExperimentSequencer.OnTrialBegan         += OnTrialBegan;
            MetaFrame.State.ExperimentSequencer.OnTrialEnded         += OnTrialEnded;
            MetaFrame.State.ExperimentSequencer.OnExperimentEnded    += OnExperimentEnded;

            Debug.Log($"[LslRouter] OnEnable — subscribed. " +
                      $"controller={(_controller != null ? "OK" : "NULL")}, " +
                      $"sequencer={(_sequencer != null ? "OK" : "NULL")}");
        }

        void OnDisable()
        {
            MetaFrame.Data.LSLService.OnHostMessage -= HandleHostMessage;
            MetaFrame.Data.LSLService.OnHostConnected -= SendReady;

            MetaFrame.State.ExperimentSequencer.OnSubjectIdConfirmed -= OnSubjectIdConfirmed;
            MetaFrame.State.ExperimentSequencer.OnExperimentBegan    -= OnExperimentBegan;
            MetaFrame.State.ExperimentSequencer.OnSessionBegan       -= OnSessionBegan;
            MetaFrame.State.ExperimentSequencer.OnSessionEnded       -= OnSessionEnded;
            MetaFrame.State.ExperimentSequencer.OnTrialBegan         -= OnTrialBegan;
            MetaFrame.State.ExperimentSequencer.OnTrialEnded         -= OnTrialEnded;
            MetaFrame.State.ExperimentSequencer.OnExperimentEnded    -= OnExperimentEnded;
        }

        // ── Inbound ────────────────────────────────────────────────────────────────

        private void HandleHostMessage(string msg)
        {
            // LSLService.OnHostMessage already handed us a trimmed string from
            // the locked host. We only see SUBJECT_ID:, SUBJECT_ID_OVERRIDE:,
            // CMD:, STATE_REQ — the DISCOVER/CONNECT handshake is fully owned
            // by LSLService and never reaches us. So this dispatcher only
            // needs to worry about the experiment-controller verbs.
            msg = msg?.Trim() ?? string.Empty;
            if (msg.Length == 0) return;

            // Echo-log to LSL so the operator can see the router received the
            // message even when the Quest console isn't accessible.
            MetaFrame.Data.LSLService.SendToHost($"LOG:LslRouter received '{(msg.Length > 40 ? msg.Substring(0, 40) : msg)}'");
            Debug.Log($"[LslRouter] HandleHostMessage('{(msg.Length > 60 ? msg.Substring(0, 60) : msg)}')");

            try
            {
                if (msg == "STATE_REQ")
                {
                    if (_lastStateMsg != null) MetaFrame.Data.LSLService.SendToHost(_lastStateMsg);
                    else                       MetaFrame.Data.LSLService.SendToHost(BuildStateMsg());
                    return;
                }

                if (msg.StartsWith("SUBJECT_ID:"))          { HandleSubjectId(msg, allowOverride: false); return; }
                if (msg.StartsWith("SUBJECT_ID_OVERRIDE:")) { HandleSubjectId(msg, allowOverride: true);  return; }
                if (msg.StartsWith("CMD:"))                 { HandleCommand(msg);                         return; }

                Debug.LogWarning($"[LslRouter] Unknown host message ignored: '{Truncate(msg, 60)}'");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LslRouter] Exception handling '{Truncate(msg, 60)}': {e}");
            }
        }

        private void HandleSubjectId(string msg, bool allowOverride)
        {
            // Format:  SUBJECT_ID:<int>:<seq>     or    SUBJECT_ID_OVERRIDE:<int>:<seq>
            string[] parts = msg.Split(':');
            if (parts.Length != 3)
            {
                Debug.LogWarning($"[LslRouter] Malformed subject-id message: '{msg}'");
                MetaFrame.Data.LSLService.SendToHost($"SUBJECT_ID_REJECT:malformed:{(parts.Length >= 3 ? parts[2] : "")}");
                return;
            }

            string seq = parts[2];

            if (!int.TryParse(parts[1], out int id) || id < 1 || id > 9999)
            {
                Debug.LogWarning($"[LslRouter] Invalid subject id in '{msg}'");
                MetaFrame.Data.LSLService.SendToHost($"SUBJECT_ID_REJECT:invalid_id:{seq}");
                return;
            }

            if (_sequencer == null)
            {
                MetaFrame.Data.LSLService.SendToHost("LOG:LslRouter ERROR — no ExperimentSequencer in scene");
                Debug.LogError("[LslRouter] No ExperimentSequencer reference — drop SUBJECT_ID. " +
                               "Make sure ExperimentSequencer is in the scene and (optionally) " +
                               "wired into LslExperimentRouter's _sequencer field.");
                MetaFrame.Data.LSLService.SendToHost($"SUBJECT_ID_REJECT:no_sequencer:{seq}");
                return;
            }

            MetaFrame.Data.LSLService.SendToHost($"LOG:LslRouter calling TrySetSubjectID({id}, override={allowOverride})");
            Debug.Log($"[LslRouter] Setting subject id {id} (allowOverride={allowOverride}, seq={seq})");

            // Idempotency: if it's already confirmed at the same id, just ACK.
            // If it's confirmed at a different id and override isn't allowed,
            // reject with a specific reason so LSL can decide whether to
            // resend as SUBJECT_ID_OVERRIDE.
            if (_sequencer.IsSubjectIdConfirmed && _sequencer.subjectID != id && !allowOverride)
            {
                MetaFrame.Data.LSLService.SendToHost($"SUBJECT_ID_REJECT:already_set:{_sequencer.subjectID}:{seq}");
                return;
            }

            if (!_sequencer.TrySetSubjectID(id, out string err, allowOverride))
            {
                MetaFrame.Data.LSLService.SendToHost($"LOG:LslRouter TrySetSubjectID rejected: {Sanitize(err ?? "unknown")}");
                Debug.LogWarning($"[LslRouter] TrySetSubjectID rejected: {err}");
                MetaFrame.Data.LSLService.SendToHost($"SUBJECT_ID_REJECT:{Sanitize(err)}:{seq}");
                return;
            }

            Debug.Log($"[LslRouter] Subject {id} accepted; sending SUBJECT_ID_ACK:{id}:{seq}");
            MetaFrame.Data.LSLService.SendToHost($"SUBJECT_ID_ACK:{id}:{seq}");
            // STATE will be pushed by OnExperimentBegan handler — no need to push here.
        }

        private void HandleCommand(string msg)
        {
            // Common formats:
            //   CMD:STEP:<seq>
            //   CMD:FORCE_STEP:<seq>
            //   CMD:SESSION:<n>:<seq>
            string[] parts = msg.Split(':');
            if (parts.Length < 3)
            {
                Debug.LogWarning($"[LslRouter] Malformed CMD: '{msg}'");
                return;
            }

            string verb = parts[1];
            string seq = parts[parts.Length - 1];

            // Dedup — if we've already responded to this seq, replay the cached
            // response and DO NOT redispatch. The LSL host's retry loop is the
            // expected source of duplicates.
            if (_seenCmdResponses.TryGetValue(seq, out string cached))
            {
                MetaFrame.Data.LSLService.SendToHost(cached);
                return;
            }

            // Gate: CMD requires a confirmed subject ID. Without one the
            // sequencer is dormant and Step() would no-op anyway, but we
            // reject early so the operator gets clear feedback in LSL.
            if (_sequencer == null || !_sequencer.IsSubjectIdConfirmed)
            {
                string rej = $"CMD_REJECT:{seq}:no_subject";
                Cache(seq, rej);
                MetaFrame.Data.LSLService.SendToHost(rej);
                return;
            }
            if (_controller == null)
            {
                string rej = $"CMD_REJECT:{seq}:no_controller";
                Cache(seq, rej);
                MetaFrame.Data.LSLService.SendToHost(rej);
                return;
            }

            // ACK on receipt — caller knows we're dispatching.
            MetaFrame.Data.LSLService.SendToHost($"CMD_ACK:{seq}");

            string done;
            try
            {
                switch (verb)
                {
                    case "STEP":
                        _controller.Step();
                        done = $"CMD_DONE:{seq}:ok";
                        break;
                    case "FORCE_STEP":
                        _controller.ForceStep();
                        done = $"CMD_DONE:{seq}:ok";
                        break;
                    case "SESSION":
                        if (parts.Length != 4 || !int.TryParse(parts[2], out int n) || n < 1)
                        {
                            done = $"CMD_DONE:{seq}:error:bad_session_arg";
                            break;
                        }
                        _controller.JumpToSession(n);
                        done = $"CMD_DONE:{seq}:ok";
                        break;
                    default:
                        done = $"CMD_DONE:{seq}:error:unknown_verb_{Sanitize(verb)}";
                        break;
                }
            }
            catch (Exception e)
            {
                done = $"CMD_DONE:{seq}:error:{Sanitize(e.Message)}";
            }

            Cache(seq, done);
            MetaFrame.Data.LSLService.SendToHost(done);

            // After every CMD, push fresh STATE — saves LSL having to ask.
            PushState();
        }

        private void Cache(string seq, string response)
        {
            _seenCmdResponses[seq] = response;
            _seenCmdOrder.Enqueue(seq);
            while (_seenCmdOrder.Count > SEEN_CMD_MAX)
            {
                string evicted = _seenCmdOrder.Dequeue();
                _seenCmdResponses.Remove(evicted);
            }
        }

        // ── Outbound state push ────────────────────────────────────────────────────

        private void OnSubjectIdConfirmed(int id) { /* state push happens after OnExperimentBegan */ }
        private void OnExperimentBegan(int id)    { PushState(); }
        private void OnSessionBegan(string label) { PushState(); }
        private void OnSessionEnded()             { PushState(); }
        private void OnTrialEnded()               { PushState(); }
        private void OnExperimentEnded()          { PushState(); }

        private void OnTrialBegan(MetaFrame.State.AnomalyDefinition anomaly, string stimulus)
        {
            _lastViolationLabel = string.IsNullOrEmpty(stimulus) ? "—" : stimulus;
            PushState();
        }

        private void PushState()
        {
            string m = BuildStateMsg();
            _lastStateMsg = m;
            MetaFrame.Data.LSLService.SendToHost(m);
        }

        private string BuildStateMsg()
        {
            if (_sequencer == null) return "STATE:session=-1,trial=-1,total=0,violation=—,gsm=no_sequencer";

            var session = _sequencer.CurrentSession;
            int sessionIdx = _sequencer.CurrentSessionIndex;
            int trial      = _sequencer.CurrentTrialIndex + 1;
            int total      = session != null ? session.TrialCount : 0;
            string label   = session != null ? Sanitize(session.sessionLabel) : "—";
            string gsmName = (_sequencer.GSM != null && _sequencer.GSM.CurrentStateDefinition != null)
                ? Sanitize(_sequencer.GSM.CurrentStateDefinition.displayName)
                : "—";

            return $"STATE:session={sessionIdx},session_label={label},trial={trial},total={total}," +
                   $"violation={Sanitize(_lastViolationLabel)},gsm={gsmName}";
        }

        // ── Handshake helpers ──────────────────────────────────────────────────────

        private void SendReady()
        {
            // Diagnostic: send a LOG line to LSL on every CONNECT/RECONNECT so
            // the operator can SEE in the LSL log panel that the router is
            // wired up. If you don't see this LOG line right after Connect,
            // the LslExperimentRouter component isn't in the active scene
            // (or it's on a disabled GameObject).
            string seqState = _sequencer == null
                ? "NO_SEQUENCER"
                : (_sequencer.IsSubjectIdConfirmed ? $"subject={_sequencer.subjectID}" : "no_subject");
            string ctlState = _controller == null ? "NO_CONTROLLER" : "controller=OK";
            MetaFrame.Data.LSLService.SendToHost($"LOG:LslRouter ready ({seqState}, {ctlState})");

            string ready = (_sequencer != null && _sequencer.IsSubjectIdConfirmed)
                ? $"READY:subject={_sequencer.subjectID}"
                : "READY:no_subject";
            MetaFrame.Data.LSLService.SendToHost(ready);

            // If a subject is already locked, also push current state so LSL UI
            // repopulates after a reconnect without needing a separate STATE_REQ.
            if (_sequencer != null && _sequencer.IsSubjectIdConfirmed)
                MetaFrame.Data.LSLService.SendToHost(BuildStateMsg());
        }

        // ── Misc ───────────────────────────────────────────────────────────────────

        // Strips the field separators from values so a stray comma/colon in a
        // session label or violation type can't break parsing on the LSL side.
        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "—";
            return s.Replace(',', '_').Replace(':', '_').Replace('\n', ' ').Replace('\r', ' ');
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
