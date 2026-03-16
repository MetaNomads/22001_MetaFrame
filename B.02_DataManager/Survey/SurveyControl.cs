using System;
using System.Collections.Generic;
using MetaFrame.Data;
using UnityEngine;
using UnityEngine.UI;


public class SurveyControl : MonoBehaviour
{
    [SerializeField] private GameObject pausibilitySurvey;
    [SerializeField] private GameObject panelOne;
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject startSession;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private Toggle toggle_y;
    [SerializeField] private Toggle toggle_n;
    [SerializeField] private ToggleGroup detectionGroup;
    [SerializeField] private ToggleGroup confidenceGroup;
    [SerializeField] private ToggleGroup pausibilityGroup;
    [SerializeField] private SurveyDataRecorder surveyDataRecorder;
    [SerializeField] private List<ToggleGroup> groups = new List<ToggleGroup>();

    public void SurveyStateManager()
    {
        if (toggle_y.isOn)
        {
            print("Yes");
        }
        else if (toggle_n.isOn)
        {
            print("No");
        }

        bool detectionAnswered = detectionGroup.AnyTogglesOn();
        bool confidenceAnswered = confidenceGroup.AnyTogglesOn();
        if (detectionAnswered && confidenceAnswered)
        {
            pausibilitySurvey.SetActive(true);
        }
        else
        {
            pausibilitySurvey.SetActive(false);
        }
    }
        
    public void tutorialSetup()
    {
        resetAllPanel();
        startPanel.SetActive(true);
    }
    public void surveySetup()
    {
        resetAllPanel();
        panelOne.SetActive(true);
        surveyDataRecorder.StartReport();
    }
    public void sessionSetup()
    {
        resetAllPanel();
        startSession.SetActive(true);
    }
    public void experimentEndSetup()
    {
        resetAllPanel();
        endPanel.SetActive(true);
    }
    public void resetAllPanel()
    {
        pausibilitySurvey.SetActive(false);
        panelOne.SetActive(false);
        startPanel.SetActive(false);
        startSession.SetActive(false);
        endPanel.SetActive(false);
    }
    public void ResetAllGroups()
    {
        foreach (var group in groups)
        {
            foreach (Toggle toggle in group.GetComponentsInChildren<Toggle>())
            {
                if (toggle.isOn)
                {
                    toggle.isOn = false;

                }
            }
        }
    }
    public void ClearUI()
    {
        print("survey clear");
        ResetAllGroups();
    }
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
    public void storeSurveyData()
    {
        surveyDataRecorder.SetDetection(GetSelectedToggleValue(detectionGroup));
        surveyDataRecorder.SetConfidence(GetSelectedToggleValue(confidenceGroup));
        surveyDataRecorder.SetPlausibility(GetSelectedToggleValue(pausibilityGroup));
    }
}
