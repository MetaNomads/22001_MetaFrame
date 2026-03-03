using UnityEngine;

public class SurveyManager : MonoBehaviour
{
    [Header("Pages")]
    public GameObject pageTitle;
    public GameObject pageSurvey;

    void Start()
    {
        ShowTitlePage();
    }

    public void ShowTitlePage()
    {
        pageTitle.SetActive(true);
        pageSurvey.SetActive(false);
    }

    public void StartSurvey()
    {
        pageTitle.SetActive(false);
        pageSurvey.SetActive(true);
    }

    public void SubmitSurvey()
    {
        Debug.Log("Survey submitted");
        // TODO: collect answers here
        gameObject.SetActive(false); // hide canvas
    }
}

