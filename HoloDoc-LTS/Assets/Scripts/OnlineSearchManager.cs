using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using PageConstants;
#if !UNITY_EDITOR
using Windows.Storage;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
#endif

public class OnlineSearchManager : MonoBehaviour {

    #region Unity UI Objects
    public Text SearchWindowTitle;
    public Text SearchContent;
    public Button BtnDoubleLeft;
    public Button BtnLeft;
    public Button [] BtnPages;
    public Button BtnRight;
    public Button BtnDoubleRight;
    public VerticalLayoutGroup SearchResults;
    public GameObject HololensCamera;
    public GameObject RootObject;
    public GameObject PDFPlugin;
    public Text PDFPluginHint;
    #endregion

    #region Const UI Design values
    /// <summary>
    /// Results per page (UI).
    /// </summary>
    private const int RESULTS_PER_PAGE = 5;
    /// <summary>
    /// Number of pages by default on the first request.
    /// </summary>
    private const int INIT_PAGES = 3;
    /// <summary>
    /// Number of results needed for the first request.
    /// </summary>
    private const int INIT_RESULTS = RESULTS_PER_PAGE * INIT_PAGES;

    private const int PAGE_ID_LEFT = 0;
    private const int PAGE_ID_MID = 1;
    private const int PAGE_ID_RIGHT = 2;

    //private const float SEARCH_WINDOW_SCALE = 0.005f;
    private const float SEARCH_WINDOW_FADE_TIME = 1f;
    private enum PARSING_STATUS { IDLE, WAITING, PENDING, DONE };

    private const float LOCAL_RESULT_SCALE_MIN = 0.5f;
    private const float LOCAL_RESULT_SCALE_MAX = 1.5f;
    #endregion

    #region Private attributes for current status
    private SearchType currSearchType;
    /// <summary>
    /// A boolean indicates whether the window is fading (open/close animation).
    /// </summary>
    private bool isWindowFading = false;

    /// <summary>
    /// A boolean indicates whether the program is waiting for the search results.
    /// </summary>
    private bool isWaitingResults = true;
    private CanvasGroup visController;

    /// <summary>
    /// This attribute corresponds to the ARDocument List. If the searched word changes, then the list doesn't valid any more. 
    /// </summary>
    private string onlineSearchContent = "";
    private string localSearchContent = "";
    /// <summary>
    /// The target page we wanted to search, determined at the call to helper.
    /// </summary>
    private int currTargetPage = 0;

    // For PageID, PageOnUI, and MaxPage, we store the separate values for online/local window.
    // We just use currXXXX attributes. Because they are up to date. When we close the window, we sync them.
    /// <summary>
    /// The current search result page ID, ranging from 0-2 based on the BtnPages.length.
    /// </summary>
    private int onlinePageID = 0;
    /// <summary>
    /// The current search result page on UI (start from 1).
    /// </summary>
    private int onlinePageOnUI = 1;
    /// <summary>
    /// The maximum page on UI, determined by the result of academic search. 
    /// </summary>
    private int onlineMaxPage = -1;
    
    private int localPageID = 0;
    private int localPageOnUI = 1;
    private int localMaxPage = -1;

    private int currPageID = 0;
    private int currPageOnUI = 1;
    private int currMaxPage = -1;

    /// <summary>
    /// All results on current UI (rotating).
    /// </summary>
    //private List<SearchResultEntry> resultEntries = new List<SearchResultEntry>();
    private SearchResultEntry[] resultEntries;
    private List<ARDocument> localResults = new List<ARDocument>();
    /// <summary>
    ///  All results received so far. Some of them might be hidden.
    /// </summary>
    private List<ARDocument> onlineResults = new List<ARDocument>();
    /// <summary>
    /// Refer to the real results. Update when switching context.
    /// </summary>
    private List<ARDocument> currResults = null;

    private MicrosoftAPIHelper currHelper = new MicrosoftAPIHelper();
    
    private PARSING_STATUS parsingStatus = PARSING_STATUS.IDLE;

    private static WordRecords wordRecords = null;

    private List<GameObject> localResultObjList = new List<GameObject>();

    private Paroxe.PdfRenderer.PDFViewer pdfViewer;

    /// <summary>
    /// Store whether there is a target PDF renderring (or should be renderred) in the PDF Viewer.
    /// </summary>
    private bool hasTargetPDF = false;
    #endregion

    // Use this for initialization
    void Start () {
        SearchContent.text = "";
        resultEntries = SearchResults.gameObject.GetComponentsInChildren<SearchResultEntry>();

        ResetPages();

        visController = gameObject.GetComponentInChildren<CanvasGroup>();
        visController.alpha = 0;

        if (PDFPlugin != null)
        {
            pdfViewer = PDFPlugin.GetComponentInChildren<Paroxe.PdfRenderer.PDFViewer>();
            PDFPluginHint.gameObject.SetActive(false);
            PDFPlugin.SetActive(false);
            hasTargetPDF = false;
        }

        // Load only once; haven't been loaded yet
        if (wordRecords == null)
        {
            DecodeRecords("WordRecord.txt");
        }

        //Debug.Log("Self Report:" + gameObject.name);
        StickyManager.AddManager(gameObject);
        // Note: because of this line, the sticky manager fails to find this obj
        gameObject.SetActive(false);
    }
	
	// Update is called once per frame
	void Update () {
#if !UNITY_EDITOR
        // Keep checking the searching Task
        if (currHelper.academicStatus == MicrosoftAPIHelper.SERVICE_STATUS.PENDING)
        {
            //TODO: show animition to avoid confusion.
            // Note: not always. The app may be just pre-loading. 
        }
        else if (currHelper.academicStatus == MicrosoftAPIHelper.SERVICE_STATUS.DONE)
        {
            // Step 2: Call functions to use this result.
            string jsonResult = currHelper.academicResult;
            Debug.Log("Received Academic Search Result, length:" + jsonResult.Length);
            // Debug;
            //SearchContent.text = jsonResult.Length.ToString();
            currHelper.academicStatus = MicrosoftAPIHelper.SERVICE_STATUS.IDLE;
            var task = Task.Run(() => ParseJSONResults(jsonResult));
        }
        else if (currHelper.academicStatus == MicrosoftAPIHelper.SERVICE_STATUS.ERROR)
        {
            SetErrorInfo(currHelper.academicResult);
            currHelper.academicStatus = MicrosoftAPIHelper.SERVICE_STATUS.IDLE;
        }

        // Keep checking the parsing Task
        if (parsingStatus == PARSING_STATUS.DONE)
        {
            Debug.Log("Received parsing results." + currPageOnUI + "," + currTargetPage);
            // Step 3: Render the results on the UI (and cancel the waiting info animation)
            isWaitingResults = false;
            if (currTargetPage == currPageOnUI && currSearchType == SearchType.ONLINE)
            {
                // Need to render. The user is waiting for this page.
                RenderResults();
                UpdateButtonStatus();
            }
            parsingStatus = PARSING_STATUS.IDLE;

            // Continue pre-loading: keep checking
            if (currMaxPage < 0 && currTargetPage < currPageOnUI + 2 && currSearchType == SearchType.ONLINE)
            {
                // New step 1: if we need to cache the next page (up to 2 pages) 
                // and if we can cache the next page (max page not met)
                currTargetPage += 1;
                parsingStatus = PARSING_STATUS.WAITING;
                var task = Task.Run(() => currHelper.AcademicSearch(onlineSearchContent, RESULTS_PER_PAGE, currResults.Count));
            }
        }

#endif
    }

    public bool HasSearched()
    {
        return localSearchContent.Length > 0 || onlineSearchContent.Length > 0;
    }

    // Switch context here.  Check if all online/local variables are stored/changed only here. 
    // Only exception is that while closing the window, they are also saved.
    // In all other places, only use currXXX variables.
    public void StartNewSearch(string content, GameObject root, SearchType sType)
    {
        RootObject = root;
        if (sType == SearchType.LOCAL)
        {
            SearchWindowTitle.text = "Local Search:";
            if (content == localSearchContent || content == "")
            {
                // Nothing changed. Just Toggle. 
                if (sType == currSearchType)
                    ToggleSearchWindow();
                // Switch from Online to Local: Sync Online old variables and load Local cached variables.
                else
                {
                    SwitchUIContext(sType);
                    // Resume
                    foreach (var go in localResultObjList)
                        go.SetActive(true);
                    // Close pdf
                    PDFPlugin.SetActive(false);
                }
            }
            else
            {
                // Start new search anyway
                localSearchContent = content;
                ResetPages();

                localPageID = 0;
                localPageOnUI = 1;
                localMaxPage = -1;
                localResults.Clear();
                
                localResults = ParseLocalDocuments(content);

                foreach (var obj in localResultObjList)
                    Destroy(obj);
                localResultObjList.Clear();

                foreach (var doc in localResults)
                {
                    // Show indicator on real paper
                    GameObject resultObj = Instantiate(Resources.Load("LocalSearchResult")) as GameObject;
                    resultObj.SetActive(true);
                    resultObj.transform.localScale *= Mathf.Min(LOCAL_RESULT_SCALE_MAX, LOCAL_RESULT_SCALE_MIN + 0.5f * Mathf.Log10(float.Parse(doc.extraInfo)));
                    var resultObjHandler = resultObj.GetComponent<LocalSearchResultCanvas>();
                    resultObjHandler.InitContent(doc, content, HololensCamera);
                    localResultObjList.Add(resultObj);
                }

                // Load local variables to curr variables: i.e, set these values to currXXX.
                LoadUIVariables(sType);

                OpenSearchWindow();

                currTargetPage = 1;
                RenderResults();
                UpdateButtonStatus();

                // Close PDF
                PDFPlugin.SetActive(false);
            }
        }
        else if (sType == SearchType.ONLINE)
        {
            SearchWindowTitle.text = "Online Search:";
            if (content == onlineSearchContent || content == "")
            {
                // Nothing changed. Just Toggle. 
                if (sType == currSearchType)
                    ToggleSearchWindow();
                // Switch from Local to Online: Sync Local old variables and load Online new cached variables.
                else
                {
                    SwitchUIContext(sType);
                    if (hasTargetPDF)
                        PDFPlugin.SetActive(true);
                }
            }
            else
            {
                // Start new search anyway
                onlineSearchContent = content;
                ResetPages();

                onlinePageID = 0;
                onlinePageOnUI = 1;
                onlineMaxPage = -1;
                onlineResults.Clear();

                // Load online variables to curr variables: i.e, set these values to currXXX.
                LoadUIVariables(sType);

                OpenSearchWindow();
                
                isWaitingResults = true;
                StartCoroutine(SearchWindowWaiting());

                MuteAllButtons();
#if !UNITY_EDITOR
                // Step 1: Start a new task, calling helper to optain academic search results (string)
                currTargetPage = 1;
                var task = Task.Run(() => currHelper.AcademicSearch(content, RESULTS_PER_PAGE, 0));
#endif
                hasTargetPDF = false;
            }
            // Hide local result objects anyway
            foreach (var obj in localResultObjList)
                obj.SetActive(false);
        }

        // Common UI change (update attributes) (it is important to update them after the code blocks above
        currSearchType = sType;
        if (content != "")
            SearchContent.text = content;
    }

    public void ToggleSearchWindow()
    {
        if (!isWindowFading)
        {
            if (visController.alpha > 0.5f)
                CloseSearchWindow();
            else
                OpenSearchWindow();
        }
    }

    public void StartPDFWindow(string pdfLink)
    {
        Debug.Log(string.Format("OnlineManager {0} received a PDF link: {1}", gameObject.name, pdfLink));
        if (pdfViewer != null)
        {
            PDFPlugin.SetActive(true);
            pdfViewer.LoadDocumentFromWeb(pdfLink, "");
        }
    }

    private void SwitchUIContext(SearchType targetType)
    {
        if (targetType == currSearchType)
        {
            Debug.Log("No need to switch.");
            return;
        }
        // Sync old
        SaveUIVariables();
        // Load to curr variables
        LoadUIVariables(targetType);

        // Set navigation bar
        ResetPages(currPageID, currPageOnUI);

        // Make sure the window is ON
        OpenSearchWindow();

        // Use the cached results and render them on UI
        RenderResults();
        UpdateButtonStatus();
    }

    /// <summary>
    /// Fade in and prepare.
    /// </summary>
    private void OpenSearchWindow()
    {
        if (!isWindowFading && visController.alpha < 0.5f)
        {
            isWindowFading = true;
            gameObject.SetActive(true);
            StartCoroutine(SearchWindowFading(true));
        }
    }

    /// <summary>
    ///  Fade out, save variables, and try to close local result objects.
    /// </summary>
    private void CloseSearchWindow()
    {
        if (!isWindowFading && visController.alpha > 0.5f)
        {
            isWindowFading = true;
            StartCoroutine(SearchWindowFading(false));
            // Sync variables.
            SaveUIVariables();
            foreach (var go in localResultObjList)
                go.SetActive(false);
        }
    }

    private void SaveUIVariables()
    {
        if (currSearchType == SearchType.LOCAL)
        {
            localPageID = currPageID;
            localPageOnUI = currPageOnUI;
            localMaxPage = currMaxPage;
            localResults = currResults;
        }
        else if (currSearchType == SearchType.ONLINE)
        {
            onlinePageID = currPageID;
            onlinePageOnUI = currPageOnUI;
            onlineMaxPage = currMaxPage;
            onlineResults = currResults;
        }
    }

    private void LoadUIVariables(SearchType targetType)
    {
        if (targetType == SearchType.LOCAL)
        {
            currPageID = localPageID;
            currPageOnUI = localPageOnUI;
            currMaxPage = localMaxPage;
            currResults = localResults;
        }
        else if (targetType == SearchType.ONLINE)
        {
            currPageID = onlinePageID;
            currPageOnUI = onlinePageOnUI;
            currMaxPage = onlineMaxPage;
            currResults = onlineResults;
        }
    }

    private IEnumerator SearchWindowFading(bool flag)
    {
        if (flag)
        {
            // Open
            gameObject.transform.position = RootObject.transform.position;
            gameObject.transform.localEulerAngles = new Vector3(HololensCamera.transform.localEulerAngles.x, HololensCamera.transform.localEulerAngles.y, 0);

            for (float i = 0; i <= SEARCH_WINDOW_FADE_TIME; i += Time.deltaTime)
            {
                float percentage = i / SEARCH_WINDOW_FADE_TIME;
                visController.alpha = percentage;
                gameObject.transform.localScale = Vector3.one * percentage;
                yield return null;
            }
            visController.alpha = 1;
        }
        else
        {
            // Close
            for (float i = 0; i <= SEARCH_WINDOW_FADE_TIME; i += Time.deltaTime)
            {
                float percentage = 1 - i / SEARCH_WINDOW_FADE_TIME;
                visController.alpha = percentage;
                gameObject.transform.localScale = Vector3.one * percentage;
                yield return null;
            }
            visController.alpha = 0;
            gameObject.SetActive(false);
        }
        isWindowFading = false;
    }

    private IEnumerator SearchWindowWaiting()
    {
        int dotNumber = 0;
        SearchResultEntry infoEntry = resultEntries[0];
        infoEntry.gameObject.SetActive(true);
        while (isWaitingResults)
        {
            dotNumber += 1;
            if (dotNumber == 10)
                dotNumber = 0;
            string waitingInfo = "Searching." + new string('.', dotNumber);
            infoEntry.SetSimpleContent(waitingInfo);
            yield return new WaitForSeconds(0.2f);
        }
    }

#if !UNITY_EDITOR

    /// <summary>
    /// Helper function: parse the json string and turn it into ARDocument List.
    /// Note: this is slow, and the caller needs to put it into a separate Task.
    /// </summary>
    /// <param name="jsonResult"></param>
    /// <returns></returns>
    //private IEnumerator ParseJSONResults(string jsonResult)
    public void ParseJSONResults(string jsonResult)
    {
        parsingStatus = PARSING_STATUS.PENDING;

        List<ARDocument> parsedResults = new List<ARDocument>();
        //academicResult.Clear();

        // Parse to output the result.
        var result = JsonConvert.DeserializeObject<JSONAcademic.RootObject>(jsonResult);
        int counter = 0;

        string title = "";
        var authorNames = new List<string>();
        string majorLastName = "";
        int year = 0;
        string source = "";
        string pdfUrl = "";
        //yield return null;

        foreach (var entity in result.entities)
        {
            //Debug.Log(string.Format("----------{0}----------", counter));
            var dict = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(entity.E);
            //yield return null;

            // Names in "E" is well-organized. But they are not guranteed. 
            title = "";
            authorNames.Clear();
            majorLastName = "";
            year = entity.Y;
            source = "";
            pdfUrl = "";

            if (dict.ContainsKey("DN"))
            {
                title = dict["DN"];
            }
            else
            {
                //Debug.LogWarning("DisplayName not exist. Have to manulay create them.");
                var titleWords = entity.Ti.Split(' ');
                foreach (var tw in titleWords)
                {
                    title += tw[0].ToString().ToUpper() + tw.Substring(1) + " ";
                }
            }
            //Debug.Log("Title: " + title);

            if (dict.ContainsKey("ANF"))
            {
                dynamic authors = dict["ANF"];
                majorLastName = authors[0]["LN"];
                foreach (var author in authors)
                {
                    // FN, LN, S
                    authorNames.Add(string.Format("{0} {1}", author["FN"], author["LN"]));
                }
            }
            else
            {
                //Debug.LogWarning("ANF not exist. Have to manulay create them.");
                foreach (var author in entity.AA)
                {
                    string lowerName = author.AuN;
                    var authorWords = lowerName.Split(' ');
                    var currAuthorName = "";
                    foreach (var aw in authorWords)
                    {
                        currAuthorName += aw[0].ToString().ToUpper() + aw.Substring(1) + " ";
                    }
                    authorNames.Add(currAuthorName);
                }
                majorLastName = authorNames[0].Split(' ').Last();

                var titleWords = entity.Ti.Split(' ');
                foreach (var tw in titleWords)
                {
                    title += tw[0].ToString().ToUpper() + tw.Substring(1) + " ";
                }
            }
            //Debug.Log("Authors: " + string.Join("    ", authorNames));

            if (dict.ContainsKey("VSN") && dict["VSN"].Length > 0)
            {
                // short conf/journal name
                source = dict["VSN"];
            }
            else if (dict.ContainsKey("PB") && dict["PB"].Length > 0)
            {
                // short journal name
                source = dict["PB"];
            }
            else if (entity.C != null && entity.C.CN.Length > 0)
            {
                source = entity.C.CN.ToUpper();
            }
            else if (entity.J != null && entity.J.JN.Length > 0)
            {
                source = entity.J.JN.ToUpper();
            }
            else if (dict.ContainsKey("VFN") && dict["VFN"].Length > 0)
            {
                // full conf name
                source = dict["VFN"];
            }
            else if (dict.ContainsKey("BV") && dict["BV"].Length > 0)
            {
                // full journal name
                source = dict["BV"];
            }
            else
            {
                //Debug.LogWarning("Source not available.");
            }

            // Cut string idea from Stack overflow: https://stackoverflow.com/a/16236570/4762924
            if (source.Length == 0)
            {
                source = "N/A";
            }
            else if (source.Length >= 50)
            {
                // Try to cut at space
                int pos = source.LastIndexOf(" ", 40);
                // No space to space is too soon, just cut without thinking.
                if (pos <= 40)
                    pos = 50;
                source = source.Substring(0, pos) + "...";
            }
            //Debug.Log(string.Format("Source: {0}-({1})", source, year));

            // S.Ty Source Type(1:HTML, 2:Text, 3:PDF, 4:DOC, 5:PPT, 6:XLS, 7:PS)
            // Sometimes I found type 0 also points to pdf link
            if (dict.ContainsKey("S"))
            {
                foreach (var urlDict in dict["S"])
                {
                    int urlType = urlDict["Ty"];
                    string urlContent = urlDict["U"];
                    if (urlType == 3)
                    {
                        pdfUrl = urlContent;
                        break;
                    }
                    else if (urlType == 0 && urlContent.Contains("pdf"))
                    {
                        pdfUrl = urlContent;
                    }
                    else
                    {
                        //Debug.Log(string.Format("----{0}; {1}", urlType, urlContent));
                    }
                }
            }
            if (pdfUrl.Length > 0)
            {
                //Debug.Log("PDF available:" + pdfUrl);
            }
            // Last name of first author + last two digits of year
            string fileName = string.Format("{0}{1}", majorLastName, (year % 100).ToString("D2"));
            //Debug.Log("Named as:" + fileName);
            ARDocument doc = new ARDocument(fileName, title, authorNames.ToArray(), source, year, pdfUrl);
            parsedResults.Add(doc);

            ++counter;
        }

        Debug.Log(string.Format("Got {0} new results. Old results: {1}.", parsedResults.Count, currResults.Count));
        currResults.AddRange(parsedResults);
        parsingStatus = PARSING_STATUS.DONE;
    }
#endif

    private void RenderResults()
    {
        int notEnough = -1;
        for (int i = 0; i < RESULTS_PER_PAGE; ++i)
        {
            int targetEntryID = (currPageOnUI - 1) * RESULTS_PER_PAGE + i;
            if (targetEntryID < currResults.Count)
            {
                // Exist.
                resultEntries[i].gameObject.SetActive(true);
                resultEntries[i].InitContent(currResults[targetEntryID]);
            }
            else
            {
                // Set flag and break.
                notEnough = i;
                break;
            }
        }

        if (notEnough == 0)
        {
            // Nothing, then display error page
            SetErrorInfo("No results found. Please try again.");
            if (currMaxPage < 0 || (currMaxPage > 0 && currMaxPage < currPageOnUI - 1))
                currMaxPage = currPageOnUI - 1;
        }
        else if (notEnough > 0)
        {
            // Not sufficient. Update Max Page (if not set yet), and disable the entry
            for (int i = notEnough; i < RESULTS_PER_PAGE; ++i)
            {
                resultEntries[i].gameObject.SetActive(false);
            }
            if (currMaxPage < 0 || (currMaxPage > 0 && currMaxPage < currPageOnUI))
                currMaxPage = currPageOnUI;
        }
    }


    /// <summary>
    /// Go to most left (page 1)
    /// </summary>
    public void SelectDoubleLeft()
    {
        Debug.Log("Select child: Double Left.");
        if (currPageOnUI != 1)
        {
            JumpToPage(1, PAGE_ID_LEFT);
        }
    }

    /// <summary>
    /// Go to left (should be ignored when already at most left)
    /// </summary>
    public void SelectLeft()
    {
        Debug.Log("Select child: Left.");
        if (currPageOnUI > 2)
        {
            // Move to left but still Mid <- Mid. 
            JumpToPage(currPageOnUI - 1, PAGE_ID_MID);
        }
        else if (currPageOnUI == 2)
        {
            // Move to left, Left <- Mid
            JumpToPage(currPageOnUI - 1, PAGE_ID_LEFT);
        }
        else
        {
            // Already on the most left.
            Debug.LogWarning(string.Format("SelectLeft Warning: already on the most left. PageID:{0}, PageNum:{1}.", currPageID, currPageOnUI));
        }
    }

    /// <summary>
    /// Use currPageOnUI and currPageID to represent pages:
    /// targetID: (pgUI - pgID + targetID).
    /// Selecting page 1 doesn't always mean it leads to page 1.
    /// Usually, it leads to page 2 but changes the text.
    /// </summary>
    public void SelectPage1()
    {
        Debug.Log("Select child: Page 1.");
        // Left <- Mid, or Left <- Left. Shouldn't be from right.
        Debug.Assert(currPageID != PAGE_ID_RIGHT);
        if (currPageID != PAGE_ID_LEFT)
        {
            Debug.Assert(currPageID == PAGE_ID_MID);
            Debug.Log("Redirecting to [LEFT] function...");
            SelectLeft();
        }
    }

    /// <summary>
    /// Selecting page 2 usually does nothing (because you are already on it). 
    /// Unless you are on the 1st page.
    /// </summary>
    public void SelectPage2()
    {
        Debug.Log("Select child: Page 2.");
        int targetPage = currPageOnUI - currPageID + PAGE_ID_MID;
        if (currPageID != PAGE_ID_MID && (currMaxPage < 0 || (currMaxPage > 0 && targetPage <= currMaxPage)))
        {
            // Only be invoked on the first page.
            Debug.Assert(currPageOnUI == 1);
            JumpToPage(targetPage, PAGE_ID_MID);
        }
    }

    /// <summary>
    /// Selecting page 3 usually leads to page 2, but it changes the text.
    /// </summary>
    public void SelectPage3()
    {
        Debug.Log("Select child: Page 3.");
        // Current page ID 'should' be at page Left/Mid, and page 3 is within range.
        int targetPage = currPageOnUI - currPageID + PAGE_ID_RIGHT;
        if (currMaxPage < 0 || (currMaxPage > 0 && targetPage <= currMaxPage))
        {
            JumpToPage(targetPage, PAGE_ID_MID);
        }
    }

    public void SelectRight()
    {
        Debug.Log("Select child: Right.");
        // Still within range
        if (currMaxPage < 0 || (currMaxPage > 0 && currPageOnUI + 1 <= currMaxPage))
        {
            Debug.Assert(currPageID < 2);
            if (currPageID == 0)
            {
                // Left -> Middle
                Debug.Assert(currPageOnUI == 1);
                JumpToPage(currPageOnUI + 1, PAGE_ID_MID);
            }
            else if (currPageID == 1)
            {
                // Mid -> Mid, just rotate the number
                JumpToPage(currPageOnUI + 1, PAGE_ID_MID);
            }
        }
        else
        {
            Debug.LogWarning(string.Format("SelectRight Warning: out of range. PageID:{0}, PageNum:{1}, MaxPage:{2}.",
                currPageID, currPageOnUI, currMaxPage));
        }
    }
    
    public void SelectDoubleRight()
    {
        Debug.Log("Select child: Double Right.");
        int targetPageNum = Mathf.CeilToInt(currResults.Count / (float)RESULTS_PER_PAGE);
        if (targetPageNum >= 2)
        {
            // Could be placed in the middle
            JumpToPage(targetPageNum, PAGE_ID_MID);
        }
        else
        {
            Debug.Log("Double Right Special Case: Still on page 1.");
        }
    }

    /// <summary>
    ///  Create a unified logic for jumpping left/right, or clicking page number directly.
    /// </summary>
    /// <param name="pageNum">Page Number on UI (starting from 1). </param>
    /// <param name="pageID">Page ID based on BtnPages (range: 0,1,2). </param>
    private void JumpToPage(int pageNum, int pageID)
    {
        // Hide current focus box based on page ID
        SetPageButtonColor(currPageID, 0);
        // Set correct focus box based on page ID
        SetPageButtonColor(pageID, 255);

        // Set correct page numbers on UI
        for (int p = 0; p < BtnPages.Length; ++p)
        {
            // Calculate the correct value for page0, then loop over it.
            SetPageButtonText(p, (pageNum - pageID + p).ToString());
        }

        // Update value
        currPageID = pageID;
        currPageOnUI = pageNum;

        // Update result entries
        if (Mathf.CeilToInt(currResults.Count / (float)RESULTS_PER_PAGE) >= pageNum)
        {
            // Update from cache
            RenderResults();
            UpdateButtonStatus();
        }
        else if (currTargetPage >= currPageOnUI)
        {
            // Already processing in the background. Need to play animation
            // TODO.
            // For the values, just need to wait.
        }

        // Need to resume pre-loading process
        // Continue pre-loading: keep checking
        if (currSearchType == SearchType.ONLINE && parsingStatus == PARSING_STATUS.IDLE && currMaxPage < 0 && currTargetPage < currPageOnUI + 2)
        {
            // New step 1: if we need to cache the next page (up to 2 pages) 
            // and if we can cache the next page (max page not met)
            currTargetPage += 1;
            parsingStatus = PARSING_STATUS.WAITING;
#if !UNITY_EDITOR
            var task = Task.Run(() => currHelper.AcademicSearch(onlineSearchContent, RESULTS_PER_PAGE, currResults.Count));
#endif
        }
    }

    private void UpdateButtonStatus()
    {
        int resultsNum = currResults.Count;
        // Set buttons' correct interactable status
        // DoubleLeft: always to 1st page, ON if any exists.
        BtnDoubleLeft.interactable = (resultsNum > 0);
        // Left: ON when pages >= 2 (id >= 1)
        BtnLeft.interactable = (currPageOnUI >= 2);
        // Page 1 (Left): always ON.
        BtnPages[PAGE_ID_LEFT].interactable = true;
        // Page 2 (Mid): ON if it is available
        BtnPages[PAGE_ID_MID].interactable = (currMaxPage < 0 || currMaxPage > 0 && (currPageOnUI - currPageID + PAGE_ID_MID) <= currMaxPage);
        // Page 3 (Right): ON if it is available
        BtnPages[PAGE_ID_RIGHT].interactable = (currMaxPage < 0 || currMaxPage > 0 && (currPageOnUI - currPageID + PAGE_ID_RIGHT) <= currMaxPage);
        // Right: ON if next page is available
        BtnRight.interactable = (currMaxPage < 0 || currMaxPage > 0 && (currPageOnUI + 1) <= currMaxPage);
        // DoubleRight: always to last page
        BtnDoubleRight.interactable = (resultsNum > 0);
    }

    private void MuteAllButtons()
    {
        BtnDoubleLeft.interactable = false;
        BtnLeft.interactable = false;
        foreach (var btn in BtnPages)
            btn.interactable = false;
        BtnRight.interactable = false;
        BtnDoubleRight.interactable = false;
    }

    private void SetErrorInfo(string errInfo)
    {
        foreach (var entry in resultEntries)
        {
            entry.gameObject.SetActive(false);
        }
        resultEntries[0].gameObject.SetActive(true);
        resultEntries[0].SetSimpleContent(errInfo);
    }

    private void ResetPages(int defaultPageID = 0, int defaultPageOnUI = 1)
    {
        int pageOnUIOffset = defaultPageOnUI - defaultPageID;
        // Default: set the focus 'box' on the first page, and set page text 1~3
        // Other case: resume the states.
        for (int i = 0; i < BtnPages.Length; ++i)
        {
            SetPageButtonColor(i, 0);
            SetPageButtonText(i, (i + pageOnUIOffset).ToString());
        }
        SetPageButtonColor(defaultPageID, 255);
        // Disable the Left buttons (Update: not necessary)
        //BtnLeft.interactable = false;

        foreach (var entry in resultEntries)
        {
            entry.gameObject.SetActive(false);
        }
    }

    private void SetPageButtonColor(int pgID, int color)
    {
        Image img = BtnPages[pgID].gameObject.GetComponent<Image>();
        img.color = new Color(img.color.r, img.color.g, img.color.b, color);
    }

    private void SetPageButtonText(int pgID, string txt)
    {
        BtnPages[pgID].gameObject.GetComponentInChildren<Text>().text = string.Format("<b>{0}</b>", txt);
    }

    private static async void DecodeRecords(string inputName)
    {
#if !UNITY_EDITOR
        StorageFolder storageFolder = KnownFolders.CameraRoll;
        var tempFile = (StorageFile)await storageFolder.TryGetItemAsync(inputName);
        if (tempFile == null)
        {
            Debug.Log("Records file not exist: " + inputName);
            return;
        }
        string result = await FileIO.ReadTextAsync(tempFile);
        wordRecords = JsonConvert.DeserializeObject<WordRecords>(result);
        Debug.Log(string.Format("Decode Records: {0} files, {1} words.", wordRecords.Files.Length, wordRecords.Dict.Count));
#endif
    }

    private static List<ARDocument> ParseLocalDocuments(string word)
    {
        string cleanedWord = word.Trim(new char[] { ' ', ',', '.', ':', '\'', '\"', '-', '?', '[', ']', '{', '}', '<', '>', ';', '*' }).ToLower();
        List<ARDocument> resultList = new List<ARDocument>();
        if (!wordRecords.Dict.ContainsKey(cleanedWord))
            return resultList;

        List<Tuple<string, int>> resultZip = wordRecords.Files.Zip(wordRecords.Dict[cleanedWord].ToArray(), (first, second) => new Tuple<string, int>(first, second)).ToList();
        resultZip.Sort((x, y) => y.Item2.CompareTo(x.Item2));
        foreach (var pair in resultZip)
        {
            Debug.Log("[Ordered Result] FileName:" + pair.Item1 + ", Hit:" + pair.Item2);

            if (PageOrganizer.DictDocuments.ContainsKey(pair.Item1))
            {
                if (pair.Item2 > 0)
                {
                    ARDocument arDoc = PageOrganizer.DictDocuments[pair.Item1];
                    arDoc.extraInfo = pair.Item2.ToString();
                    // Add to list (to be displayed on window)
                    resultList.Add(arDoc);
                }
                else
                    Debug.Log("Word didn't appear in target document:" + pair.Item1);
            }
            else
                Debug.Log("FileName from local records not exist in PageOrganizer.DictDocument: " + pair.Item1);
        }
        return resultList;
    }
}

public class WordRecords
{
    public string[] Files { get; set; }
    public Dictionary<string, List<int>> Dict { get; set; }

    public WordRecords()
    {
        Dict = new Dictionary<string, List<int>>();
    }

    public override string ToString()
    {
        string fStr = string.Join(";", Files);
        List<string> dList = new List<string>();
        foreach (var item in Dict)
        {
            dList.Add(string.Format("{0}:{1}", item.Key, string.Join(",", item.Value)));
        }
        return fStr + "\n" + string.Join("\n", dList);
    }
}