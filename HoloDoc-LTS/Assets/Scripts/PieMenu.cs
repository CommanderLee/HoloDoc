using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PageConstants;

public class PieMenu : MonoBehaviour {

    #region Public Unity Objects
    /// <summary>
    /// List of buttons. Make the 0th button as Cancel in the center.
    /// </summary>
    public List<PieMenuItem> items = new List<PieMenuItem>();

    public GameObject PhysicalPancake;
    //[Tooltip("Visualize the current cursor position")]
    //public GameObject cursorObj;

    public bool isFullCircle;
    //public Image innerImage;

    // TO be changed later in Editor
    public Color NormalColor;
    public Color HoverColor;
    public Color PressedColor;
    public Color DisabledColor;

    public int NormalFont;
    public int HoverFont;
    public int PressedFont;
    public int DisabledFont;

    //public Color NormalFontColor;
    //public Color HoverFontColor;
    //public Color PressedFontColor;
    //public Color DisabledFontColor;
    #endregion

    #region Pre-loaded contents and constant values
    private PieMenuItem innerItem;
    private CanvasGroup objCanvasGroup;
    private PenOrganizer penOrg;

    private const float PieMenuRadius = 0.06f;
    //private const float InerCircleRadius = 0.02f;
    /// <summary>
    /// The sector is pushed outwards for this distance, at the direction of its angle bisector.
    /// </summary>
    /// Update: move to PieMenuItem.MoveToAngle method.
    //private const float DRIFT_DIST = 0.001f;

    private const float FADE_IN = 1.0f;
    private const float FADE_OUT = 1.0f;
    private const float SPIN_TIME = 1.0f;

    /// <summary>
    /// Counting down from 4 seconds when hand disappears.
    /// </summary>
    private const float COUNTING_DOWN = 4.0f;
    /// <summary>
    /// Start to scale down the pie menu to save some space for the major content.
    /// 2.0 -> 0: 100% -> 50%;
    /// </summary>
    private const float SCALING_DOWN = 2.0f;
    private const float MIN_PIE_SCALE = 0.5f;
    #endregion

    #region Values indicating the current status
    public PieMenuType MenuType { get; private set; }
    public string FileName { get; private set; }
    /// <summary>
    /// The current menu itme being pressed. 
    /// The item will not show as pressed if the active cursor is on other menu item.
    /// </summary>
    public int currPressedID = -1;
    /// <summary>
    /// Current Menu ID with gaze cursor in it.
    /// </summary>
    public int currMenuID = -1;
    // Ignore this. Use cleaner logic.
    //private int prevMenuID = 0;
    private Vector2 mousePosition = Vector2.zero;

    private float eachAngle;
    private float innerCircleRadius;
    
    /// <summary>
    /// Update: I changed the logic so there will be a default option showing the meta-data.
    /// Which means, it will be NOT_INIT -> ON_ANIMATION -> PRESSED.
    /// </summary>
    public enum PieMenuState { NOT_INIT, ON_ANIMATION, READY, PRESSED };
    public PieMenuState MenuState = PieMenuState.NOT_INIT;

    private float scalingTimer = 0;
    #endregion

    private Dictionary<string, Sprite> dictIcon;

    #region Pie Menu Initialization
    // Use this for initialization
    void Start () {
        //menuItemNumbers = items.Count;
        // Should have Cancel inside
        if (items.Count == 0)
        {
            Debug.Log("Error: Empty Menu.");
        }
        else
        {
            innerItem = items[0];
            innerCircleRadius = innerItem.radius;
            if (items.Count > 1)
            {
                // While debugging, there are some preloaded items.
                if (isFullCircle)
                {
                    eachAngle = 360.0f / (items.Count - 1);
                }
                else
                {
                    eachAngle = 180.0f / (items.Count - 1);
                }
            }
        }
        objCanvasGroup = gameObject.GetComponent<CanvasGroup>();
        objCanvasGroup.alpha = 0;
        //PhysicalPancake.SetActive(false);
        //cursorObj.SetActive(false);

        LoadIcons();
        //Debug.Log(string.Format("Cursor pos: {0} (local), {1} (global)", cursorObj.transform.localPosition, cursorObj.transform.position));
        //InitPieMenu(new List<string>(new string[] { "Cover", "Full Paper" }));
	}

    // Update is called once per frame
    void Update()
    {
        if (!isFullCircle)
        {
            // Semi-circle, world-anchored
            if (MenuState == PieMenuState.READY)
            {
                if (mousePosition.magnitude > PieMenuRadius * 2)
                {
                    // Shrink
                    mousePosition = mousePosition.normalized * PieMenuRadius;
                }
                //cursorObj.transform.localPosition = new Vector3(mousePosition.x, mousePosition.y, cursorObj.transform.localPosition.z);
            }
        }
        else
        {
            // Full circle, world-anchored
            if (MenuState == PieMenuState.READY || MenuState == PieMenuState.PRESSED)
            {
                if (mousePosition.magnitude > PieMenuRadius)
                {
                    // Shrink to fit it on the boundry
                    mousePosition = mousePosition.normalized * PieMenuRadius;
                }
                //cursorObj.transform.localPosition = new Vector3(mousePosition.x, mousePosition.y, cursorObj.transform.localPosition.z);

                // Debug: get the hand state
                if (HandsManager.Instance.HandDetected)
                {
                    //objCanvasGroup.alpha = 1;
                    // Reset the timer
                    if (gameObject.transform.localScale.x < 1.0f)
                    {
                        // Reset the UI (Update: continuously increase)
                        gameObject.transform.localScale = Vector3.one * Mathf.Min(1.0f, gameObject.transform.localScale.x + 0.06f);
                    }
                    scalingTimer = COUNTING_DOWN;
                }
                else
                {
                    //objCanvasGroup.alpha = 0;
                    // Do nothing if it already reaches 0
                    if (scalingTimer > 0)
                    {
                        scalingTimer -= Time.deltaTime;
                        if (scalingTimer < SCALING_DOWN)
                        {
                            // Adapt the UI
                            gameObject.transform.localScale = Vector3.one * (MIN_PIE_SCALE + (1 - MIN_PIE_SCALE) / SCALING_DOWN * scalingTimer);
                        }
                    }
                    

                }
            }
        }
    }

    private void LoadIcons()
    {
        dictIcon = new Dictionary<string, Sprite>
        {
            { "Close", Resources.Load<Sprite>("PNG/icons8-cancel-100-w") },
            { "Figures", Resources.Load<Sprite>("PNG/icons8-bar-chart-64") },
            { "Preview", Resources.Load<Sprite>("PNG/icons8-document-100-w") },
            { "Video", Resources.Load<Sprite>("PNG/icons8-tv-show-100-w") },
            { "Full Papers", Resources.Load<Sprite>("PNG/icons8-documents-100-w") },
            { "Tags", Resources.Load<Sprite>("PNG/icons8-tags-100") },
            { "Meta-Data", Resources.Load<Sprite>("PNG/icons8-user-manual-96") }
        };
    }

    /// <summary>
    /// Reset parameters, only got 1 button in the middle (Cancel), without other post-defined buttons
    /// </summary>
    private void ResetParams()
    {
        mousePosition = new Vector2(0, 0);
        //prevMenuID = 0;
        currMenuID = -1;
        currPressedID = -1;
        MenuState = PieMenuState.NOT_INIT;
        SetHoverMenuItem(0);
    }

    /// <summary>
    /// Initialize the pie menu with given array of names.
    /// The position and rotation of this pie menu should be determined in advance.
    /// </summary>
    /// <param name="names"></param>
    /// <param name="defaultOption">Highlight a default option. If the value is invalid (e.g., -1), igore it</param>
    public void InitPieMenu(string fName, string[] names, bool[] states, PenOrganizer po, PieMenuType tp, int defaultOption = -1)
    {
        gameObject.SetActive(true);
        FileName = fName;
        MenuType = tp;
        if (names.Length != states.Length)
        {
            Debug.Log("InitPieMenu Error: the names and states array should have the same length.");
            return;
        }

        for (int i = 1; i < items.Count; ++i)
        {
            Destroy(items[i].gameObject);
        }
        items.Clear();
        items.Add(innerItem);

        ResetParams();
        penOrg = po;

        // Full Circle, 360 degrees, world-anchored.
        if (isFullCircle)
        {
            // Calculate the basic shapes for this menu
            eachAngle = 360.0f / names.Length;

            Debug.Log(string.Format("InitPieMenu (FullCircle): {0} sectors, {1} each, position: {2} (local) and {3} (global), radius: {4}",
                names.Length, eachAngle, gameObject.transform.localPosition, gameObject.transform.position, PieMenuRadius));
            for (int i = 0; i < names.Length; ++i)
            {
                string pName = names[i];
                float startAngle = i * eachAngle;
                float bisector = startAngle + eachAngle * 0.5f;
                GameObject pieMenuItemObj = Instantiate(Resources.Load("PieItem")) as GameObject;
                pieMenuItemObj.transform.SetParent(gameObject.transform);
                // Make some blank spaces between the sectors, by pushing them outwards for a bit (handled by PieMenuItem.MoveToAngle now)
                pieMenuItemObj.transform.localPosition = Vector3.zero;
                pieMenuItemObj.transform.localEulerAngles = Vector3.zero;
                pieMenuItemObj.transform.localScale = Vector3.one;
                //Debug.Log("Item Size:" + pieMenuItemObj.GetComponent<RectTransform>().sizeDelta + "Local Scale:" + pieMenuItemObj.transform.localScale);
                PieMenuItem pieMenuItem = pieMenuItemObj.GetComponent<PieMenuItem>();
                // React to parameters 
                if (states[i])
                {
                    pieMenuItem.SetShape(PieMenuRadius, startAngle, eachAngle, NormalColor);
                    pieMenuItem.SetText(pName, NormalFont);
                    pieMenuItem.clickable = true;
                }
                else
                {
                    pieMenuItem.SetShape(PieMenuRadius, startAngle, eachAngle, DisabledColor);
                    pieMenuItem.SetText(pName, DisabledFont);
                    pieMenuItem.clickable = false;
                    Debug.Log(string.Format("[PieMenu] [{0}] {1} is now disabled.", items.Count + 1, pName));
                }
                if (dictIcon.ContainsKey(pName))
                    pieMenuItem.SetIcon(dictIcon[pName]);
                items.Add(pieMenuItem);
            }
        }
        else
        {
            // Semi-circle, bottom half.
            // Calculate the basic shapes for this menu
            eachAngle = 180.0f / names.Length;
            // The transform should have been changed outside.
            Debug.Log(string.Format("InitPieMenu (SemiCircle): {0} sectors, {1} each, position: {2} (local) and {3} (global), radius: {4}",
                names.Length, eachAngle, gameObject.transform.localPosition, gameObject.transform.position, PieMenuRadius));
            for (int i = 0; i < names.Length; ++i)
            {
                string pName = names[i];
                float startAngle = 180 + i * eachAngle;
                float bisector = startAngle + eachAngle * 0.5f;
                GameObject pieMenuItemObj = Instantiate(Resources.Load("PieItem")) as GameObject;
                pieMenuItemObj.transform.SetParent(gameObject.transform);
                // Make some blank spaces between the sectors, by pushing them outwards for a bit (handled by PieMenuItem.MoveToAngle now)
                pieMenuItemObj.transform.localPosition = Vector3.zero;
                pieMenuItemObj.transform.localEulerAngles = Vector3.zero;
                PieMenuItem pieMenuItem = pieMenuItemObj.GetComponent<PieMenuItem>();
                // React to parameters 
                if (states[i])
                {
                    pieMenuItem.SetShape(PieMenuRadius, startAngle, eachAngle, NormalColor);
                    pieMenuItem.SetText(pName, NormalFont);
                    pieMenuItem.clickable = true;
                }
                else
                {
                    pieMenuItem.SetShape(PieMenuRadius, startAngle, eachAngle, DisabledColor);
                    pieMenuItem.SetText(pName, DisabledFont);
                    pieMenuItem.clickable = false;
                    Debug.Log(string.Format("[PieMenu] [{0}] {1} is now disabled.", items.Count + 1, pName));
                }
                if (dictIcon.ContainsKey(pName))
                    pieMenuItem.SetIcon(dictIcon[pName]);
                items.Add(pieMenuItem);
            }
        }

        StartCoroutine(PieMenuFade(true));
        if (defaultOption >= 0 && defaultOption < items.Count)
        {
            Debug.Log("Set Pressed by Default.");
            SetPressedMenuItem(defaultOption);
        }
    }
    #endregion

    #region PieMenu Animations
    private IEnumerator PieMenuFade(bool fadeIn)
    {
        MenuState = PieMenuState.ON_ANIMATION;
        if (fadeIn)
        {
            //gameObject.SetActive(true);
            //PhysicalPancake.SetActive(true);
            //cursorObj.SetActive(false);
            for (float i = 0; i <= FADE_IN; i += Time.deltaTime)
            {
                float percentage = i / FADE_IN;
                objCanvasGroup.alpha = percentage;
                gameObject.transform.localScale = Vector3.one * percentage;
                //PhysicalPancake.transform.localScale = Vector3.one * percentage;
                yield return null;
            }
            objCanvasGroup.alpha = 1;
            //cursorObj.SetActive(true);
            // Ready, start mvoing the cursor after the animation
            MenuState = PieMenuState.READY;
        }
        else
        {
            //PhysicalPancake.SetActive(false);
            for (float i = FADE_OUT; i >= 0; i -= Time.deltaTime)
            {
                float percentage = i / FADE_OUT;
                objCanvasGroup.alpha = percentage;
                gameObject.transform.localScale = Vector3.one * percentage;
                //PhysicalPancake.transform.localScale = Vector3.one * percentage;
                yield return null;
            }
            objCanvasGroup.alpha = 0;
            gameObject.transform.localScale = Vector3.zero;
            //cursorObj.SetActive(false);
            MenuState = PieMenuState.NOT_INIT;
            gameObject.SetActive(false);
        }
    }

    private IEnumerator SpinPieMenu(int pieID, float targetDegree)
    {
        float currBisector = items[pieID].RotationStartZ + eachAngle * 0.5f;
        float diffDegree = targetDegree - currBisector;
        // It seems like no need spinning
        if (Mathf.Abs(diffDegree) < 1)
            yield break;

        MenuState = PieMenuState.ON_ANIMATION;
        // Make sure it is not spinning too much
        if (diffDegree < -180)
            diffDegree += 360;
        else if (diffDegree > 180)
            diffDegree -= 360;
        float degreePerSec = diffDegree / SPIN_TIME;

        for (float i = 0; i <= SPIN_TIME; i += Time.deltaTime)
        {
            float currRotation = i * degreePerSec;
            for (int p = 1; p < items.Count; ++p)
            {
                items[p].MoveToAngle(items[p].RotationStartZ + currRotation);
            }
            yield return null;
        }

        // Set the accurate value and set to RotationStartZ
        for (int p = 1; p < items.Count; ++p)
        {
            float targetAxisZ = items[p].RotationStartZ + diffDegree;
            if (targetAxisZ < 0)
                targetAxisZ += 360;
            else if (targetAxisZ > 360)
                targetAxisZ -= 360;
            items[p].MoveToAngle(targetAxisZ);
            items[p].RotationStartZ = targetAxisZ;
        }
        MenuState = PieMenuState.PRESSED;
    }
    #endregion

    #region Mouse Event Handler
    /// <summary>
    /// When using other input devices, this public function could be called by the container.
    /// Otherwise, it could also obtain the mouse position from its Update function on each frame.
    /// </summary>
    /// <param name="newMousePos"></param>
    public int MouseMove (Vector2 newMousePos)
    {
        if (MenuState == PieMenuState.ON_ANIMATION)
            return -1;

        if (gameObject.transform.localScale.x < 1.0f)
        {
            // Reset the UI (Update: continuously increase)
            gameObject.transform.localScale = Vector3.one * Mathf.Min(1.0f, gameObject.transform.localScale.x + 0.06f);
        }
        scalingTimer = COUNTING_DOWN;

        // Input.mousePosition.x and y
        mousePosition = newMousePos;
        int newID = CalcMenuItemID();
        
        // Update if this is a new hover state, or the cursor just entered (curr == -1 in this case)
        if (currMenuID != newID)
        {
            // Resume if necessary
            if (currMenuID >= 0)
                ResumeMenuItem(currMenuID);

            // Make sure only one highlighted item could exist on the UI
            if (currPressedID >= 0 && currPressedID != newID)
                ResumeMenuItem(currPressedID);

            // Update the hovered effect on UI
            SetHoverMenuItem(newID);
            currMenuID = newID;
        }
        return currMenuID;
    }

    public void MouseLeave ()
    {
        // Revoke current hover state. If there is a pressed item, resume it.
        if (currPressedID >= 0 && currPressedID < items.Count)
        {
            if (currMenuID != currPressedID && currMenuID >= 0)
            {
                ResumeMenuItem(currMenuID);
            }
            Debug.Log("Set Pressed on Mouse Leave.");
            SetPressedMenuItem(currPressedID);
        }
        // Reset the currMenuID to N/A.
        currMenuID = -1;
    }


    /// <summary>
    /// Triggered by GazeGestureManager.
    /// In order to be consistent with the UI, 
    /// it may ignore the passed position parameter and fire the Pressed event on current hoverred item
    /// </summary>
    /// <param name="newMousePos"></param>
    /// <returns></returns>
    public int MousePressed(bool invokeOnCurrItem, Vector2 newMousePos)
    {
        // Ignore this click if the pie menu is 'moving'
        if (MenuState == PieMenuState.ON_ANIMATION)
        {
            Debug.Log("Pie Menu is moving now. Try later. ON_ANIMATION.");
            return -1;
        }

        // Received a tap but the cursor is not on pie menu
        if (invokeOnCurrItem && currMenuID < 0)
        {
            Debug.Log("Pie Menu is not being focused while tapping.");
            return -2;
        }
            
        // Input.mousePosition.x and y. If invoke on current item, then just use currMenuID
        if (!invokeOnCurrItem)
        {
            mousePosition = newMousePos;
            currMenuID = CalcMenuItemID();
        }

        // Update: set the proper UI style
        bool pressedStateChanged = SetPressedMenuItem(currMenuID);

        if (pressedStateChanged)
        {
#if !UNITY_EDITOR
            // Trigger the caller's function
            penOrg.PieMenuMousePressed(MenuType, currMenuID);
#endif

            // Trigger its own function
            if (currMenuID == 0)
            {
                // Close and reset
                StartCoroutine(PieMenuFade(false));
            }

            // Reset the attributes
            return currMenuID;
        }
        else
        {
            Debug.Log("The UI style is set, but it was on the previous pressed button. Do Nothing.");
            return -3;
        }
    }
    #endregion

    #region Private Helper functions
    private int CalcMenuItemID()
    {
        int retID = 0;
        // Check the range: If it falls in the inner circle, it is the item[0] (Cancel)
        if (mousePosition.magnitude >= innerCircleRadius && items.Count > 1)
        {
            // Note: this funtion would take account the x==0 cases for us
            // Convert to degrees so it will be easier to debug
            float angle = Mathf.Atan2(mousePosition.y, mousePosition.x) * Mathf.Rad2Deg;
            if (angle < 0)
                angle += 360;
            // only calculates the remaining (n-1) sectors
            if (!isFullCircle)
            {
                // Semi-circle
                retID = (int)((angle - 180) / eachAngle) + 1;
            }
            else
            {
                // Full-circle
                // Update: if it is spinning, we'd better use a loop to find it. 
                for (int p = 1; p < items.Count; ++p)
                {
                    if (angle >= items[p].RotationStartZ && angle <= items[p].RotationStartZ + eachAngle)
                    {
                        retID = p;
                        break;
                    }
                }
                //retID = (int)(angle / eachAngle) + 1;
            }
        }
        if (retID < 0 || retID >= items.Count)
        {
            // Calculation error
            Debug.Log("CalcMenuItemID Error: invalid return value: " + retID);
            return 0;
        }
        return retID;
    }

    /// <summary>
    /// A helper function that resumes the normal (or disabled) color. Frequently used. 
    /// </summary>
    /// <param name="pID"></param>
    private void ResumeMenuItem(int pID)
    {
        if (items[pID].clickable)
        {
            items[pID].imageBkg.color = NormalColor;
            items[pID].ItemText.fontSize = NormalFont;
        }
        else
        {
            items[pID].imageBkg.color = DisabledColor;
            items[pID].ItemText.fontSize = DisabledFont;
        }
    }

    private void SetHoverMenuItem(int newID)
    {
        Debug.Assert(newID >= 0 && newID < items.Count);

        if (!items[newID].clickable)
        {
            //Debug.Log("SetHoverMenuItem Error: This menu item has already been disabled: " + currID);
            return;
        }
        // Update the UI to hovered style
        items[newID].imageBkg.color = HoverColor;
        items[newID].ItemText.fontSize = HoverFont;
        //Debug.Log(string.Format("[PieMenu] Hover on [{0}] {1}", currID, items[currID].name));
    }

    /// <summary>
    /// Set proper UI style for the prev. and new pie menu item.
    /// Move them if necessary (new version: spinning pie menu)
    /// </summary>
    /// <param name="currID"></param>
    private bool SetPressedMenuItem(int currID)
    {
        Debug.Assert(currID >= 0 && currID < items.Count);
        bool stateChanged = false;

        if (!items[currID].clickable)
        {
            //Debug.Log("SetPressedMenuItem Error: This menu item has already been disabled: " + currID);
            return stateChanged;
        }
        items[currID].imageBkg.color = PressedColor;
        items[currID].ItemText.fontSize = PressedFont;

        if (currPressedID != currID)
        {
            currPressedID = currID;
            // Spinning: rotate this pie menu item facing the content.
            StartCoroutine(SpinPieMenu(currID, 45.0f));
            stateChanged = true;
        }
        Debug.Log(string.Format("[PieMenu] Press on [{0}] {1}", currID, items[currID].name));
        return stateChanged;
    }

    private void SetDisabledMenuItem(int targetID)
    {
        if (targetID > 0 && targetID < items.Count)
        {
            items[targetID].clickable = false;
            items[targetID].imageBkg.color = DisabledColor;
            items[targetID].ItemText.fontSize = DisabledFont;
            Debug.Log(string.Format("[PieMenu] [{0}] {1} is now disabled.", targetID, items[targetID].name));
        }
    }
    #endregion
}