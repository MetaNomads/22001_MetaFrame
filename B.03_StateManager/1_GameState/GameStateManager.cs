using MetaFrame.Data;
using UnityEngine;
using System.Collections.Generic;
using System;
using static MetaFrame.Data.SurveyDataRecorder;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaFrame.State
{
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager instance;

        [Header("Reference Scripts")]
        [SerializeField] private SurveyDataRecorder surveyDataRecorder;
        [SerializeField] private SpawnMechanism spawnMechanism;

        // ── Enums ──────────────────────────────────────────────────

        public enum SessionType
        {
            TUTORIAL,
            SESSION_A,
            SESSION_B,
            SESSION_C,
        }

        [System.Flags]
        public enum LevelState
        {
            Idle      = 1 << 0,
            Initial   = 1 << 1,
            At_Source = 1 << 2,
            In_Hand   = 1 << 3,
            At_Target = 1 << 4,
            Removal   = 1 << 5,
        }

        // ── Data Structs ───────────────────────────────────────────

        [System.Serializable]
        public struct TrialData
        {
            [Tooltip("Anomaly to occur during this trial. Drag an AnomalyDefinition asset here.\n" +
                    "Leave null for a NORMAL (no-anomaly) trial.")]
            public AnomalyDefinition anomalyDefinition;

            [System.NonSerialized] public string trialStartTime;
            [System.NonSerialized] public string trialEndTime;
            [System.NonSerialized] public Dictionary<LevelState, string> levelStateTimestamps;
        }

        [System.Serializable]
        public struct SequenceData
        {
            public List<TrialData> trialData;
        }

        [System.Serializable]
        public struct SessionData
        {
            public SessionType sessionType;
            public List<SequenceData> sequences;
            [System.NonSerialized] public int currentSequence;
        }

        // ── Session Data ───────────────────────────────────────────

        [SerializeField] private List<SessionData> SessionSequences = new List<SessionData>();
        [SerializeField] private SessionData? currentSessionData = null;

        // ── Events ─────────────────────────────────────────────────

        /// <summary>Fires at the start of every trial.</summary>
        public event Action<SessionData?, int, TrialData> GameStateTrigger;

        /// <summary>Fires whenever the object reaches a new level state.</summary>
        public event Action<SessionData?, int, LevelState> LevelStateTrigger;

        // ── Internal ───────────────────────────────────────────────

        private int trialNumber = 0;
        private bool experimentInProgress = false;
        private List<TrialData> currentTrialList;

        // ── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (instance != null)
            {
                Debug.LogWarning("A second GameStateManager was detected and deleted!");
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        // ── Session Control ────────────────────────────────────────

        public void UpdateSessionType(SessionType sessionType)
        {
            currentSessionData = MatchSessionTypeToData(sessionType);
            currentTrialList   = GetRandomSequenceFromSession(sessionType);
        }

        public SessionData? MatchSessionTypeToData(SessionType sessionType)
        {
            foreach (var session in SessionSequences)
                if (session.sessionType == sessionType)
                    return session;

            Debug.LogError($"[GSM] Session '{sessionType}' not found in SessionSequences!");
            return null;
        }

        private List<TrialData> GetRandomSequenceFromSession(SessionType sessionType)
        {
            for (int i = 0; i < SessionSequences.Count; i++)
            {
                if (SessionSequences[i].sessionType != sessionType) continue;

                int selected        = UnityEngine.Random.Range(0, SessionSequences[i].sequences.Count);
                SessionData session = SessionSequences[i];
                session.currentSequence = selected;
                SessionSequences[i]     = session;
                return SessionSequences[i].sequences[selected].trialData;
            }

            Debug.LogError($"[GSM] Session '{sessionType}' not registered in GetRandomSequenceFromSession!");
            return null;
        }

        // ── Trial Control ──────────────────────────────────────────

        public void ProgressTrial()
        {
            if (!experimentInProgress)
            {
                if (currentSessionData == null)
                {
                    Debug.LogError("[GSM] Session type not set. Will not begin experiment.");
                    return;
                }
                BeginNextTrial();
                experimentInProgress = true;
                Debug.Log("[GSM] Experiment started.");
            }
            else
            {
                UpdateDataThenBeginNextTrial(surveyDataRecorder.stateD);
            }
        }

        public void BeginNextTrial()
        {
            if (trialNumber >= currentTrialList.Count)
            {
                Debug.LogWarning("[GSM] No more trials available!");
                return;
            }

            if (!CheckIfTrialCanContinue()) return;

            trialNumber++;

            TrialData trialData             = currentTrialList[trialNumber];
            trialData.trialStartTime        = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            trialData.levelStateTimestamps  = new Dictionary<LevelState, string>();
            currentTrialList[trialNumber]   = trialData;

            string anomalyLabel = trialData.anomalyDefinition != null
                ? trialData.anomalyDefinition.ToString()
                : "NORMAL";
            Debug.Log($"[GSM] Trial {trialNumber} starting. Anomaly: {anomalyLabel}");

            GameStateTrigger?.Invoke(currentSessionData, trialNumber, currentTrialList[trialNumber]);
            spawnMechanism.SpawnCup();
        }

        private bool CheckIfTrialCanContinue()
        {
            if ((trialNumber + 1) < currentTrialList.Count) return true;
            ConcludeSession();
            return false;
        }

        private void ConcludeSession()
        {
            Debug.Log("[GSM] Session concluded.");
            experimentInProgress = false;
            currentSessionData   = null;
            currentTrialList     = null;
        }

        public void UpdateDataThenBeginNextTrial(StateData stateData)
        {
            TrialData trialData           = currentTrialList[trialNumber];
            trialData.trialEndTime        = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            currentTrialList[trialNumber] = trialData;

            EndCurrentTrial();
            BeginNextTrial();
        }

        public void EndCurrentTrial()
        {
            spawnMechanism.DestroyCup();
            Debug.Log($"[GSM] Ending trial {trialNumber}.");
        }

        // ── Level State Signals ────────────────────────────────────

        public void FireLevelState(LevelState levelState)
        {
            TrialData trialData = currentTrialList[trialNumber];
            if (trialData.levelStateTimestamps == null)
                trialData.levelStateTimestamps = new Dictionary<LevelState, string>();
            trialData.levelStateTimestamps[levelState] = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            currentTrialList[trialNumber] = trialData;

            LevelStateTrigger?.Invoke(currentSessionData, trialNumber, levelState);
        }

        public void HandGrabbedObjectSignal()   => FireLevelState(LevelState.In_Hand);
        public void ObjectReachedSourceSignal() => FireLevelState(LevelState.At_Source);
        public void ObjectReachedTargetSignal() => FireLevelState(LevelState.At_Target);

        // ── Getters ────────────────────────────────────────────────

        public SessionData? GetCurrentSessionData() => currentSessionData;
        public TrialData?   GetCurrentTrialData()   => currentTrialList?[trialNumber];
        public int          GetTrialNumber()        => trialNumber;

        /// <summary>
        /// Returns the display name of the anomaly assigned to the current trial,
        /// or "NORMAL" if no anomaly definition is assigned.
        /// </summary>
        public string GetCurrentAnomalyName()
        {
            var trial = GetCurrentTrialData();
            return trial?.anomalyDefinition != null
                ? trial.Value.anomalyDefinition.ToString()
                : "NORMAL";
        }
    }

    // ── Editor ─────────────────────────────────────────────────────────────────────

    #if UNITY_EDITOR
    [CustomEditor(typeof(GameStateManager))]
    public class GameStateManagerEditor : Editor
    {
        private bool _sessionFoldout = true;
        private bool _levelFoldout   = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "m_Script");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Enum Reference Lists", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "These lists show current enum values. To add a new value, edit the enum in GameStateManager.cs.\n\n" +
                "Anomaly types are now AnomalyDefinition ScriptableObjects — " +
                "create them via right-click → Anomaly / Anomaly Definition.",
                MessageType.Info);

            DrawEnumList<GameStateManager.SessionType>("Session Types", ref _sessionFoldout);
            DrawEnumList<GameStateManager.LevelState> ("Level States",  ref _levelFoldout);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEnumList<TEnum>(string title, ref bool foldout) where TEnum : Enum
        {
            var values = (TEnum[])Enum.GetValues(typeof(TEnum));
            var style  = new GUIStyle(GUI.skin.box) { padding = new RectOffset(6, 6, 6, 6) };
            EditorGUILayout.BeginVertical(style);

            EditorGUILayout.BeginHorizontal();
            foldout = EditorGUILayout.Foldout(foldout, $"{title}  ({values.Length})", true, EditorStyles.foldoutHeader);
            EditorGUILayout.EndHorizontal();

            if (foldout)
            {
                EditorGUI.indentLevel++;
                foreach (var val in values)
                {
                    EditorGUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.Label(val.ToString(), EditorStyles.label);
                    GUILayout.Label($"= {Convert.ToInt32(val)}", EditorStyles.miniLabel, GUILayout.Width(40));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(
                    $"To add a new {title.TrimEnd('s')} value, add it to the {typeof(TEnum).Name} enum in GameStateManager.cs.",
                    MessageType.None);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
    }
    #endif
}