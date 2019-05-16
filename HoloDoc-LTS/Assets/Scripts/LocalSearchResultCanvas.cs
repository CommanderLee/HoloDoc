using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LocalSearchResultCanvas : MonoBehaviour {

    public Text TextTitle;
    public Text TextAuthors;
    public Text TextSource;
    public Text TextExtraInfo;

    private GameObject hololensCamera = null;
    private GameObject rootObject = null;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        if (rootObject != null)
        {
            gameObject.transform.position = rootObject.transform.position;
            gameObject.transform.Translate(rootObject.transform.right * 0.1f, Space.World);
        }
        if (hololensCamera != null)
        {
            gameObject.transform.localEulerAngles = new Vector3(hololensCamera.transform.localEulerAngles.x, hololensCamera.transform.localEulerAngles.y, 0);
        }
	}

    public void InitContent (ARDocument doc, string words, GameObject cam)
    {
        TextTitle.text = doc.title;
        TextAuthors.text = string.Join(", ", doc.authors);
        TextSource.text = string.Format("{0} - {1}", doc.year, doc.source);
        TextExtraInfo.text = string.Format("keyword(s) <i>{0}</i> appeared <b>{1}</b> times", words, doc.extraInfo);
        hololensCamera = cam;
        rootObject = null;
        if (PageOrganizer.DictPrintedDocObjects.ContainsKey(doc.filename))
            rootObject = PageOrganizer.DictPrintedDocObjects[doc.filename];
    }

    public void InitTagContent(ARDocument doc, string words, GameObject cam)
    {
        TextTitle.text = doc.title;
        TextAuthors.text = string.Join(", ", doc.authors);
        TextSource.text = string.Format("{0} - {1}", doc.year, doc.source);
        TextExtraInfo.text = string.Format("Tag: <i>{0}</i>", words);
        hololensCamera = cam;
        rootObject = null;
        if (PageOrganizer.DictPrintedDocObjects.ContainsKey(doc.filename))
            rootObject = PageOrganizer.DictPrintedDocObjects[doc.filename];
    }
}
