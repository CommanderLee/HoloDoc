using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SearchResultEntry : MonoBehaviour {

    public Text ResultTitle;
    public Text ResultAuthors;
    public Text ResultSource;
    public Button BtnPDF;

    private string pdfLink = "";
    private OnlineSearchManager parentManager = null;

	// Use this for initialization
	void Start () {
        //ResultTitle.text = "";
        //ResultAuthors.text = "";
        //ResultSource.text = "";
        parentManager = GetComponentInParent<OnlineSearchManager>();
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public void InitContent (ARDocument doc)
    {
        ResultTitle.text = doc.title;
        ResultAuthors.text = string.Join(", ", doc.authors);
        ResultSource.text = string.Format("<i>{0} - {1}</i>", doc.year, doc.source);
        BtnPDF.gameObject.SetActive(true);
        pdfLink = doc.pdfLink;
        BtnPDF.interactable = pdfLink.Length > 0;
        //Debug.Log("Init Content:" + ResultTitle.text + ResultTitle.font);
    }

    public void SetSimpleContent (string content)
    {
        ResultTitle.text = content;
        ResultAuthors.text = "";
        ResultSource.text = "";
        BtnPDF.gameObject.SetActive(false);
    }

    public void SelectPDFButton()
    {
        if (BtnPDF.interactable)
        {
            Debug.Log("Select PDF Button, " + gameObject.name + ", " + pdfLink);
            if (parentManager != null)
            {
                parentManager.StartPDFWindow(pdfLink);
            }
        }
    }
}
