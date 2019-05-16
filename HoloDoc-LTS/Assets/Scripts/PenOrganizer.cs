using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PageConstants;
using UnityEngine.XR.WSA.WebCam;
using System.Linq;
#if !UNITY_EDITOR
using Neosmartpen.Net;
using Neosmartpen.Net.Bluetooth;
using PDollarGestureRecognizer;
using PDollarDemo;
using System.Threading.Tasks;
using Windows.UI.Input.Inking;
using Windows.Foundation;
using Windows.Storage;
#endif

public class PenOrganizer : MonoBehaviour {
    #region Unity GameObjects to display the info
    public GameObject HololensCamera;

    public Text penInfoText;
    public Text penTextTitleBar;
    public GameObject strokeInfo;
    public Text strokeHint;
    //public GameObject PieMenuCanvas;

    public GameObject penIconPanel;
    public GameObject penIconImg;

    public GameObject calibObject;

    public GameObject StickyManagerObj;

    public Canvas InputHintCanvas;
    #endregion

    private LineRenderer strokeRenderer;
    
    private List<List<Vector3>> strokes;
    private List<Vector3>       dots;
    private Vector3 dotTopLeft, dotBotRight;
    private Vector2 currPenPos;

    #region Constant values about Anoto patterns

    /// <summary>
    /// Pen position coordinate (pen coordniate) -> real world coordinate (m)
    /// </summary>
    private const float NCODE_UNIT = 0.002371f;         // 2.371 mm / dot-pixel

    private const float REF_HIT_THRESHOLD_DOTS = 0.013f / NCODE_UNIT;     // 1.3 cm
    private const float TITLE_HIT_THRESHOLD_DOTS = 0.15f / NCODE_UNIT;    // 15 cm
    private const float AUTHOR_HIT_THRESHOLD_DOTS = 0.04f / NCODE_UNIT;   // 4 cm
    private const float LINE_HIT_THRESHOLD_DOTS = 0.012f / NCODE_UNIT;    // 1.2 cm
    private const float PEN_HIT_THRESHOLD_DOTS = 0.04f / NCODE_UNIT;     // 4 cm
    /// <summary>
    /// Used to get the top-left corner relative position.
    /// </summary>
    private const float MARKER_RADIUS = 0.02f;  // Marker size: 4 cm

    public static float AnotoPageHeight = 118;
    public static float AnotoPageWidth = 91;

    private enum STICKY_CODE { NONE, TOP_BTN, BOT_BTN, INPUT_BOX };
    private const float ORIGIN_X = 6.17f;
    private const float ORIGIN_Y = 5.425f;
    private const float CELL_WIDTH = 26.27f;
    private const float CELL_HEIGHT = 26.235f;
    private const float BTN_LEFT = 17f;
    private const float BTN_MID = 9.075f;
    private const float BTN_BOT = 17.375f;
    // Standard size of a letter-size paper
    private const float PAPER_WIDTH = 0.216f;
    private const float PAPER_HEIGHT = 0.279f;

    private const string CONTROL_SHEET = "3-25-510";
    public static CONTROL_CODE PenControlCode = CONTROL_CODE.DEFAULT;
    #endregion

    // Interacting with other Unity GameObjects
    private CanvasGroup penIconGroup;
    private Text inputHintText;
    private PageOrganizer pageOrg;
    private StickyManager stickyManager;
    // Debug.
    private OnlineSearchManager onlineSearchManager = null;

    private string calibFileName = "calibration.txt";
    private int calibCounter = 0;

    private ARUWPController aruwpControl;

#if !UNITY_EDITOR
    /// <summary>
    /// Save the information about Anoto Pages.
    /// Key: anoto pattern string ("section-note-page"). Value: Anoto Page class.
    /// </summary>
    private Dictionary<string, AnotoPage> _anotoPages = new Dictionary<string, AnotoPage>();

    #region Members for the NeoSmartpen SDK Bluetooth controller (Moved from PenService)
    private BluetoothPenClient  _client;
    private PenController       _controller;
    private PenInformation      _targetPen = null;
    private bool                _connected = false;
    #endregion

    #region Current pen status
    /// <summary>
    /// Current Pen Event Type: based on Neosmartpen SDK, PEN DOWN (start writing): 0, MOVE: 1, and UP (stand by: normal state): 2.
    /// </summary>
    private DotTypes    _currPenEvent;
    /// <summary>
    /// An indicator to store some debugging info of the pen status (during and after the initPen process)
    /// </summary>
    private string      _currStatus;
    /// <summary>
    /// Level of the clicks if there are continuous actions to be taken
    /// 0: default, 1: simple click, 2 and more: based on definition.
    /// For reference: 1: title and meta-data; 2: front page; 3: full paper; 4: printing action
    /// </summary>
    //private int         _penClickLevel = 0;
    #endregion

    #region Current page status
    // Anoto Patterns (Digital Pen)
    private int _currSection = 0;
    private int _currNote = 0;
    private int _currPage = 0;
    /// <summary>
    /// "section-note-page", also the key in the _anotoPages
    /// </summary>
    private string      _currAnotoPatt = "0-0-0";
    private AnotoPage   _currAnotoPage = null;
    private string      _currFileName = "DefaultName";
    private string      _currRefName = "DefaultRefName";
    private ARDocument  _currDocument = null;
    #endregion

    #region Current stroke information

    /// <summary>
    /// When PEN_DOWN, check if the pen tip is in the Sticky's Input Box. 
    /// Then this variable is used as a condition when PEN_UP
    /// </summary>
    private bool isStickyInput = false;
    private bool isControlSheet = false;

    /// <summary>
    /// Useful to determine whether we need to call the API service. Or just use the old results.
    /// Set true for every stroke in Sticky Input Box, and set false when processed and save to currInkingResults.
    /// </summary>
    private bool hasNewStickyInputStrokes = false;
    private int         _strokeCounter = 0;
    private const float MULTI_STROKE_THRESHOLD = 1000;
    /// <summary>
    /// The Timestamp when the last stroke finished.
    /// Used to determine whether two strokes are connected, and whether we are ready to report the command to the Unity App
    /// </summary>
    private DateTime _lastStrokeTime;
    /// <summary>
    /// Count the current stroke number until they are recognized
    /// </summary>
    private int _multiStrokeFlag = 1;
    private float _multiMinX, _multiMaxX, _multiMinY, _multiMaxY;
    
    private const float CLICK_GAP = 6.3f;                  // Approximately 1.5cm
    private const float CLICK_HEIGHT_THRESHOLD = 6.3f;     // Approximatedly 1.5cm, for 10 dots

    /// <summary>
    /// This flag indicates whether a click hits any valuable objects on the paper
    /// 0: waiting, 1: started, -1: ended (pen up) or not valid (not hit any target)
    /// </summary>
    private int _clickHitFlag = 0;
    /// <summary>
    /// This is the threshold for triggering the click
    /// </summary>
    private const int CLICK_TRIGGER = 5;             // Got at least 5 dots
    #endregion

    #region Microsoft API Service
    Queue<MicrosoftAPIHelper> helperQueue = new Queue<MicrosoftAPIHelper>();
    //MicrosoftAPIHelper currHelper = new MicrosoftAPIHelper();
    // Containers for the handwriting recognition API
    /// <summary>
    /// Store all the formal "Stroke"s. 
    /// </summary>
    private InkStrokeContainer inkStrokes;
    private Dictionary<string, InkStrokeContainer> dictAnoto2Strokes = new Dictionary<string, InkStrokeContainer>();
    /// <summary>
    /// Handler (manager) of the strokes. Build the strokes.
    /// </summary>
    private InkStrokeBuilder strokeBuilder;
    /// <summary>
    /// Temporary container for the points. Once finished, we convert pointsList to actual "Stroke".
    /// This container clears itself for every pen-down-move-up cycle.
    /// </summary>
    //private List<InkPoint> pointsForInking = new List<InkPoint>();
    /// <summary>
    /// Plain points are used to replace "points for inking", since CreatePointsFromInkpoints are not available.
    /// Instead, we need to construct a point list, and turn it into a stroke.
    /// </summary>
    private List<Windows.Foundation.Point> plainPoints = new List<Windows.Foundation.Point>();
    private string currInkingResult;
    private GameObject currOCRRoot;
    enum HelperEventListener { N_A, DO_ONLINE_SEARCH, DO_LOCAL_SEARCH, SEARCH_FROM_OCR };
    private HelperEventListener currListener = HelperEventListener.N_A;

    /// <summary>
    /// Since the original pen stroke resolution is 91x118, we scale it up x10.
    /// </summary>
    public static float INKING_SCALE = 10.0f;
    /// <summary>
    /// This matrix determines how the pointLists are mapped to the target stroke.
    /// </summary>
    private System.Numerics.Matrix3x2 strokeMatrix;
    #endregion

    #region PDollar API Service
    /// <summary>
    /// Points container for $P service. This container clears when the stroke recognition is completed (or timeout)
    /// </summary>
    private List<PDollarGestureRecognizer.Point> pointsForPDollar = new List<PDollarGestureRecognizer.Point>();   // mouse points acquired from the user
    private Gesture[] trainingSet = null;   // training set loaded from XML files

    #endregion

    #region PieMenu variables
    /// <summary>
    /// Pie Menu instance. Could be part of title/reference/sticky game object.
    /// Note: pie click now use virtual click version
    /// </summary>
    //private PieMenu pieMenu;
    // Update: if we are going to support multiple pie menus, then these should be indepedent. Moved to parameter.
    //private PieMenuType currPieType = PieMenuType.NOT_INIT;
    private Vector2 pieClickNormPos = Vector2.zero;
    /// <summary>
    /// The context of current pie menu. 
    /// For title events, it is the current file name. 
    /// For reference events, it is the active ref name.
    /// </summary>
    //private string currPieFileName = "";
    #endregion
#endif

    // Use this for initialization
    void Start () {
        
        pageOrg = GameObject.Find("PageOrganizer").GetComponent<PageOrganizer>();
        ResetDotBoundry();

        strokeRenderer = strokeInfo.GetComponent<LineRenderer>();
        strokeRenderer.widthMultiplier = 0.5f;

        penIconGroup = penIconPanel.GetComponent<CanvasGroup>();
        penIconImg.SetActive(false);
        // Video shooting mode: close debug menu
        penIconGroup.alpha = 0;

        // Clear
        strokeHint.text = "";

        stickyManager = StickyManagerObj.GetComponent<StickyManager>();
        // DEBUG
        //onlineSearchManager = StickyManagerObj.GetComponentInChildren<OnlineSearchManager>();

        // These attributes were used to receive app call response from PenService.
        currPenPos = Vector2.zero;
        dots = new List<Vector3>();
        strokes = new List<List<Vector3>>();

        inputHintText = InputHintCanvas.GetComponentInChildren<Text>();
        inputHintText.text = "";
        //inputHintText.gameObject.SetActive(false);

        aruwpControl = GameObject.Find("ARUWP Controller").GetComponent<ARUWPController>();
        
#if !UNITY_EDITOR
        // Pen status initialization.
        _currPenEvent = DotTypes.PEN_UP;
        _currStatus = "Initialization.";

        // Page status initialization
        LoadAnotoPageInfo();
        LoadTrainingSet();

        // Stroke status initialization
        _lastStrokeTime = DateTime.Now;
        _multiMinX = float.MaxValue;
        _multiMinY = float.MaxValue;
        _multiMaxX = float.MinValue;
        _multiMaxY = float.MinValue;

        InitPen();

        // Pie Menu Info
        //pieMenu = PieMenuCanvas.GetComponentInChildren<PieMenu>();

        // External API service handler initialization
        inkStrokes = new InkStrokeContainer();
        strokeBuilder = new InkStrokeBuilder();
        InkDrawingAttributes inkDrawingAttributes = new InkDrawingAttributes();
        inkDrawingAttributes.Size = new Size(3, 3);
        inkDrawingAttributes.IgnorePressure = true;
        strokeBuilder.SetDefaultDrawingAttributes(inkDrawingAttributes);
        strokeMatrix = System.Numerics.Matrix3x2.CreateScale(INKING_SCALE);

        /*
        // Create a file for calibration
        Task task = new Task(
            async () =>
            {
                // HoloLens path
                StorageFolder appFolder = KnownFolders.CameraRoll;
                StorageFile calibFile = await appFolder.CreateFileAsync(calibFileName, CreationCollisionOption.GenerateUniqueName);
                calibFileName = calibFile.Name;
                Debug.Log("Output file:" + calibFileName);
            });
        task.Start();
        task.Wait();

        calibCounter = 0;
        //calibObject.transform.position = new Vector3(-0.2f, -0.33f, 0.8f);
        calibObject.transform.localPosition = new Vector3(-0.05f, 0, 0.5f);
        calibObject.SetActive(false);
        */
        
#endif
    }

    // Update is called once per frame
    void Update () {
#if !UNITY_EDITOR

        //if (currPieType == PieMenuType.TITLE)
        //{
        //    PieMenuCanvas.transform.position = pageOrg.TitleRootObj.transform.position;
        //    PieMenuCanvas.transform.rotation = pageOrg.TitleRootObj.transform.rotation;
        //    PieMenuCanvas.transform.Translate(new Vector3(-0.1f, -0.1f, -0.05f), pageOrg.TitleRootObj.transform);
        //}
        //else if (currPieType == PieMenuType.REFERENCE)
        //{
        //    PieMenuCanvas.transform.position = pageOrg.RefRootObj.transform.position;
        //    PieMenuCanvas.transform.rotation = pageOrg.RefRootObj.transform.rotation;
        //    PieMenuCanvas.transform.Translate(new Vector3(-0.1f, -0.1f, -0.05f), pageOrg.RefRootObj.transform);
        //}

        if (helperQueue.Count > 0)
        {
            MicrosoftAPIHelper front = helperQueue.Peek();
            if (front.ocrStatus != MicrosoftAPIHelper.SERVICE_STATUS.IDLE)
            {
                // OCR algorithm going on
                if (front.ocrStatus == MicrosoftAPIHelper.SERVICE_STATUS.DONE)
                {
                    // Call functions to use this result.
                    Debug.Log(string.Format("Update(): Async OCR function done with result <{0}>.", front.ocrResult));
                    helperQueue.Dequeue();
                    string result = front.ocrResult.Trim('.', ',', '\'', '\"', '[', ']', '-', ':', ';');

                    // Find it from the marks
                    int deleteID = -1;
                    for (int i = _currAnotoPage.marks.Count - 1; i >= 0; --i)
                    {
                        if (_currAnotoPage.marks[i].x == front.ocrAvgX && _currAnotoPage.marks[i].y == front.ocrAvgY)
                        {
                            // Found it.
                            if (result.Length > 0)
                            {
                                _currAnotoPage.marks[i].other = result;
                                // See if the user is waiting for this result.
                                if (currListener == HelperEventListener.SEARCH_FROM_OCR)
                                {
                                    inputHintText.text = "";
                                    Vector2 normXY = NormalizeAnotoPos(front.ocrAvgX, front.ocrAvgY);
                                    Debug.Assert(onlineSearchManager != null);
                                    onlineSearchManager.StartNewSearch(result, createOCRRoot(normXY), SearchType.ONLINE);
                                }
                                else
                                {
                                    // Just show the result
                                    inputHintText.text = "[" + result + "]";
                                }
                            }
                            else
                            {
                                // Found but failed. Delete it. 
                                inputHintText.text = "";
                                deleteID = i;
                            }
                            break;
                        }
                    }
                    if (deleteID >= 0)
                    {
                        _currAnotoPage.marks.RemoveAt(deleteID);
                    }
                    // Debug: Test Search Function.
                    //if (front.ocrResult.Length > 0)
                    //{
                    //    onlineSearchManager.StartNewSearch(front.ocrResult);
                    //    //MicrosoftAPIHelper helper = new MicrosoftAPIHelper();
                    //    //helper.AcademicSearch(front.ocrResult, 10, 0);
                    //    //helperQueue.Enqueue(helper);
                    //}
                }
                else if (front.ocrStatus == MicrosoftAPIHelper.SERVICE_STATUS.ERROR)
                {
                    Debug.Log(string.Format("Update(): Async OCR function error with info <{0}>.", front.ocrResult));
                    helperQueue.Dequeue();

                    // Find it from the marks
                    int deleteID = -1;
                    for (int i = _currAnotoPage.marks.Count - 1; i >= 0; --i)
                    {
                        if (_currAnotoPage.marks[i].x == front.ocrAvgX && _currAnotoPage.marks[i].y == front.ocrAvgY)
                        {
                            // Found but failed. Delete it. 
                            inputHintText.text = "";
                            deleteID = i;
                            break;
                        }
                    }
                    if (deleteID >= 0)
                    {
                        _currAnotoPage.marks.RemoveAt(deleteID);
                    }
                }
            }
            else if (front.inkingStatus != MicrosoftAPIHelper.SERVICE_STATUS.IDLE)
            {
                // Inking service going on
                if (front.inkingStatus == MicrosoftAPIHelper.SERVICE_STATUS.DONE)
                {
                    // Call functions to use this result.
                    Debug.Log(string.Format("Update(): Async Inking function done with result <{0}>.", front.inkingResult));
                    helperQueue.Dequeue();
                    currInkingResult = front.inkingResult;
                    //inputHintText.gameObject.SetActive(false);
                    inputHintText.text = "";

                    if (currListener != HelperEventListener.N_A)
                    {
                        if (currInkingResult.Length > 0)
                        {
                            if (currListener == HelperEventListener.DO_ONLINE_SEARCH)
                            {
                                Debug.Assert(onlineSearchManager != null);
                                onlineSearchManager.StartNewSearch(currInkingResult, pageOrg.CurrDocObj, SearchType.ONLINE);
                            }
                            else if (currListener == HelperEventListener.DO_LOCAL_SEARCH)
                            {
                                Debug.Assert(onlineSearchManager != null);
                                onlineSearchManager.StartNewSearch(currInkingResult, pageOrg.CurrDocObj, SearchType.LOCAL);
                            }
                            //inputHintText.gameObject.SetActive(false);
                        }
                        else
                        {
                            inputHintText.text = "Recognition error. Please try again.";
                        }
                        currListener = HelperEventListener.N_A;
                    }
                }
                else if (front.inkingStatus == MicrosoftAPIHelper.SERVICE_STATUS.ERROR)
                {
                    Debug.Log(string.Format("Update(): Async Inking function error with info <{0}>.", front.inkingResult));
                    helperQueue.Dequeue();
                    inputHintText.text = front.inkingResult;
                }
            }
        }

        if (_currPenEvent == DotTypes.PEN_DOWN || _currPenEvent == DotTypes.PEN_MOVE)
        {
            // Just toggle the pen icon, but always show the boundary for debugging
            //penIconGroup.alpha = 1;
            penIconImg.SetActive(true);

            // Trigger the file writing for calibration operation
            //SaveCalibration();
        }
        else if (_currPenEvent == DotTypes.PEN_UP)
        {
            //penIconGroup.alpha = 0;
            penIconImg.SetActive(false);
        }
        else
        {
            Debug.Log("Unknown Pen Event:" + _currPenEvent);
        }

        penInfoText.text = String.Format("Update: " + _currAnotoPatt + ";" + _clickHitFlag);
        penTextTitleBar.text = _currStatus;// + ";" + currPenCmd;

        // Update Input Hint
        //if (pageOrg.CurrDocObj != null)
        //{
        //    // TODO: Put this to real pen tip position. Could also used to show pen tip feedback.
        //    InputHintCanvas.transform.position = pageOrg.CurrDocObj.transform.position;
        //}
#endif
        //var points = new Vector3[100];
        //var t = Time.time;
        //for (int i = 0; i < 100; i++)
        //{
        //    points[i] = new Vector3(i * 0.5f, Mathf.Sin(i + t), 0.0f);
        //}
        //strokeRenderer.SetPositions(points);
        //var points = _dots.ToArray();
        //strokeRenderer.SetPositions(points);
        //Debug.Log("``````" + _dots.Count);
    }

    private void ResetDotBoundry()
    {
        dotTopLeft = new Vector3(200, 200, 0);     // Make sure they will be updated.
        dotBotRight = Vector3.zero;
    }

#if !UNITY_EDITOR

    #region Preload information and data
    private void LoadAnotoPageInfo()
    {
        // TODO: load the info from local JSON files.
        string pattern00 = "3-24-0";
        AnotoPage page00 = new AnotoPage("Norrie03", 1);
        // AnotoMark class: type, id, x, y
        page00.marks.Add(new AnotoMark(AnotoMarkTypes.TITLE, 0, 46, 17));
        page00.marks.Add(new AnotoMark(AnotoMarkTypes.AUTHOR, 0, 30, 22));
        page00.marks.Add(new AnotoMark(AnotoMarkTypes.AUTHOR, 1, 60, 22));

        page00.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 15, 39, 77));
        page00.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 32, 42, 77));
        page00.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 14, 36, 80));
        page00.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 29, 35, 94));
        page00.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 27, 35, 106));
        page00.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 19, 52, 40));
        page00.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 28, 58, 52));
        page00.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 25, 50, 64));

        _anotoPages.Add(pattern00, page00);

        for (int pg = 2; pg <= 10; ++pg)
        {
            string patterni = string.Format("3-24-{0}", pg - 1);
            AnotoPage pagei = new AnotoPage("Norrie03", pg);
            _anotoPages.Add(patterni, pagei);
        }

        string pattern01 = "3-24-20";
        AnotoPage page01 = new AnotoPage("OHara97", 1);
        page01.marks.Add(new AnotoMark(AnotoMarkTypes.TITLE, 0, 46, 8));
        page01.marks.Add(new AnotoMark(AnotoMarkTypes.AUTHOR, 0, 42, 12));
        page01.marks.Add(new AnotoMark(AnotoMarkTypes.AUTHOR, 1, 56, 12));
        _anotoPages.Add(pattern01, page01);

        for (int pg = 2; pg <= 9; ++pg)
        {
            string patterni = string.Format("3-24-{0}", pg - 1 + 20);
            AnotoPage pagei = new AnotoPage("OHara97", pg);
            _anotoPages.Add(patterni, pagei);
        }

        string pattern02 = "3-24-30";
        AnotoPage page02 = new AnotoPage("Johnson93", 1);
        page02.marks.Add(new AnotoMark(AnotoMarkTypes.TITLE, 0, 48, 12));
        page02.marks.Add(new AnotoMark(AnotoMarkTypes.TITLE, 1, 48, 16));
        page02.marks.Add(new AnotoMark(AnotoMarkTypes.AUTHOR, 0, 27, 21));
        page02.marks.Add(new AnotoMark(AnotoMarkTypes.AUTHOR, 1, 38, 21));
        page02.marks.Add(new AnotoMark(AnotoMarkTypes.AUTHOR, 2, 48, 21));
        page02.marks.Add(new AnotoMark(AnotoMarkTypes.AUTHOR, 3, 59, 21));
        page02.marks.Add(new AnotoMark(AnotoMarkTypes.AUTHOR, 4, 70, 21));
        _anotoPages.Add(pattern02, page02);

        for (int pg = 2; pg <= 6; ++pg)
        {
            string patterni = string.Format("3-24-{0}", pg - 1 + 30);
            AnotoPage pagei = new AnotoPage("Johnson93", pg);
            _anotoPages.Add(patterni, pagei);
        }

        string pattern03 = "3-24-40";
        AnotoPage page03 = new AnotoPage("Klamka17", 1);
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.TITLE, 0, 46, 13));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.AUTHOR, 0, 40, 17));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.AUTHOR, 1, 53, 17));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 35, 80, 58));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 34, 83, 58));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 17, 83, 64));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 55, 68, 82));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 34, 71, 82));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 59, 73, 82));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 65, 61, 85));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 44, 74, 85));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 27, 83, 85));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 28, 48, 87));
        page03.marks.Add(new AnotoMark(AnotoMarkTypes.REFERENCE, 33, 68, 87));
        _anotoPages.Add(pattern03, page03);

        for (int pg = 2; pg <= 13; ++pg)
        {
            string patterni = string.Format("3-24-{0}", pg - 1 + 40);
            AnotoPage pagei = new AnotoPage("Klamka17", pg);
            _anotoPages.Add(patterni, pagei);
        }

        string pattern52 = "3-25-0";
        AnotoPage page52 = new AnotoPage("Sticky52", 1);
        _anotoPages.Add(pattern52, page52);

        string pattern53 = "3-25-1";
        AnotoPage page53 = new AnotoPage("Sticky53", 1);
        _anotoPages.Add(pattern53, page53);

        string pattern54 = "3-25-2";
        AnotoPage page54 = new AnotoPage("Sticky54", 1);
        _anotoPages.Add(pattern54, page54);

        string pattern55 = "3-25-3";
        AnotoPage page55 = new AnotoPage("Sticky55", 1);
        _anotoPages.Add(pattern55, page55);

        string pattern56 = "3-25-4";
        AnotoPage page56 = new AnotoPage("Sticky56", 1);
        _anotoPages.Add(pattern56, page56);
    }

    /// <summary>
    /// Loads training gesture samples from XML files, for $P recognizer.
    /// </summary>
    /// <returns></returns>
    private void LoadTrainingSet()
    {
        // While testing on the HoloLens, the gestures are in the Roaming State folder from the Device Portal.
        List<Gesture> gestures = new List<Gesture>();

        Task task = new Task(
            async () =>
            {
                StorageFolder appFolder = KnownFolders.CameraRoll;
                IReadOnlyList<StorageFile> allFiles = await appFolder.GetFilesAsync();
                foreach (StorageFile sf in allFiles)
                {
                    //Debug.Log("Found file: " + sf.Path);
                    if (sf.Name.Contains("gesture"))
                    {
                        gestures.Add(GestureIO.ReadGesture(sf.Path));
                        //Debug.Log("Gestures:" + gestures.Count);
                    }
                }

                Debug.Log("Gestures:" + gestures.Count);
                trainingSet = gestures.ToArray();
                Debug.Log("Trainset:" + trainingSet.Length);
            });
        task.Start();
        task.Wait();
    }
    #endregion

    private void InitPen()
    {
        // Create PenController instance.
        _controller = new PenController();
        // Create BluetoothPenClient instance. and bind PenController.
        // BluetoothPenClient is implementation of bluetooth function.
        _client = new BluetoothPenClient(_controller);

        // Pen controller event
        //_controller.PenStatusReceived += MController_PenStatusReceived;
        _controller.Connected += MController_Connected;
        _controller.Disconnected += MController_Disconnected;
        _controller.Authenticated += MController_Authenticated;
        _controller.DotReceived += MController_DotReceived;
        _controller.PasswordRequested += MController_PasswordRequested;

        SearchPairedPen();
    }

    private async void SearchPairedPen()
    {
        _client.StopWatcher();

        List<PenInformation> result = await _client.FindPairedDevices();

        foreach (PenInformation item in result)
        {
            Debug.Log("Found: " + item.MacAddress);

            if (item.MacAddress == "9c:7b:d2:02:c3:9a")
            {
                _targetPen = item;
                break;
            }
        }

        if (_targetPen != null)
        {
            Debug.Log("SearchPaired: " + _targetPen.MacAddress);
            _currStatus = "btnSearchPaired_Click(): Found target pen.";

            ConnectPen();
        }
    }

    private async void ConnectPen()
    {
        try
        {
            bool result = await _client.Connect(_targetPen);
            Debug.Log("Try to connect to pen: " + _targetPen.ToString());

            if (!result)
            {
                Debug.Log("Connection failed:" + _targetPen.MacAddress);
                _currStatus = "Connection failed";
            }
            else
            {
                Debug.Log("Connection succeeded.");
                _currStatus = "Connection succeeded.";
                _connected = true;
            }
        }
        catch (Exception ex)
        {
            Debug.Log("conection exception : " + ex.Message);
            Debug.Log("conection exception : " + ex.StackTrace);
        }
    }

    #region Major callback functions from Neosmartpen SDK 
    private void MController_PasswordRequested(IPenClient sender, PasswordRequestedEventArgs args)
    {
        Debug.Log("Password Requested");
        //ShowPasswordForm(args.RetryCount, args.ResetCount);
        // You can input password immediately, please refer to below code.
        //mController.InputPassword("0000");

        _controller.InputPassword("5166");
    }

    private void MController_Authenticated(IPenClient sender, object args)
    {
        //_controller.RequestPenStatus();
        _controller.AddAvailableNote();
        //_controller.RequestOfflineDataList();

        // MController_Connected에 있으니 비밀번호 입력창이 뜰때 연결끊김
        // 펜 세팅값으로 넣어줘야 할듯
        //cbColor.SelectedIndex = cbColor.Items.Count - 1;

        //ShowToast("Device is connected");
        Debug.Log("Smartpen is Authenticated.");
        _currStatus = "Smartpen is Authenticated.";
        //_webSockTask = WebSock_SendMessage(_webSock, "Smartpen is Authenticated.");
    }

    private void MController_Disconnected(IPenClient sender, object args)
    {
        //ToggleControls(this.Content, false);

        //await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
        //    _progressDialog?.Hide();
        //    btnConnect.Content = "Connect";
        //});

        //ShowToast("Device is disconnected");
        Debug.Log("Smartpen is Disconnected.");
        _currStatus = "Smartpen is Disconnected.";
    }

    private void MController_Connected(IPenClient sender, ConnectedEventArgs args)
    {
        //ToggleControls(this.Content, true);

        if (args.DeviceName == null)
        {
            //textBox.Text = String.Format("Firmware Version : {0}", args.FirmwareVersion);
            Debug.Log(String.Format("Firmware Version : {0}", args.FirmwareVersion));
        }
        else
        {
            //textBox.Text = String.Format("Mac : {0}\r\n\r\nName : {1}\r\n\r\nSubName : {2}\r\n\r\nFirmware Version : {3}\r\n\r\nProtocol Version : {4}", args.MacAddress, args.DeviceName, args.SubName, args.FirmwareVersion, args.ProtocolVersion);
            Debug.Log(String.Format("Mac : {0}\r\n\r\nName : {1}\r\n\r\nSubName : {2}\r\n\r\nFirmware Version : {3}\r\n\r\nProtocol Version : {4}", args.MacAddress, args.DeviceName, args.SubName, args.FirmwareVersion, args.ProtocolVersion));
        }

        _client.StopWatcher();
    }

    private void MController_DotReceived(IPenClient sender, DotReceivedEventArgs args)
    {
        ProcessDot(args.Dot);
    }
    #endregion

    #region Customized functions handling the strokes
    /// <summary>
    /// Major callback function, process each dot when received.
    /// </summary>
    /// <param name="dot"></param>
    private void ProcessDot(Dot dot)
    {
        // Update Pen status
        _currPenEvent = dot.DotType;
        _currStatus = Enum.GetName(_currPenEvent.GetType(), _currPenEvent);

        //if (pieClickFlag == 1)
        //{
        //    // Update flag
        //    pieClickRelativeX = dot.X - pieClickCenterX;
        //    pieClickRelativeY = dot.Y - pieClickCenterY;
        //    currCommand.CmdParam = string.Format("{0},{1},{2},{3}", clickInfo, 1, pieClickRelativeX, pieClickRelativeY);
        //    Debug.Log(string.Format("CCCCC -- Click event updated:{0},{1}", pieClickRelativeX, pieClickRelativeY));
        //}

        if (_currPenEvent == DotTypes.PEN_DOWN || plainPoints.Count == 0)
        {
            // Stop using _stroke since it is expensive. Unless we need to handle strokes across the paper.
            //_stroke = new Stroke(dot.Section, dot.Owner, dot.Note, dot.Page);
            // Start a new stroke. So we actually count stroke from 1.
            ++_strokeCounter;

            // Update Page status
            _currSection = dot.Section;
            _currNote = dot.Note;
            _currPage = dot.Page;
            string tempPatt = string.Format("{0}-{1}-{2}", _currSection, _currNote, _currPage);
            Debug.Log(String.Format("----[#{0}] {1}.", _strokeCounter, tempPatt));

            // Update current file name and current document class instance
            UpdateCurrFileInfo(tempPatt);

            // Update Stroke status (new stroke starts for inking lib and $P lib)
            //pointsForInking.Clear();
            //pointsForInking.Add(new InkPoint(new Windows.Foundation.Point(dot.X, dot.Y), dot.Force));
            plainPoints.Clear();
            plainPoints.Add(new Windows.Foundation.Point(dot.X * INKING_SCALE, dot.Y * INKING_SCALE));
            //if (_stroke.Count < 1000)
            //    _stroke.Add(dot);
            //if (_cachedDots.Count < 1000)
            //    _cachedDots.Add(dot);

            // Check multi/single stroke status for $P recognizer: 
            // The initial value for lastStrokeTime is App start time, and the regular value for that is the last pen-up time.
            double spanTime = DateTime.Now.Subtract(_lastStrokeTime).TotalMilliseconds;
            if (spanTime < MULTI_STROKE_THRESHOLD)
            {
                // Continued stroke, keep on recording on the same point list.
                _multiStrokeFlag++;
            }
            else
            {
                _multiStrokeFlag = 1;

                // Reset multi-stroke container and its stats attributes.
                pointsForPDollar.Clear();
                _multiMinX = dot.X;
                _multiMinY = dot.Y;
                _multiMaxX = dot.X;
                _multiMaxY = dot.Y;
            }
            // Update multi-stroke attributes (for $P recognizer)
            pointsForPDollar.Add(new PDollarGestureRecognizer.Point(dot.X, dot.Y, _strokeCounter));
            _multiMinX = Math.Min(_multiMinX, dot.X);
            _multiMaxX = Math.Max(_multiMaxX, dot.X);
            _multiMinY = Math.Min(_multiMinY, dot.Y);
            _multiMaxY = Math.Max(_multiMaxY, dot.Y);

            // Refreshing hit flags
            _clickHitFlag = 0;
            isStickyInput = false;
            isControlSheet = false;

            inputHintText.text = "";

            if (tempPatt == CONTROL_SHEET)
            {
                PenClick("Control", "", dot.X, dot.Y);
                isControlSheet = true;
            }
            else if (_currDocument.title == "Sticky")
            {
                PenClick("Sticky", _currDocument.year.ToString(), dot.X, dot.Y);
            }
        }
        else if (_currPenEvent == DotTypes.PEN_UP)
        {
            // DEBUG
            //Debug.Log("Before asnc call to Academic Search");
            //onlineSearchManager.StartNewSearch("Augmented Reality");
            //Debug.Log("After asnc call to Academic Search");

            // Call Azure API to detect the text if certain conditions are checked
            // Candidates: OCR; Handwriting recognition; Academic Search.
            //RecognizeText();
            //MicrosoftAPIHelper.MakeOCRRequest(string.Format("{0}-{1}.jpg", _currAnotoPage.fileName, _currAnotoPage.page.ToString("D2")));

            // Parse the stroke. Special case: pieClickFlag is valid and has invoked some actions, then we should send a ending signal
            // Update: now the pie command is not invoked by pen event anymore. Instead, it is invoked by hand gestures.
            if (plainPoints.Count > 0)
            {
                if (isControlSheet)
                {
                    isControlSheet = false;
                }
                // Save to strokes.
                else if (isStickyInput)
                {
                    //InkStroke stroke = strokeBuilder.CreateStrokeFromInkPoints(pointsForInking, strokeMatrix);
                    InkStroke stroke = strokeBuilder.CreateStroke(plainPoints);
                    Windows.Foundation.Rect rect = stroke.BoundingRect;
                    inkStrokes.AddStroke(stroke);
                    // TODO: check if changes on inkStrokes applies to the dict.
                    Debug.Log(string.Format("Add new stroke:[{0}, {1}] - [{2}, {3}]. Stroke counter {4},{5}",
                        rect.Left, rect.Top, rect.Right, rect.Bottom, inkStrokes.GetStrokes().Count, dictAnoto2Strokes[_currAnotoPatt].GetStrokes().Count));

                    // Call Recognize Text API.
                    //MicrosoftAPIHelper helper = new MicrosoftAPIHelper();
                    //helper.RecognizeInking(inkStrokes.GetStrokes(), AnotoPageWidth * INKING_SCALE, AnotoPageHeight * INKING_SCALE);
                    //helperQueue.Enqueue(helper);
                }
                else
                {
                    // Parse only if we got new points in this stroke
                    Debug.Log("ParseStrokeResult:" + ParseStroke());
                }
            }

            _lastStrokeTime = DateTime.Now;
        }
        else if (dot.DotType == DotTypes.PEN_MOVE || dot.DotType == DotTypes.PEN_HOVER && !isControlSheet)
        {
            //pointsForInking.Add(new InkPoint(new Windows.Foundation.Point(dot.X, dot.Y), 1));
            plainPoints.Add(new Windows.Foundation.Point(dot.X * INKING_SCALE, dot.Y * INKING_SCALE));
            pointsForPDollar.Add(new PDollarGestureRecognizer.Point(dot.X, dot.Y, _strokeCounter));
            _multiMinX = Math.Min(_multiMinX, dot.X);
            _multiMaxX = Math.Max(_multiMaxX, dot.X);
            _multiMinY = Math.Min(_multiMinY, dot.Y);
            _multiMaxY = Math.Max(_multiMaxY, dot.Y);

            try
            {
                int currCount = plainPoints.Count;
                float estMoveX = Mathf.Abs((float)plainPoints[0].X - (float)plainPoints[currCount - 1].X);
                float estMoveY = Mathf.Abs((float)plainPoints[0].Y - (float)plainPoints[currCount - 1].Y);
                if (_clickHitFlag == 0 && currCount >= CLICK_TRIGGER)
                {
                    // Stable enough?
                    if (estMoveX <= CLICK_HEIGHT_THRESHOLD && estMoveY <= CLICK_HEIGHT_THRESHOLD)
                    {
                        // Waiting for the recognition
                        float xx = (float)plainPoints[currCount / 2].X;
                        float yy = (float)plainPoints[currCount / 2].Y;
                        AnotoMark minMark = FindMark(xx, yy);

                        if (minMark != null)
                        {
                            _clickHitFlag = HitMark(minMark, xx, yy);
                        }
                        else
                        {
                            // Not found a target, set to -1
                            _clickHitFlag = -1;
                            Debug.Log("ProcessDot: mark not found. Estimated location:" + xx + "," + yy);
                        }
                    }
                    else
                    {
                        // Not found a target because the pen tip moves fast.
                        _clickHitFlag = -1;
                        Debug.Log("ProcessDot: Not seems to be a click. Estimated movement:" + estMoveX + "," + estMoveY);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.Source);
                Debug.Log(e.StackTrace);
                Debug.Log(e.Message);
            }
        }
    }

    /// <summary>
    /// Helper function: find mark at (x,y) from current Anoto page.
    /// Return null if fail to find.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    private AnotoMark FindMark(float x, float y)
    {
        // Find target
        AnotoMark minMark = null;
        float minDist = float.MaxValue;

        if (_currAnotoPage != null)
        {
            foreach (AnotoMark mark in _currAnotoPage.marks)
            {
                // The threshold for x-axis is bigger for the longer titles and author names.
                float distX = Math.Abs(x - mark.x);
                float distY = Math.Abs(y - mark.y);
                if (distY < LINE_HIT_THRESHOLD_DOTS &&
                    ((mark.type == AnotoMarkTypes.REFERENCE && distX < REF_HIT_THRESHOLD_DOTS) ||
                    (mark.type == AnotoMarkTypes.TITLE && distX < TITLE_HIT_THRESHOLD_DOTS) ||
                    (mark.type == AnotoMarkTypes.AUTHOR && distX < AUTHOR_HIT_THRESHOLD_DOTS) ||
                    (mark.type == AnotoMarkTypes.PEN_CIRCLE && distX < PEN_HIT_THRESHOLD_DOTS)))
                {
                    float newDist = distX + distY;
                    if (newDist < minDist)
                    {
                        minMark = mark;
                        minDist = newDist;
                    }
                }
            }
        }
        return minMark;
    }

    private int HitMark(AnotoMark targetMark, float x, float y)
    {
        int foundFlag = 1;
        if (targetMark.type == AnotoMarkTypes.REFERENCE && _currDocument.references.Length > targetMark.id)
        {
            string function = "Click";
            string refName = _currDocument.references[targetMark.id];
            // FileName, Page, MarkInfo (MarkType, Content), avgX, avgY, status
            string paramList = String.Format("{0},{1},{2},{3},{4},{5},{6}", _currFileName, _currAnotoPage.page, "Reference",
                refName, x / AnotoPageWidth, y / AnotoPageHeight, 0);
            // Call corresponding function
            Debug.Log(string.Format("ParseDot (PEN MOVE) Function: {0}({1})", function, paramList));
            PenClick("Reference", refName, x, y);
        }
        else if (targetMark.type == AnotoMarkTypes.TITLE)
        {
            string function = "Click";
            // FileName, Page, MarkInfo (MarkType, Content), avgX, avgY, status
            string paramList = String.Format("{0},{1},{2},{3},{4},{5},{6}", _currFileName, _currAnotoPage.page, "Title",
                targetMark.id, x / AnotoPageWidth, y / AnotoPageHeight, 0);
            // Call corresponding function
            Debug.Log(string.Format("ParseDot (PEN MOVE) Function: {0}({1})", function, paramList));
            PenClick("Title", targetMark.id.ToString(), x, y);
        }
        else if (targetMark.type == AnotoMarkTypes.AUTHOR && _currDocument.authors.Length > targetMark.id)
        {
            string function = "Click";
            string authorName = _currDocument.authors[targetMark.id];
            // FileName, Page, MarkInfo (MarkType, Content), avgX, avgY, status
            string paramList = String.Format("{0},{1},{2},{3},{4},{5},{6}", _currFileName, _currAnotoPage.page, "Author",
                authorName, x / AnotoPageWidth, y / AnotoPageHeight, 0);
            // Call corresponding function
            Debug.Log(string.Format("ParseDot (PEN MOVE) Function: {0}({1})", function, paramList));
            PenClick("Author", authorName, x, y);
        }
        else if (targetMark.type == AnotoMarkTypes.PEN_CIRCLE && targetMark.other.Length > 0)
        {
            // Start online search, for the keyword in 'other' field. 
            string function = "Click";
            string searchWord = targetMark.other;
            // FileName, Page, MarkInfo (MarkType, Content), avgX, avgY, status
            string paramList = String.Format("{0},{1},{2},{3},{4},{5},{6}", _currFileName, _currAnotoPage.page, "PenCircle",
                searchWord, x / AnotoPageWidth, y / AnotoPageHeight, 0);
            // Call corresponding function
            Debug.Log(string.Format("ParseDot (PEN MOVE) Function: {0}({1})", function, paramList));
            PenClick("PenCircle", searchWord, x, y);
        }
        else
        {
            Debug.Log(string.Format("ProcessDot Error: minMark found but not valid. Type:{0}, Id:{1}", targetMark.type, targetMark.id));
            foundFlag = -1;
        }
        return foundFlag;
    }

    /// <summary>
    /// Update file info based on the section, note, and page id.
    /// And if this is a new document, load the reference list.
    /// This page id is just for Anoto-Pattern, based on Neopen SDK,
    /// and is different from the actual page id.
    /// </summary>
    private void UpdateCurrFileInfo(string newAnotoPatt)
    {
        // Pattern changed and still valid
        if (newAnotoPatt != _currAnotoPatt && _anotoPages.ContainsKey(newAnotoPatt))
        {
            _currAnotoPatt = newAnotoPatt;
            // Update strokes.
            if (!dictAnoto2Strokes.ContainsKey(_currAnotoPatt))
                dictAnoto2Strokes.Add(_currAnotoPatt, new InkStrokeContainer());
            inkStrokes = dictAnoto2Strokes[_currAnotoPatt];
            currInkingResult = "";

            // Could be just new page of current file
            _currAnotoPage = _anotoPages[_currAnotoPatt];
            pageOrg.UpdateCurrDoc(_currAnotoPage.fileName, _currAnotoPage.page);

            if (_currAnotoPage.fileName != _currFileName)
            {
                // New File
                _currFileName = _currAnotoPage.fileName;
                Debug.Log(string.Format("Get new file(page):{0}-p{1}", _currAnotoPage.fileName, _currAnotoPage.page));

                if (PageOrganizer.DictDocuments.ContainsKey(_currFileName))
                {
                    _currDocument = PageOrganizer.DictDocuments[_currFileName];

                    // Update new onlineManager
                    // Check for Sticky Notes Patterns
                    if (_currDocument.title == "Sticky")
                    {
                        onlineSearchManager = StickyManager.GetManager(_currDocument.year);
                        Debug.LogAssertion(onlineSearchManager != null);
                    }
                    else
                    {
                        // A defulat manager without any binding to sticky notes (99).
                        onlineSearchManager = StickyManager.GetManager(99);
                        Debug.LogAssertion(onlineSearchManager != null);
                    }
                }
                else
                {
                    Debug.Log("Error: this file name is not registered in <documents> Dict. " + _currFileName);
                }

                
            }
        }
        else if (newAnotoPatt != _currAnotoPatt)
        {
            Debug.Log("Error: anoto pattern not registered in <_anotoPages> Dict.");
        }
    }

    /// <summary>
    /// Parse the stroke (_stroke), and return the command type.
    /// Being called while lifting the pen.
    /// </summary>
    /// <returns></returns>
    private bool ParseStroke()
    {
        // Transfer to $P Format. Get average point.
        float avgX = 0, avgY = 0;
        foreach (var pp in pointsForPDollar)
        {
            avgX += pp.X;
            avgY += pp.Y;
        }
        avgX /= pointsForPDollar.Count;
        avgY /= pointsForPDollar.Count;
        Debug.Log(string.Format("[AVG (#{0}-#{1})] X:{2}, Y:{3}", pointsForPDollar[0].StrokeID, pointsForPDollar[pointsForPDollar.Count - 1].StrokeID, avgX, avgY));

        // See if we have already recognized this click event.
        // +1: hit and triggerred click event
        if (_clickHitFlag > 0)
        {
            string function = "Click";
            Debug.Log(string.Format("ParseStroke Function (already hit): {0}({1})", function, _clickHitFlag));
            return true;
        }

        string gestureClass = "";
        // Debug: use fixed value here
        //gestureClass = "circle";

        // Apply $P Gesture Recognizer
        Gesture candidate = null;
        try
        {
            Debug.Log("Gesture Sets:" + trainingSet.Length);
            candidate = new Gesture(pointsForPDollar.ToArray());
            gestureClass = PointCloudRecognizer.Classify(candidate, trainingSet, _multiStrokeFlag);
            Debug.Log("Recognized as: " + gestureClass + "; Stroke Num:" + _multiStrokeFlag);
        }
        catch (Exception e)
        {
            Debug.Log(e.Source);
            Debug.Log(e.Message);
            return false;
        }

        // $P recognizer not successful
        if (gestureClass.Length == 0)
        {
            return false;
        }
        int tempResultFlag = 0;

        // Get real (physical, m) height and weight of the strokes
        float realStrokesHeight = (_multiMaxY - _multiMinY) * NCODE_UNIT;
        float realStrokesWidth = (_multiMaxX - _multiMinX) * NCODE_UNIT;

        // 10% width: 2.16cm, 5% height: 1.4cm, and multi-stroke.
        // Update: Real dimension: width larger than 0.5cm (regular word)
        if (gestureClass == "cropRect" && realStrokesWidth > 0.005f && _multiStrokeFlag > 1)
        {
            string function = "Crop";
            // FileName, Page, MarkInfo (tlX, tlY, brX, brY)
            string paramList = string.Format("{0},{1},{2},{3},{4},{5}", _currFileName, _currAnotoPage.page,
                _multiMinX, _multiMinY, _multiMaxX, _multiMaxY);
            // Call corresponding function
            Debug.Log(string.Format("ParseStroke Function: {0}({1})", function, paramList));
            PenCrop(_multiMinX, _multiMinY, _multiMaxX, _multiMaxY);
        }
        // thin (20% width: 4.3cm), vertical (5% height: 1.4cm), single line/curve on the side.
        // Update: vertical >= 1cm, boundary on left/right sides: 1.8cm
        else if (gestureClass == "largecrop" && _multiStrokeFlag == 1 && (avgX * NCODE_UNIT <= 0.018f || (AnotoPageWidth - avgX) * NCODE_UNIT <= 0.018f) && realStrokesHeight >= 0.01f)
        {
            string function = "Crop";

            // For largecrop, automatically select the half column
            float tempMinX = _multiMinX;
            float tempMaxX = _multiMaxX;
            if (avgX < AnotoPageWidth * 0.5f)
            {
                // largecrop on the left side
                tempMaxX = AnotoPageWidth * 0.5f;
            }
            else
            {
                // largecrop on the right side
                tempMinX = AnotoPageWidth * 0.5f;
            }
            // FileName, Page, MarkInfo (tlX, tlY, brX, brY)
            string paramList = string.Format("{0},{1},{2},{3},{4},{5}", _currFileName, _currAnotoPage.page,
                tempMinX, _multiMinY, tempMaxX, _multiMaxY);
            // Call corresponding function
            Debug.Log(string.Format("ParseStroke Function: {0}({1})", function, paramList));
            PenCrop(tempMinX, _multiMinY, tempMaxX, _multiMaxY);
        }
        // Circle, triangle (possible mistake)
        else if ((gestureClass == "circle" || gestureClass == "line" || gestureClass == "triangle") && _multiStrokeFlag == 1)
        {
            // [CLick]
            AnotoMark minMark = FindMark(avgX, avgY);

            if (minMark != null)
            {
                tempResultFlag = HitMark(minMark, avgX, avgY);
            }
            else if (realStrokesWidth > 0.005f)
            {
                Vector2 normTopLeft = NormalizeAnotoPos(_multiMinX, _multiMinY);
                Vector2 normBotRight = NormalizeAnotoPos(_multiMaxX, _multiMaxY);

                AnotoMark newMark = new AnotoMark(AnotoMarkTypes.PEN_CIRCLE, 0, (int)(avgX), (int)(avgY), "...");
                _currAnotoPage.marks.Add(newMark);
                Debug.Log("Make new mark and wait for OCR:" + newMark.x + ", " + newMark.y + " at page:" + _currAnotoPage.fileName);

                GameObject go = createOCRRoot(normTopLeft);

                InputHintCanvas.transform.position = go.transform.position;
                InputHintCanvas.transform.localEulerAngles = new Vector3(InputHintCanvas.transform.localEulerAngles.x, HololensCamera.transform.localEulerAngles.y, InputHintCanvas.transform.localEulerAngles.z);
                inputHintText.text = "Recognizing...";

                MicrosoftAPIHelper helper = new MicrosoftAPIHelper();
                // As an id
                helper.ocrAvgX = (int)(avgX);
                helper.ocrAvgY = (int)(avgY);
                helperQueue.Enqueue(helper);
                var task = Task.Run(() => helper.MakeOCRRequest(string.Format("{0}-{1}.jpg", _currFileName, _currAnotoPage.page.ToString("D2")), normTopLeft, normBotRight));
            }
            else
            {
                // TODO: when the user draws a circle but didn't find anything important
                // he might want to circle the keyword.
                // Not found a target, set to -1
                tempResultFlag = -1;
                Debug.Log("ProcessDot Error: mark not found.");
            }
        }
        //else if (gestureClass == "line")
        //{
        //    Debug.Log("line: currently merged with circle.");
        //}
        //else if (gestureClass == "triangle")
        //{
        //    currCommand.CmdKey = "Text";
        //    currCommand.CmdParam = string.Format("{0},{1}", _currFileName, _currAnotoPage.page);
        //    call PenText(blablabla)
        //}
        else
        {
            // Marks not found.
            Debug.Log("Unknown gesture:" + gestureClass);
            tempResultFlag = -1;
        }

        return tempResultFlag > 0;
    }

    #endregion

    async void SaveCalibration()
    {
        StorageFolder appFolder = KnownFolders.CameraRoll;
        StorageFile file = await appFolder.GetFileAsync(calibFileName);
        if (pageOrg.CurrDocObj)
        {
            //Matrix4x4 matrix = pageOrg.CurrDocObj.transform.localToWorldMatrix;
            //Vector3 pos = calibObject.transform.position;
            Vector3 obPos = pageOrg.CurrDocObj.transform.localPosition;
            Vector3 pos = calibObject.transform.localPosition;

            //string output = string.Format("{0},{1},{2},{3}\n{4},{5},{6},{7}\n", calibCounter, matrix.m03, matrix.m13, matrix.m23,
            //    calibCounter, pos.x, pos.y, pos.z);
            string output = string.Format("{0},{1},{2},{3}\n{4},{5},{6},{7}\n", calibCounter, obPos.x, obPos.y, obPos.z,
                calibCounter, pos.x, pos.y, pos.z);
            await FileIO.AppendTextAsync(file, output);
            Debug.Log(output);

            calibCounter++;
            // Move to next location. x: [-0.2, 0], y: -0.33, z: [0.6, 0.8], dt: 0.1
            // 5x5=25 test cases
            //float dt = 0.05f;   
            //if (pos.x + dt > 0.01f)
            //{
            //    if (pos.z - dt < 0.59f)  // End one turn
            //    {
            //        Debug.Log("Calibration ended. Continue.");
            //        pos = new Vector3(-0.2f, pos.y, 0.8f);
            //    }
            //    else                // to next z
            //    {
            //        pos = new Vector3(-0.2f, pos.y, pos.z - dt);
            //    }
            //}
            //else                    // Normal: to next x
            //{
            //    pos = new Vector3(pos.x + dt, pos.y, pos.z);
            //}
            //calibObject.transform.position = pos;

            // Local (camera): x [ -0.05, 0.05], y: [0, 0.05], z: 0.5, 0.6
            // 5*3*2=30 cases
            float dt = 0.025f;
            if (pos.x + dt > 0.051f)
            {
                if (pos.y + dt > 0.051f)  
                {
                    if (pos.z + dt > 0.61f) // End
                    {
                        Debug.Log("Calibration ended.");
                        calibObject.transform.localScale = Vector3.one * 3;
                        return;
                    }
                    else // to next z
                    {
                        pos = new Vector3(-0.05f, 0, pos.z + 0.1f);
                    }
                }
                else                // to next y
                {
                    pos = new Vector3(-0.05f, pos.y + dt, pos.z);
                }
            }
            else                    // Normal: to next x
            {
                pos = new Vector3(pos.x + dt, pos.y, pos.z);
            }
            calibObject.transform.localPosition = pos;
        }
    }

    /// <summary>
    /// Callback function for pie menu click/tap/pressed events
    /// </summary>
    /// <param name="pressedID"></param>
    public void PieMenuMousePressed(PieMenuType menuType, int pressedID)
    {
        Debug.Log("PenOrg received mouse event, pieType: " + menuType + ", pressedID: " + pressedID);
        if (menuType == PieMenuType.TITLE)
        {
            if (pressedID == 0)        // Close
            {
                pageOrg.TitlePage.UpdateTitleEvent(TitleEventStatus.OFF);
                //currPieType = PieMenuType.NOT_INIT;
                //pieMenu = null;
                //currPieFileName = "";
            }
            else if (pressedID == 1)    // Meta-Data
            {
                pageOrg.TitlePage.UpdateTitleEvent(TitleEventStatus.META_DATA);
            }
            else if (pressedID == 2)    // Figures
            {
                pageOrg.TitlePage.UpdateTitleEvent(TitleEventStatus.FIGURES);
            }
            else if (pressedID == 3)    // Tags
            {
                pageOrg.TitlePage.UpdateTitleEvent(TitleEventStatus.TAGS);
            }
            else if (pressedID == 4)    // Video
            {
                pageOrg.TitlePage.UpdateTitleEvent(TitleEventStatus.VIDEO);
            }
        }
        else if (menuType == PieMenuType.REFERENCE)
        {
            if (pressedID == 0)        // Close
            {
                pageOrg.RefPage.UpdateRefEvent(RefEventStatus.OFF);
                //currPieType = PieMenuType.NOT_INIT;
                //pieMenu = null;
                //currPieFileName = "";
            }
            else if (pressedID == 1)   // Meta-data
            {
                pageOrg.RefPage.UpdateRefEvent(RefEventStatus.META_DATA);
            }
            else if (pressedID == 2)   // Cover page (preview)
            {
                pageOrg.RefPage.UpdateRefEvent(RefEventStatus.PREVIEW);
            }
            else if (pressedID == 3)   // Full papers
            {
                pageOrg.RefPage.UpdateRefEvent(RefEventStatus.FULL_PAPER);
            }
            else if (pressedID == 4)   // Video
            {
                pageOrg.RefPage.UpdateRefEvent(RefEventStatus.VIDEO);
            }
            
        }
        else if (menuType == PieMenuType.STICKY)
        {
            // TODO: finish this logic
        }
    }

    /// <summary>
    /// Converting NCode coordinates into percentage value of the raw image
    /// </summary>
    /// <param name="posX"></param>
    /// <param name="posY"></param>
    /// <returns></returns>
    private Vector2 NormalizeAnotoPos(float posX, float posY)
    {
        // Based on experiments.
        return new Vector2(-0.004882f + 0.0110093f * posX, 0.0013664f + 0.0084734f * posY);
    }

    /// <summary>
    ///  Adjust the transform of PieMenu using current pen tip position and camera rotation.
    ///  Update: just assign it to the Title/Ref canvas root object
    /// </summary>
    //private void AdjustPieMenuTransform(GameObject newParent)
    //{
    //    PieMenuCanvas.transform.SetParent(newParent.transform);
    //    PieMenuCanvas.transform.localPosition = new Vector3(-0.1f, -0.05f, -0.05f);
    //    PieMenuCanvas.transform.localEulerAngles = Vector3.zero;
    //    PieMenuCanvas.transform.localScale = Vector3.one;
    //}

    /// <summary>
    /// Helper function to set up the title window, avoiding duplicate code blocks.
    /// Only in this function, the PieMenu's locaiton could be changed. Otherwise, the UpdateTitleEvent shouldn't care about the file
    /// </summary>
    /// <param name="pieClickNormPos"></param>
    private void SetupTitleWindow(Vector2 pieClickNormPos)
    {
        // Show the meta-data (brief info) view as a default view, together with the pie menu
        pageOrg.TitlePage.UpdateTitleEvent(TitleEventStatus.META_DATA, _currFileName, pieClickNormPos.x, pieClickNormPos.y);

        // Start a new menu
        Debug.LogAssertion(pageOrg.CurrDocObj != null && pageOrg.TitleRootObj != null);
        // Start the new pie menu for title
        //AdjustPieMenuTransform(pageOrg.TitleRootObj);
        pageOrg.TitlePage.PieMenu.InitPieMenu(_currFileName, new string[] { "Meta-Data", "Figures", "Tags", "Video" },
            new bool[] { true, pageOrg.CheckThumbnails(), pageOrg.TitlePage.CheckTags(), pageOrg.CheckVideo() }, this, PieMenuType.TITLE, 1);
        //currPieType = PieMenuType.TITLE;
        //currPieFileName = _currFileName;
    }

    /// <summary>
    /// Similar to SetupTitleWindow, only this function could actually update the Ref's root canvas.
    /// </summary>
    /// <param name="pieClickNormPos"></param>
    /// <param name="markContent"></param>
    private void SetupRefWindow(Vector2 pieClickNormPos, string markContent)
    {
        // Show meta-data (title info) first, together with the pie menu
        pageOrg.RefPage.UpdateRefEvent(RefEventStatus.META_DATA, _currFileName, _currRefName, pieClickNormPos.x, pieClickNormPos.y);

        // Start a new menu
        Debug.LogAssertion(pageOrg.CurrDocObj != null && pageOrg.RefRootObj != null);
        // Assign the parent
        //AdjustPieMenuTransform(pageOrg.canvasRefObj);
        pageOrg.RefPage.PieMenu.InitPieMenu(_currRefName, new string[] { "Meta-Data", "Preview", "Full Papers", "Video" }, 
            new bool[] { true, pageOrg.CheckPreview(markContent), pageOrg.CheckFullDocuments(markContent), pageOrg.CheckVideo(markContent) }, this, PieMenuType.REFERENCE, 1);
        //currPieType = PieMenuType.REFERENCE;
        //currPieFileName = _currRefName;
    }

    /// <summary>
    /// Pen Click function. Based on the markType, run different functions. 
    /// </summary>
    /// <param name="markType">type of the Anoto Marks (e.g., "Title")</param>
    /// <param name="markContent">content of the Anoto Marks (e.g., reference name)</param>
    /// <param name="posX">click position-X (range: [0, AnotoWidth])</param>
    /// <param name="posY">click position-Y (range: [0, AnotoHeight])</param>
    private void PenClick(string markType, string markContent, float posX, float posY)
    {
        //Vector2 normPos = NormalizeAnotoPos(posX, posY);
        pieClickNormPos = NormalizeAnotoPos(posX, posY);
        if (markType == "Title")
        {
            PieMenu pieMenu = pageOrg.TitlePage.PieMenu;
            Debug.Assert(pieMenu != null);
            // Open new title menu when we don't have one.
            if (pieMenu.MenuState == PieMenu.PieMenuState.NOT_INIT)
            {
                SetupTitleWindow(pieClickNormPos);
            }
            else if (pieMenu.MenuState == PieMenu.PieMenuState.READY || pieMenu.MenuState == PieMenu.PieMenuState.PRESSED)
            {
                Debug.Log(string.Format("Debug Pen Click on Title: newFile:{0}, current pie file:{1}", _currFileName, pieMenu.FileName));
                // Pie Menu already exists: 1) Same-Doc: toggle to close; 2) Diff-Doc: switch to new one (test if the options exists).
                //if (currPieType == PieMenuType.TITLE && currPieFileName == _currFileName)
                // Update: only cares about title events. Indepedently handled.
                if (pieMenu.FileName == _currFileName)
                {
                    // Toggle to close title events, just as if the user taps the "close" pie menu button
                    // Call this pieMenu method, and it call the PieMenuMousePressed method.
                    pieMenu.MousePressed(false, Vector2.zero);
                }
                else
                {
                    SetupTitleWindow(pieClickNormPos);
                }
            }
        }
        else if (markType == "Reference")
        {
            _currRefName = markContent;
            // TODO: Update piemenu from pageOrg
            PieMenu pieMenu = pageOrg.RefPage.PieMenu;
            Debug.Assert(pieMenu != null);
            // Open new ref pie menu when we don't have one
            if (pieMenu.MenuState == PieMenu.PieMenuState.NOT_INIT)
            {
                SetupRefWindow(pieClickNormPos, markContent);
            }
            else if (pieMenu.MenuState == PieMenu.PieMenuState.READY || pieMenu.MenuState == PieMenu.PieMenuState.PRESSED)
            {
                // Already exists: 1) Same target doc: toggle to close; 2) Diff target doc: switch to new one.
                //if (currPieType == PieMenuType.REFERENCE && currPieFileName == _currRefName)
                if (pieMenu.FileName == _currRefName)
                {
                    // Toggle to close everything. Reuse the callback function
                    pieMenu.MousePressed(false, Vector2.zero);
                }
                else
                {
                    SetupRefWindow(pieClickNormPos, markContent);
                }
            }
        }
        else if (markType == "Sticky")
        {
            var stickyCode = DecodeSticky(posX, posY);
            Debug.Log(stickyCode.ToString());
            if (stickyCode == STICKY_CODE.INPUT_BOX)
            {
                // Start tracking and display current result
                // Update: now we show input hint *after* click button, *before* the button finish the request.
                isStickyInput = true;
                hasNewStickyInputStrokes = true;
            }
            else if (stickyCode == STICKY_CODE.TOP_BTN) // Local
            {
                // Check if the flag is dirty
                if (hasNewStickyInputStrokes)
                {
                    InputHintCanvas.transform.position = pageOrg.CurrDocObj.transform.position;
                    InputHintCanvas.transform.localEulerAngles = new Vector3(InputHintCanvas.transform.localEulerAngles.x, HololensCamera.transform.localEulerAngles.y, InputHintCanvas.transform.localEulerAngles.z);
                    inputHintText.text = "Recognizing...";
                    currListener = HelperEventListener.DO_LOCAL_SEARCH;

                    MicrosoftAPIHelper helper = new MicrosoftAPIHelper();
                    helper.RecognizeInking(inkStrokes.GetStrokes(), AnotoPageWidth * INKING_SCALE, AnotoPageHeight * INKING_SCALE);
                    helperQueue.Enqueue(helper);

                    hasNewStickyInputStrokes = false;
                }
                else if (currInkingResult.Length > 0)
                {
                    Debug.Assert(onlineSearchManager != null);
                    onlineSearchManager.StartNewSearch(currInkingResult, pageOrg.CurrDocObj, SearchType.LOCAL);
                }
                else if (onlineSearchManager.HasSearched())
                {
                    onlineSearchManager.StartNewSearch("", pageOrg.CurrDocObj, SearchType.LOCAL);
                }
                else
                {
                    // Empty recognition result
                    inputHintText.text = "The input box is empty, or no handwriting is detected.";
                }
            }
            else if (stickyCode == STICKY_CODE.BOT_BTN) // Online
            {
                // Check if the flag is dirty
                if (hasNewStickyInputStrokes)
                {
                    InputHintCanvas.transform.position = pageOrg.CurrDocObj.transform.position;
                    InputHintCanvas.transform.localEulerAngles = new Vector3(InputHintCanvas.transform.localEulerAngles.x, HololensCamera.transform.localEulerAngles.y, InputHintCanvas.transform.localEulerAngles.z);
                    inputHintText.text = "Recognizing...";
                    currListener = HelperEventListener.DO_ONLINE_SEARCH;

                    MicrosoftAPIHelper helper = new MicrosoftAPIHelper();
                    helper.RecognizeInking(inkStrokes.GetStrokes(), AnotoPageWidth * INKING_SCALE, AnotoPageHeight * INKING_SCALE);
                    helperQueue.Enqueue(helper);
                    hasNewStickyInputStrokes = false;
                }
                else if (currInkingResult.Length > 0)
                {
                    Debug.Assert(onlineSearchManager != null);
                    onlineSearchManager.StartNewSearch(currInkingResult, pageOrg.CurrDocObj, SearchType.ONLINE);
                }
                else if (onlineSearchManager.HasSearched())
                {
                    onlineSearchManager.StartNewSearch("", pageOrg.CurrDocObj, SearchType.ONLINE);
                }
                else
                {
                    // Empty recognition result
                    inputHintText.text = "The input box is empty, or no handwriting is detected.";
                }
            }
        }
        else if (markType == "Control")
        {
            PenControlCode = DecodeControl(posX, posY);
            Debug.Log("Tap on control sheet:" + PenControlCode.ToString());
            // Experimental solution: use control sheet to pause/resume the video capture.
            if (PenControlCode == CONTROL_CODE.PAUSE_VIDEO)
            {
                //VideoCapture.CreateAsync(false, OnVideoCaptureCreated);
                if (aruwpControl.status == ARUWP.ARUWP_STATUS_RUNNING)
                {
                    aruwpControl.Pause();
                    Debug.Log("Pause the ARUWP Video Capture:" + aruwpControl.status);
                }
            }
            else if (PenControlCode == CONTROL_CODE.RESUME_VIDEO)
            {
                //StopRecordingVideo();
                if (aruwpControl.status == ARUWP.ARUWP_STATUS_CTRL_INITIALIZED)
                {
                    aruwpControl.Resume();
                    Debug.Log("Resume the ARUWP Video Capture:" + aruwpControl.status);
                }
            }
        }
        else if (markType == "PenCircle")
        {
            if (markContent == "...")
            {
                // Still waiting
                //GameObject go = createOCRRoot(pieClickNormPos);

                //InputHintCanvas.transform.position = go.transform.position;
                //InputHintCanvas.transform.localEulerAngles = new Vector3(InputHintCanvas.transform.localEulerAngles.x, HololensCamera.transform.localEulerAngles.y, InputHintCanvas.transform.localEulerAngles.z);
                //inputHintText.text = "Recognizing...";
                currListener = HelperEventListener.SEARCH_FROM_OCR;
            }
            else if (markContent.Length > 0)
            {
                Debug.Assert(onlineSearchManager != null);
                GameObject go = createOCRRoot(pieClickNormPos);
                onlineSearchManager.StartNewSearch(markContent, go, SearchType.ONLINE);
            }
        }
        else
        {
            Debug.Log("PenClick Error: unknown mark type:" + markType);
        }
    }

    //VideoCapture m_VideoCapture = null;
    //void OnVideoCaptureCreated(VideoCapture videoCapture)
    //{
    //    if (videoCapture != null)
    //    {
    //        m_VideoCapture = videoCapture;

    //        Resolution cameraResolution = VideoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
    //        float cameraFramerate = VideoCapture.GetSupportedFrameRatesForResolution(cameraResolution).OrderByDescending((fps) => fps).First();

    //        CameraParameters cameraParameters = new CameraParameters();
    //        cameraParameters.hologramOpacity = 0.0f;
    //        cameraParameters.frameRate = cameraFramerate;
    //        cameraParameters.cameraResolutionWidth = cameraResolution.width;
    //        cameraParameters.cameraResolutionHeight = cameraResolution.height;
    //        cameraParameters.pixelFormat = CapturePixelFormat.BGRA32;

    //        m_VideoCapture.StartVideoModeAsync(cameraParameters,
    //                                            VideoCapture.AudioState.ApplicationAudio,
    //                                            OnStartedVideoCaptureMode);
    //    }
    //    else
    //    {
    //        Debug.LogError("Failed to create VideoCapture Instance!");
    //    }
    //}

    //void OnStartedVideoCaptureMode(VideoCapture.VideoCaptureResult result)
    //{
    //    if (result.success)
    //    {
    //        string filename = string.Format("MyVideo_{0}.mp4", Time.time);
    //        string filepath = System.IO.Path.Combine(Application.persistentDataPath, filename);

    //        m_VideoCapture.StartRecordingAsync(filepath, OnStartedRecordingVideo);
    //    }
    //}

    //void OnStartedRecordingVideo(VideoCapture.VideoCaptureResult result)
    //{
    //    Debug.Log("Started Recording Video!");
    //    // We will stop the video from recording via other input such as a timer or a tap, etc.
    //}

    //void StopRecordingVideo()
    //{
    //    m_VideoCapture.StopRecordingAsync(OnStoppedRecordingVideo);
    //}

    //void OnStoppedRecordingVideo(VideoCapture.VideoCaptureResult result)
    //{
    //    Debug.Log("Stopped Recording Video!");
    //    m_VideoCapture.StopVideoModeAsync(OnStoppedVideoCaptureMode);
    //}

    //void OnStoppedVideoCaptureMode(VideoCapture.VideoCaptureResult result)
    //{
    //    m_VideoCapture.Dispose();
    //    m_VideoCapture = null;
    //}

    private GameObject createOCRRoot(Vector2 pieClickNormPos)
    {
        GameObject ocrRoot = new GameObject();

        // Rotate based on view point
        Vector3 targetVec = pageOrg.CalcTargetEulerAngles();
        ocrRoot.transform.localEulerAngles = targetVec;

        // Set starting position
        ocrRoot.transform.position = pageOrg.CurrDocObj.transform.position;
        // Move along right (x-axis of AR tag, RIGHT)
        ocrRoot.transform.Translate(pageOrg.CurrDocObj.transform.right * (pieClickNormPos.x * PAPER_WIDTH - 0.02f), Space.World);
        // Move along bottom (y-axis of AR tag, UP)
        ocrRoot.transform.Translate(pageOrg.CurrDocObj.transform.up * (pieClickNormPos.y * PAPER_HEIGHT - 0.02f), Space.World);

        return ocrRoot;
    }

    /// <summary>
    /// Crop the given area and handle the events
    /// </summary>
    /// <param name="tlX">Top Left X</param>
    /// <param name="tlY">Top Left Y</param>
    /// <param name="brX">Bottom Right X</param>
    /// <param name="brY">Bottom Right Y</param>
    private void PenCrop(float tlX, float tlY, float brX, float brY)
    {
        Vector2 normTopLeft = NormalizeAnotoPos(tlX, tlY);
        Vector2 normBotRight = NormalizeAnotoPos(brX, brY);
        // TODO: need to show the preview first (text: recognized words; paragraph: digital paragraph thumbnail), then invoke a piemenu on next click.
        // This preview will disappear soon. Or when next hit.
        if (!isStickyInput)
            pageOrg.NotePage.UpdateNoteBoard(_currFileName, _currAnotoPage.page, normTopLeft.x, normTopLeft.y, normBotRight.x, normBotRight.y, _currDocument.source);

        // Debug: without popping the pie menu, invoke the academic search directly.
        //MicrosoftAPIHelper helper = new MicrosoftAPIHelper();
        //helperQueue.Enqueue(helper);
        //var task = Task.Run(() => helper.MakeOCRRequest(string.Format("{0}-{1}.jpg", _currFileName, _currAnotoPage.page.ToString("D2")), normTopLeft, normBotRight));
    }

    //private void PenText()
    //{
    //    // Debugging:
    //    Debug.Log("TestSticky:" + stickyManager.MainCamera.transform.position);
    //    NormalizeStrokes();
    //    stickyManager.CreateSticky(pageOrg.CurrDocStr, _strokes, pageOrg.CurrDocObj);
    //    ResetDotBoundry();
    //    _strokes.Clear();

    //    // PenCmdParam: [Text]: CurrentFileName, currentPageNo, tlX, tlY, text
    //    string[] cmds = penCmdParam.Split(',');
    //    // Debugging: while receive this end mark, stop and clear the stroke.

    //    //_dots.Clear();

    //    if (cmds.Length == 4)
    //    {
    //        Debug.Log("Text, with incomplete parameter. Waiting for text recognition...");
    //    }
    //    else if (cmds.Length == 5)
    //    {
    //        string currFileName = cmds[0].Trim();
    //        Int32.TryParse(cmds[1].Trim(), out int currFilePage);
    //        float.TryParse(cmds[2].Trim(), out float tlX);
    //        float.TryParse(cmds[3].Trim(), out float tlY);
    //        string recoText = cmds[4].Trim();
    //        Debug.Log("[Text]:" + recoText);
    //    }
    //}

#endif

    private List<Vector3> ParseDots()
    {
        // TODO: remove duplicate ones
        List<Vector3> newDots = new List<Vector3>();
        newDots.Add(dots[0]);
        Vector3 prevDot = dots[0];
        
        for (int i = 0; i < dots.Count; ++i)
        {
            Vector3 dist = dots[i] - prevDot;
            if (dist.magnitude > StickyManager.STROKE_WIDTH * 0.5f)
            {
                newDots.Add(dots[i]);
                prevDot = dots[i];
                // Update the boundry
                if (dots[i].x < dotTopLeft.x)
                    dotTopLeft.x = dots[i].x;
                if (dots[i].y < dotTopLeft.y)
                    dotTopLeft.y = dots[i].y;
                if (dots[i].x > dotBotRight.x)
                    dotBotRight.x = dots[i].x;
                if (dots[i].y > dotBotRight.y)
                    dotBotRight.y = dots[i].y;
            }
        }
        Debug.Log(string.Format("Parsed Dots:{0}/{1}.", newDots.Count, dots.Count));
        return newDots;
    }

    /// <summary>
    /// Goal: based on sticky manager's canvas size, in Unity axis dir.
    /// </summary>
    private void NormalizeStrokes()
    {
        float maxSize = StickyManager.CANVAS_SIZE;
        float multiX = maxSize / (dotBotRight.x - dotTopLeft.x);
        float multiY = maxSize / (dotBotRight.y - dotTopLeft.y);
        float multi = Mathf.Min(multiX, multiY);
        float bias = maxSize / 2;
        // Remove the last one, when it is the end symbol
        for (int s = 0; s < strokes.Count - 1; ++s)
        {
            List<Vector3> stroke = strokes[s];
            for (int i = 0; i < stroke.Count; ++i)
            {
                stroke[i] = (stroke[i] - dotTopLeft) * multi;
                stroke[i] = new Vector3(stroke[i].x - bias, bias - stroke[i].y, 0);
            }
        }
        Debug.Log(string.Format("Normalize from:<{0}, {1}>, multi:{2}", dotTopLeft, dotBotRight, multi));
    }

    private STICKY_CODE DecodeSticky(float posX, float posY)
    {
        float relativeX = posX - ORIGIN_X;
        float relativeY = posY - ORIGIN_Y;
        if (relativeX < 0 || relativeY < 0 || relativeX > CELL_WIDTH * 3 || relativeY > CELL_HEIGHT * 4)
        {
            return STICKY_CODE.NONE;
        }
        int col = (int)Math.Floor(relativeX / CELL_WIDTH);
        int row = (int)Math.Floor(relativeY / CELL_HEIGHT);
        float localX = relativeX - col * CELL_WIDTH;
        float localY = relativeY - row * CELL_HEIGHT;
        if (localX >= BTN_LEFT && localY <= BTN_BOT)
        {
            // Buttons
            if (localY < BTN_MID)
                return STICKY_CODE.TOP_BTN;
            else
                return STICKY_CODE.BOT_BTN;
        }
        else if (localY >= BTN_BOT)
            return STICKY_CODE.INPUT_BOX;
        else
            return STICKY_CODE.NONE;
    }

    private CONTROL_CODE DecodeControl(float posX, float posY)
    {
        float relativeX = posX - ORIGIN_X;
        float relativeY = posY - ORIGIN_Y;
        if (relativeX < 0 || relativeY < 0 || relativeX > CELL_WIDTH * 3 || relativeY > CELL_HEIGHT * 4)
        {
            Debug.LogError("Decode Control Error: Fail to decode the control sheet coordinates. Return Dafault value.");
            return CONTROL_CODE.DEFAULT;
        }
        int col = (int)Math.Floor(relativeX / CELL_WIDTH);
        int row = (int)Math.Floor(relativeY / CELL_HEIGHT);
        int retID = row * 3 + col;
        int currLength = Enum.GetNames(typeof(CONTROL_CODE)).Length;
        return (CONTROL_CODE)(retID % currLength);
    }
}
