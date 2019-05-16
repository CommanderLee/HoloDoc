using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// From Microsoft HoloLens Academy 210.
/// GazeManager determines the location of the user's gaze, hit position and normals.
/// </summary>
public class GazeManager : MySingleton<GazeManager>
{
    [Tooltip("Maximum gaze distance for calculating a hit.")]
    public float MaxGazeDistance = 5.0f;

    [Tooltip("Select the layers raycast should target.")]
    public LayerMask RaycastLayerMask = Physics.DefaultRaycastLayers;

    public PageOrganizer PageOrg;

    public GameObject hololensCamera;

    /// <summary>
    /// Physics.Raycast result is true if it hits a Hologram.
    /// </summary>
    public bool Hit { get; private set; }

    /// <summary>
    /// HitInfo property gives access
    /// to RaycastHit public members.
    /// </summary>
    public RaycastHit HitInfo { get; private set; }

    /// <summary>
    /// Position of the user's gaze.
    /// </summary>
    public Vector3 Position { get; private set; }

    /// <summary>
    /// RaycastHit Normal direction.
    /// </summary>
    public Vector3 Normal { get; private set; }

    private GazeStabilizer gazeStabilizer;
    private Vector3 gazeOrigin;
    private Vector3 gazeDirection;
    //private GameObject prevObject = null;
    private Button prevButton = null;
    private PieMenu prevPieMenu = null;
    private Toggle prevToggle = null;
    private GameObject prevPanelTag = null;

    void Awake()
    {
        gazeStabilizer = GetComponent<GazeStabilizer>();
    }

    private void Update()
    {
        gazeOrigin = hololensCamera.transform.position;

        gazeDirection = hololensCamera.transform.forward;

        gazeStabilizer.UpdateHeadStability(gazeOrigin, hololensCamera.transform.rotation);

        gazeOrigin = gazeStabilizer.StableHeadPosition;

        UpdateRaycast();
    }

    /// <summary>
    /// Calculates the Raycast hit position and normal.
    /// </summary>
    private void UpdateRaycast()
    {
        RaycastHit hitInfo;

        Hit = Physics.Raycast(gazeOrigin,
                       gazeDirection,
                       out hitInfo,
                       MaxGazeDistance,
                       RaycastLayerMask);

        HitInfo = hitInfo;

        if (Hit)
        {
            // If raycast hit a hologram...

            Position = hitInfo.point;
            Normal = hitInfo.normal;

            GameObject newObj = hitInfo.collider.gameObject;
            if (newObj.name.Contains("Btn"))
            {
                Button newBtn = newObj.GetComponent<Button>();
                if (prevButton == null)
                {
                    // New: Off -> On
                    newBtn.OnPointerEnter(null);

                    // Special judge for resize
                    if (newObj.name.Contains("Resize"))
                    {
                        GazeGestureManager.Instance.Transition(GazeGestureManager.Instance.ManipulationRecognizer);
                    }
                }
                else if (prevButton != newBtn)
                {
                    // Changed: Other Btn -> On
                    prevButton.OnPointerExit(null);
                    newBtn.OnPointerEnter(null);

                    // Special judge for resize
                    if (newObj.name.Contains("Resize"))
                    {
                        GazeGestureManager.Instance.Transition(GazeGestureManager.Instance.ManipulationRecognizer);
                    }
                }
                prevButton = newBtn;
            }
            else if (prevButton != null)
            {
                // On button -> other obj
                prevButton.OnPointerExit(null);
                prevButton = null;

                // Special judge for resize
                if (newObj.name.Contains("Resize") && !GazeGestureManager.Instance.IsManipulating)
                {
                    GazeGestureManager.Instance.ResetGestureRecognizers();
                }
            }

            // Update pie menu hover state
            if (newObj.name.Contains("Pie"))
            {
                // Important: distinguish between InverseTransformPoint vs. xxxVector vs. xxxDirection.
                // https://answers.unity.com/questions/1021968/difference-between-transformtransformvector-and-tr.html
                PieMenu pieMenu = newObj.GetComponent<PieMenu>();
                //GameObject pieMenuRoot = newObj.transform.parent.gameObject;
                Vector3 localPos = pieMenu.transform.InverseTransformPoint(Position);
                pieMenu.MouseMove(localPos);
                //Debug.Log(string.Format("Hit Hover:{0},{1},{2}. PieMenu's local pos:{3},{4},{5}",
                //    localPos.x.ToString("f3"), localPos.y.ToString("f3"), localPos.z.ToString("f3"),
                //    newObj.transform.localPosition.x.ToString("f3"), newObj.transform.localPosition.y.ToString("f3"), newObj.transform.localPosition.z.ToString("f3")));
                //pieMenu.MouseMove(new Vector2(Position.x - newObj.transform.position.x, Position.y - newObj.transform.position.y));
                // Debug: output the format. Result: x+: right, y+: top, using real world coordinate.
                //Debug.Log(string.Format("Hit Local:({0},{1}), Name:{2}",
                //    (Position.x - newObj.transform.position.x).ToString("f2"), (Position.y - newObj.transform.position.y).ToString("f2"), newObj.name));
                prevPieMenu = pieMenu;
            }
            else if (prevPieMenu != null)
            {
                // On piemenu -> other obj
                prevPieMenu.MouseLeave();
                prevPieMenu = null;
            }

            if (newObj.name.Contains("PanelTag"))
            {
                if (prevPanelTag == null)
                {
                    // New Tag: Off -> ON
                    PageOrg.TitlePage.TagPointerEnter(newObj);
                }
                else if (prevPanelTag != newObj)
                {
                    // Changed
                    PageOrg.TitlePage.TagPointerExit(prevPanelTag);
                    PageOrg.TitlePage.TagPointerEnter(newObj);
                }
                prevPanelTag = newObj;
            }
            else if (prevPanelTag != null)
            {
                // Tag -> other obj
                PageOrg.TitlePage.TagPointerExit(prevPanelTag);
                prevPanelTag = null;
            }

            if (newObj.name.Contains("Toggle"))
            {
                Toggle newToggle = newObj.GetComponent<Toggle>();
                if (prevToggle == null)
                {
                    // New: Off -> On
                    newToggle.OnPointerEnter(null);
                }
                else if (prevToggle != newToggle)
                {
                    // Changed: Other Btn -> On
                    prevToggle.OnPointerExit(null);
                    newToggle.OnPointerEnter(null);
                }
                prevToggle = newToggle;
            }
            else if (prevToggle != null)
            {
                // On button -> other obj
                prevToggle.OnPointerExit(null);
                prevToggle = null;
            }

            if (newObj.name.Contains("BoardTag"))
            {
                PageOrg.NotePage.GazeOnBoardTag(true);
            }
            else
            {
                PageOrg.NotePage.GazeOnBoardTag(false);
            }
        }
        else
        {
            // If raycast did not hit a hologram...
            // Save defaults ...
            Position = gazeOrigin + (gazeDirection * MaxGazeDistance);
            Normal = gazeDirection;

            // On Button -> None
            if (prevButton != null)
            {
                //prevObject.SendMessage("OnGazeExit", SendMessageOptions.DontRequireReceiver);
                //prevObject = null;
                prevButton.OnPointerExit(null);
                
                // Special judge for resize
                if (prevButton.gameObject.name.Contains("Resize") && !GazeGestureManager.Instance.IsManipulating)
                {
                    GazeGestureManager.Instance.ResetGestureRecognizers();
                }
                prevButton = null;
            }

            // On Toggle -> None
            if (prevToggle != null)
            {
                prevToggle.OnPointerExit(null);
                prevToggle = null;
            }

            // On PieMenu -> None
            if (prevPieMenu != null)
            {
                prevPieMenu.MouseLeave();
                prevPieMenu = null;
            }

            // On PanelTag -> None
            if (prevPanelTag != null)
            {
                PageOrg.TitlePage.TagPointerExit(prevPanelTag);
                prevPanelTag = null;
            }

            // On BoardTag -> OFF
            PageOrg.NotePage.GazeOnBoardTag(false);
        }
    }
}

