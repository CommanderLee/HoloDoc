using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.WSA.Input;

public class WorldCursor : MonoBehaviour
{
    public GameObject cursor;
    public GameObject cursorLight;
    public UnityEngine.UI.Text HintText;
    public GameObject hololensCamera;
    public string LastHint = "";

    private float MAX_GAZE_DISTANCE = 3.0f;
    private PageOrganizer pageOrganizer;
    
    // Use this for initialization
    void Start()
    {
        // Grab the mesh renderer that's on the same object as this script.
        //meshRenderer = this.gameObject.GetComponentInChildren<MeshRenderer>();
        // Reset to origin.
        cursor.transform.localPosition = Vector3.zero;
        cursorLight.transform.localPosition = Vector3.zero;
        cursor.SetActive(false);
        cursorLight.SetActive(false);
        HintText.text = "";
        HintText.gameObject.transform.localPosition = new Vector3(0, 0, -0.03f);
        HintText.gameObject.transform.localEulerAngles = new Vector3(HintText.gameObject.transform.localEulerAngles.x + 90, HintText.gameObject.transform.localEulerAngles.y, HintText.gameObject.transform.localEulerAngles.z);

        pageOrganizer = GameObject.Find("PageOrganizer").GetComponent<PageOrganizer>();
    }

    // Update is called once per frame
    void Update()
    {
        // Do a raycast into the world based on the user's
        // head position and orientation.
        var headPosition = hololensCamera.transform.position;
        var gazeDirection = hololensCamera.transform.forward;

        RaycastHit hitInfo;

        if (Physics.Raycast(headPosition, gazeDirection, out hitInfo))
        {
            cursor.SetActive(true);
            cursorLight.SetActive(false);

            // Move the cursor to the point where the raycast hit.
            this.transform.position = hitInfo.point;

            // Rotate the cursor to hug the surface of the hologram.
            this.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);

            if (hitInfo.collider.gameObject.name.Contains("CanvasBoard") && pageOrganizer.RefPage.RefFlag != PageConstants.RefEventStatus.OFF)
            {
                // If there is an active reference page
                if (pageOrganizer.RefPage.GetRefName() != LastHint)
                {
                    HintText.text = pageOrganizer.RefPage.GetRefName();
                }
                else
                {
                    HintText.text = "";
                }
            }
        }
        else
        {
            cursor.SetActive(false);
            cursorLight.SetActive(true);

            this.transform.position = hololensCamera.transform.position + hololensCamera.transform.forward * MAX_GAZE_DISTANCE;
            // If the raycast did not hit a hologram, hide the cursor mesh.
            //meshRenderer.enabled = false;
            LastHint = "";
            HintText.text = "";
        }

        //if (GazeManager.Instance == null || cursor == null)
        //{
        //    return;
        //}

        //if (GazeManager.Instance.Hit)
        //{
        //    // If the raycast hit a hologram...
        //    cursor.SetActive(true);

        //    // Move the cursor to the point where the raycast hit.
        //    cursor.transform.position = GazeManager.Instance.Position;

        //    // Rotate the cursor to hug the surface of the hologram.
        //    cursor.transform.rotation = Quaternion.FromToRotation(Vector3.up, GazeManager.Instance.Normal);
        //    //Debug.Log("Hit");
        //}
        //else
        //{
        //    cursor.SetActive(false);
        //    //Debug.Log("Lost");
        //}
    }
}
