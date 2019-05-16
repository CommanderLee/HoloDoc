using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Sticky Manager controls the behaviors of sticy notes.
/// E.g., Online Search. 
/// </summary>
public class StickyManager : MonoBehaviour {

    public GameObject MainCamera;

    //public Canvas canvasPin;

    public const float STROKE_WIDTH = 0.001f;
    public const float CANVAS_SIZE = 0.08f;

    //private LineRenderer lineRendererSin;
    //private int lineCount = 200;

    private List<Sticky> stickyList;
    private const float STICKY_SCALE = 1;
    private const float STICKY_FADE_TIME = 1;
    protected static Dictionary<int, OnlineSearchManager> onlineSearchManagerDict = new Dictionary<int, OnlineSearchManager>();

	// Use this for initialization
	void Start () {
        stickyList = new List<Sticky>();

        //var stickyManagerList = gameObject.GetComponentsInChildren<OnlineSearchManager>();
//        var stickyManagerList = FindObjectsOfType<OnlineSearchManager>();
//        Debug.Log("Found OnlineManagers:" + stickyManagerList.Length);
//#if!UNITY_EDITOR
//        foreach (var manager in stickyManagerList)
//        {
//            string numberStr = new string(manager.gameObject.name.Where(char.IsDigit).ToArray());
//            if (int.TryParse(numberStr, out int number))
//            {
//                Debug.Log("Found Manager for Sticky:" + number);
//                onlineSearchManagerDict.Add(number, manager);
//            }
//        }
//#endif
        //GameObject lineManager = Instantiate(Resources.Load("LineManager")) as GameObject;
        //lineManager.transform.SetParent(canvasPin.transform);
        //lineManager.transform.localPosition = Vector3.zero;
        //lineManager.transform.localEulerAngles = Vector3.zero;
        //lineRendererSin = lineManager.GetComponent<LineRenderer>();
        //lineRendererSin.widthMultiplier = 0.001f;
        //lineRendererSin.alignment = LineAlignment.Local;
        //lineRendererSin.positionCount = lineCount;
    }
	
	// Update is called once per frame
	void Update () {
        Vector3 bias = new Vector3(0.1f, 0.1f, 0.1f);
        foreach (Sticky sticky in stickyList)
        {
            if (sticky.LinkedObject)
            {
                sticky.MainCanvas.transform.rotation = sticky.LinkedObject.transform.rotation;
                sticky.MainCanvas.transform.position = sticky.LinkedObject.transform.position;
                sticky.MainCanvas.transform.Translate(bias, Space.Self);
            }
            sticky.MainCanvas.transform.localEulerAngles = new Vector3(0, MainCamera.transform.localEulerAngles.y, 0);
        }
    }

    public static void AddManager(GameObject managerObj)
    {
#if !UNITY_EDITOR
        string numberStr = new string(managerObj.name.Where(char.IsDigit).ToArray());
        if (int.TryParse(numberStr, out int number))
        {
            Debug.Log("Found Manager for Sticky:" + number);
            onlineSearchManagerDict.Add(number, managerObj.GetComponent<OnlineSearchManager>());
        }
#endif
    }

    public static OnlineSearchManager GetManager(int managerID)
    {
        if (onlineSearchManagerDict.ContainsKey(managerID))
            return onlineSearchManagerDict[managerID];
        else
            return null;
    }

    public void CreateSticky (string tagName, List<List<Vector3>> strokes, GameObject linkedObj)
    {
        Sticky sticky = new Sticky(tagName, strokes, linkedObj);
        StartCoroutine(StickyFade(sticky, true));
        stickyList.Add(sticky);
        Debug.Log(string.Format("[{0}]: Create new sticky.", tagName));
    }

    private IEnumerator StickyFade(Sticky sticky, bool flag)
    {
        if (flag)
        {
            // Open
            for (float i = 0; i <= STICKY_FADE_TIME; i += Time.deltaTime)
            {
                float percentage = i / STICKY_FADE_TIME;
                sticky.MainCanvas.transform.localScale = Vector3.one * STICKY_SCALE * percentage;
                yield return null;
            }
            sticky.MainCanvas.transform.localScale = Vector3.one * STICKY_SCALE;
        }
        else
        {
            // Close
            yield return null;
        }
    }

    /// <summary>
    /// Class for each of the sticky notes
    /// </summary>
    class Sticky
    {
        public string TagName;
        public Canvas MainCanvas;
        public List<LineRenderer> Lines;
        public GameObject LinkedObject;

        public Sticky(string tagName, List<List<Vector3>> strokes, GameObject linkedObj)
        {
            TagName = tagName;
            GameObject go = Instantiate(Resources.Load("CanvasPin")) as GameObject;
            MainCanvas = go.GetComponent<Canvas>();
            Lines = new List<LineRenderer>();
            Vector3 bias = new Vector3(0.15f, 0.1f, -0.1f);

            foreach (List<Vector3> stroke in strokes)
            {
                GameObject lineManager = Instantiate(Resources.Load("LineManager")) as GameObject;
                lineManager.transform.SetParent(MainCanvas.transform);
                lineManager.transform.localPosition = Vector3.zero;
                lineManager.transform.localEulerAngles = Vector3.zero;
                LineRenderer lineRenderer = lineManager.GetComponent<LineRenderer>();
                lineRenderer.widthMultiplier = STROKE_WIDTH;
                lineRenderer.alignment = LineAlignment.Local;
                lineRenderer.positionCount = stroke.Count;
                lineRenderer.SetPositions(stroke.ToArray());
                Lines.Add(lineRenderer);
            }
            Debug.Log(string.Format("[{0}]: Add {1} strokes.", tagName, strokes.Count));

            LinkedObject = linkedObj;
            if (linkedObj)
            {
                MainCanvas.transform.position = linkedObj.transform.position + bias;
                Debug.Log("Sticky placed at:" + MainCanvas.transform.position);
            }
        }
    }


}
