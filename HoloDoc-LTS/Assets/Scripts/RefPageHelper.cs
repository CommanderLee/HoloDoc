using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PageConstants;

public class RefPageHelper : MonoBehaviour {

    #region Unity Game Objects for Reference Events
    [Tooltip("Reference title holder: meta-data")]
    public GameObject MetaDataObj;
    public Text MetaDataTitle;
    public Text MetaDataAuthors;
    public Text MetaDataSource;

    [Tooltip("Reference Preview holder")]
    public GameObject PreviewObj;
    public Image imagePreview;

    [Tooltip("Reference document: full papers")]
    public GameObject FullPaperObj;
    [Tooltip("The video player obj for the references")]
    public GameObject VideoObj;

    public PieMenu PieMenu;
    public PageOrganizer PageOrg;
    #endregion

    #region Constant values
    private const float CANVAS_OPEN_TIME = 1.0f;
    private const float CANVAS_CLOSE_TIME = 1.0f;

    // Standard size of a letter-size paper
    private const float PAPER_WIDTH = 0.216f;
    private const float PAPER_HEIGHT = 0.279f;

    // Height of the reference canvas: small (title), medium (first page)
    private const int REF_CANVAS_S_H = 15;
    private const int REF_CANVAS_M_H = 34;
    // Correspond to the RefEventStatus enum
    private readonly int[] RefCanvasHeight = { 0, 15, 34, 34, 30 };
    private readonly int[] RefCanvasWidth = { 0, 22, 22, 22, 45 };
    private const float REFERENCE_HEIGHT = 40;

    private const float HUD_FORWARD = 0.9f;
    #endregion

    #region Important Dictionaries
    private Dictionary<string, List<Sprite>> refPreloadSprites = new Dictionary<string, List<Sprite>>();
    #endregion

    #region UI-related private objects
    /// <summary>
    /// The GameObject representing the current reference's physcial location (AR Tag)
    /// </summary>
    private GameObject refPrintedObj = null;
    private Material refPrintedMaterial = null;

    private CanvasGroup bkgMetaData;
    private CanvasGroup bkgPreview;
    private CanvasGroup bkgFullPaper;

    private HorizontalLayoutGroup fullPaperGrid;
    private List<GameObject> fullPaperPages = new List<GameObject>();
    #endregion

    #region Class status attributes
    //private PageOrganizer pageOrg;
    /// <summary>
    /// The current reference document file name that the reader is interested in.
    /// </summary>
    public RefEventStatus RefFlag { get; private set; } = RefEventStatus.OFF;

    private string refFileName = "";
    private ARDocument refDoc = null;

    private int refAnimationCounter = 0;
    private Vector3 prevManipulationPosition;
    private bool startFollowing = false;
    #endregion
    
    // Use this for initialization
    void Start () {
        // Reference events: Title (meta-data), FrontPage, Full Doc, Video
        fullPaperGrid = FullPaperObj.GetComponent<HorizontalLayoutGroup>();

        bkgMetaData = MetaDataObj.GetComponent<CanvasGroup>();
        bkgMetaData.alpha = 0;

        bkgPreview = PreviewObj.GetComponent<CanvasGroup>();
        bkgPreview.alpha = 0;

        bkgFullPaper = FullPaperObj.GetComponent<CanvasGroup>();
        bkgFullPaper.alpha = 0;


        VideoObj.SetActive(false);   // YouTube Video panel not controlled by UI transparency
    }
	
	// Update is called once per frame
	void Update () {
		if (PenOrganizer.PenControlCode == CONTROL_CODE.HUD)
        {
            // Don't move in a range.
            Vector3 oldPos = gameObject.transform.position;

            // Calculate the possible position
            gameObject.transform.position = PageOrg.hololensCamera.transform.position;
            gameObject.transform.Translate(PageOrg.hololensCamera.transform.forward * HUD_FORWARD, Space.World);

            Vector3 posDist = gameObject.transform.position - oldPos;
            if (!startFollowing && posDist.magnitude >= 0.4f)
            {
                startFollowing = true;
            }
            else if (startFollowing && posDist.magnitude <= 0.02f)
            {
                gameObject.transform.localEulerAngles = new Vector3(PageOrg.hololensCamera.transform.localEulerAngles.x, PageOrg.hololensCamera.transform.localEulerAngles.y, 0);
                startFollowing = false;
            }

            if (startFollowing)
            {
                gameObject.transform.position = oldPos + posDist.normalized * Mathf.Max(posDist.magnitude * 0.1f, 0.02f);
                gameObject.transform.localEulerAngles = new Vector3(PageOrg.hololensCamera.transform.localEulerAngles.x, PageOrg.hololensCamera.transform.localEulerAngles.y, 0);
            }
            else
            {
                gameObject.transform.position = oldPos;
            }
        }
	}

    //public void Init ()
    //{
    //    fullPaperGrid = FullPaperObj.GetComponent<HorizontalLayoutGroup>();

    //    bkgMetaData = MetaDataObj.GetComponent<CanvasGroup>();
    //    bkgMetaData.alpha = 0;

    //    bkgPreview = PreviewObj.GetComponent<CanvasGroup>();
    //    bkgPreview.alpha = 0;

    //    bkgFullPaper = FullPaperObj.GetComponent<CanvasGroup>();
    //    bkgFullPaper.alpha = 0;


    //    VideoObj.SetActive(false);   // YouTube Video panel not controlled by UI transparency
    //}

    #region Animations (IEnumerator)
    /// <summary>
    /// 
    /// </summary>
    /// <param name="status"></param>
    /// <param name="newRefStatus"></param>
    /// <param name="refAnimationCounter"></param>
    /// <param name="normPosX">Normalized position X</param>
    /// <param name="normPosY">Normalized position Y</param>
    /// <returns></returns>
    private IEnumerator ReferenceCanvasFade(CanvasFadeStatus status, RefEventStatus newRefStatus, int refAnimationCounter, float normPosX = 0, float normPosY = 0)
    {
        // While closing, newRef is currRef. While opening or modifying, newRef is new, and _refFlag is old.
        float currHeight = RefCanvasHeight[(int)newRefStatus];

        if (status == CanvasFadeStatus.CLOSE)
        {
            for (float i = CANVAS_CLOSE_TIME; i >= 0; i -= Time.deltaTime)
            {
                float percentage = i / CANVAS_CLOSE_TIME;
                gameObject.transform.localScale = Vector3.one * percentage;
                yield return null;
            }
        }
        else if (status == CanvasFadeStatus.OPEN)
        {
            if (newRefStatus == RefEventStatus.FULL_PAPER)
            {
                // Dynamic ones: For full papers, the width currently stores value for single page
                PreloadRefPages(refFileName);
            }

            Vector3 moveUp = Vector3.zero;
            switch (PenOrganizer.PenControlCode)
            {
                case CONTROL_CODE.DEFAULT:
                    //gameObject.transform.SetParent(PageOrg.transform);

                    // Rotate based on view point
                    Vector3 targetVec = PageOrg.CalcTargetEulerAngles();
                    gameObject.transform.localEulerAngles = targetVec;
                    
                    // Set starting position
                    gameObject.transform.position = PageOrg.CurrDocObj.transform.position;
                    // Move along right (x-axis of AR tag, RIGHT)
                    gameObject.transform.Translate(PageOrg.CurrDocObj.transform.right * (normPosX * PAPER_WIDTH - 0.02f), Space.World);
                    // Move along bottom (y-axis of AR tag, UP)
                    gameObject.transform.Translate(PageOrg.CurrDocObj.transform.up * (normPosY * PAPER_HEIGHT - 0.02f - 0.06f * Mathf.Sin(targetVec.x * Mathf.Deg2Rad)), Space.World);
                    
                    // Set moving direction (world coordinate)
                    moveUp = new Vector3(0, 0.06f * Mathf.Cos(targetVec.x * Mathf.Deg2Rad), 0);
                    break;

                case CONTROL_CODE.SIDE_BY_SIDE:
                    //gameObject.transform.SetParent(PageOrg.transform);

                    // Flat on surface
                    gameObject.transform.localEulerAngles = new Vector3(90, 0, 0); 

                    // Set starting position
                    gameObject.transform.position = PageOrg.CurrDocObj.transform.position;
                    // Move along right (x-axis of AR tag, RIGHT) to side by side
                    gameObject.transform.Translate(PageOrg.CurrDocObj.transform.right * PAPER_WIDTH, Space.World);
                    // Move along bottom (y-axis of AR tag, UP) to lower bottom
                    gameObject.transform.Translate(PageOrg.CurrDocObj.transform.up * PAPER_HEIGHT * 0.65f, Space.World);

                    break;

                case CONTROL_CODE.VERTICAL:
                    //gameObject.transform.SetParent(PageOrg.transform);

                    gameObject.transform.localEulerAngles = Vector3.zero;

                    // Set starting position
                    gameObject.transform.position = new Vector3(-0.1f, -0.15f, 0.8f);

                    break;

                case CONTROL_CODE.HUD:
                    //gameObject.transform.SetParent(PageOrg.hololensCamera.transform);

                    gameObject.transform.localEulerAngles = new Vector3(PageOrg.hololensCamera.transform.localEulerAngles.x, PageOrg.hololensCamera.transform.localEulerAngles.y, 0);
                    gameObject.transform.position = PageOrg.hololensCamera.transform.position;
                    gameObject.transform.Translate(PageOrg.hololensCamera.transform.forward * HUD_FORWARD, Space.World);
                    break;
            }

            
            // Scale up, move up
            for (float i = 0; i <= CANVAS_OPEN_TIME; i += Time.deltaTime)
            {
                float percentage = i / CANVAS_OPEN_TIME;
                if (PenOrganizer.PenControlCode != CONTROL_CODE.HUD)
                    gameObject.transform.Translate(moveUp * Time.deltaTime / CANVAS_OPEN_TIME, Space.World);
                gameObject.transform.localScale = Vector3.one * percentage;
                yield return null;
            }
            Debug.Log(string.Format("New Opened Reference position:{0}, euler angles:{1}", gameObject.transform.position, gameObject.transform.localEulerAngles));
        }
        else if (status == CanvasFadeStatus.MODIFY)
        {
            if (newRefStatus == RefEventStatus.FULL_PAPER)
            {
                // Dynamic ones: For full papers, the width currently stores value for single page
                PreloadRefPages(refFileName);
            }

            Vector3 oldPos = gameObject.transform.position;
            Vector3 posDist = Vector3.zero;

            switch (PenOrganizer.PenControlCode)
            {
                case CONTROL_CODE.DEFAULT:
                    // Rotate the canvas
                    Vector3 targetVec = PageOrg.CalcTargetEulerAngles();
                    gameObject.transform.localEulerAngles = targetVec;

                    gameObject.transform.position = PageOrg.CurrDocObj.transform.position;
                    // Move along right (x-axis of AR tag, RIGHT)
                    gameObject.transform.Translate(PageOrg.CurrDocObj.transform.right * (normPosX * PAPER_WIDTH - 0.02f), Space.World);
                    // Move along bottom (y-axis of AR tag, UP)
                    gameObject.transform.Translate(PageOrg.CurrDocObj.transform.up * (normPosY * PAPER_HEIGHT - 0.02f - 0.06f * Mathf.Sin(targetVec.x * Mathf.Deg2Rad)), Space.World);
                    // Move up (world)
                    gameObject.transform.Translate(0, 0.06f * Mathf.Cos(targetVec.x * Mathf.Deg2Rad), 0, Space.World);

                    posDist = gameObject.transform.position - oldPos;
                    break;

                case CONTROL_CODE.SIDE_BY_SIDE:
                    // Set starting position
                    gameObject.transform.position = PageOrg.CurrDocObj.transform.position;
                    // Move along right (x-axis of AR tag, RIGHT) to side by side
                    gameObject.transform.Translate(PageOrg.CurrDocObj.transform.right * PAPER_WIDTH, Space.World);
                    // Move along bottom (y-axis of AR tag, UP) to lower bottom
                    gameObject.transform.Translate(PageOrg.CurrDocObj.transform.up * PAPER_HEIGHT * 0.65f, Space.World);

                    posDist = gameObject.transform.position - oldPos;
                    break;

                case CONTROL_CODE.HUD:
                    // Directly go to the new place
                    gameObject.transform.localEulerAngles = new Vector3(PageOrg.hololensCamera.transform.localEulerAngles.x, PageOrg.hololensCamera.transform.localEulerAngles.y, 0);
                    gameObject.transform.position = PageOrg.hololensCamera.transform.position;
                    gameObject.transform.Translate(PageOrg.hololensCamera.transform.forward * HUD_FORWARD, Space.World);
                    break;
            }

            if (PenOrganizer.PenControlCode != CONTROL_CODE.HUD)
            {
                for (float i = 0; i <= CANVAS_OPEN_TIME; i += Time.deltaTime)
                {
                    float percentage = i / CANVAS_OPEN_TIME;
                    gameObject.transform.position = oldPos + posDist * percentage;
                    //gameObject.transform.localEulerAngles += anglePerSec * Time.deltaTime;
                    yield return null;
                }
            }

            Debug.Log(string.Format("Modified Reference position:{0}, euler angles:{1}", gameObject.transform.position, gameObject.transform.localEulerAngles));
        }
    }

    /// <summary>
    /// The reference event animations use morph effect because they are closer.
    /// For the title event, since we explicitly switch context, we just shrink and re-open the new one.
    /// </summary>
    /// <param name="flag"></param>
    /// <returns></returns>
    private IEnumerator ReferenceMetaDataFade(ChildObjFadeStatus flag)//, float posX=0, float posY=0)
    {
        if (flag == ChildObjFadeStatus.CLOSE)
        {
            // Handle the fade in/out effect of the [current] printed reference on the desk, if available
            CanvasGroup prevPrintedGroup = null;
            if (refPrintedObj)
            {
                prevPrintedGroup = refPrintedObj.GetComponent<CanvasGroup>();
            }

            // ON -> OFF: Fade out the title alpha
            for (float i = CANVAS_CLOSE_TIME; i >= 0; i -= Time.deltaTime)
            {
                float percentage = i / CANVAS_CLOSE_TIME;
                bkgMetaData.alpha = percentage;

                if (prevPrintedGroup)
                {
                    prevPrintedGroup.alpha = percentage;
                    refPrintedMaterial.color = new Color(refPrintedMaterial.color.r, refPrintedMaterial.color.g, refPrintedMaterial.color.b, percentage);
                }
                yield return null;
            }
            MetaDataObj.SetActive(false);
            if (refPrintedObj)
            {
                // Finally, hide and clear the printed reference object
                refPrintedObj.SetActive(false);
                refPrintedObj = null;
            }
            Debug.Log("Ref: Title closed.");
        }
        else if (flag == ChildObjFadeStatus.OPEN)
        {
            MetaDataObj.SetActive(true);

            CanvasGroup currPrintedGroup = null;
            // Update new reference's printed object (AR Tag)
            if (PageOrganizer.DictPrintedDocObjects.ContainsKey(refFileName))
            {
                refPrintedObj = PageOrganizer.DictPrintedDocObjects[refFileName];
                refPrintedObj.SetActive(true);
                currPrintedGroup = refPrintedObj.GetComponent<CanvasGroup>();
            }

            // Note the (x,y) of the 2D document maps to X and -Z axis of Unity3D
            if (PageOrganizer.DictDocuments.ContainsKey(refFileName))
            {
                ARDocument refDoc = PageOrganizer.DictDocuments[refFileName];
                MetaDataTitle.text = refDoc.title;
                MetaDataAuthors.text = string.Join(", ", refDoc.authors);
                MetaDataSource.text = string.Format("{0} - {1}", refDoc.year, refDoc.source);
            }
            else
            {
                Debug.LogError("Error: reference name not found in documents dict:" + refFileName);
            }
            
            for (float i = 0; i <= CANVAS_OPEN_TIME; i += Time.deltaTime)
            {
                float percentage = i / CANVAS_OPEN_TIME;
                bkgMetaData.alpha = percentage;

                if (currPrintedGroup)
                {
                    currPrintedGroup.alpha = percentage;
                    refPrintedMaterial.color = new Color(refPrintedMaterial.color.r, refPrintedMaterial.color.g, refPrintedMaterial.color.b, percentage);
                }
                yield return null;
            }
        }
        else if (flag == ChildObjFadeStatus.CLOSE_AND_OPEN)
        {
            if (PageOrganizer.DictDocuments.ContainsKey(refFileName))
            {
                ARDocument refDoc = PageOrganizer.DictDocuments[refFileName];
                Debug.Log("Update Ref Title: " + refDoc.title);

                // Create new panel
                GameObject newMetaDataObj = Instantiate(MetaDataObj, gameObject.transform);
                Canvas newMetaDataCanvas = newMetaDataObj.GetComponent<Canvas>();
                newMetaDataCanvas.overrideSorting = true;
                newMetaDataCanvas.sortingOrder = 0;

                Text[] texts = newMetaDataObj.GetComponentsInChildren<Text>();

                // Handle the fade in/out effect of the [current] printed reference on the desk, if available
                CanvasGroup prevPrintedGroup = null;
                if (refPrintedObj)
                {
                    prevPrintedGroup = refPrintedObj.GetComponent<CanvasGroup>();
                }

                CanvasGroup currPrintedGroup = null;
                GameObject currPrintedObj = null;
                // Update new reference's printed object (AR Tag)
                if (PageOrganizer.DictPrintedDocObjects.ContainsKey(refFileName))
                {
                    currPrintedObj = PageOrganizer.DictPrintedDocObjects[refFileName];
                    currPrintedObj.SetActive(true);
                    currPrintedGroup = currPrintedObj.GetComponent<CanvasGroup>();
                }

                foreach (Text text in texts)
                {
                    if (text.name.Contains("Title"))
                    {
                        text.text = "<b>" + refDoc.title + "</b>";
                        MetaDataTitle = text;
                    }
                    else if (text.name.Contains("Authors"))
                    {
                        text.text = string.Join(", ", refDoc.authors);
                        MetaDataAuthors = text;
                    }
                    else if (text.name.Contains("Source"))
                    {
                        text.text = string.Format("<i>{0} - {1}</i>", refDoc.year, refDoc.source);
                        MetaDataSource = text;
                    }
                }
                CanvasGroup newGroup = newMetaDataObj.GetComponent<CanvasGroup>();

                // hand-made Morph effect: fade in/out at the same time.
                for (float i = 0; i <= CANVAS_OPEN_TIME; i += Time.deltaTime)
                {
                    float percentage = i / CANVAS_OPEN_TIME;
                    // Fading out
                    bkgMetaData.alpha = 1 - percentage;
                    if (prevPrintedGroup)
                    {
                        prevPrintedGroup.alpha = 1 - percentage;
                    }

                    // Fading in
                    newGroup.alpha = percentage;
                    if (currPrintedGroup)
                    {
                        currPrintedGroup.alpha = percentage;
                    }
                    yield return null;
                }
                if (refPrintedObj)
                {
                    // Finally, hide and clear the printed reference object
                    refPrintedObj.SetActive(false);
                    refPrintedObj = null;
                }
                if (currPrintedObj)
                {
                    // Update the printed reference object
                    refPrintedObj = currPrintedObj;
                }

                newGroup.alpha = 1;

                // Update the variables
                MetaDataObj.SetActive(false);
                MetaDataObj = newMetaDataObj;
                bkgMetaData = newGroup;

                Debug.Log("Ref: New title has replaced the old one:" + MetaDataObj.name);
            }
            else
            {
                Debug.LogError("Error: reference name not found in documents dict:" + refFileName);
            }
        }
    }

    /// <summary>
    /// Animation for the reference page.
    /// </summary>
    /// <param name="status"></param>
    /// <param name="sourceX">Source of the animation, x-coordinate</param>
    /// <param name="sourceY">Source of the animation, y-coordinate</param>
    /// <returns></returns>
    private IEnumerator ReferencePreviewFade(ChildObjFadeStatus status)//, float sourceX = 0, float sourceY = 0)
    {
        if (status == ChildObjFadeStatus.CLOSE)
        {
            //// Handle the fade in/out effect of the [current] printed reference on the desk, if available
            //CanvasGroup prevPrintedGroup = null;
            //if (refPrintedObj)
            //{
            //    prevPrintedGroup = refPrintedObj.GetComponent<CanvasGroup>();
            //}

            for (float i = CANVAS_CLOSE_TIME; i >= 0; i -= Time.deltaTime)
            {
                float percentage = i / CANVAS_CLOSE_TIME;
                bkgPreview.alpha = percentage;
                //if (prevPrintedGroup)
                //{
                //    prevPrintedGroup.alpha = percentage;
                //    refPrintedMaterial.color = new Color(refPrintedMaterial.color.r, refPrintedMaterial.color.g, refPrintedMaterial.color.b, percentage);
                //}
                yield return null;
            }
            PreviewObj.SetActive(false);
            //if (refPrintedObj)
            //{
            //    // Finally, hide and clear the printed reference object
            //    refPrintedObj.SetActive(false);
            //    refPrintedObj = null;
            //}
            Debug.Log("Ref: Front page closed:" + refFileName);
        }
        else if (status == ChildObjFadeStatus.OPEN)
        {
            PreviewObj.SetActive(true);

            //CanvasGroup currPrintedGroup = null;
            //// Update new reference's printed object (AR Tag)
            //if (PageOrganizer.DictPrintedDocObjects.ContainsKey(refFileName))
            //{
            //    refPrintedObj = PageOrganizer.DictPrintedDocObjects[refFileName];
            //    refPrintedObj.SetActive(true);
            //    currPrintedGroup = refPrintedObj.GetComponent<CanvasGroup>();
            //}

            // Show, find, or create, Flag: OFF -> ON
            // Load the sprite from cached dict or resources
            imagePreview.overrideSprite = PageOrg.FindSprite(refFileName);

            for (float i = 0; i <= CANVAS_OPEN_TIME; i += Time.deltaTime)
            {
                float percentage = i / CANVAS_OPEN_TIME;
                bkgPreview.alpha = percentage;
                //if (currPrintedGroup)
                //{
                //    currPrintedGroup.alpha = percentage;
                //    refPrintedMaterial.color = new Color(refPrintedMaterial.color.r, refPrintedMaterial.color.g, refPrintedMaterial.color.b, percentage);
                //}
                yield return null;
            }
        }
        else if (status == ChildObjFadeStatus.CLOSE_AND_OPEN)
        {
            //// Handle the fade in/out effect of the [current] printed reference on the desk, if available
            //CanvasGroup prevPrintedGroup = null;
            //if (refPrintedObj)
            //{
            //    prevPrintedGroup = refPrintedObj.GetComponent<CanvasGroup>();
            //}

            //CanvasGroup currPrintedGroup = null;
            //GameObject currPrintedObj = null;
            //// Update new reference's printed object (AR Tag)
            //if (PageOrganizer.DictPrintedDocObjects.ContainsKey(refFileName))
            //{
            //    currPrintedObj = PageOrganizer.DictPrintedDocObjects[refFileName];
            //    currPrintedObj.SetActive(true);
            //    currPrintedGroup = currPrintedObj.GetComponent<CanvasGroup>();
            //}

            // Show, find, or create, Flag: OFF -> ON
            GameObject newPreviewObj = Instantiate(PreviewObj, gameObject.transform);
            newPreviewObj.SetActive(true);
            Image newImagePreview = newPreviewObj.GetComponent<Image>();
            // Load the sprite from cached dict or resources
            newImagePreview.overrideSprite = PageOrg.FindSprite(refFileName);
            CanvasGroup newPreviewBkg = newPreviewObj.GetComponent<CanvasGroup>();

            for (float i = CANVAS_CLOSE_TIME; i >= 0; i -= Time.deltaTime)
            {
                float percentage = i / CANVAS_CLOSE_TIME;
                // Fading out
                bkgPreview.alpha = percentage;
                //if (prevPrintedGroup)
                //{
                //    prevPrintedGroup.alpha = percentage;
                //}
                // Fading in
                newPreviewBkg.alpha = 1 - percentage;
                //if (currPrintedGroup)
                //{
                //    currPrintedGroup.alpha = 1 - percentage;
                //}
                yield return null;
            }
            //if (refPrintedObj)
            //{
            //    // Finally, hide and clear the printed reference object
            //    refPrintedObj.SetActive(false);
            //    refPrintedObj = null;
            //}
            //if (currPrintedObj)
            //{
            //    // Update the printed reference object
            //    refPrintedObj = currPrintedObj;
            //}
            
            PreviewObj.SetActive(false);
            PreviewObj = newPreviewObj;
            imagePreview = newImagePreview;
            bkgPreview = newPreviewBkg;
            Debug.Log("Ref: frontpage replaced:" + refFileName);
        }
    }

    private IEnumerator ReferenceFullPaperFade(ChildObjFadeStatus status)
    {
        if (status == ChildObjFadeStatus.CLOSE)
        {
            for (float i = CANVAS_CLOSE_TIME; i >= 0; i -= Time.deltaTime)
            {
                float percentage = i / CANVAS_CLOSE_TIME;
                bkgFullPaper.alpha = percentage;
                yield return null;
            }
            FullPaperObj.SetActive(false);

            // Reset to navigation recognizer
            GazeGestureManager.Instance.Transition(GazeGestureManager.Instance.NavigationRecognizer);
        }
        else if (status == ChildObjFadeStatus.OPEN)
        {
            FullPaperObj.SetActive(true);
            // Clear, if any.
            foreach (GameObject go in fullPaperPages)
            {
                Destroy(go);
            }

            UpdateRefDocObjects(fullPaperGrid, fullPaperPages);

            prevManipulationPosition = Vector3.zero;
            
            for (float i = 0; i <= CANVAS_OPEN_TIME; i += Time.deltaTime)
            {
                float percentage = i / CANVAS_OPEN_TIME;
                bkgFullPaper.alpha = percentage;
                yield return null;
            }

            // Start the manipulation recognizer
            GazeGestureManager.Instance.Transition(GazeGestureManager.Instance.ManipulationRecognizer);
        }
        else if (status == ChildObjFadeStatus.CLOSE_AND_OPEN)
        {
            // Show, find, or create, Flag: OFF -> ON
            //GameObject newDocumentObj = Instantiate(Resources.Load("PanelRefDocument")) as GameObject;\
            GameObject newFullPaperObj = Instantiate(FullPaperObj, gameObject.transform);
            newFullPaperObj.SetActive(true);
            newFullPaperObj.transform.position = FullPaperObj.transform.position;
            newFullPaperObj.transform.rotation = FullPaperObj.transform.rotation;

            HorizontalLayoutGroup newFullPaperGrid = newFullPaperObj.GetComponent<HorizontalLayoutGroup>();
            List<GameObject> newFullPaperPages = new List<GameObject>();
            UpdateRefDocObjects(newFullPaperGrid, newFullPaperPages);

            CanvasGroup newBkgFullPaper = newFullPaperObj.GetComponent<CanvasGroup>();
            
            for (float i = CANVAS_OPEN_TIME; i >= 0; i -= Time.deltaTime)
            {
                float percentage = i / CANVAS_OPEN_TIME;
                // Fading out
                bkgFullPaper.alpha = percentage;
                // Fading in
                newBkgFullPaper.alpha = 1 - percentage;
                yield return null;
            }

            // Replace the attributes
            foreach (GameObject go in fullPaperPages)
            {
                Destroy(go);
            }
            fullPaperPages = newFullPaperPages;
            fullPaperGrid = newFullPaperGrid;

            FullPaperObj.SetActive(false);
            FullPaperObj = newFullPaperObj;
            bkgFullPaper = newBkgFullPaper;
            Debug.Log("Ref: full paper replaced:" + refFileName);
        }
    }
    #endregion

    #region Public Interfaces

    public void SetPrintedMaterial(Material mat)
    {
        refPrintedMaterial = mat;
    }

    public string GetRefName()
    {
        return refFileName;
    }

    public string GetRefSource()
    {
        if (refDoc != null && refDoc.source.Length > 0)
            return refDoc.source;
        else
            return "N/A";
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="newDoc"></param>
    /// <param name="newRef"></param>
    /// <param name="normPosX">Normalized position X</param>
    /// <param name="normPosY">Normalized position Y</param>
    /// <param name="newRefType"></param>
    public void UpdateRefEvent(RefEventStatus newRefEvent, string newDoc = "", string newRef = "", float normPosX = 0, float normPosY = 0)
    {
        refAnimationCounter++;

        bool docDiff = (newDoc.Length > 0);

        bool refDiff = false;
        if (newRef.Length > 0 && newRef != refFileName)
        {
            refFileName = newRef;
            refDiff = true;
            if (PageOrganizer.DictDocuments.ContainsKey(refFileName))
                refDoc = PageOrganizer.DictDocuments[refFileName];
            else
                refDoc = null;
        }

        // Should update the canvas if the ref changed.
        bool shouldUpdate = docDiff || refDiff;

        Debug.Log(string.Format("parsing the events, name:{0}, current status:{1}, new status:{2}", refFileName, RefFlag, newRefEvent));

        // Same Ref *Diff Type: if from OFF: open canvas, open new content, else: clear old, keep canvas, and open new content
        // Diff Ref *Diff Type: if from OFF: open canvas, open new content, else: clear old, modify canvas, open new
        // Diff Ref Same Type: modify canvas, close&open new
        // Check the level info from the pen, and update the _refFlag
        switch (newRefEvent)
        {
            case RefEventStatus.OFF:
                if (RefFlag != RefEventStatus.OFF)
                {
                    ClearReferenceObjects(RefFlag);
                    // Close (ON -> OFF)
                    StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.CLOSE, RefFlag, refAnimationCounter));
                    RefFlag = RefEventStatus.OFF;
                }
                break;

            case RefEventStatus.META_DATA:
                if (RefFlag != RefEventStatus.META_DATA)
                {
                    if (RefFlag == RefEventStatus.OFF)
                    {
                        StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.OPEN, newRefEvent, refAnimationCounter, normPosX, normPosY));
                    }
                    else
                    {
                        ClearReferenceObjects(RefFlag);

                        if (shouldUpdate)
                            StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.MODIFY, newRefEvent, refAnimationCounter, normPosX, normPosY));
                    }
                    
                    StartCoroutine(ReferenceMetaDataFade(ChildObjFadeStatus.OPEN));
                }
                else if (shouldUpdate)
                {
                    // level didn't change, but contents changed
                    StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.MODIFY, newRefEvent, refAnimationCounter, normPosX, normPosY));
                    StartCoroutine(ReferenceMetaDataFade(ChildObjFadeStatus.CLOSE_AND_OPEN));
                }
                RefFlag = RefEventStatus.META_DATA;
                break;

            case RefEventStatus.PREVIEW:
                if (RefFlag != RefEventStatus.PREVIEW)
                {
                    if (RefFlag == RefEventStatus.OFF)
                    {
                        StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.OPEN, newRefEvent, refAnimationCounter, normPosX, normPosY));
                    }
                    else
                    {
                        ClearReferenceObjects(RefFlag);
                        if (shouldUpdate)
                            StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.MODIFY, newRefEvent, refAnimationCounter, normPosX, normPosY));
                    }
                    
                    StartCoroutine(ReferencePreviewFade(ChildObjFadeStatus.OPEN));
                }
                else if (shouldUpdate)
                {
                    // level didn't change, but contents changed
                    StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.MODIFY, newRefEvent, refAnimationCounter, normPosX, normPosY));
                    StartCoroutine(ReferencePreviewFade(ChildObjFadeStatus.CLOSE_AND_OPEN));
                }
                RefFlag = RefEventStatus.PREVIEW;
                break;

            case RefEventStatus.FULL_PAPER:
                if (RefFlag != RefEventStatus.FULL_PAPER)
                {
                    if (RefFlag == RefEventStatus.OFF)
                    {
                        StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.OPEN, newRefEvent, refAnimationCounter, normPosX, normPosY));
                    }
                    else
                    {
                        ClearReferenceObjects(RefFlag);
                        if (shouldUpdate)
                            StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.MODIFY, newRefEvent, refAnimationCounter, normPosX, normPosY));
                    }
                    
                    StartCoroutine(ReferenceFullPaperFade(ChildObjFadeStatus.OPEN));
                }
                else if (shouldUpdate)
                {
                    // level didn't change, but contents changed
                    StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.MODIFY, newRefEvent, refAnimationCounter, normPosX, normPosY));
                    StartCoroutine(ReferenceFullPaperFade(ChildObjFadeStatus.CLOSE_AND_OPEN));
                }
                RefFlag = RefEventStatus.FULL_PAPER;
                break;
            case RefEventStatus.VIDEO:
                if (RefFlag != RefEventStatus.VIDEO)
                {
                    if (RefFlag == RefEventStatus.OFF)
                    {
                        StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.OPEN, newRefEvent, refAnimationCounter, normPosX, normPosY));
                    }
                    else
                    {
                        ClearReferenceObjects(RefFlag);
                        if (shouldUpdate)
                            StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.MODIFY, newRefEvent, refAnimationCounter, normPosX, normPosY));
                    }
                    
                    if (refDoc != null && refDoc.videoLink.Length > 0)
                    {
                        StartCoroutine(PageOrg.VideoFade(VideoObj, ChildObjFadeStatus.OPEN, refDoc.videoLink));
                    }
                }
                else if (shouldUpdate)
                {
                    if (refDoc != null && refDoc.videoLink.Length > 0)
                    {
                        StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.MODIFY, newRefEvent, refAnimationCounter, normPosX, normPosY));
                        StartCoroutine(PageOrg.VideoFade(VideoObj, ChildObjFadeStatus.CLOSE_AND_OPEN, refDoc.videoLink));
                    }
                }
                RefFlag = RefEventStatus.VIDEO;
                break;
            default:
                // negative values
                Debug.Log("UpdateRef: negative values: " + newRefEvent);
                break;
        }
    }

    private int UpdateRefDocObjects(HorizontalLayoutGroup parentGrid, List<GameObject> goList)
    {
        PreloadRefPages(refFileName);
        List<Sprite> refSprites = refPreloadSprites[refFileName];
        foreach (Sprite tempSprite in refSprites)
        {
            GameObject tempObj = Instantiate(Resources.Load("RefDocument")) as GameObject;
            Image tempImg = tempObj.GetComponent<Image>();
            tempImg.sprite = tempSprite;
            tempObj.transform.SetParent(parentGrid.transform);
            tempObj.transform.localScale = Vector3.one;
            tempObj.transform.localPosition = Vector3.zero;
            tempObj.transform.localRotation = parentGrid.transform.localRotation;
            goList.Add(tempObj);
        }
        return fullPaperPages.Count;
    }

    public void CloseRefDocument()
    {
        if (RefFlag == RefEventStatus.FULL_PAPER)
        {
            // Close the full paper
            refAnimationCounter++;
            StartCoroutine(ReferenceCanvasFade(CanvasFadeStatus.CLOSE, RefFlag, refAnimationCounter));
            ClearReferenceObjects(RefFlag);
            RefFlag = RefEventStatus.OFF;
            GazeGestureManager.Instance.Transition(GazeGestureManager.Instance.NavigationRecognizer);
            Debug.Log("Fullpaper closed by gesture.");
        }
    }

    public void MoveRefDocument(Vector3 direction)
    {
        if (RefFlag == RefEventStatus.FULL_PAPER)
        {
            //Vector3 moveVector = Vector3.zero;
            //moveVector = direction - prevManipulationPosition;
            //prevManipulationPosition = direction;

            FullPaperObj.transform.position += direction;// new Vector3(moveVector.x, moveVector.y, 0);
            //float totalWidth = ContextDocWidth * _contextObjects.Count;
            //// Calculate maximum movement.
            //float currLocalX = canvasContextContent.transform.localPosition.x;
            //// Handle edge case
            //if ((currLocalX < totalWidth / 2 && direction.x > 0) || (currLocalX > -totalWidth / 2 && direction.x < 0))
            //{
            //    canvasContextContent.transform.Translate(direction, Space.Self);
            //    //Debug.Log(canvasContextContent.transform.localPosition.x);
            //}
        }
    }
    #endregion

    #region Private helpers
    /// <summary>
    /// Clear the reference objects based on current flag
    /// </summary>
    /// <param name="flag"></param>
    private void ClearReferenceObjects(RefEventStatus flag)
    {
        switch (flag)
        {
            case RefEventStatus.OFF:
                break;
            case RefEventStatus.META_DATA:
                // Hide the title
                StartCoroutine(ReferenceMetaDataFade(ChildObjFadeStatus.CLOSE));
                break;
            case RefEventStatus.PREVIEW:
                // Hide the reference. Flag: ON -> OFF.
                StartCoroutine(ReferencePreviewFade(ChildObjFadeStatus.CLOSE));
                break;
            case RefEventStatus.FULL_PAPER:
                // Hide the full paper.
                StartCoroutine(ReferenceFullPaperFade(ChildObjFadeStatus.CLOSE));
                break;
            case RefEventStatus.VIDEO:
                // Stop the video.
                StartCoroutine(PageOrg.VideoFade(VideoObj, ChildObjFadeStatus.CLOSE));
                break;
            default:
                Debug.Log("unhandled levels:" + RefFlag);
                break;
        }
    }

    /// <summary>
    /// search the refname from local dict or resources
    /// </summary>
    /// <param name="refName"></param>
    /// <returns>number of pages found</returns>
    private int PreloadRefPages(string refName)
    {
        if (refPreloadSprites.ContainsKey(refName))
        {
            return refPreloadSprites[refName].Count;
        }
        else
        {
            int pageID = 1;
            Sprite tempDocSprite;
            var newSprites = new List<Sprite>();
            while (true)
            {
                tempDocSprite = PageOrg.FindSprite(string.Format("{0}/{1}-{2}", refName, refName, pageID.ToString("D2")));
                if (tempDocSprite == null)
                {
                    tempDocSprite = PageOrg.FindSprite(string.Format("{0}/{1}-{2}", refName, refName, pageID));
                }

                if (tempDocSprite == null)
                {
                    break;
                }
                else
                {
                    newSprites.Add(tempDocSprite);
                    pageID++;
                }
            }
            if (newSprites.Count == 0)
            {
                // Not full papers available, then just use the front page.
                if (tempDocSprite = PageOrg.FindSprite(refFileName))
                {
                    newSprites.Add(tempDocSprite);
                }

            }
            refPreloadSprites.Add(refName, newSprites);
            return newSprites.Count;
        }
    }

    #endregion

}
