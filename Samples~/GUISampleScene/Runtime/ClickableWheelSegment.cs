using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;


public class ClickableWheelSegment : MonoBehaviour
{
    public Camera mainCamera;
    public RectTransform canvasRect; // Assigned in Inspector
    public InputConfig inputConfig; // Assigned in Inspector
    public GraphicRaycaster graphicRaycaster; // Assign the Canvas's GraphicRaycaster in Inspector
    public GameObject dropdown1; // Assign the dropdowns that should block clicks in Inspector
    public GameObject dropdown2; // Assign the dropdowns that should block clicks in Inspector

    private InputAction clickAction; // Click action retrieved dynamically

    private void OnEnable()
    {
        LoadClickAction(); // Load input action dynamically

        if (clickAction != null)
        {
            clickAction.Enable();
            clickAction.performed += OnClick;
        }
        else
        {
            Debug.LogError("Click action could not be found.");
        }
    }

    private void OnDisable()
    {
        if (clickAction != null)
        {
            clickAction.performed -= OnClick;
            clickAction.Disable();
        }
    }

    private void LoadClickAction()
    {
        
        if (inputConfig != null && inputConfig.inputActions != null)
        {
            clickAction = inputConfig.inputActions.FindAction("UI/Click", true);
            clickAction.Enable();
        }
        else
        {
            Debug.LogError("InputConfig or InputActionAsset is missing! Ensure it's assigned in the Inspector.");
        }

    }

    private void OnClick(InputAction.CallbackContext context) 
    {
        if (Pointer.current == null) return;

        Vector2 screenPoint = Pointer.current.position.ReadValue(); // Get pointer position


        // 🔹 Check if the dropdowns are in the path of the click
        if (IsPointerOverDropdown(screenPoint))
        {
            return;
        }

        Vector2 worldPoint = ScreenToWorldPoint(screenPoint); // Convert UI position to world space

        // Check if the point overlaps with any Collider2D
        Collider2D hitCollider = Physics2D.Raycast(worldPoint, Vector2.zero).collider;
        if (hitCollider != null)
        {
            // Debug.Log("Clicked on: " + hitCollider.gameObject.name);

            TextStyler.Instance.buttonClicked(hitCollider.gameObject.name);
        }
    }
    private bool IsPointerOverDropdown(Vector2 screenPosition)
    {
        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        graphicRaycaster.Raycast(pointerEventData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == dropdown1 || result.gameObject == dropdown2)
            {
                return true; // Pointer is over one of the dropdowns
            }
        }

        return false;
    }


    private Vector3 ScreenToWorldPoint(Vector2 screenPoint)
    {
        // 🔥 Make sure to get the UI Camera (since Canvas is in "Screen Space - Camera")
        Camera uiCamera = Camera.main; // Or assign your UI camera if different

        if (uiCamera == null)
        {
            Debug.LogError("UI Camera is null! Ensure the Canvas is set to 'Screen Space - Camera' and has a Camera assigned.");
            return Vector3.zero;
        }

        // 🔹 Convert Screen Point to World Point
        Vector3 worldPoint = uiCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, uiCamera.nearClipPlane));

        return worldPoint;
    }
    private Vector2 OldScreenToWorldPoint(Vector2 screenPoint)
    {
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out localPoint))
        {
            return canvasRect.TransformPoint(localPoint); // Convert to world position
        }
        return Vector2.zero;
    }
    // public void OnPointerDown(PointerEventData eventData)
    // {
        
    //     if (TextStyler.Instance != null)
    //     {
    //         Debug.Log("-----------Button clicked: " + gameObject.name + "-----------");
    //         TextStyler.Instance.buttonClicked(gameObject.name);
    //     }
    //     else
    //     {
    //         Debug.LogWarning("TextStyler instance not found!");
    //     } 

    // }
}
