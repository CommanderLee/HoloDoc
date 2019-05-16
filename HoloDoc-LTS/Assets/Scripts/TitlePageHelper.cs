using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PageConstants;

public class TitlePageHelper : MonoBehaviour {

    #region Unity Game Objects for Title Events

    [Tooltip("Displaying title's meta-data")]
    public GameObject MetaDataObj;
    public Text TextMetaDataTitle;
    public Text TextMetaDataAuthors;
    public Text TextMetaDataSource;

    [Tooltip("Title tags manager")]
    public GameObject TagsObj;
    [Tooltip("Figure/Table thumbnails holder")]
    public GameObject ThumbObj;
    [Tooltip("The video player obj for title events (using YouTube API)")]
    public GameObject VideoObj;

    public PieMenu PieMenu;
    public PageOrganizer PageOrg;
    #endregion

    #region Constant values
    private const float CANVAS_OPEN_TIME = 1.0f;
    private const float CANVAS_CLOSE_TIME = 1.0f;

    private readonly Vector3 TAG_SCALE = new Vector3(0.01f, 0.01f, 0.01f);
    private const int TAG_ENTER_SIZE = 9;
    private const int TAG_EXIT_SIZE = 7;

    private readonly Vector2 THUMBNAIL_CELL_SIZE = new Vector2(20, 12);
    #endregion

    #region UI-related private objects

    private GridLayoutGroup tagsGrid;
    private List<GameObject> tagsList = new List<GameObject>();
    private List<GameObject> tagResultObjList = new List<GameObject>();
    private GridLayoutGroup thumbGrid;
    private List<GameObject> thumbList = new List<GameObject>();

    private CanvasGroup bkgMetaData;
    private CanvasGroup bkgTags;
    private CanvasGroup bkgThumb;
    #endregion

    #region Class status attributes
    //private PageOrganizer pageOrg;
    private TitleEventStatus titleFlag = TitleEventStatus.OFF;
    private string currClickedTag = "";
    #endregion

    #region Helper Reference
    /*
    * Tag system index: tag string -> doc list; tag string -> color; doc string -> tag list; color -> tag string
    */
    private Dictionary<string, List<string>> Tag2Docs = new Dictionary<string, List<string>>();
    private Dictionary<string, Color> Tag2Color = new Dictionary<string, Color>();
    private Dictionary<string, List<string>> Doc2Tags = new Dictionary<string, List<string>>();
    private Dictionary<Color, string> Color2Tag = new Dictionary<Color, string>();
    #endregion
    // Use this for initialization
    void Start () {
        //pageOrg = gameObject.transform.parent.GetComponent<PageOrganizer>();

        //PieMenu = gameObject.GetComponentInChildren<PieMenu>();
        
        // Title events: Brief Info (Meta-data), Tag, Thumbnails (Figure/Table), and Video.
        bkgMetaData = MetaDataObj.GetComponent<CanvasGroup>();
        bkgMetaData.alpha = 0;

        tagsGrid = TagsObj.GetComponent<GridLayoutGroup>();
        bkgTags = TagsObj.GetComponent<CanvasGroup>();
        bkgTags.alpha = 0;

        // Use the GridView then no need to worry about size and location.
        // I don't know why I had to set the scale and position again, but I tried many times. 
        thumbGrid = ThumbObj.GetComponent<GridLayoutGroup>();
        // By default, make the canvas group invisible, matching the flag status.
        bkgThumb = ThumbObj.GetComponent<CanvasGroup>();
        bkgThumb.alpha = 0;

        VideoObj.SetActive(false);
    }
	
	// Update is called once per frame
	void Update () {
		
	}

    #region Public interfaces
    /// <summary>
    /// 
    /// </summary>
    /// <param name="newDoc"></param>
    /// <param name="normPosX">Normalized position X</param>
    /// <param name="normPosY">Normalized position Y</param>
    /// <param name="newTitleEvent"></param>
    public void UpdateTitleEvent(TitleEventStatus newTitleEvent, string newDoc = "", float normPosX = 0, float normPosY = 0)
    {
        bool updateDoc = (newDoc.Length > 0);

        // Same Doc Diff Type: if from OFF: open canvas, open new content, else: clear old, keep canvas, and open new content
        // Diff Doc Diff Type: if from OFF: open canvas, open new content, else: clear old, modify canvas, open new
        // Diff Doc Same Type: modify canvas, close&open new
        switch (newTitleEvent)
        {
            case TitleEventStatus.OFF:
                if (titleFlag != TitleEventStatus.OFF)
                {
                    ClearTitleEventObjects(titleFlag);
                    StartCoroutine(TitleCanvasFade(CanvasFadeStatus.CLOSE, titleFlag));
                    titleFlag = TitleEventStatus.OFF;
                }
                break;

            case TitleEventStatus.META_DATA:
                if (titleFlag != TitleEventStatus.META_DATA)
                {
                    if (titleFlag == TitleEventStatus.OFF)
                    {
                        StartCoroutine(TitleCanvasFade(CanvasFadeStatus.OPEN, TitleEventStatus.META_DATA, normPosX, normPosY));
                    }
                    else
                    {
                        ClearTitleEventObjects(titleFlag);

                        if (updateDoc)
                            StartCoroutine(TitleCanvasFade(CanvasFadeStatus.MODIFY, TitleEventStatus.META_DATA, normPosX, normPosY));
                    }
                    StartCoroutine(TitleMetaDataFade(ChildObjFadeStatus.OPEN, PageOrg.CurrARDoc));
                }
                else if (updateDoc)
                {
                    // Switch to different content (different location),  flag unchanged
                    StartCoroutine(TitleCanvasFade(CanvasFadeStatus.MODIFY, TitleEventStatus.META_DATA, normPosX, normPosY));
                    StartCoroutine(TitleMetaDataFade(ChildObjFadeStatus.CLOSE_AND_OPEN, PageOrg.CurrARDoc));
                }
                titleFlag = TitleEventStatus.META_DATA;
                break;

            case TitleEventStatus.FIGURES:
                if (titleFlag != TitleEventStatus.FIGURES)
                {
                    if (titleFlag == TitleEventStatus.OFF)
                    {
                        StartCoroutine(TitleCanvasFade(CanvasFadeStatus.OPEN, TitleEventStatus.FIGURES, normPosX, normPosY));
                    }
                    else
                    {
                        ClearTitleEventObjects(titleFlag);

                        if (updateDoc)
                            StartCoroutine(TitleCanvasFade(CanvasFadeStatus.MODIFY, TitleEventStatus.FIGURES, normPosX, normPosY));
                    }
                    
                    // Turn it ON
                    if (thumbList.Count > 0 && thumbList[0].name.Contains(PageOrg.CurrDocStr))
                    {
                        // Exists, and is the same, then just resume.
                        StartCoroutine(ThumbnailFade(ChildObjFadeStatus.OPEN, normPosX, normPosY));
                    }
                    else
                    {
                        // Not loaded yet, then load now (to the _thumbObjects dict)
                        LoadNewThumbnails(PageOrg.CurrDocStr);
                        // Open the canvas
                        StartCoroutine(ThumbnailFade(ChildObjFadeStatus.OPEN, normPosX, normPosY));
                    }
                }
                else if (updateDoc)
                {
                    // Different: destroy old ones and create new ones, flag unchanged
                    StartCoroutine(TitleCanvasFade(CanvasFadeStatus.MODIFY, TitleEventStatus.FIGURES, normPosX, normPosY));
                    StartCoroutine(ThumbnailFade(ChildObjFadeStatus.CLOSE_AND_OPEN, normPosX, normPosY));
                }
                titleFlag = TitleEventStatus.FIGURES;
                break;
            case TitleEventStatus.TAGS:
                if (titleFlag != TitleEventStatus.TAGS)
                {
                    // Close the old one, and then open the tags
                    
                    if (titleFlag == TitleEventStatus.OFF)
                    {
                        StartCoroutine(TitleCanvasFade(CanvasFadeStatus.OPEN, TitleEventStatus.TAGS, normPosX, normPosY));
                    }
                    else
                    {
                        ClearTitleEventObjects(titleFlag);

                        if (updateDoc)
                            StartCoroutine(TitleCanvasFade(CanvasFadeStatus.MODIFY, TitleEventStatus.TAGS, normPosX, normPosY));
                    }
                    
                    // Turn it ON
                    if (tagsList.Count > 0 && tagsList[0].name.Contains(PageOrg.CurrDocStr))
                    {
                        // Exists, and is the same, then just resume.
                        StartCoroutine(TitleTagsFade(ChildObjFadeStatus.OPEN));
                    }
                    else
                    {
                        // Not loaded yet, then load now
                        LoadTags(PageOrg.CurrDocStr);
                        // Start the animation
                        StartCoroutine(TitleTagsFade(ChildObjFadeStatus.OPEN));
                    }
                }
                else if (updateDoc)
                {
                    // Different: destroy old ones and create new ones, flag unchanged
                    StartCoroutine(TitleCanvasFade(CanvasFadeStatus.MODIFY, TitleEventStatus.TAGS, normPosX, normPosY));
                    StartCoroutine(TitleTagsFade(ChildObjFadeStatus.CLOSE_AND_OPEN));
                }
                titleFlag = TitleEventStatus.TAGS;
                break;
            case TitleEventStatus.VIDEO:
                if (titleFlag != TitleEventStatus.VIDEO)
                {
                    if (titleFlag == TitleEventStatus.OFF)
                    {
                        StartCoroutine(TitleCanvasFade(CanvasFadeStatus.OPEN, TitleEventStatus.VIDEO, normPosX, normPosY));
                    }
                    else
                    {
                        ClearTitleEventObjects(titleFlag);
                        if (updateDoc)
                            StartCoroutine(TitleCanvasFade(CanvasFadeStatus.MODIFY, TitleEventStatus.VIDEO, normPosX, normPosY));
                    }
                    
                    if (PageOrg.CurrARDoc != null && PageOrg.CurrARDoc.videoLink.Length > 0)
                    {
                        StartCoroutine(PageOrg.VideoFade(VideoObj, ChildObjFadeStatus.OPEN, PageOrg.CurrARDoc.videoLink));
                    }
                }
                else if (updateDoc)
                {
                    // Play video for anothe document. 
                    if (PageOrg.CurrARDoc != null && PageOrg.CurrARDoc.videoLink.Length > 0)
                    {
                        StartCoroutine(TitleCanvasFade(CanvasFadeStatus.MODIFY, TitleEventStatus.VIDEO, normPosX, normPosY));
                        StartCoroutine(PageOrg.VideoFade(VideoObj, ChildObjFadeStatus.CLOSE_AND_OPEN, PageOrg.CurrARDoc.videoLink));
                    }
                }
                titleFlag = TitleEventStatus.VIDEO;
                break;
            default:
                Debug.Log("UpdateTitleEvent: Unrecognized event id " + newTitleEvent);
                break;
        }
    }

    /// <summary>
    /// Check if target document has tags.
    /// Default: check current doc.
    /// </summary>
    /// <param name="docName"></param>
    /// <returns></returns>
    public bool CheckTags(string docName = "")
    {
        if (docName.Length == 0)
            docName = PageOrg.CurrDocStr;

        if (PageOrganizer.DictDocuments.ContainsKey(docName))
        {
            PreloadTags(docName);
            return Doc2Tags[docName].Count > 0;
        }
        else
        {
            Debug.Log("CheckTags Error: docName not in dictionary." + docName);
            return false;
        }
    }

    public void PreloadTags(string newDoc)
    {
        // Not yet prepared
        if (!Doc2Tags.ContainsKey(newDoc) && PageOrganizer.DictDocuments.ContainsKey(newDoc))
        {
            ARDocument newARDoc = PageOrganizer.DictDocuments[newDoc];
            Doc2Tags.Add(newDoc, new List<string>());
            // Add all authors
            foreach (string author in newARDoc.authors)
            {
                AddNewTag(author, newDoc);
            }
            // Add source and year
            AddNewTag(newARDoc.source, newDoc);
            AddNewTag(newARDoc.year.ToString(), newDoc);
        }
    }

    public void TagPointerEnter(GameObject tagObj)
    {
        Debug.Log("Enter Tag:" + tagObj.name);
        tagObj.GetComponentInChildren<Text>().fontSize = TAG_ENTER_SIZE;
    }

    public void TagPointerExit(GameObject tagObj)
    {
        Debug.Log("Exit Tag:" + tagObj.name);
        tagObj.GetComponentInChildren<Text>().fontSize = TAG_EXIT_SIZE;
    }

    public void TagPointerClick(GameObject tagObj)
    {
        string tagName = tagObj.GetComponentInChildren<Text>().text;
        Debug.Log("Click on Tag:" + tagName);
        if (tagName != currClickedTag)
        {
            // Clear old ones and open new one
            foreach (var obj in tagResultObjList)
                Destroy(obj);
            tagResultObjList.Clear();

            if (Tag2Docs.ContainsKey(tagName))
            {
                var docs = Tag2Docs[tagName];
                Debug.Log("Tag2Docs:" + string.Join(", ", docs));
                foreach (var doc in docs)
                {
                    // No need to create for current document
                    if (doc != PageOrg.CurrDocStr && PageOrganizer.DictDocuments.ContainsKey(doc))
                    {
                        ARDocument currDoc = PageOrganizer.DictDocuments[doc];
                        GameObject tagResultObj = Instantiate(Resources.Load("LocalSearchResult")) as GameObject;
                        tagResultObj.SetActive(true);
                        var tagResultHandler = tagResultObj.GetComponent<LocalSearchResultCanvas>();
                        tagResultHandler.InitTagContent(currDoc, tagName, PageOrg.hololensCamera);
                        tagResultObjList.Add(tagResultObj);
                    }
                    else if (doc != PageOrg.CurrDocStr)
                        Debug.Log("Tag Clicked: cannot find target document from DictDocuments:" + doc);
                }
                currClickedTag = tagName;
            }
            else
                Debug.Log("Tag Clicked: cannot find target tag from Tag2Docs: " + tagName);
        }
    }

    #endregion

    #region Animations (IEnumerator)
    /// <summary>
    /// Animtate the position and scale of the root canvas of title event objects
    /// </summary>
    /// <param name="status"></param>
    /// <param name="newTitleStatus"></param>
    /// <returns></returns>
    private IEnumerator TitleCanvasFade(CanvasFadeStatus status, TitleEventStatus newTitleStatus, float posX = 0, float posY = 0)
    {
        if (status == CanvasFadeStatus.CLOSE)
        {
            for (float i = CANVAS_CLOSE_TIME; i >= 0; i -= Time.deltaTime)
            {
                float percentage = i / CANVAS_CLOSE_TIME;
                // Cell gets bigger and bigger, until it finally appears.
                gameObject.transform.localScale = Vector3.one * percentage;
                yield return null;
            }
        }
        else if (status == CanvasFadeStatus.OPEN)
        {
            // For the x-rotation, it is restricted to 0 (vertical) to 90 (flat horizontal). For the y-axis, it is free (user turns head 360 degrees in round desk).
            // And z-rotation is set to 0 so its text is parallel to ground.
            Vector3 targetVec = PageOrg.CalcTargetEulerAngles();
            gameObject.transform.localEulerAngles = targetVec;

            // Update: since the pie menu center is anchored to center, we just need to move a little bit to save space for titles.
            gameObject.transform.position = PageOrg.CurrDocObj.transform.position;
            // Move along top (negative y-axis of AR tag, DOWN)
            gameObject.transform.Translate(-PageOrg.CurrDocObj.transform.up * (0.06f * Mathf.Sin(targetVec.x * Mathf.Deg2Rad)), Space.World);

            //gameObject.transform.Translate(new Vector3(0, 0.06f * Mathf.Sin(targetVec.x * Mathf.Deg2Rad), 0.06f * Mathf.Cos(targetVec.x * Mathf.Deg2Rad)), Space.World);

            Vector3 moveUp = new Vector3(0, 0.06f * Mathf.Cos(targetVec.x * Mathf.Deg2Rad), 0);

            for (float i = 0; i <= CANVAS_OPEN_TIME; i += Time.deltaTime)
            {
                float percentage = i / CANVAS_OPEN_TIME;
                // Cell gets bigger and bigger, until it finally appears.
                gameObject.transform.Translate(moveUp * Time.deltaTime / CANVAS_OPEN_TIME, Space.World);
                gameObject.transform.localScale = Vector3.one * percentage;
                yield return null;
            }
            Debug.Log("New Opened Title euler angles:" + gameObject.transform.localEulerAngles);
        }
        else if (status == CanvasFadeStatus.MODIFY)
        {
            Vector3 targetVec = PageOrg.CalcTargetEulerAngles();
            gameObject.transform.localEulerAngles = targetVec;

            Vector3 oldPos = gameObject.transform.position;
            gameObject.transform.position = PageOrg.CurrDocObj.transform.position;
            // Move along top (negative y-axis of AR tag, DOWN)
            gameObject.transform.Translate(-PageOrg.CurrDocObj.transform.up * (0.06f * Mathf.Sin(targetVec.x * Mathf.Deg2Rad)), Space.World);
            // Move up (world)
            gameObject.transform.Translate(0, 0.06f * Mathf.Cos(targetVec.x * Mathf.Deg2Rad), 0, Space.World);
            Vector3 posDist = gameObject.transform.position - oldPos;

            for (float i = 0; i <= CANVAS_OPEN_TIME; i += Time.deltaTime)
            {
                float percentage = i / CANVAS_OPEN_TIME;
                //gameObject.transform.localEulerAngles += anglePerSec * Time.deltaTime;
                gameObject.transform.position = oldPos + posDist * percentage;
                yield return null;
            }
            Debug.Log("Modified Title euler angles:" + gameObject.transform.localEulerAngles);
        }
    }

    /// <summary>
    /// The title meta-data (brief info)'s position / size is controled by the parent canvas.
    /// In this animator, only transparency is controlled. 
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    private IEnumerator TitleMetaDataFade(ChildObjFadeStatus status, ARDocument currDoc = null)
    {
        if (status == ChildObjFadeStatus.CLOSE || status == ChildObjFadeStatus.CLOSE_AND_OPEN)
        {
            for (float i = CANVAS_CLOSE_TIME; i >= 0; i -= Time.deltaTime)
            {
                float percentage = i / CANVAS_CLOSE_TIME;
                bkgMetaData.alpha = percentage;
                yield return null;
            }
            MetaDataObj.SetActive(false);
        }

        if (status == ChildObjFadeStatus.OPEN || status == ChildObjFadeStatus.CLOSE_AND_OPEN)
        {
            MetaDataObj.SetActive(true);
            if (currDoc != null)
            {
                TextMetaDataTitle.text = currDoc.title;
                TextMetaDataAuthors.text = string.Join(", ", currDoc.authors);
                TextMetaDataSource.text = string.Format("{0} - {1}", currDoc.year, currDoc.source);
            }
            else
            {
                Debug.LogError("Title Fade Error: Fail to update Title Meta-data");
            }

            //titleBriefText.text = sourceText;
            for (float i = 0; i <= CANVAS_OPEN_TIME; i += Time.deltaTime)
            {
                float percentage = i / CANVAS_OPEN_TIME;
                bkgMetaData.alpha = percentage;
                yield return null;
            }
        }
    }

    /// <summary>
    /// The title tags' position / size is controled by the parent canvas.
    /// In this animator, only transparency is controlled. 
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    private IEnumerator TitleTagsFade(ChildObjFadeStatus status)
    {
        if (status == ChildObjFadeStatus.CLOSE || status == ChildObjFadeStatus.CLOSE_AND_OPEN)
        {
            for (float i = CANVAS_CLOSE_TIME; i >= 0; i -= Time.deltaTime)
            {
                float percentage = i / CANVAS_CLOSE_TIME;
                bkgTags.alpha = percentage;
                //titleTagsObj.transform.localScale = TAG_SCALE * percentage;
                yield return null;
            }
            TagsObj.SetActive(false);
            currClickedTag = "";
            foreach (var obj in tagResultObjList)
                Destroy(obj);
            tagResultObjList.Clear();
        }

        if (status == ChildObjFadeStatus.CLOSE_AND_OPEN)
        {
            // Prepare for the new document
            LoadTags(PageOrg.CurrDocStr);
        }

        if (status == ChildObjFadeStatus.OPEN || status == ChildObjFadeStatus.CLOSE_AND_OPEN)
        {
            TagsObj.SetActive(true);
            for (float i = 0; i <= CANVAS_OPEN_TIME; i += Time.deltaTime)
            {
                float percentage = i / CANVAS_OPEN_TIME;
                bkgTags.alpha = percentage;
                //titleTagsObj.transform.localScale = TAG_SCALE * percentage;
                yield return null;
            }
        }
    }

    /// <summary>
    /// Controls the transparency change of the contents.
    /// </summary>
    /// <param name="status"></param>
    /// <param name="posX"></param>
    /// <param name="posY"></param>
    /// <returns></returns>
    private IEnumerator ThumbnailFade(ChildObjFadeStatus status, float posX = 0, float posY = 0)
    {
        // Close the thumbnails, or open them, or close-and-open.
        if (status == ChildObjFadeStatus.CLOSE || status == ChildObjFadeStatus.CLOSE_AND_OPEN)
        {
            for (float i = CANVAS_CLOSE_TIME; i >= 0; i -= Time.deltaTime)
            {
                float percentage = i / CANVAS_CLOSE_TIME;
                // Cell gets smaller and smaller, until it disappears.
                //titleThumbnailGrid.cellSize = THUMBNAIL_CELL_SIZE * percentage;
                bkgThumb.alpha = percentage;
                yield return null;
            }
            bkgThumb.alpha = 0;
        }

        // Need to destroy and create before opening it
        if (status == ChildObjFadeStatus.CLOSE_AND_OPEN)
        {
            LoadNewThumbnails(PageOrg.CurrDocStr);
        }

        if (status == ChildObjFadeStatus.OPEN || status == ChildObjFadeStatus.CLOSE_AND_OPEN)
        {
            // No need to adpat for thumbnail objects; this depends on the canvas size.(100 x 50 here)
            //float figuresHeight = 0.5f; // = 0.155f * Mathf.CeilToInt(_thumbObjects.Count / 5.0f);

            // Observe the xyz direction of marker and thumbnail canvas. 
            // Rotate based on view point
            //float cameraEularX = hololensCamera.transform.localEulerAngles.x;
            //float targetEularX = Mathf.Max(5, Mathf.Min(85, cameraEularX));
            //float targetRad = targetEularX * Mathf.Deg2Rad;

            // Starting position: top-center. Here we are using the ARTag local coordinate
            //canvasThumbObj.transform.position = CurrDocObj.transform.position;
            //canvasThumbObj.transform.Translate(new Vector3(posX * PAPER_WIDTH - 0.02f, posY * PAPER_HEIGHT - 0.02f - figuresHeight * 0.5f * Mathf.Sin(targetRad), 0), CurrDocObj.transform);

            //canvasThumbObj.transform.localEulerAngles = new Vector3(targetEularX, hololensCamera.transform.localEulerAngles.y, 0);

            // Reset the children's angle
            foreach (GameObject go in thumbList)
            {
                go.transform.localEulerAngles = Vector3.zero;
            }
            // Set moving direction (world coordinate)
            //Vector3 targetDirection = new Vector3(0, Mathf.Cos(targetRad), 0) * figuresHeight * 0.5f;
            //Debug.Log("[Thumb] initial position:" + canvasThumbObj.transform.position + "figure dimension:" + canvasThumbObj.GetComponent<RectTransform>().sizeDelta);
            //Debug.Log("[Thumb] initial rotation:" + canvasThumbObj.transform.rotation);
            // Loop over one second
            for (float i = 0; i <= CANVAS_OPEN_TIME; i += Time.deltaTime)
            {
                float percentage = i / CANVAS_OPEN_TIME;
                // Cell gets bigger and bigger, until it finally appears.
                //titleThumbnailGrid.cellSize = THUMBNAIL_CELL_SIZE * percentage;
                bkgThumb.alpha = percentage;
                //canvasThumbObj.transform.Translate(targetDirection * Time.deltaTime / CANVAS_OPEN_TIME, Space.World);
                yield return null;
            }
            Debug.Log("[Thumb] current position:" + ThumbObj.transform.position);
            bkgThumb.alpha = 1;
        }
    }
    #endregion

    #region private helper functions.
    private void ClearTitleEventObjects(TitleEventStatus flag)
    {
        switch (flag)
        {
            case TitleEventStatus.OFF:
                break;
            case TitleEventStatus.META_DATA:
                StartCoroutine(TitleMetaDataFade(ChildObjFadeStatus.CLOSE));
                break;
            case TitleEventStatus.FIGURES:
                StartCoroutine(ThumbnailFade(ChildObjFadeStatus.CLOSE));
                break;
            case TitleEventStatus.TAGS:
                StartCoroutine(TitleTagsFade(ChildObjFadeStatus.CLOSE));
                break;
            case TitleEventStatus.VIDEO:
                StartCoroutine(PageOrg.VideoFade(VideoObj, ChildObjFadeStatus.CLOSE));
                break;
            default:
                Debug.Log("[TitleEvent] unhandled levels:" + flag);
                break;
        }
    }

    private void AddNewTag(string tag, string newDoc)
    {
        // Sometimes even for a new document, it may contains the existing tag
        if (!Tag2Color.ContainsKey(tag))
        {
            // Get a unique color: random hue, very saturated, a little lighter, fixed alpha
            Color color = Random.ColorHSV(0f, 1f, 0.9f, 1f, 0.5f, 1f, 0.7f, 0.7f);
            while (Color2Tag.ContainsKey(color))
                color = Random.ColorHSV(0f, 1f, 0.9f, 1f, 0.5f, 1f, 0.7f, 0.7f);

            Color2Tag.Add(color, tag);
            Tag2Color.Add(tag, color);
        }

        if (!Tag2Docs.ContainsKey(tag))
        {
            Tag2Docs.Add(tag, new List<string>());
        }
        Tag2Docs[tag].Add(newDoc);

        Doc2Tags[newDoc].Add(tag);
    }

    /// <summary>
    /// Load tags for the target document (usually the current one)
    /// </summary>
    /// <param name="docName"></param>
    private void LoadTags(string docName)
    {
        // Destroy the old ones
        foreach (GameObject go in tagsList)
        {
            Destroy(go);
        }
        tagsList.Clear();

        if (Doc2Tags.ContainsKey(docName) && Doc2Tags[docName].Count > 0)
        {
            // Create new ones. 
            foreach (string tag in Doc2Tags[docName])
            {
                GameObject tempTag = Instantiate(Resources.Load("PanelTag")) as GameObject;
                tempTag.name = string.Format("PanelTag-{0}-{1}", docName, tagsList.Count);
                tempTag.GetComponent<Image>().color = Tag2Color[tag];
                tempTag.GetComponentInChildren<Text>().text = tag;

                tempTag.transform.SetParent(tagsGrid.transform);
                tempTag.transform.localScale = Vector3.one;
                tempTag.transform.localPosition = Vector3.zero;
                tempTag.transform.localRotation = tagsGrid.transform.localRotation;
                tagsList.Add(tempTag);
            }
            Debug.Log(string.Format("Load {0} tags of {1}", tagsList.Count, docName));
        }
    }

    private bool LoadNewThumbnails(string docName)
    {
        // Destroy the old ones
        foreach (GameObject go in thumbList)
        {
            Destroy(go);
        }
        thumbList.Clear();

        // Create new ones. TODO: what if nothing is in there?
        int figID = 1;
        Sprite tempTestSpriteID;
        while (tempTestSpriteID = Resources.Load<Sprite>(string.Format("{0}/Fig{1}", docName, figID)))
        {
            GameObject tempObjID = new GameObject(string.Format("{0}/Fig{1}", docName, figID));
            Image tempImgID = tempObjID.AddComponent<Image>();
            tempImgID.sprite = tempTestSpriteID;
            tempImgID.preserveAspect = true;

            tempObjID.transform.SetParent(thumbGrid.transform);
            tempObjID.transform.localScale = Vector3.one;
            tempObjID.transform.localPosition = Vector3.zero;
            tempObjID.transform.localRotation = thumbGrid.transform.localRotation;
            thumbList.Add(tempObjID);
            figID++;
        }
        Debug.Log(string.Format("Load {0} sprites from {1}", figID - 1, PageOrg.CurrDocStr));
        return thumbList.Count > 0;
    }
    #endregion
}
