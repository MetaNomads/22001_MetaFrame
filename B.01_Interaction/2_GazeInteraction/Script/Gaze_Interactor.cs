using UnityEngine;

public class Gaze_Interactor : MonoBehaviour
{

    [SerializeField]
    private GameObject[] raycastHits;
    [SerializeField]
    private Gaze_Interactable_Object[] gameInteractableObjectsHit;


    // Update is called once per frame
    void Update()
    {
        RaycastHit[] hits;
        //Assume the forward direction of the GameObject is the gaze direction
        hits = Physics.RaycastAll(transform.position, transform.forward);
        GameObject furthestObjectHit = hits[0].collider.gameObject;

        for (int i = 0; i < hits.Length; i++)
        {
            raycastHits[i] = hits[i].collider.gameObject;
            if (raycastHits[i].GetComponent<Gaze_Interactable_Object>() != null)
            {
                gameInteractableObjectsHit[i] = raycastHits[i].GetComponent<Gaze_Interactable_Object>();
                //gameInteractableObjectsHit[i].OnGazeEnter();
            }

            if (Vector3.Distance(transform.position, gameInteractableObjectsHit[i].transform.position) > Vector3.Distance(transform.position, furthestObjectHit.transform.position))
            {
                furthestObjectHit = raycastHits[i];
            }
        }
        Debug.Log("Number of raycast hits: " + hits.Length + " Number of interactable objects hit: " + gameInteractableObjectsHit.Length + " Furthest hit object: " + furthestObjectHit.name);
        Debug.DrawRay(transform.position, transform.forward * 50, Color.magenta);
    }
}
