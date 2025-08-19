using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;

public class ClickableWheelSegment : MonoBehaviour
{
    public Camera mainCamera;
    public RectTransform canvasRect; // Assigned in Inspector
    public InputConfig inputConfig; // Assigned in Inspector
    public GraphicRaycaster graphicRaycaster; // Assign the Canvas's GraphicRaycaster in Inspector
    public GameObject dropdown1; // Assign the dropdowns that should block clicks in Inspector
    public GameObject dropdown2; // Assign the dropdowns that should block clicks in Inspector

    private InputAction clickAction;
    private bool duringClick = false;

    private void OnEnable()
    {
        LoadClickAction();

        if (clickAction != null)
        {
            clickAction.Enable();
            clickAction.performed += OnClick;
        }
        else
        {
            Lingotion.Thespeon.Core.LingotionLogger.Error("Click action could not be found.");
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
            Lingotion.Thespeon.Core.LingotionLogger.Error("InputConfig or InputActionAsset is missing! Ensure it's assigned in the Inspector.");
        }

    }

    private IEnumerator ResetDuringClickFlag()
    {
        yield return new WaitForSeconds(0.5f);
        duringClick = false;
    }

    private void OnClick(InputAction.CallbackContext context)
    {
        // clickAction fires on both mouse down and mouse up, leading to double emotion annotations unless this is in place.
        if (duringClick) return;
        duringClick = true;
        StartCoroutine(ResetDuringClickFlag());
        if (Pointer.current == null) return;

        Vector2 screenPoint = Pointer.current.position.ReadValue();


        if (IsPointerOverDropdown(screenPoint))
        {
            return;
        }

        Vector2 worldPoint = ScreenToWorldPoint(screenPoint);

        Collider2D hitCollider = Physics2D.Raycast(worldPoint, Vector2.zero).collider;
        if (hitCollider != null)
        {
            RLETextAnnotator.Instance.EmotionClicked(hitCollider.gameObject.name);
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
                return true;
            }
        }

        return false;
    }

    private Vector3 ScreenToWorldPoint(Vector2 screenPoint)
    {
        Camera uiCamera = Camera.main;

        if (uiCamera == null)
        {
            Lingotion.Thespeon.Core.LingotionLogger.Error("UI Camera is null! Ensure the Canvas is set to 'Screen Space - Camera' and has a Camera assigned.");
            return Vector3.zero;
        }

        Vector3 worldPoint = uiCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, uiCamera.nearClipPlane));

        return worldPoint;
    }
}
