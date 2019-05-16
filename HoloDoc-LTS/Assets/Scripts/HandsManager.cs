using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.WSA.Input;

/// <summary>
/// HandsManager keeps track of when a hand is detected.
/// From HoloLens Academy 211.
/// </summary>
public class HandsManager : MySingleton<HandsManager>
{
    [Tooltip("Audio clip to play when Finger Pressed.")]
    public AudioClip FingerPressedSound;
    
    //public PageOrganizer PageOrg;

    private AudioSource audioSource;
    //private WorldCursor worldCursor;

    /// <summary>
    /// Tracks the hand detected state.
    /// </summary>
    public bool HandDetected
    {
        get;
        private set;
    }

    // Keeps track of the GameObject that the hand is interacting with.
    public GameObject FocusedGameObject { get; private set; }

    private PageOrganizer pageOrganizer;

    void Awake()
    {
        EnableAudioHapticFeedback();

        InteractionManager.InteractionSourceDetected += InteractionManager_InteractionSourceDetected;
        InteractionManager.InteractionSourceLost += InteractionManager_InteractionSourceLost;

        InteractionManager.InteractionSourcePressed += InteractionManager_InteractionSourcePressed;
        InteractionManager.InteractionSourceReleased += InteractionManager_InteractionSourceReleased;

        FocusedGameObject = null;

        pageOrganizer = GameObject.Find("PageOrganizer").GetComponent<PageOrganizer>();
        //worldCursor = GameObject.Find("Cursor101").GetComponent<WorldCursor>();
    }

    private void EnableAudioHapticFeedback()
    {
        // If this hologram has an audio clip, add an AudioSource with this clip.
        if (FingerPressedSound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.clip = FingerPressedSound;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1;
            audioSource.dopplerLevel = 0;
        }
    }

    private void InteractionManager_InteractionSourceDetected(InteractionSourceDetectedEventArgs obj)
    {
        HandDetected = true;
    }

    private void InteractionManager_InteractionSourceLost(InteractionSourceLostEventArgs obj)
    {
        HandDetected = false;

        ResetFocusedGameObject();
    }

    private void InteractionManager_InteractionSourcePressed(InteractionSourcePressedEventArgs hand)
    {
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.Play();

            if (GazeManager.Instance != null && GazeManager.Instance.Hit)
            {
                GameObject focusedObject = GazeManager.Instance.HitInfo.collider.gameObject;
                
                // I found that the Unity will create something like "CustomNotes(Clone)"
                if (focusedObject.name.Contains("CustomNotes"))
                {
                    // Notes (pieces of cropped content) being clicked
                    CustomNotesObject notesObject = focusedObject.GetComponent<CustomNotesObject>();
                    Debug.Log(string.Format("Tap on:{0}, page #{1}", notesObject.FileName, notesObject.Page));
                    pageOrganizer.NotePage.UpdateContextDocument(notesObject.FileName, notesObject.Page);
                }
                else if (focusedObject.name.Contains("BoardTag"))
                {
                    // The board tag being clicked
                    pageOrganizer.NotePage.TapOnBoardTag();
                }
                else if (focusedObject.name.Contains("CanvasBoard"))
                {
                    // The blank whiteboard being clicked
                    pageOrganizer.NotePage.TapOnBoard();
                }
                else if (focusedObject.name.Contains("Btn"))
                {
                    Button btn = focusedObject.GetComponent<Button>();
                    btn.onClick.Invoke();
                    //focusedObject.SendMessage("OnSelect", focusedObject, SendMessageOptions.DontRequireReceiver);
                }
                else if (focusedObject.name.Contains("Toggle"))
                {
                    Toggle tg = focusedObject.GetComponent<Toggle>();
                    tg.isOn = !tg.isOn;
                }
                else if (focusedObject.name.Contains("Pie"))
                {
                    PieMenu pieMenu = focusedObject.GetComponent<PieMenu>();
                    PageConstants.RefEventStatus prevRefFlag = pageOrganizer.RefPage.RefFlag;
                    // Invoke on current cursor position
                    int tryTap = pieMenu.MousePressed(true, Vector2.zero);
                    //pieMenu.MousePressed(new Vector2(GazeManager.Instance.Position.x - focusedObject.transform.position.x, GazeManager.Instance.Position.y - focusedObject.transform.position.y), true);
                    // Previously in Full Paper mode (manipulation mode) and now changed
                    if (prevRefFlag == PageConstants.RefEventStatus.FULL_PAPER && tryTap >= 0)
                        GazeGestureManager.Instance.ResetGestureRecognizers();
                }
                else if (focusedObject.name.Contains("PanelTag"))
                {
                    pageOrganizer.TitlePage.TagPointerClick(focusedObject);
                }
                else if (focusedObject.name.Contains("ThumbnailItem"))
                {
                    // Item in PDF Viewer
                    Button btn = focusedObject.GetComponent<Button>();
                    btn.onClick.Invoke();
                }

                Debug.Log("Tap:" + focusedObject.name + " parent: " + focusedObject.transform.parent.gameObject.name + ", pos: " + GazeManager.Instance.Position);
            }
        }
            
        //if (InteractibleManager.Instance.FocusedGameObject != null)
        //{
        //    // Play a select sound if we have an audio source and are not targeting an asset with a select sound.
        //    if (audioSource != null && !audioSource.isPlaying &&
        //        (InteractibleManager.Instance.FocusedGameObject.GetComponent<Interactible>() != null &&
        //        InteractibleManager.Instance.FocusedGameObject.GetComponent<Interactible>().TargetFeedbackSound == null))
        //    {
        //        audioSource.Play();
        //    }

        //    // Cache InteractibleManager's FocusedGameObject in FocusedGameObject.
        //    FocusedGameObject = InteractibleManager.Instance.FocusedGameObject;
        //}
    }

    private void InteractionManager_InteractionSourceReleased(InteractionSourceReleasedEventArgs hand)
    {
        ResetFocusedGameObject();
    }

    private void ResetFocusedGameObject()
    {
        FocusedGameObject = null;

        // Call ResetGestureRecognizers to complete any currently active gestures.
        //GestureManager.Instance.ResetGestureRecognizers();
    }

    void OnDestroy()
    {
        InteractionManager.InteractionSourceDetected -= InteractionManager_InteractionSourceDetected;
        InteractionManager.InteractionSourceLost -= InteractionManager_InteractionSourceLost;

        InteractionManager.InteractionSourceReleased -= InteractionManager_InteractionSourceReleased;
        InteractionManager.InteractionSourcePressed -= InteractionManager_InteractionSourcePressed;
    }
}
