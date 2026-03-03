using System;
using System.Collections.Generic;
using MetaFrame.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class SurveyControl : MonoBehaviour
{
    public GameObject survey;
    public Toggle toggle_y;
    public Toggle toggle_n;
    public SurveyDataRecorder surveyDataRecorder;
    public List<ToggleGroup> groups = new List<ToggleGroup>();

    public void SurveyStateManager()
    {

        if (toggle_y.isOn == true)
        {
            surveyDataRecorder.surveyD.detection = "Yes";
            surveyDataRecorder.StartSurvey();
            survey.SetActive(true);
        }
        else 
        {
            surveyDataRecorder.surveyD.detection = "No";
            surveyDataRecorder.StartSurvey();
            survey.SetActive(false); 
        }
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
}
