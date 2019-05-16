using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SearchNavButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

    private Image imgPanel;
    private Button btn;

    private const float PRESS_FADE_TIME = 0.3f;
	// Use this for initialization
	void Start () {
        imgPanel = gameObject.GetComponentInChildren<Image>();
        btn = gameObject.GetComponent<Button>();

        btn.onClick.AddListener(OnSelect);
        
	}
	
	// Update is called once per frame
	void Update () {
		
	}
    
    //public void OnGazeEnter()
    //{
    //    //btnImg.color = btn.colors.highlightedColor;
    //    Debug.Log("Gaze Enter:" + gameObject.name);
    //}

    //public void OnGazeExit()
    //{
    //    //btnImg.color = btn.colors.normalColor;
    //    Debug.Log("Gaze Exit:" + gameObject.name);
    //}

    public void OnSelect()
    {
        Debug.Log("On Select:" + gameObject.name);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("OnPointerEnter:" + gameObject.name);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("OnPointerExit:" + gameObject.name);
    }
}
