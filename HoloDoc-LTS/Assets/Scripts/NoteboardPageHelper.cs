using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PageConstants;
using System;

public class NoteboardPageHelper : MonoBehaviour {

    #region Unity Game Objects for Noteboard Events
    public GameObject panelBoardObj;
    public GameObject canvasContextRoot;
    public GameObject canvasContextContent;
    public GameObject CanvasMenu;

    public GameObject PanelBackground;
    [Tooltip("Information of the board tag, part of the Root Game Object.")]
    public Canvas CanvasBoardInfo;
    //public Text BoardHintText;
    public GameObject BoardBorder;

    public PageOrganizer PageOrg;
    public string UpdateByClick = "";
    public int MarkerTopLeftID;
    public bool IsResizing = false;
    //public int MarkerBotRightID;
    #endregion

    #region Constant values
    private const float CANVAS_OPEN_TIME = 1.0f;
    private const float CANVAS_CLOSE_TIME = 1.0f;

    // For randomize the location of panels (cm)
    private const float NoteBoardWidth = 1.5f;
    private const float NoteBoardHeight = 1.2f;

    private const float BOARD_MAX_WIDTH = 4.0f;
    private const float BOARD_MAX_HEIGHT = 4.0f;
    private const float BOARD_MIN_WIDTH = 0.5f;
    private const float BOARD_MIN_HEIGHT = 0.5f;

    private const float BOARD_MENU_STD_HEIGHT = 1.2f;
    private const float BOARD_RESIZE_STD_SIZE = 0.3f;

    private const float BoardTagHeight = 0.2f;
    private const float BoardTagWidth = 0.28f;

    private const float CropSafeEdge = 0.005f;
    private const float NotePaddingScale = 0.02f;
    private const float ContextDocWidth = 0.3f;
    #endregion

    #region UI-related private objects
    private List<CustomNotesObject> noteObjList = new List<CustomNotesObject>();
    private SortedList<string, List<CustomNotesObject>> sourceNoteDict = new SortedList<string, List<CustomNotesObject>>();
    private SortedList<string, List<CustomNotesObject>> nameNoteDict = new SortedList<string, List<CustomNotesObject>>();

    private Dictionary<string, GameObject> dateTagDict = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> nameTagDict = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> sourceTagDict = new Dictionary<string, GameObject>();
    private string currTag = "";

    private HorizontalLayoutGroup contextGrid;
    private List<GameObject> contextObjects = new List<GameObject>();

    private WorldCursor worldCursor;

    // board info
    private Text boardHintText;
    private CanvasGroup boardInfoAlpha;
    private GameObject boardImgMove;
    private GameObject boardImgSave;
    private GameObject boardTagBorder;
    private BoxCollider boardCollider;
    private GameObject resizeBtnObj;
    #endregion

    #region Class status attributes
    /// <summary>
    /// How the content notes on this whiteboard is sorted.
    /// Time (date) created, Name, and Source.
    /// </summary>
    public SortingMethods CurrSortMethod = SortingMethods.TIME;

    //private PageOrganizer pageOrg;
    public bool ContextFlag { get; private set; } = false;
    /// <summary>
    /// While opening a piece of objets from the whiteboard, its context information will appear (usually the full documents)
    /// </summary>
    private string contextFileName = "";
    private BoardStatus boardStatus = BoardStatus.NOT_INIT;

    private Vector3 prevManipulationPosition;
    // The next safe top-left point (old: pivot center, start from -W/2, H/2; new: pivor top-left, start from 0, 0)
    private float nextNoteX = 0;//-NoteBoardWidth / 2;
    private float nextNoteY = 0;// NoteBoardHeight / 2;

    private GameObject topLeftObj = null;
    private GameObject botRightObj = null;

    private float markerLostCounter = 3.0f;
    private float currBoardWidth = NoteBoardWidth;
    private float currBoardHeight = NoteBoardHeight;
    private float avgNoteHeight = NoteBoardHeight * 0.3f;
    private float notePadding = NoteBoardWidth * NotePaddingScale;
    #endregion

    // Use this for initialization
    void Start () {
        //pageOrg = gameObject.transform.parent.GetComponent<PageOrganizer>();
        worldCursor = GameObject.Find("Cursor101").GetComponent<WorldCursor>();

        // Noteboard events
        contextGrid = canvasContextContent.GetComponent<HorizontalLayoutGroup>();
        canvasContextContent.SetActive(false);

        // Set the not-init state
        boardStatus = BoardStatus.NOT_INIT;

        BoardBorder.SetActive(false);
        PanelBackground.SetActive(false);
        CanvasMenu.SetActive(false);

        // Set board-tag info
        boardHintText = CanvasBoardInfo.GetComponentInChildren<Text>();
        boardImgMove = GameObject.Find("Image-Move");
        boardImgSave = GameObject.Find("Image-Save");
        boardTagBorder = GameObject.Find("Panel-TagBorder");
        boardCollider = panelBoardObj.GetComponent<BoxCollider>();

        resizeBtnObj = GameObject.Find("BtnResize");

        // DEBUG MODE
        boardHintText.text = "";
        boardImgMove.SetActive(false);
        boardImgSave.SetActive(false);
        boardTagBorder.SetActive(false);

        panelBoardObj.SetActive(false);
    }
	
	// Update is called once per frame
	void Update () {
        if (boardStatus != BoardStatus.COMPLETE && PageOrg && PageOrg.LoadedMarkers 
            && PageOrganizer.DictBarcodeToARUWPMarker.ContainsKey(MarkerTopLeftID)) 
            //&& PageOrganizer.DictBarcodeToARUWPMarker.ContainsKey(MarkerBotRightID))
        {
            //foreach (var key in ARUWPController.markers.Keys)
            //    Debug.Log("key:" + key);
            //Debug.Log(PageOrganizer.DictBarcodeToARUWPMarker[MarkerTopLeftID] + "," + PageOrganizer.DictBarcodeToARUWPMarker[MarkerBotRightID]);
            ARUWPMarker marker1 = ARUWPController.markers[PageOrganizer.DictBarcodeToARUWPMarker[MarkerTopLeftID]];
            //ARUWPMarker marker2 = ARUWPController.markers[PageOrganizer.DictBarcodeToARUWPMarker[MarkerBotRightID]];

            bool found1 = marker1.GetMarkerVisibility();
            //bool found2 = marker2.GetMarkerVisibility();
            //BoardHintText.text = marker1.GetMarkerVisibility() + "," + marker2.GetMarkerVisibility();
            switch (boardStatus)
            {
                case BoardStatus.NOT_INIT:
                    if (found1)
                    {
                        // Ready for moving and further confirmation
                        //BoardBorder.SetActive(true);
                        boardImgMove.SetActive(true);
                        boardHintText.text = "Tap to confirm";
                        if (topLeftObj == null)
                            topLeftObj = marker1.target;
                        //AdaptBoardSize(NoteBoardWidth, NoteBoardHeight);

                        boardStatus = BoardStatus.MOVING;
                        Debug.Log("NOT_INIT -> MOVING");
                    }
                    break;
                case BoardStatus.MOVING:
                    /*
                    if (found2)
                    {
                        // Ready to adjust again
                        // TODO: other visual elements.
                        boardHintText.text = "Tap to confirm (resizing)";
                        if (botRightObj == null)
                            botRightObj = marker2.target;
                        boardStatus = BoardStatus.BOTH_ON;
                        Debug.Log("FIRST_ON -> BOTH_ON");
                    }
                    else if (found1)
                    {
                        // Adjust the position of the origin. Size untouched.
                        gameObject.transform.position = topLeftObj.transform.position;
                        gameObject.transform.rotation = topLeftObj.transform.rotation;
                        gameObject.transform.Rotate(Vector3.left, 180);
                        gameObject.transform.localEulerAngles = GetStableAngle(gameObject.transform.localEulerAngles);

                        boardHintText.text = "Tap to confirm\n (Debug info:" + gameObject.transform.position + ")";
                        // Reset the timer
                        markerLostCounter = 3.0f;
                    }
                    */
                    if (found1)
                    {
                        markerLostCounter = 3.0f;
                    }
                    else
                    {
                        // 00: See if it is time to go back to NOT_INIT
                        markerLostCounter -= Time.deltaTime;
                        if (markerLostCounter <= 0)
                        {
                            //BoardBorder.SetActive(false);
                            boardHintText.text = "";
                            boardImgMove.SetActive(false);
                            boardStatus = BoardStatus.NOT_INIT;
                            Debug.Log("MOVING -> NOT_INIT due to time out.");
                        }
                    }
                    break;

                    /*
                case BoardStatus.BOTH_ON:
                    if (found1 || found2)
                    {
                        float testDirection = Vector3.Dot(topLeftObj.transform.forward, botRightObj.transform.forward);
                        // Test if the two labels are both facing to the same direction
                        if (testDirection >= 0.9f)
                        {
                            // Accept and resize.
                            Vector3 relativeBotRight = topLeftObj.transform.InverseTransformPoint(botRightObj.transform.position);
                            currBoardWidth = Math.Abs(relativeBotRight.x);
                            currBoardHeight = Math.Abs(relativeBotRight.y);
                            boardHintText.fontSize = GetProperFont(currBoardWidth);

                            gameObject.transform.position = topLeftObj.transform.position;
                            gameObject.transform.rotation = topLeftObj.transform.rotation;
                            gameObject.transform.Rotate(Vector3.left, 180);
                            gameObject.transform.localEulerAngles = GetStableAngle(gameObject.transform.localEulerAngles);

                            panelBoardObj.transform.localPosition = new Vector3(currBoardWidth / 2, -currBoardHeight / 2, 0);
                            panelBoardObj.GetComponent<RectTransform>().sizeDelta = new Vector2(currBoardWidth, currBoardHeight);
                            BoardBorder.GetComponent<RectTransform>().sizeDelta = new Vector2(currBoardWidth, currBoardHeight);

                            boardHintText.text = string.Format("Tap to confirm\n(debug info: {0} x {1}, from {2})", currBoardWidth, currBoardHeight, topLeftObj.transform.position);
                        }
                        else
                            boardHintText.text = "Tap to confirm\nDirection not aligned:" + testDirection;
                        // Reset the timer
                        markerLostCounter = 3.0f;
                    }
                    else
                    {
                        // 00: See if it is time to go back to NOT_INIT
                        markerLostCounter -= Time.deltaTime;
                        if (markerLostCounter <= 0)
                        {
                            BoardBorder.SetActive(false);
                            boardStatus = BoardStatus.NOT_INIT;
                            Debug.Log("BOTH_ON -> NOT_INIT due to time out.");
                        }
                    }
                    break;

                case BoardStatus.COMPLETE:
                    // Note: the user taps to transfer from MOVING/FIRST_ON/BOTH_ON to COMPLETE, 
                    // so this transition is not in this switch-logic. 
                    //if (found1)
                    //{
                    //    // update location based on marker 1
                    //    gameObject.transform.position = topLeftObj.transform.position;
                    //    gameObject.transform.rotation = topLeftObj.transform.rotation;
                    //    gameObject.transform.Rotate(Vector3.left, 180);
                    //    gameObject.transform.localEulerAngles = GetStableAngle(gameObject.transform.localEulerAngles);

                    //    panelBoardObj.transform.localPosition = new Vector3(currBoardWidth / 2, -currBoardHeight / 2, 0);

                    //}
                    //else if (found2)
                    //{
                    //    // update location based on marker 2
                    //    gameObject.transform.position = botRightObj.transform.position;
                    //    gameObject.transform.rotation = botRightObj.transform.rotation;
                    //    gameObject.transform.Rotate(Vector3.left, 180);
                    //    gameObject.transform.localEulerAngles = GetStableAngle(gameObject.transform.localEulerAngles);
                    //    // Move to correct position
                    //    gameObject.transform.Translate(new Vector3(-currBoardWidth, currBoardHeight, 0), Space.Self);

                    //    panelBoardObj.transform.localPosition = new Vector3(currBoardWidth / 2, -currBoardHeight / 2, 0);
                    //}
                    break;
                    */
            }
        }
	}

    #region Private Helper
    private int GetProperFont(float realSize)
    {
        int result = Math.Max(1, Math.Min(6, (int)(realSize / 0.4f)));
        return result;
    }

    private const float AngleStableThreshold = 5;
    private Vector3 GetStableAngle(Vector3 rawAngle)
    {
        Vector3 result = new Vector3(rawAngle.x, rawAngle.y, rawAngle.z);
        for (int i = 0; i < 3; ++i)
        {
            for (int j = 0; j < 360; j += 90)
            {
                if (Math.Abs(j - rawAngle[i]) < AngleStableThreshold)
                {
                    // Stable it
                    result[i] = j;
                    break;
                }
            }
        }
        return result;
    }

    private void AdaptBoardSize(float newWidth, float newHeight)
    {
        // Update board size
        currBoardWidth = Mathf.Max(Mathf.Min(newWidth, BOARD_MAX_WIDTH), BOARD_MIN_WIDTH);
        currBoardHeight = Mathf.Max(Mathf.Min(newHeight, BOARD_MAX_HEIGHT), BOARD_MIN_HEIGHT);

        float newMenuScale = currBoardHeight / BOARD_MENU_STD_HEIGHT;
        CanvasMenu.transform.localScale = new Vector3(newMenuScale, newMenuScale, 1);

        // Button: resize, and move to corner
        resizeBtnObj.transform.localScale = new Vector3(newMenuScale, newMenuScale, 1);
        //resizeBtnObj.transform.localPosition = new Vector3(-BOARD_RESIZE_STD_SIZE * newMenuScale / 2, BOARD_RESIZE_STD_SIZE * newMenuScale / 2, 0);

        // TODO: Update UI size of Menu
        //boardHintText.fontSize = GetProperFont(currBoardWidth);
        // Update box collider location, size
        boardCollider.center = new Vector3(currBoardWidth / 2, -currBoardHeight / 2, boardCollider.center.z);
        boardCollider.size = new Vector3(currBoardWidth, currBoardHeight, boardCollider.size.z);
        // Update board image/background info
        //panelBoardObj.transform.localPosition = new Vector3(currBoardWidth / 2, -currBoardHeight / 2, 0);
        panelBoardObj.GetComponent<RectTransform>().sizeDelta = new Vector2(currBoardWidth, currBoardHeight);
        BoardBorder.GetComponent<RectTransform>().sizeDelta = new Vector2(currBoardWidth, currBoardHeight);
    }

    //private void checkCornerGameObjects()
    //{
    //    // Check the GO exist
    //    if (topLeftObj == null && PageOrg.DictBarcodeFileNames.ContainsKey(MarkerTopLeftID))
    //    {
    //        string tempName = PageOrg.DictBarcodeFileNames[MarkerTopLeftID];
    //        if (PageOrganizer.DictPrintedDocObjects.ContainsKey(tempName))
    //            topLeftObj = PageOrganizer.DictPrintedDocObjects[tempName];
    //    }
    //    if (botRightObj == null && PageOrg.DictBarcodeFileNames.ContainsKey(MarkerBotRightID))
    //    {
    //        string tempName = PageOrg.DictBarcodeFileNames[MarkerBotRightID];
    //        if (PageOrganizer.DictPrintedDocObjects.ContainsKey(tempName))
    //            botRightObj = PageOrganizer.DictPrintedDocObjects[tempName];
    //    }
    //}
    #endregion

    #region Public Interfaces
    /// <summary>
    /// Update the noteboard with given document info and cropped region coordinates.
    /// </summary>
    /// <param name="targetDoc"></param>
    /// <param name="page"></param>
    /// <param name="normTLX">Normalized Top-Left X</param>
    /// <param name="normTLY">Normalized Top-Left Y</param>
    /// <param name="normBRX">Normalized Bottom-Right X</param>
    /// <param name="normBRY">Normalized Bottom-Right Y</param>
    public void UpdateNoteBoard(string targetDoc, int page, float normTLX, float normTLY, float normBRX, float normBRY, string source = "")
    {
        // Note: some of them has leading zero. make sure to clean the Resources name format.
        string _fullName = string.Format("{0}/{1}-{2}", targetDoc, targetDoc, page.ToString("D2"));
        //string _fullName = "Norrie03/Norrie03-01";
        Sprite tempBoardSprite = PageOrg.FindSprite(_fullName);
        if (tempBoardSprite == null)
        {
            _fullName = string.Format("{0}/{1}-{2}", targetDoc, targetDoc, page);
            tempBoardSprite = PageOrg.FindSprite(_fullName);
        }
        if (tempBoardSprite == null)
        {
            Debug.Log("UpdateNoteBoard Error: target sprite not found:" + _fullName);
            return;
        }

        GameObject tempObj = Instantiate(Resources.Load("CustomNotes")) as GameObject;
        CustomNotesObject noteObj = tempObj.GetComponent<CustomNotesObject>();
        noteObj.fullImage.sprite = tempBoardSprite;
        
        noteObj.SetMask(normTLX - CropSafeEdge, 1 - normTLY + CropSafeEdge, normBRX, 1 - normBRY - CropSafeEdge);
        noteObj.SetNoteInfo(targetDoc, page, noteObjList.Count, source, DateTime.Today.ToString("d"));
        // Move to fader
        tempObj.transform.SetParent(panelBoardObj.transform);
        tempObj.transform.localEulerAngles = Vector3.zero;

        float newX = 0, newY = 0;
        if (CurrSortMethod == SortingMethods.TIME)
        {
            // Default: put them one-by-one based on when they were created.
            //if (nextNoteX + noteObj.width / 2 > currBoardWidth / 2 || (currTag.Length > 0 && currTag != noteObj.CreateDate))
            if (nextNoteX + noteObj.width / 2 > currBoardWidth || (currTag.Length > 0 && currTag != noteObj.CreateDate))
            {
                // Move to the new line. 
                nextNoteX = notePadding;// -currBoardWidth / 2;
                nextNoteY -= avgNoteHeight;
            }
            if (currTag == "" || currTag != noteObj.CreateDate)
            {
                currTag = noteObj.CreateDate;
                AddTag(currTag, dateTagDict);
            }

            newX = nextNoteX + noteObj.width / 2;
            newY = nextNoteY - noteObj.height / 2;

            nextNoteX += noteObj.width + notePadding;
        }
        else if (CurrSortMethod == SortingMethods.NAME)
        {
            bool adjustFlag = false;
            int insertedID = -1;
            if (nameNoteDict.ContainsKey(targetDoc))
            {
                // make it grow to the next line
                var lastObj = nameNoteDict[targetDoc][nameNoteDict[targetDoc].Count - 1];
                nextNoteX = lastObj.gameObject.transform.localPosition.x + lastObj.width / 2 + notePadding;
                nextNoteY = lastObj.gameObject.transform.localPosition.y + lastObj.height / 2;

                //if (nextNoteX + noteObj.width / 2 > currBoardWidth / 2)
                if (nextNoteX + noteObj.width / 2 > currBoardWidth)
                {
                    // Move to the new line. 
                    nextNoteX = notePadding;// -currBoardWidth / 2;
                    nextNoteY -= avgNoteHeight;
                    adjustFlag = true;
                    insertedID = nameNoteDict.IndexOfKey(targetDoc);
                }
            }
            else
            {
                nameNoteDict[targetDoc] = new List<CustomNotesObject>();
                insertedID = nameNoteDict.IndexOfKey(targetDoc);
                nextNoteX = notePadding;// -currBoardWidth / 2;
                //newX = -currBoardWidth / 2 + noteObj.width / 2;
                if (insertedID == 0)
                    nextNoteY = -notePadding;// currBoardHeight / 2;
                else
                {
                    // Find last note y pos
                    var prevObj = nameNoteDict[nameNoteDict.Keys[insertedID - 1]][nameNoteDict[nameNoteDict.Keys[insertedID - 1]].Count - 1];
                    nextNoteY = prevObj.gameObject.transform.localPosition.y + prevObj.height / 2 - avgNoteHeight;
                }
                // Add a new tag, which will also change the nextNoteX/Y
                AddTag(targetDoc, nameTagDict);
                adjustFlag = true;
            }
            newX = nextNoteX + noteObj.width / 2;
            newY = nextNoteY - noteObj.height / 2;

            if (adjustFlag)
            {   
                // Adjust the stuff under this line (affected)
                for (int i = insertedID + 1; i < nameNoteDict.Keys.Count; ++i)
                {
                    foreach (var obj in nameNoteDict[nameNoteDict.Keys[i]])
                    {
                        Vector3 src = obj.gameObject.transform.position;
                        obj.gameObject.transform.localPosition += new Vector3(0, -avgNoteHeight, 0);
                        StartCoroutine(obj.SlowMove(src, obj.gameObject.transform.position));
                    }
                }
            }
        }
        else if (CurrSortMethod == SortingMethods.SOURCE)
        {
            bool adjustFlag = false;
            int insertedID = -1;
            if (sourceNoteDict.ContainsKey(source))
            {
                var lastObj = sourceNoteDict[source][sourceNoteDict[source].Count - 1];
                nextNoteX = lastObj.gameObject.transform.localPosition.x + lastObj.width / 2 + notePadding;
                nextNoteY = lastObj.gameObject.transform.localPosition.y + lastObj.height / 2;

                //if (nextNoteX + noteObj.width / 2 > currBoardWidth / 2)
                if (nextNoteX + noteObj.width / 2 > currBoardWidth)
                {
                    // Move to the new line. 
                    nextNoteX = notePadding;// -currBoardWidth / 2;
                    nextNoteY -= avgNoteHeight;
                    adjustFlag = true;
                    insertedID = sourceNoteDict.IndexOfKey(source);
                }
            }
            else
            {
                sourceNoteDict[source] = new List<CustomNotesObject>();
                insertedID = sourceNoteDict.IndexOfKey(source);
                nextNoteX = notePadding;// -currBoardWidth / 2;
                //newX = -currBoardWidth / 2 + noteObj.width / 2;
                if (insertedID == 0)
                    nextNoteY = -notePadding;// currBoardHeight / 2;
                else
                {
                    // Find last note y pos
                    var prevObj = sourceNoteDict[sourceNoteDict.Keys[insertedID - 1]][sourceNoteDict[sourceNoteDict.Keys[insertedID - 1]].Count - 1];
                    nextNoteY = prevObj.gameObject.transform.localPosition.y + prevObj.height / 2 - avgNoteHeight;
                }
                // Add a new tag, which will also change the nextNoteX/Y
                AddTag(source, sourceTagDict);
                adjustFlag = true;
            }
            newX = nextNoteX + noteObj.width / 2;
            newY = nextNoteY - noteObj.height / 2;

            if (adjustFlag)
            {
                // Adjust the stuff under this line (affected)
                for (int i = insertedID + 1; i < sourceNoteDict.Keys.Count; ++i)
                {
                    foreach (var obj in sourceNoteDict[sourceNoteDict.Keys[i]])
                    {
                        Vector3 src = obj.gameObject.transform.position;
                        obj.gameObject.transform.localPosition += new Vector3(0, -avgNoteHeight, 0);
                        StartCoroutine(obj.SlowMove(src, obj.gameObject.transform.position));
                    }
                }
            }
        }
        
        Vector3 srcPos = PageOrg.hololensCamera.transform.position + PageOrg.hololensCamera.transform.forward * 0.8f + PageOrg.hololensCamera.transform.up * (-0.05f);
        //DEBUG:         //newX = newY = 0;
        noteObj.currTransform.localPosition = new Vector3(newX, newY, -0.01f);
        Vector3 destPos = noteObj.currTransform.position;
        Debug.Log("SlowMove:" + srcPos + ", " + destPos);
        StartCoroutine(noteObj.SlowMove(srcPos, destPos));

        // Add to the list and update the dictionary
        noteObjList.Add(noteObj);

        if (!nameNoteDict.ContainsKey(targetDoc))
            nameNoteDict[targetDoc] = new List<CustomNotesObject>();
        nameNoteDict[targetDoc].Add(noteObj);

        if (source.Length > 0)
        {
            if (!sourceNoteDict.ContainsKey(source))
                sourceNoteDict[source] = new List<CustomNotesObject>();
            sourceNoteDict[source].Add(noteObj);
        }

        //tempObj.transform.localPosition = new Vector3(newX, newY, 0);

        Debug.Log(string.Format("Update note board; Load sprite:{0}, At ({1}, {2})", _fullName, newX, newY));
    }

    public void UpdateContextDocument(string fileName, int page)
    {
        Debug.Log(string.Format("Context Document:{0}, page:{1}", fileName, page));
        canvasContextContent.SetActive(true);
        ContextFlag = true;

        if (fileName != contextFileName)
        {
            contextFileName = fileName;

            StartCoroutine(OpenContextDoc());

            // Clear, if any.
            foreach (GameObject go in contextObjects)
            {
                Destroy(go);
            }

            int pageID = 1;
            Sprite tempDocSprite;
            while (tempDocSprite = Resources.Load<Sprite>(string.Format("{0}/{1}-{2}", fileName, fileName, pageID.ToString("D2"))))
            {
                GameObject tempObj = Instantiate(Resources.Load("ContextDocument")) as GameObject;
                Image tempImg = tempObj.GetComponent<Image>();
                tempImg.sprite = tempDocSprite;
                tempObj.transform.SetParent(contextGrid.transform);
                tempObj.transform.localScale = Vector3.one;
                tempObj.transform.localPosition = Vector3.zero;
                tempObj.transform.localRotation = contextGrid.transform.localRotation;
                contextObjects.Add(tempObj);
                pageID++;
            }

            prevManipulationPosition = Vector3.zero;
        }
        // Start the manipulation recognizer
        GazeGestureManager.Instance.Transition(GazeGestureManager.Instance.ManipulationRecognizer);
    }

    public void CloseContextDocument()
    {
        if (ContextFlag)
        {
            StartCoroutine(CloseContextDoc());
            GazeGestureManager.Instance.Transition(GazeGestureManager.Instance.NavigationRecognizer);
            Debug.Log("Context closed by gesture.");
        }
    }

    public void MoveContextContent(Vector3 direction)
    {
        if (ContextFlag)
        {
            //Vector3 moveVector = Vector3.zero;
            //moveVector = direction - prevManipulationPosition;
            //prevManipulationPosition = direction;

            canvasContextContent.transform.position += direction;//new Vector3(direction.x, direction.y, 0);
        }
    }

    public void TapOnBoard()
    {
        string refName = PageOrg.RefPage.GetRefName();
        if (refName.Length > 0 && refName != UpdateByClick && boardStatus ==  BoardStatus.COMPLETE)
        {
            // Move this ref to the board. TODO: which board?
            UpdateNoteBoard(refName, 1, 0.05f, 0.05f, 0.95f, 0.95f, PageOrg.RefPage.GetRefSource());
            UpdateByClick = refName;
            worldCursor.LastHint = refName;
        }
    }

    public void TapOnResizeBtn()
    {
        Debug.Log("Tap on Resize Btn");
        IsResizing = true;
        GazeGestureManager.Instance.Transition(GazeGestureManager.Instance.ManipulationRecognizer);
    }

    public void ResizeBoardGesture(Vector3 gestureVec)
    {
        AdaptBoardSize(currBoardWidth + gestureVec.x * 5, currBoardHeight - gestureVec.y * 5);
    }

    public void ResizeComplete()
    {
        // Adjust all the contents on it.
        if (CurrSortMethod == SortingMethods.TIME)
            ToggleSortByTime(true);
        else if (CurrSortMethod == SortingMethods.NAME)
            ToggleSortByName(true);
        else if (CurrSortMethod == SortingMethods.SOURCE)
            ToggleSortBySource(true);
    }

    public void TapOnBoardTag()
    {
        if (boardStatus == BoardStatus.MOVING)
        {
            //BoardBorder.SetActive(false);
            panelBoardObj.SetActive(true);
            boardImgMove.SetActive(false);

            // Tap to confirm the positioning and resizing
            gameObject.transform.position = topLeftObj.transform.position;
            gameObject.transform.rotation = topLeftObj.transform.rotation;
            gameObject.transform.Rotate(Vector3.left, 180);
            gameObject.transform.localEulerAngles = GetStableAngle(gameObject.transform.localEulerAngles);

            AdaptBoardSize(currBoardWidth, currBoardHeight);

            // zoom out the menu and contents.
            StartCoroutine(ResumeBoard());

            avgNoteHeight = currBoardHeight * 0.3f;
            notePadding = currBoardWidth * NotePaddingScale;
            // TODO: resize the font on the menu.
            boardStatus = BoardStatus.COMPLETE;
            Debug.Log("Tap to transfer to COMPLETE.");
        }
        else if (boardStatus == BoardStatus.ARCHIVE_READY)
        {
            // Play an archive animation.
            boardImgSave.SetActive(false);
            StartCoroutine(ArchiveBoard());
            Debug.Log("Tap to archive the content, transfer to NOT_INIT");
        }
    }

    public void GazeOnBoardTag(bool flag)
    {
        boardTagBorder.SetActive(flag);
        if (flag)
        {
            if (boardStatus == BoardStatus.COMPLETE)
            {
                // COMPLETE to ARCHIVE_READY
                boardImgSave.SetActive(true);
                boardHintText.text = "Archive and close";
                boardStatus = BoardStatus.ARCHIVE_READY;
            }
        }
        else
        {
            if (boardStatus == BoardStatus.ARCHIVE_READY)
            {
                // Back to COMPLETE
                boardImgSave.SetActive(false);
                boardHintText.text = "";
                boardStatus = BoardStatus.COMPLETE;
            }
        }
    }

    #endregion

    #region UI Behaviors

    private IEnumerator ArchiveBoard()
    {
        for (float i = 1.0f; i > 0; i -= Time.deltaTime)
        {
            gameObject.transform.localScale = Vector3.one * i;
            yield return null;
        }
        // Hide menu
        CanvasMenu.SetActive(false);
        PanelBackground.SetActive(false);
        boardHintText.text = "";
        boardStatus = BoardStatus.NOT_INIT;
    }

    private IEnumerator ResumeBoard()
    {
        CanvasMenu.SetActive(true);
        PanelBackground.SetActive(true);
        for (float i = 0; i < 1.0f; i += Time.deltaTime)
        {
            gameObject.transform.localScale = Vector3.one * i;
            yield return null;
        }
    }

    private IEnumerator OpenContextDoc()
    {
        // Move as the user moves
        // TODO: Challenge: if the user looks down, the canvas will appear at the bottom
        canvasContextRoot.transform.position = PageOrg.hololensCamera.transform.position;
        canvasContextRoot.transform.rotation = PageOrg.hololensCamera.transform.rotation;
        canvasContextRoot.transform.Translate(PageOrg.hololensCamera.transform.forward, Space.World);

        for (float i = 0; i < 1; i += Time.deltaTime)
        {
            canvasContextRoot.gameObject.GetComponent<RectTransform>().localScale = Vector3.one * i;
            yield return null;
        }
    }

    private IEnumerator CloseContextDoc()
    {
        for (float i = 1; i > 0; i -= Time.deltaTime)
        {
            canvasContextRoot.gameObject.GetComponent<RectTransform>().localScale = Vector3.one * i;
            yield return null;
        }
        canvasContextContent.SetActive(false);
        ContextFlag = false;
    }

    private void AddTag(string tagValue, Dictionary<string, GameObject> targetDict)
    {
        if (!targetDict.ContainsKey(tagValue))
        {
            GameObject tagObj = Instantiate(Resources.Load("WhiteboardTag")) as GameObject;
            tagObj.transform.SetParent(panelBoardObj.transform);
            tagObj.transform.localEulerAngles = Vector3.zero;
            Text tagInfo = tagObj.GetComponentInChildren<Text>();
            tagInfo.text = tagValue;
            Debug.Log("add new tag:" + tagValue);
            targetDict.Add(tagValue, tagObj);
        }
        targetDict[tagValue].SetActive(true);
        targetDict[tagValue].transform.localPosition = new Vector3(nextNoteX + BoardTagWidth / 2, nextNoteY - BoardTagHeight / 2, -0.01f);
        nextNoteX += BoardTagWidth;
    }

    /// <summary>
    /// Sort by time, if current method is not 'time'.
    /// </summary>
    /// <param name="value">If value is true, then sort anyway.</param>
    public void ToggleSortByTime(bool value)
    {
        Debug.Log("On Button Sort-by-Time" + value);
        if (CurrSortMethod != SortingMethods.TIME || value)
        {
            // Clear the other tags
            foreach (var tagObj in nameTagDict.Values)
            {
                tagObj.SetActive(false);
            }
            foreach (var tagObj in sourceTagDict.Values)
            {
                tagObj.SetActive(false);
            }

            // sort by time
            nextNoteX = notePadding;// -currBoardWidth / 2;
            nextNoteY = -notePadding;// currBoardHeight / 2;
            currTag = "";
            
            foreach (var obj in noteObjList)
            {
                //if (nextNoteX + obj.width / 2 > currBoardWidth / 2 || (currTag.Length > 0 && obj.CreateDate != currTag))
                if (nextNoteX + obj.width / 2 > currBoardWidth || (currTag.Length > 0 && obj.CreateDate != currTag))
                {
                    // Move to the new line. 
                    nextNoteX = notePadding;// -currBoardWidth / 2;
                    nextNoteY -= avgNoteHeight;
                }
                if (obj.CreateDate != currTag)
                {
                    currTag = obj.CreateDate;
                    AddTag(currTag, dateTagDict);
                }

                float newX = nextNoteX + obj.width / 2;
                float newY = nextNoteY - obj.height / 2;
                Vector3 src = obj.gameObject.transform.position;
                obj.currTransform.localPosition = new Vector3(newX, newY, -0.01f);

                nextNoteX += obj.width + notePadding;

                StartCoroutine(obj.SlowMove(src, obj.gameObject.transform.position));
            }
            CurrSortMethod = SortingMethods.TIME;
        }
    }

    public void ToggleSortByName(bool value)
    {
        Debug.Log("On Button Sort-by-Name" + value);
        if (CurrSortMethod != SortingMethods.NAME || value)
        {
            // Clear the other tags
            foreach (var tagObj in sourceTagDict.Values)
            {
                tagObj.SetActive(false);
            }
            foreach (var tagObj in dateTagDict.Values)
            {
                tagObj.SetActive(false);
            }

            // sort by name
            int rowID = 0;
            nextNoteX = notePadding;// -currBoardWidth / 2;
            nextNoteY = -notePadding;// currBoardHeight / 2;

            // Create the first tag
            currTag = "";

            foreach (var key in nameNoteDict.Keys)
            {
                foreach (var obj in nameNoteDict[key])
                {
                    //if (nextNoteX >= currBoardWidth / 2 || (currTag.Length > 0 && obj.FileName != currTag))
                    if (nextNoteX >= currBoardWidth || (currTag.Length > 0 && obj.FileName != currTag))
                    {
                        // Move to the new line. 
                        nextNoteX = notePadding;// -currBoardWidth / 2;
                        nextNoteY -= avgNoteHeight;
                    }
                    if (obj.FileName != currTag)
                    {
                        currTag = obj.FileName;
                        AddTag(currTag, nameTagDict);
                    }

                    float newX = nextNoteX + obj.width / 2;
                    float newY = nextNoteY - obj.height / 2;
                    Vector3 src = obj.gameObject.transform.position;
                    obj.currTransform.localPosition = new Vector3(newX, newY, -0.01f);

                    nextNoteX += obj.width + notePadding;

                    StartCoroutine(obj.SlowMove(src, obj.gameObject.transform.position));
                }
                rowID++;
            }
            CurrSortMethod = SortingMethods.NAME;
        }
    }

    public void ToggleSortBySource(bool value)
    {
        Debug.Log("On Button Sort-by-Source" + value);
        if (CurrSortMethod != SortingMethods.SOURCE || value)
        {
            // Clear the other tags
            foreach (var tagObj in nameTagDict.Values)
            {
                tagObj.SetActive(false);
            }
            foreach (var tagObj in dateTagDict.Values)
            {
                tagObj.SetActive(false);
            }

            // sort by source
            int rowID = 0;
            nextNoteX = notePadding;// -currBoardWidth / 2;
            nextNoteY = -notePadding;// currBoardHeight / 2;
            
            // Create the first tag
            currTag = "";

            foreach (var key in sourceNoteDict.Keys)
            {
                foreach (var obj in sourceNoteDict[key])
                {
                    //if (nextNoteX >= currBoardWidth / 2 || (currTag.Length > 0 && obj.Source != currTag))
                    if (nextNoteX >= currBoardWidth || (currTag.Length > 0 && obj.Source != currTag))
                    {
                        // Move to the new line. 
                        nextNoteX = notePadding;// -currBoardWidth / 2;
                        nextNoteY -= avgNoteHeight;
                    }
                    if (obj.Source != currTag)
                    {
                        currTag = obj.Source;
                        AddTag(currTag, sourceTagDict);
                    }

                    float newX = nextNoteX + obj.width / 2;
                    float newY = nextNoteY - obj.height / 2;
                    Vector3 src = obj.gameObject.transform.position;
                    obj.currTransform.localPosition = new Vector3(newX, newY, -0.01f);

                    nextNoteX += obj.width + notePadding;

                    StartCoroutine(obj.SlowMove(src, obj.gameObject.transform.position));
                }
                rowID++;
            }
            CurrSortMethod = SortingMethods.SOURCE;
        }
    }
    #endregion
}
