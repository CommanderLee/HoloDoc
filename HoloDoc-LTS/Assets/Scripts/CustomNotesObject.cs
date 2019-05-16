using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CustomNotesObject : MonoBehaviour {

    public Image fullImage;
    public RectTransform currTransform;
    public BoxCollider boxCollider;
    public Text TextInfo;

    public float width;
    public float height;

    public string FileName;
    public int Page;
    public int ID;
    public string Source;
    public string CreateDate;

    // Based on the prefab size
    private const float ScaleX = 0.8f;
    private const float ScaleY = 1.0335f;
    private const float boxSizeZ = 0.02f;
    private const float NOTE_MAX_HEIGHT = 0.34f;
    private CanvasGroup alphaControl;

	// Use this for initialization
	void Start () {
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public void SetMask(float tlX, float tlY, float brX, float brY)
    {
        float midX = (tlX + brX) / 2;
        float midY = (tlY + brY) / 2;
        
        // See if the image is too tall (MAX allowed: 
        height = Mathf.Abs(tlY - brY) * ScaleY;
        float shrinkScale = Mathf.Min(1.0f, NOTE_MAX_HEIGHT / height);

        // 0.5 is the pivor location
        fullImage.transform.localPosition = new Vector3((0.5f - midX) * ScaleX * shrinkScale, (0.5f - midY) * ScaleY * shrinkScale, 0);
        fullImage.rectTransform.sizeDelta = new Vector2(fullImage.rectTransform.sizeDelta.x * shrinkScale, fullImage.rectTransform.sizeDelta.y * shrinkScale);

        width = Mathf.Abs(tlX - brX) * ScaleX * shrinkScale;
        height = Mathf.Abs(tlY - brY) * ScaleY * shrinkScale;
        currTransform.sizeDelta = new Vector2(width, height);
        boxCollider.size = new Vector3(width, height, boxSizeZ);
    }

    public IEnumerator SlowMove(Vector3 src, Vector3 dest)
    {
        Vector3 dist = dest - src;
        gameObject.transform.position = src;
        CanvasGroup alphaControl = gameObject.GetComponent<CanvasGroup>();
        for (float i = 0; i < 1; i += Time.deltaTime)
        {
            // Make it becomes visible soon
            alphaControl.alpha = i;
            yield return null;
        }
        for (float i = 0; i < 1; i += Time.deltaTime * 0.85f)
        {
            gameObject.transform.position = src + dist * i;
            yield return null;
        }
    }

    public void SetNoteInfo(string fName, int page, int id, string src, string cDate)
    {
        FileName = fName;
        Page = page;
        ID = id;
        Source = src;
        CreateDate = cDate;

        TextInfo.fontSize = 2;
        TextInfo.text = string.Format("<b>{0}</b>, <i>{1}</i>\nCreated on: {2}", FileName, Source, CreateDate);
        TextInfo.transform.localPosition = new Vector3(0, -height / 2 + 0.03f, 0);
    }

    //private IEnumerator fadeText()
    //{
    //    for (float i = 1; i >= 0; i -= Time.deltaTime)
    //    {
    //        TextInfo
    //    }
    //}
}
