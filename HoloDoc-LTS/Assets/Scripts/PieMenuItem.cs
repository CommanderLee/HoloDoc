using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PieMenuItem : MonoBehaviour {

    public Image    imageBkg;
    public bool clickable = true;

    public GameObject PanelIconText;
    public Image    ItemIcon;
    /* 
     * For PieMenuItem, the following values are decided while being created by the PieMenu class
     * So the initial value in the Unity would be ignored.
     */ 
    /// <summary>
    /// Text Object displaying the name.
    /// Placed at the middle of the pie menu.
    /// </summary>
    public Text     ItemText;
    public float    radius;

    /// <summary>
    /// The distance between the icon&text block center to the canvas item object center.
    /// Calculated by: 0.03 (box dimension) / sqrt(2) + 0.02 (inner button radius).
    /// </summary>
    private const float CANVAS_ITEM_RADIUS = 0.04121f;
    /// <summary>
    /// The sector is pushed outwards for this distance, at the direction of its angle bisector.
    /// </summary>
    private const float DRIFT_DIST = 0.001f;

    /// <summary>
    /// Angle of the sector [0,360]. Set by Init function.
    /// </summary>
    private float   fillAngle;
    /// <summary>
    /// Eular angle of the start point, anti-clockwise, count from x+ to fit the math formular.
    /// Set when initialization, and after spinning.
    /// </summary>
    public float    RotationStartZ;
    /// <summary>
    /// This value are real-time set to the actual bisector. 
    /// While the RotationStartZ is kind of "official" and changes only to discrete values.
    /// Could be set during spinning.
    /// </summary>
    private float   bisectorAngle;

    private const float fontScale = 0.05f;

    // Use this for initialization
    void Start () {
        if (imageBkg == null || ItemText == null)
        {
            Debug.Log("Error in PieMenuItem: Image and Text GameObject not linked.");
            return;
        }
        //image.sprite = Resources.Load("circle") as Sprite;
        //image.type = Image.Type.Filled;
        //image.rectTransform.localPosition = Vector3.zero;
    }
	
	// Update is called once per frame
	void Update () {
		
	}

    /// <summary>
    /// In order to leave blank spaces between sectors, we push the sectors outside for a bit.
    /// This part was adjusted in the PieMenu class, by setting the parent transform.
    /// </summary>
    /// <param name="rad"></param>
    /// <param name="startAngle"></param>
    /// <param name="eachAngle"></param>
    /// <param name="defaultColor"></param>
    public void SetShape(float rad, float startAngle, float eachAngle, Color defaultColor)
    {
        radius = rad;
        RotationStartZ = startAngle;
        fillAngle = eachAngle;

        // Fill the background (Update: now the blank spaces are handled in PieMenu.InitPieMenu)
        imageBkg.fillAmount = fillAngle / 360.0f;
        imageBkg.color = defaultColor;

        // Set Icon & Text position: use a separate method here, make it easier for spinning function
        MoveToAngle(RotationStartZ);

        Debug.Log(string.Format("Adjust image for PieMenuItem, angle [{0}, {1}], sprite {2}, color {3}, local position: {4}, global: {5}", 
            RotationStartZ, RotationStartZ + fillAngle, imageBkg.sprite.name, imageBkg.color.ToString(), imageBkg.transform.localPosition, imageBkg.transform.position));
    }

    public void MoveToAngle(float startAxis)
    {
        bisectorAngle = startAxis + fillAngle * 0.5f;
        // Move root object (outside according to the bisector, make space), Move background image (sector shape), Move icon & text local position.
        gameObject.transform.localPosition = new Vector3(Mathf.Cos(bisectorAngle * Mathf.Deg2Rad), Mathf.Sin(bisectorAngle * Mathf.Deg2Rad), 0) * DRIFT_DIST;
        imageBkg.transform.localEulerAngles = new Vector3(0, 0, startAxis);
        PanelIconText.gameObject.transform.localPosition = new Vector3(Mathf.Cos(bisectorAngle * Mathf.Deg2Rad),
            Mathf.Sin(bisectorAngle * Mathf.Deg2Rad), 0) * CANVAS_ITEM_RADIUS;
    }

    public void SetText(string pName, int defaultSize)
    {
        ItemText.text = pName;
        ItemText.fontSize = defaultSize;
        //Debug.Log(string.Format("Create text for PieMenuItem, local position:{0}, global {1}, text: {2}, font: {3}-{4}",
        //    text.transform.localPosition, text.transform.position, text.text, text.fontSize, text.color));
    }

    public void SetIcon(Sprite icon)
    {
        ItemIcon.sprite = icon;
        //Debug.Log(string.Format("Create icon for PieMenuItem, local position:{0}, global {1}, text position: {2}/{3}",
        //    imageIcon.transform.localPosition, imageIcon.transform.position, text.transform.localPosition, text.transform.position));
    }

    public override string ToString()
    {
        return string.Format("Root position:{0}/{1}, Image position:{2}/{3}, local euler:{4}, size:{5}, Text position:{6}/{7}, size:{8}, scale:{9}/{10}",
            gameObject.transform.localPosition, gameObject.transform.position, imageBkg.transform.localPosition, imageBkg.transform.position,
            imageBkg.transform.localEulerAngles, imageBkg.rectTransform.sizeDelta, ItemText.transform.localPosition, ItemText.transform.position, ItemText.fontSize, gameObject.transform.localScale, gameObject.transform.lossyScale);
    }
}
