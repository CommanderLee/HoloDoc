using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CursorManager class takes Cursor GameObjects.
/// One that is on Holograms and another off Holograms.
/// Shows the appropriate Cursor when a Hologram is hit.
/// Places the appropriate Cursor at the hit position.
/// Matches the Cursor normal to the hit surface.
/// </summary>
public class CursorManager : MySingleton<CursorManager>
{
    [Tooltip("Drag the Cursor object to show when it hits a hologram.")]
    public GameObject CursorOnHolograms;

    [Tooltip("Drag the Cursor object to show when it does not hit a hologram.")]
    public GameObject CursorOffHolograms;

    void Awake()
    {
        if (CursorOnHolograms == null || CursorOffHolograms == null)
        {
            return;
        }

        // Hide the Cursors to begin with.
        CursorOnHolograms.SetActive(false);
        CursorOffHolograms.SetActive(false);

        SetLayerCollisions();
    }

    // This is important so our interactible objects don't collide with each other
    // when we change their sizes using gestures.
    private static void SetLayerCollisions()
    {
        int maxLayers = 31;
        // To protect apps that don't have an Interactible layer in their project.
        int interactibleLayer = LayerMask.NameToLayer("Interactible");

        if (interactibleLayer < 0 || interactibleLayer > maxLayers)
        {
            return;
        }

        // Ignore all collisions with UI except for Cursor collisions.
        // Unity has 31 possible layers.  There is no way to get this value in code.
        for (int i = 0; i < maxLayers; i++)
        {
            // Ensure the Interactible objects do not collide with other layers.
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Interactible"), i, true);
        }

        // Ensures the Cursor can collide with the Interactible objects only.
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Interactible"), LayerMask.NameToLayer("Cursor"), false);
    }

    void Update()
    {
        if (GazeManager.Instance == null || CursorOnHolograms == null || CursorOffHolograms == null)
        {
            return;
        }

        if (GazeManager.Instance.Hit)
        {
            GameObject focusedObject = GazeManager.Instance.HitInfo.collider.gameObject;
            if (focusedObject.name == "CanvasBoard")
            {
                CursorOffHolograms.SetActive(true);
                CursorOnHolograms.SetActive(false);
            }
            else
            {
                //Debug.Log(focusedObject.name);
                CursorOnHolograms.SetActive(true);
                CursorOffHolograms.SetActive(false);
            }

            // Move the cursor to the point where the raycast hit.
            gameObject.transform.position = GazeManager.Instance.Position;

            // Rotate the cursor to hug the surface of the hologram.
            gameObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, GazeManager.Instance.Normal);
        }
        else
        {
            CursorOffHolograms.SetActive(false);
            CursorOnHolograms.SetActive(false);
        }

        //// Place the cursor at the calculated position.
        //gameObject.transform.position = GazeManager.Instance.Position;

        //// Reorient the cursors to match the hit object normal.
        //CursorOnHolograms.transform.parent.transform.up = GazeManager.Instance.Normal;
    }
}
