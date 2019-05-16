using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.WSA.Input;

// Refer: Microsoft Academy HoloLens 211.
public class GazeGestureManager : MonoBehaviour
{
    //public GameObject handObj;
    //public GameObject navHandRingObj;
    public GameObject navCloseObj;
    public GameObject navLeftObj;
    public GameObject navRightObj;

    public static GazeGestureManager Instance { get; private set; }

    // Represents the hologram that is currently being gazed at.
    //public GameObject FocusedObject { get; private set; }

    // Tap and Navigation gesture recognizer.
    public GestureRecognizer NavigationRecognizer { get; private set; }

    // Manipulation gesture recognizer.
    public GestureRecognizer ManipulationRecognizer { get; private set; }

    // Currently active gesture recognizer.
    public GestureRecognizer ActiveRecognizer { get; private set; }

    // Navigation works as scroll bar, and manipulation works as 1:1 moving 

    public bool IsNavigating { get; private set; }

    public Vector3 NavigationPosition { get; private set; }

    public bool IsManipulating { get; private set; }

    public Vector3 ManipulationPosition { get; private set; }
    private Vector3 prevManipulationPosition = Vector3.zero;

    /// <summary>
    /// Offset > UI Threshold: show the UI; trigger continuous action.
    /// Offset > UI Protect Threshold: show the UI and lock, hide UI on other directions
    /// Offset > Trigger Threshold: Trigger discrete action.
    /// </summary>
    private const float NavUIThreshold = 0.1f;
    private const float NavProtectThreshold = 0.5f;
    private const float NavTriggerThreshold = 0.9f;
    private const float UIMoveSensitivity = 0.01f;

    private const float ManipulationTriggerThreshold = 0.2f;
    private const float ManipulationUIThreshold = 0.1f;

    private enum ManipulationTarget { N_A, FULL_PAPER, RESIZE_BOARD, UNKNOWN }
    private ManipulationTarget currManipulationTarget = ManipulationTarget.N_A;

    private bool hasTriggeredClose;
    private PageOrganizer pageOrganizer;

    // Use this for initialization
    void Awake()
    {
        hasTriggeredClose = false;
        pageOrganizer = GameObject.Find("PageOrganizer").GetComponent<PageOrganizer>();

        Instance = this;

        // Hide the navigation arrows on the UI
        //navHandRingObj.SetActive(false);
        navCloseObj.SetActive(false);
        navLeftObj.SetActive(false);
        navRightObj.SetActive(false);

        // Set up a GestureRecognizer to detect Select gestures.
        NavigationRecognizer = new GestureRecognizer();
        NavigationRecognizer.SetRecognizableGestures(GestureSettings.Tap | GestureSettings.NavigationX | GestureSettings.NavigationY);
        //recognizer.Tapped += (args) =>
        //{
        //    // Inform the microphone manager
        //    //_spManager.switchTextUI();
        //    bool result = _micManager.switchTextUI();

        //    Debug.Log("GazeGestureManager:Tap, result:" + result);
        //};

        NavigationRecognizer.Tapped += NavigationRecognizer_Tapped;
        //NavigationRecognizer.NavigationStarted += NavigationRecognizer_NavigationStarted;
        //NavigationRecognizer.NavigationUpdated += NavigationRecognizer_NavigationUpdated;
        //NavigationRecognizer.NavigationCompleted += NavigationRecognizer_NavigationCompleted;
        //NavigationRecognizer.NavigationCanceled += NavigationRecognizer_NavigationCanceled;

        //NavigationRecognizer.StartCapturingGestures();

        // Instantiate the ManipulationRecognizer.
        ManipulationRecognizer = new GestureRecognizer();

        // Add the ManipulationTranslate GestureSetting to the ManipulationRecognizer's RecognizableGestures.
        ManipulationRecognizer.SetRecognizableGestures(
            GestureSettings.ManipulationTranslate);

        // Register for the Manipulation events on the ManipulationRecognizer.
        ManipulationRecognizer.ManipulationStarted += ManipulationRecognizer_ManipulationStarted;
        ManipulationRecognizer.ManipulationUpdated += ManipulationRecognizer_ManipulationUpdated;
        ManipulationRecognizer.ManipulationCompleted += ManipulationRecognizer_ManipulationCompleted;
        ManipulationRecognizer.ManipulationCanceled += ManipulationRecognizer_ManipulationCanceled;

        ResetGestureRecognizers();
    }

    /// <summary>
    /// Revert back to the default GestureRecognizer.
    /// </summary>
    public void ResetGestureRecognizers()
    {
        // Default to the navigation gestures.
        Transition(NavigationRecognizer);
    }

    /// <summary>
    /// Transition to a new GestureRecognizer.
    /// </summary>
    /// <param name="newRecognizer">The GestureRecognizer to transition to.</param>
    public void Transition(GestureRecognizer newRecognizer)
    {
        if (newRecognizer == null)
        {
            return;
        }

        if (ActiveRecognizer != null)
        {
            if (ActiveRecognizer == newRecognizer)
            {
                return;
            }

            ActiveRecognizer.CancelGestures();
            ActiveRecognizer.StopCapturingGestures();
        }

        newRecognizer.StartCapturingGestures();
        ActiveRecognizer = newRecognizer;
    }

    // Update is called once per frame
    void Update()
    {
        if (IsNavigating)
        {
            float currX = NavigationPosition.x;

            // Block close if the x-axis movement is bigger than Protect Threshold
            bool blockClose = false;

            if (currX > NavUIThreshold)
            {
                navLeftObj.SetActive(false);
                navRightObj.SetActive(true);
                // move smoothly: fire all the events here, and decide in their own methods
                //pageOrganizer.canvasContextContent.transform.Translate(new Vector3(currX * UIMoveSensitivity, 0, 0), Space.Self);
                pageOrganizer.NotePage.MoveContextContent(new Vector3(currX * UIMoveSensitivity, 0, 0));
                if (currX > NavProtectThreshold)
                {
                    blockClose = true;
                }
            }
            else if (currX > -NavUIThreshold)
            {
                navLeftObj.SetActive(false);
                navRightObj.SetActive(false);
            }
            else
            {
                navLeftObj.SetActive(true);
                navRightObj.SetActive(false);
                // move smoothly
                //pageOrganizer.canvasContextContent.transform.Translate(new Vector3(currX * UIMoveSensitivity, 0, 0), Space.Self);
                pageOrganizer.NotePage.MoveContextContent(new Vector3(currX * UIMoveSensitivity, 0, 0));
                if (currX < -NavProtectThreshold)
                {
                    blockClose = true;
                }
            }

            float currY = NavigationPosition.y;

            if (!blockClose && (currY > NavUIThreshold || currY < -NavUIThreshold))
            {
                navCloseObj.SetActive(true);
                if (!hasTriggeredClose && (currY > NavTriggerThreshold || currY < -NavTriggerThreshold))
                {
                    // Activate the close action
                    pageOrganizer.NotePage.CloseContextDocument();
                    hasTriggeredClose = true;
                    Debug.Log("Trigger Close Action");
                }
            }
            else
            {
                navCloseObj.SetActive(false);
            }
        }

        if (IsManipulating)
        {
            if (currManipulationTarget == ManipulationTarget.FULL_PAPER)
            {
                float currY = ManipulationPosition.y;

                if (Math.Abs(currY) > ManipulationUIThreshold)
                {
                    navCloseObj.SetActive(true);
                    if (!hasTriggeredClose && Math.Abs(currY) >= ManipulationTriggerThreshold)
                    {
                        pageOrganizer.NotePage.CloseContextDocument();
                        pageOrganizer.RefPage.CloseRefDocument();
                        hasTriggeredClose = true;
                        Debug.Log("Trigger Close Action");
                    }
                }
                else
                {
                    navCloseObj.SetActive(false);
                }
            }
            
        }

        //navHandRingObj.SetActive(IsNavigating);        
        //if (!handObj)
        //{
        //    handObj.SetActive(HandsManager.Instance.HandDetected);
        //    Debug.Log(HandsManager.Instance.HandDetected);
        //}
        
        //// Figure out which hologram is focused this frame.
        //GameObject oldFocusObject = FocusedObject;

        //// Do a raycast into the world based on the user's
        //// head position and orientation.
        //var headPosition = Camera.main.transform.position;
        //var gazeDirection = Camera.main.transform.forward;

        //RaycastHit hitInfo;
        //if (Physics.Raycast(headPosition, gazeDirection, out hitInfo))
        //{
        //    // If the raycast hit a hologram, use that as the focused object.
        //    FocusedObject = hitInfo.collider.gameObject;
        //}
        //else
        //{
        //    // If the raycast did not hit a hologram, clear the focused object.
        //    FocusedObject = null;
        //}

        //// If the focused object changed this frame,
        //// start detecting fresh gestures again.
        //if (FocusedObject != oldFocusObject)
        //{
        //    recognizer.CancelGestures();
        //    recognizer.StartCapturingGestures();
        //}
    }

    private void NavigationRecognizer_Tapped(TappedEventArgs obj)
    {
        // Pause/Resume dictation to allow user speaking

        // Debug: move them to HandManager
        /*
        if (GazeManager.Instance != null && GazeManager.Instance.Hit)
        {
            GameObject focusedObject = GazeManager.Instance.HitInfo.collider.gameObject;

            // Note: the method is not migrated yet.
            // I found that the Unity will create something like "CustomNotes(Clone)"
            if (focusedObject.name.Contains("CustomNotes"))
            {
                CustomNotesObject notesObject = focusedObject.GetComponent<CustomNotesObject>();
                Debug.Log(string.Format("Tap on:{0}, page #{1}", notesObject.fileName, notesObject.page));
                pageOrganizer.NotePage.UpdateContextDocument(notesObject.fileName, notesObject.page);
            }
            else if (focusedObject.name.Contains("Btn"))
            {
                Button btn = focusedObject.GetComponent<Button>();
                btn.onClick.Invoke();
                //focusedObject.SendMessage("OnSelect", focusedObject, SendMessageOptions.DontRequireReceiver);
            }
            else if (focusedObject.name.Contains("Pie"))
            {
                PieMenu pieMenu = focusedObject.GetComponent<PieMenu>();
                // Invoke on current cursor position
                pieMenu.MousePressed(true, Vector2.zero);
                //pieMenu.MousePressed(new Vector2(GazeManager.Instance.Position.x - focusedObject.transform.position.x, GazeManager.Instance.Position.y - focusedObject.transform.position.y), true);
            }

            Debug.Log("Tap:" + focusedObject.name + " parent: " + focusedObject.transform.parent.gameObject.name + ", pos: " + GazeManager.Instance.Position);
        }
        */
    }

    private void NavigationRecognizer_NavigationStarted(NavigationStartedEventArgs obj)
    {
        Debug.Log("Debug: navigation started.");
        //IsNavigating = true;
        //hasTriggeredClose = false;
        //NavigationPosition = Vector3.zero;
    }

    private void NavigationRecognizer_NavigationUpdated(NavigationUpdatedEventArgs obj)
    {
        Debug.Log("Debug: navigation updated.");
        //IsNavigating = true;

        //// -1 ~ 1. Start from 0. There are also some space out of range (-1 to +1) and were counted as +1/-1.
        //NavigationPosition = obj.normalizedOffset;
        ////Debug.Log(NavigationPosition.x);
    }

    private void NavigationRecognizer_NavigationCompleted(NavigationCompletedEventArgs obj)
    {
        Debug.Log("Debug: navigation completed.");
        //IsNavigating = false;
        //navCloseObj.SetActive(false);
        //navLeftObj.SetActive(false);
        //navRightObj.SetActive(false);
    }

    private void NavigationRecognizer_NavigationCanceled(NavigationCanceledEventArgs obj)
    {
        Debug.Log("Debug: navigation canceled.");
        //IsNavigating = false;
        //navCloseObj.SetActive(false);
        //navLeftObj.SetActive(false);
        //navRightObj.SetActive(false);
    }

    private void ManipulationRecognizer_ManipulationStarted(ManipulationStartedEventArgs obj)
    {
        // Tap on resize
        if (pageOrganizer.NotePage.IsResizing)
        {
            IsManipulating = true;
            ManipulationPosition = Vector3.zero;
            prevManipulationPosition = Vector3.zero;
            currManipulationTarget = ManipulationTarget.RESIZE_BOARD;
        }
        // Manipulate Context Canvas
        else if (pageOrganizer.NotePage.ContextFlag || pageOrganizer.RefPage.RefFlag == PageConstants.RefEventStatus.FULL_PAPER)
        {
            IsManipulating = true;
            ManipulationPosition = Vector3.zero;
            prevManipulationPosition = Vector3.zero;
            hasTriggeredClose = false;
            currManipulationTarget = ManipulationTarget.FULL_PAPER;
            //Debug.Log("Test: Manipulation started??");
        }
        else
        {
            Debug.Log("Unknown source triggerred the manipulation mode.");
            currManipulationTarget = ManipulationTarget.UNKNOWN;
        }

        // Try to invoke the click gesture, if valid
        // Update: moved to HandsManager.cs
        //if (pageOrganizer.RefPage.RefFlag == PageConstants.RefEventStatus.FULL_PAPER && GazeManager.Instance != null && GazeManager.Instance.Hit)
        //{
        //    int tryTap = pageOrganizer.RefPage.PieMenu.MousePressed(true, Vector2.zero);
        //    if (tryTap >= 0)
        //        ResetGestureRecognizers();
        //}
    }

    private void ManipulationRecognizer_ManipulationUpdated(ManipulationUpdatedEventArgs obj)
    {
        IsManipulating = true;

        if (currManipulationTarget == ManipulationTarget.RESIZE_BOARD)
        {
            ManipulationPosition = obj.cumulativeDelta;

            pageOrganizer.NotePage.ResizeBoardGesture(ManipulationPosition - prevManipulationPosition);

            prevManipulationPosition = ManipulationPosition;
        }
        else if (currManipulationTarget == ManipulationTarget.FULL_PAPER)
        {
            // Manipulate Context Canvas
            if (pageOrganizer.NotePage.ContextFlag)
            {
                ManipulationPosition = obj.cumulativeDelta;
                //Debug.Log(obj.cumulativeDelta);

                pageOrganizer.NotePage.MoveContextContent(ManipulationPosition - prevManipulationPosition);
                prevManipulationPosition = ManipulationPosition;
            }
            if (pageOrganizer.RefPage.RefFlag == PageConstants.RefEventStatus.FULL_PAPER)
            {
                ManipulationPosition = obj.cumulativeDelta;

                pageOrganizer.RefPage.MoveRefDocument(ManipulationPosition - prevManipulationPosition);
                prevManipulationPosition = ManipulationPosition;
                //Debug.Log("Test: Manipulation updated??");
            }
        }
        
    }

    private void ManipulationRecognizer_ManipulationCompleted(ManipulationCompletedEventArgs obj)
    {
        IsManipulating = false;
        if (currManipulationTarget == ManipulationTarget.RESIZE_BOARD)
        {
            // Only use once. 
            currManipulationTarget = ManipulationTarget.N_A;
            pageOrganizer.NotePage.ResizeComplete();
            ResetGestureRecognizers();
        }
        else if (currManipulationTarget == ManipulationTarget.FULL_PAPER)
        {
            navCloseObj.SetActive(false);
        }
    }

    private void ManipulationRecognizer_ManipulationCanceled(ManipulationCanceledEventArgs obj)
    {
        IsManipulating = false;
        if (currManipulationTarget == ManipulationTarget.RESIZE_BOARD)
        {
            // Only use once. 
            currManipulationTarget = ManipulationTarget.N_A;
            pageOrganizer.NotePage.ResizeComplete();
            ResetGestureRecognizers();
        }
        else if (currManipulationTarget == ManipulationTarget.FULL_PAPER)
        {
            navCloseObj.SetActive(false);
        }
    }

    void OnDestroy()
    {
        // Unregister the Tapped and Navigation events on the NavigationRecognizer.
        NavigationRecognizer.Tapped -= NavigationRecognizer_Tapped;

        //NavigationRecognizer.NavigationStarted -= NavigationRecognizer_NavigationStarted;
        //NavigationRecognizer.NavigationUpdated -= NavigationRecognizer_NavigationUpdated;
        //NavigationRecognizer.NavigationCompleted -= NavigationRecognizer_NavigationCompleted;
        //NavigationRecognizer.NavigationCanceled -= NavigationRecognizer_NavigationCanceled;

        // Unregister the Manipulation events on the ManipulationRecognizer.
        ManipulationRecognizer.ManipulationStarted -= ManipulationRecognizer_ManipulationStarted;
        ManipulationRecognizer.ManipulationUpdated -= ManipulationRecognizer_ManipulationUpdated;
        ManipulationRecognizer.ManipulationCompleted -= ManipulationRecognizer_ManipulationCompleted;
        ManipulationRecognizer.ManipulationCanceled -= ManipulationRecognizer_ManipulationCanceled;
    }
}