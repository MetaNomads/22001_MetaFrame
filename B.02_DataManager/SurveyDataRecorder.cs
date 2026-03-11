using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UI;
using static GameStateManager;

namespace MetaFrame.Data
{
    public class SurveyDataRecorder : MonoBehaviour
    {
        public DataRecorder dataRecorder;
        public bool survey_C = false;
        public bool firstTimeActivation = true;
        public SurveyControl questionUI; 
        public class SurveyData
        {
            public string detection;
            public string confidence;
            public string pausibility;
            public string report_S; 
        }
        public class StateData
        {
            public string session_T;
            public string sequence_T;
            public string triallNo;
            public string anomaly_T;
            public string initial_T;
            public string at_Source_T;
            public string in_Hand_T;
            public string at_Target_T;
            public string report_E;
            public string anomaly_S;
            public string anomaly_E;
        }

        public class TrialJson
        {
            public string SessionType;
            public string SequenceType;
            public string TrialNo;
            public string AnomalyType;
            public string Detection;
            public string Confidence;
            public string Plausibility;
            public string Initialization;
            public string AtSource;
            public string InHand;
            public string AtTarget;
            public string ReportStart;
            public string ReportEnd;
            public string AnomalyStart;
            public string AnomalyEnd;
            public TrialJson(SurveyData survey, StateData state)
            {
                SessionType = state.session_T;
                SequenceType = state.sequence_T;
                TrialNo = state.triallNo;
                AnomalyType = state.anomaly_T;
                Detection = survey.detection;
                Confidence = survey.confidence;
                Plausibility = survey.pausibility;
                Initialization = state.initial_T;
                AtSource = state.at_Source_T;
                InHand = state.in_Hand_T;
                AtTarget = state.at_Target_T;
                ReportStart = survey.report_S;
                ReportEnd = state.report_E;
                AnomalyStart = state.anomaly_S;
                AnomalyEnd = state.anomaly_E;
            }
        }

        public SurveyData surveyD;
        public StateData stateD;

        void Awake()
        {
            surveyD = new SurveyData();
            stateD = new StateData();
        }
        public void StartSurvey()
        {
            if (firstTimeActivation == true)
            {
                surveyD.report_S = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                firstTimeActivation = false;
            }
        }

        public void SubmitSurvey()
        {
            StoreToggleValues();
            stateDataUpdate();
            surveyCompletion();
            //Debug.Log("Survey Data: " + stateD.triallNo + " " + surveyD.detection + " " + surveyD.confidence + " " + surveyD.explanation + " " + stateD.triall_S + " " + surveyD.report_S + " " + stateD.triall_E + " " + stateD.placed);
            if (survey_C == true)
            {
                firstTimeActivation = true;
                SaveTrial(stateD);
            }
            else
            {
                Debug.Log("survey incomplete");
            }
        }

        public bool surveyCompletion()
        {
            survey_C = IsSurveyDataComplete(surveyD);
            return survey_C;
        }

        public bool IsSurveyDataComplete(SurveyData survey)
        {
            if(survey.detection == "Yes")
            {
                return !string.IsNullOrEmpty(survey.confidence)
                && !string.IsNullOrEmpty(survey.pausibility)
                && !string.IsNullOrEmpty(survey.report_S);
            }
            else if (survey.detection == "No")
            {
                return !string.IsNullOrEmpty(survey.confidence)
                && !string.IsNullOrEmpty(survey.report_S);
            }
            else
            {
                return false;
            }
        }

        public void SaveTrial(StateData updatedStateData)
        {
            TrialJson trialJson = new TrialJson(surveyD, updatedStateData);
            string json = JsonUtility.ToJson(trialJson, false);
            string filePath = Path.Combine(dataRecorder.sessionPath, "Survey.json");
            File.AppendAllText(filePath, json + "\n");

            //Reset
            clearSelection();
            questionUI.ClearUI();
            Debug.Log("survey complete");
        }

        private void clearSelection()
        {
            surveyD = new SurveyData();
            stateD = new StateData();
            firstTimeActivation = true;
        }


        public ToggleGroup toggleGroupConfidence;
        public ToggleGroup toggleGroupExplanation;

        public string GetSelectedToggleValue(ToggleGroup toggleGroup)
        {
            foreach (Toggle toggle in toggleGroup.GetComponentsInChildren<Toggle>())
            {
                if (toggle.isOn)
                {
                    ToggleID id = toggle.GetComponent<ToggleID>();
                    if (id != null)
                    {
                        return id.value;
                    }
                }
            }
            return null;
        }
        public void StoreToggleValues()
        {
            surveyD.confidence = GetSelectedToggleValue(toggleGroupConfidence);
            surveyD.pausibility = GetSelectedToggleValue(toggleGroupExplanation);

            Debug.Log($"Confidence: {surveyD.confidence}, Explanation: {surveyD.pausibility}");
        }

        public void stateDataUpdate()
        {
            stateD.session_T = GameStateManager.instance.GetCurrentSessionData().ToString();
            stateD.triallNo = GameStateManager.instance.GetTrialNumber().ToString();
            var gsm = GameStateManager.instance;
            GameStateManager.TrialData? trialData = gsm.GetCurrentTrialData();
            if (trialData.HasValue)
            {
                TrialData trial = trialData.Value;
                stateD.anomaly_T = trial.state.ToString();
                stateD.initial_T = trial.trialStartTime.ToString();
                //stateD.in_Hand_T = trial.objectInHand.ToString();
                //stateD.at_Target_T = trial.objectAtTarget.ToString();
                stateD.report_E = trial.trialEndTime.ToString();
                //stateD.at_Source_T = trial.objectAtSource.ToString();
            }

        }
    }

}
