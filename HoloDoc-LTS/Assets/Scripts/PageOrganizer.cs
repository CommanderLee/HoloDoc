using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YoutubeLight;
using PageConstants;

namespace PageConstants
{
    public enum CanvasFadeStatus { CLOSE, OPEN, MODIFY }
    public enum ChildObjFadeStatus { CLOSE, OPEN, CLOSE_AND_OPEN, SWITCH }
    public enum TitleEventStatus { OFF, META_DATA, FIGURES, TAGS, VIDEO }
    public enum RefEventStatus { OFF, META_DATA, PREVIEW, FULL_PAPER, VIDEO }
    public enum PieMenuType { NOT_INIT, TITLE, REFERENCE, STICKY }
    public enum SearchType { ONLINE, LOCAL }
    public enum CONTROL_CODE { DEFAULT, SIDE_BY_SIDE, VERTICAL, HUD, PAUSE_VIDEO, RESUME_VIDEO }
    public enum SortingMethods { TIME, NAME, SOURCE }
    /// <old>
    /// NOT_INIT -(1X)-> FIRST_ON, adjust position -(Tap)-> COMPLETE
    /// FIRST_ON -(X1)-> BOTH_ON, adjust position and size if parallel, -(Tap)-> COMPLETE
    /// FIRST_ON/BOTH_ON -(00 for 2 seconds)-> NOT_INIT, disappear.
    /// (New)
    /// NOT_INIT -(visible)-> MOVING, show icon & text -(tap)-> COMPLETE -(gaze enter)-> ARCHIVE_READY
    /// </summary>
    public enum BoardStatus { NOT_INIT, MOVING, COMPLETE, ARCHIVE_READY }
}

public class PageOrganizer : MonoBehaviour
{
    #region Unity Game Objects
    public GameObject hololensCamera;

    [Tooltip("Parent of all title-related objects")]
    public GameObject TitleRootObj;
    [Tooltip("Parent of all reference related objects")]
    public GameObject RefRootObj;
    public GameObject NoteRootObj;

    public TitlePageHelper TitlePage { get; private set; }
    public RefPageHelper RefPage { get; private set; }
    public NoteboardPageHelper NotePage { get; private set; }
    #endregion

    #region UI-related private objects

    #endregion

    #region Current status indicator
    /// <summary>
    /// The GameObject representing the current document's AR Tag.
    /// </summary>
    public GameObject   CurrDocObj { get; private set; } = null;

    /// <summary>
    ///  Current AR Document instance.
    /// </summary>
    public ARDocument CurrARDoc { get; private set; } = null;

    /// <summary>
    /// The name of the document which currently being read (determined by the pen stroke).
    /// </summary>
    public string       CurrDocStr { get; private set; } = "";

    /// <summary>
    /// Get marker information from the controller
    /// </summary>
    public bool            LoadedMarkers = false;
    private ARUWPController _controller = null;
    #endregion

    #region Important reference dictionaries
    /// <summary>
    /// Dictionary of the documents.
    /// Key: filename, Value: ARDocument.
    /// </summary>
    public static Dictionary<string, ARDocument> DictDocuments = new Dictionary<string, ARDocument>();

    /// <summary>
    /// Dictionary of the printed documents, which were assigned with a barcode, 
    /// and/or a GameObject if it is in the scene.
    /// </summary>
    public static Dictionary<string, GameObject> DictPrintedDocObjects = new Dictionary<string, GameObject>();

    public static Dictionary<int, int> DictBarcodeToARUWPMarker = new Dictionary<int, int>();

    /// <summary>
    /// Dictionary of the sprites (screenshots) of the documents.
    /// Key: filename, Value: Sprite (from the Resources).
    /// </summary>
    private Dictionary<string, Sprite>          DictSprites = new Dictionary<string, Sprite>();

    /// <summary>
    /// Query filenames using the barcode.
    /// </summary>
    public Dictionary<int, string>             DictBarcodeFileNames = new Dictionary<int, string>();
    #endregion

    #region Constant/Shared values
    public const float CANVAS_OPEN_TIME = 1.0f;
    public const float CANVAS_CLOSE_TIME = 1.0f;

    // Standard size of a letter-size paper
    private const float PAPER_WIDTH = 0.216f;
    private const float PAPER_HEIGHT = 0.279f;
    #endregion

    // Use this for initialization
    void Start()
    {
        // General attributes.
        _controller = GameObject.Find("ARUWP Controller").GetComponent<ARUWPController>();
        if (_controller == null)
        {
            Debug.LogError("Page Organizer: not able to find ARUWPController");
            Application.Quit();
        }

        TitlePage = TitleRootObj.GetComponent<TitlePageHelper>();
        RefPage = RefRootObj.GetComponent<RefPageHelper>();
        NotePage = NoteRootObj.GetComponent<NoteboardPageHelper>();

        //RefPage.Init();

        LoadDocuments();
    }

    // Update is called once per frame
    void Update()
    {
        // Load the markers if not done yet and the controller is ready
        if (LoadedMarkers == false && _controller.status == ARUWP.ARUWP_STATUS_RUNNING)
        {
            Debug.Log("Size of markers from controller:" + ARUWPController.markers.Count);
            foreach (var key in ARUWPController.markers.Keys)
            {
                ARUWPMarker marker = ARUWPController.markers[key];
                if (marker.type == ARUWPMarker.MarkerType.single_barcode && marker.target != null)
                {
                    // Get the barcode ID, target GameObject
                    if (DictBarcodeFileNames.ContainsKey(marker.singleBarcodeID))
                    {
                        string fName = DictBarcodeFileNames[marker.singleBarcodeID];
                        GameObject gObj = marker.target;
                        //gObj.SetActive(false);
                        // Load the material for reference (they share the common material)
                        if (marker.singleBarcodeID == 0)
                        {
                            MeshRenderer meshRenderer = gObj.GetComponentInChildren<MeshRenderer>();
                            RefPage.SetPrintedMaterial(meshRenderer.sharedMaterial);
                            Debug.Log("Found printed item material");
                            // Debugging
                            //gObj.SetActive(true);
                            //_refPrintedMaterial.color = new Color(_refPrintedMaterial.color.r, _refPrintedMaterial.color.g, _refPrintedMaterial.color.b, 0.1f);
                        }
                        if (marker.singleBarcodeID >= 52)
                        {
                            // Sticker
                            gObj.SetActive(true);
                        }
                        else if (marker.singleBarcodeID >= 41)
                        {
                            // Board
                            gObj.SetActive(true);
                            DictBarcodeToARUWPMarker.Add(marker.singleBarcodeID, key);
                        }
                        else
                        {
                            // Other files
                            gObj.SetActive(false);
                            TitlePage.PreloadTags(fName);
                        }

                        DictPrintedDocObjects.Add(fName, gObj);
                        Debug.Log("Found the GameObject for:" + fName);
                    }
                    else
                    {
                        Debug.Log("Error: barcode not found:" + marker.singleBarcodeID);
                    }
                }
            }
            LoadedMarkers = true;
        }
    }

    #region Public helper and shared tools
    /// <summary>
    /// Animating the changes of alpha value.
    /// The position and scale is changed by the parent object. 
    /// </summary>
    /// <param name="videoPlayerObj"></param>
    /// <param name="status"></param>
    /// <param name="videoID"></param>
    /// <returns></returns>
    public IEnumerator VideoFade(GameObject videoPlayerObj, ChildObjFadeStatus status, string videoID = "")
    {
        HighQualityPlayback videoPlayer = videoPlayerObj.GetComponent<HighQualityPlayback>();
        VideoController videoController = videoPlayerObj.GetComponent<VideoController>();
        MeshRenderer videoRenderer = videoPlayerObj.GetComponent<MeshRenderer>();
        Material videoMaterial = videoRenderer.material;
        Debug.Log(string.Format("Video Event:{0}, {1}", videoPlayerObj.name, status));

        if (status == ChildObjFadeStatus.CLOSE)
        {
            videoController.Stop();

            for (float i = CANVAS_CLOSE_TIME; i >= 0; i -= Time.deltaTime)
            {
                float percentage = i / CANVAS_CLOSE_TIME;
                //videoPlayerObj.transform.localScale = VIDEO_SCALE * percentage;
                videoMaterial.color = new Color(videoMaterial.color.r, videoMaterial.color.g, videoMaterial.color.b, percentage);
                yield return null;
            }
            videoPlayerObj.SetActive(false);
        }
        else if (status == ChildObjFadeStatus.OPEN && videoID.Length > 0)
        {
            videoPlayerObj.SetActive(true);

            for (float i = 0; i <= CANVAS_OPEN_TIME; i += Time.deltaTime)
            {
                float percentage = i / CANVAS_OPEN_TIME;
                //videoPlayerObj.transform.localScale = VIDEO_SCALE * percentage;
                videoMaterial.color = new Color(videoMaterial.color.r, videoMaterial.color.g, videoMaterial.color.b, percentage);
                yield return null;
            }
            videoPlayer.PlayYoutubeVideo(videoID);
        }
        else if (status == ChildObjFadeStatus.CLOSE_AND_OPEN && videoID.Length > 0)
        {
            videoController.Stop();

            videoPlayer.PlayYoutubeVideo(videoID);
        }
    }

    public bool UpdateCurrDoc(string newDoc, int newPage = -1)
    {
        bool isDiff = false;
        if (CurrDocStr != newDoc)
        {
            if (DictPrintedDocObjects.ContainsKey(newDoc))
            {
                CurrDocObj = DictPrintedDocObjects[newDoc];
            }
            else
            {
                Debug.LogError("Error: Current document object not found:" + newDoc);
                return true;
            }

            if (DictDocuments.ContainsKey(newDoc))
                CurrARDoc = DictDocuments[newDoc];
            else
                Debug.LogError("Error: Current ARDocument instance doesn't exist:" + newDoc);

            CurrDocStr = newDoc;
            isDiff = true;
            TitlePage.PreloadTags(newDoc);
        }
        return isDiff;
    }


    /// <summary>
    /// Check if this document contains figures (thumbnail resources).
    /// Default: check current doc.
    /// </summary>
    /// <param name="docName"></param>
    /// <returns>Positive if true, negative if false</returns>
    public bool CheckThumbnails(string docName = "")
    {
        if (docName.Length == 0)
            docName = CurrDocStr;

        if (DictDocuments.ContainsKey(docName))
        {
            if (DictDocuments[docName].hasFigures == 0)
            {
                if (Resources.Load<Sprite>(string.Format("{0}/Fig1", docName)))
                    DictDocuments[docName].hasFigures = 1;
                else
                    DictDocuments[docName].hasFigures = -1;
            }
            return DictDocuments[docName].hasFigures > 0;
        }
        else
        {
            Debug.Log("CheckThumbnails Error: docName not in dictionary." + docName);
            return false;
        }
    }

    /// <summary>
    /// Check if the YouTube video link exists.
    /// Default: check current doc.
    /// </summary>
    /// <param name="docName"></param>
    /// <returns>Greater than 0: valid url; else: invalid</returns>
    public bool CheckVideo(string docName = "")
    {
        if (docName.Length == 0)
            docName = CurrDocStr;

        if (DictDocuments.ContainsKey(docName))
        {
            return DictDocuments[docName].videoLink.Length > 0;
        }
        else
        {
            Debug.Log("CheckVideo Error: docName not in dictionary." + docName);
            return false;
        }
    }

    /// <summary>
    /// Check if target document has a preview page.
    /// Default: check current document.
    /// </summary>
    /// <param name="docName"></param>
    /// <returns></returns>
    public bool CheckPreview(string docName = "")
    {
        if (docName.Length == 0)
            docName = CurrDocStr;

        if (DictDocuments.ContainsKey(docName))
        {
            if (DictDocuments[docName].hasPreview == 0)
            {
                if (Resources.Load<Sprite>(string.Format("{0}", docName)) ||
                    Resources.Load<Sprite>(string.Format("{0}/{1}-01", docName, docName)) ||
                    Resources.Load<Sprite>(string.Format("{0}/{1}-1", docName, docName)))
                    DictDocuments[docName].hasPreview = 1;
                else
                    DictDocuments[docName].hasPreview = -1;
            }
            return DictDocuments[docName].hasPreview > 0;
        }
        else
        {
            Debug.Log("CheckPreview Error: docName not in dictionary." + docName);
            return false;
        }
    }

    /// <summary>
    /// Check if the target documents has full pages available
    /// For books and PhD thesis, sometimes we only list the first 10 pages here,
    /// but they are still counted as "full" documents.
    /// Default: check current document.
    /// </summary>
    /// <param name="docName"></param>
    /// <returns></returns>
    public bool CheckFullDocuments(string docName = "")
    {
        if (docName.Length == 0)
            docName = CurrDocStr;

        if (DictDocuments.ContainsKey(docName))
        {
            if (DictDocuments[docName].hasFullDocs == 0)
            {
                if (Resources.Load<Sprite>(string.Format("{0}/{1}-02", docName, docName)) ||
                    Resources.Load<Sprite>(string.Format("{0}/{1}-2", docName, docName)))
                    DictDocuments[docName].hasFullDocs = 1;
                else
                    DictDocuments[docName].hasFullDocs = -1;
            }
            return DictDocuments[docName].hasFullDocs > 0;
        }
        else
        {
            Debug.Log("CheckFullDocuments Error: docName not in dictionary." + docName);
            return false;
        }
    }

    public Sprite FindSprite(string spriteName)
    {
        // Load the sprite from cached dict or resources
        if (DictSprites.ContainsKey(spriteName))
        {
            Debug.Log("Loading from dict:" + spriteName);
            return DictSprites[spriteName];
        }
        else
        {
            // Load from resources
            Sprite newRefSprite = Resources.Load<Sprite>(spriteName);
            if (!newRefSprite)
            {
                newRefSprite = Resources.Load<Sprite>(string.Format("{0}/{0}-01", spriteName));
            }

            if (newRefSprite)
            {
                DictSprites.Add(spriteName, newRefSprite);
                Debug.Log(string.Format("Loading from file:{0}, current dict size:{1}", spriteName, DictSprites.Count));
                return newRefSprite;
            }
            else
            {
                Debug.Log("Error: reference sprite resources not found. " + spriteName);
                return null;
            }
        }
    }

    public Vector3 CalcTargetEulerAngles()
    {
        float cameraEulerX = hololensCamera.transform.localEulerAngles.x;
        float targetEulerX = Mathf.Max(0, Mathf.Min(90, cameraEulerX));
        float targetEulerY = hololensCamera.transform.localEulerAngles.y;
        for (float i = 0; i < 360; i += 90)
        {
            if (Mathf.Abs(i - targetEulerY) < 10)
            {
                targetEulerY = i;
                break;
            }
        }
        return new Vector3(targetEulerX, targetEulerY, 0);
    }

    //public Vector3 CalcEulerAngleDiff(Vector3 prevAngle)
    //{
    //    float diffX = Mathf.Max(0, Mathf.Min(90, hololensCamera.transform.localEulerAngles.x)) - prevAngle.x;
    //    if (diffX > 180)
    //        diffX -= 360;
    //    else if (diffX < -180)
    //        diffX += 360;

    //    float targetEulerY = hololensCamera.transform.localEulerAngles.y;
    //    for (float i = 0; i < 360; i += 90)
    //    {
    //        if (Mathf.Abs(i - targetEulerY) < 10)
    //        {
    //            targetEulerY = i;
    //            break;
    //        }
    //    }
    //    float diffY = targetEulerY - prevAngle.y;
    //    if (diffY > 180)
    //        diffY -= 360;
    //    else if (diffY < -180)
    //        diffY += 360;

    //    float diffZ = 0 - prevAngle.z;
    //    if (diffZ > 180)
    //        diffZ -= 360;
    //    else if (diffZ < -180)
    //        diffZ += 360;
        
    //    return new Vector3(diffX, diffY, diffZ);
    //}
    #endregion


    #region Private helper
    private void LoadDocuments()
    {
        // TODO: Load document from local files.
        DictDocuments.Add("Norrie03", new ARDocument("Norrie03", "Switching over to Paper: A New Web Channel", new string[] { "Moira C. Norrie", "Beat Signer" }, "WISE", 2003));
        DictDocuments.Add("OHara97", new ARDocument("OHara97", "A Comparison of Reading Paper and On-Line Documents", new string[] { "Kenton O'Hara", "Abigail Sellen" }, "CHI", 1997));
        DictDocuments.Add("Johnson93", new ARDocument("Johnson93", "Bridging the Paper and Electronic Worlds: The Paper User Interface", new string[] { "Walter Johnson", "Herbert Jellinek", "Leigh Klotz", "Jr. Ramana Rao", "Stuart Card" }, "CHI", 1993));
        DictDocuments.Add("Klamka17", new ARDocument("Klamka17", "IllumiPaper: Illuminated Interactive Paper", new string[] { "Konstantin Klamka", "Raimund Dachselt" }, "CHI", 2017, "", "r56KYevR_Vw"));

        DictDocuments.Add("Sellen03", new ARDocument("Sellen03", "The Myth of the Paperless Office", new string[] { "Abigail J. Sellen", "Richard HR Harper" }, "MIT press", 2003));
        DictDocuments.Add("Logitech06", new ARDocument("Logitech06", "Logitech IO Personal Digital Pen", new string[] { "Logitech Inc." }, "http://www.logitech.com", 2006));
        DictDocuments.Add("Paper00", new ARDocument("Paper00", "Paper++ Project", new string[] { "Disappearing Computer Initiative" }, "http://www.paperplusplus.net", 2000));
        DictDocuments.Add("Levy01", new ARDocument("Levy01", "Scrolling forward: Making sense of documents in the digital age", new string[] { "David M. Levy" }, "Arcade Publishing", 2001));
        DictDocuments.Add("OneNote07", new ARDocument("OneNote07", "Microsoft OneNote", new string[] { "Microsoft Corporation" }, "http://www.microsoft.com/office/onenote/", 2007));
        DictDocuments.Add("Norrie02", new ARDocument("Norrie02", "Web-based integration of printed and digital information", new string[] { "Moira C. Norrie", "Beat Signer" }, "DI-Web", 2002));

        DictDocuments.Add("Luff04", new ARDocument("Luff04", "Only touching the surface: creating affinities between digital content and paper", new string[] { "Paul Luff", "Christian Heath", "Moira Norrie", "Beat Signer", "Peter Herdman" }, "CSCW", 2004));
        DictDocuments.Add("Luff07", new ARDocument("Luff07", "Augmented Paper: Developing Relationships Between Digital Content and Paper", new string[] { "Paul Luff", "Guy Adams", "Wolfgang Bock", "Adam Drazin", "David Frohlich", "Christian Heath", "Peter Herdman", "Heather King", "Nadja Linketscher", "Rachel Murphy", "Moira Norrie", "Abigail Sellen", "Beat Signer", "Ella Tallyn", "Emil Zeller" }, "The Disappearing Computer", 2007));
        DictDocuments.Add("Hansen88", new ARDocument("Hansen88", "Reading and writing with computers: a framework for explaining differences in performance", new string[] { "Wilfred J. Hansen", "Christina Haas" }, "Communications of the ACM", 1988));
        DictDocuments.Add("Signer06", new ARDocument("Signer06", "Fundamental concepts for interactive paper and cross-media information spaces", new string[] { "Beat Signer" }, "Ph.D. Dissertation ETH Zurich", 2006));
        DictDocuments.Add("Steimle12", new ARDocument("Steimle12", "Pen-and-Paper User Interfaces", new string[] { "Jurgen Steimle" }, "Springer", 2012));
        DictDocuments.Add("Wellner93", new ARDocument("Wellner93", "Interacting with paper on the DigitalDesk", new string[] { "Pierre Wellner" }, "Communications of the ACM", 1993, "", "2NRLHqNdRNE"));
        DictDocuments.Add("Norrie07", new ARDocument("Norrie07", "Context-aware platform for mobile data management", new string[] { "Moira C. Norrie", "Beat Signer", "Michael Grossniklaus", "Rudi Belotti", "Corsin Decurtins", "Nadir Weibel"}, "Wireless Networks", 2007));
        DictDocuments.Add("Lee04", new ARDocument("Lee04", "Haptic pen: a tactile feedback stylus for touch screens", new string[] { "Johnny C. Lee", "Paul H. Dietz", "Darren Leigh", "William S. Yerazunis", "Scott E. Hudson" }, "UIST", 2004, "", "Sk-ExWeA03Y"));
        DictDocuments.Add("Liao06", new ARDocument("Liao06", "Pen-top feedback for paper-based interfaces", new string[] { "Chunyuan Liao", "François Guimbretière", "Corinna E. Loeckenhoff" }, "UIST", 2006, "", "9q7wD3Ihspg"));
        DictDocuments.Add("Lopes16", new ARDocument("Lopes16", "Muscle-plotter: An Interactive System based on Electrical Muscle Stimulation that Produces Spatial Output", new string[] { "Pedro Lopes", "Doăa Yüksel", "	François Guimbretière", "Patrick Baudisch" }, "UIST", 2016, "", "u5vQIZflQzQ"));

        // Sticky notes
        DictDocuments.Add("Sticky52", new ARDocument("Sticky52", "Sticky", new string[] { }, "", 52));
        DictDocuments.Add("Sticky53", new ARDocument("Sticky53", "Sticky", new string[] { }, "", 53));
        DictDocuments.Add("Sticky54", new ARDocument("Sticky54", "Sticky", new string[] { }, "", 54));
        DictDocuments.Add("Sticky55", new ARDocument("Sticky55", "Sticky", new string[] { }, "", 55));
        DictDocuments.Add("Sticky56", new ARDocument("Sticky56", "Sticky", new string[] { }, "", 56));

        // Printed documents.
        DictBarcodeFileNames.Add(0, "Norrie03");
        DictBarcodeFileNames.Add(1, "OHara97");
        DictBarcodeFileNames.Add(2, "Johnson93");
        DictBarcodeFileNames.Add(3, "Klamka17");

        DictBarcodeFileNames.Add(52, "Sticky52");
        DictBarcodeFileNames.Add(53, "Sticky53");
        DictBarcodeFileNames.Add(54, "Sticky54");
        DictBarcodeFileNames.Add(55, "Sticky55");
        DictBarcodeFileNames.Add(56, "Sticky56");

        DictBarcodeFileNames.Add(41, "Board1TL");
        DictBarcodeFileNames.Add(42, "Board1BR");
        DictBarcodeFileNames.Add(43, "Board2TL");
        DictBarcodeFileNames.Add(44, "Board2BR");
    }
    #endregion

    


}