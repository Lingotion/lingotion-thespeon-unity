#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
[RequireComponent(typeof(RectTransform))]
public class PolygonColliderManager : MonoBehaviour
{
    private RectTransform rectTransform;
    private Vector2 lastSize;
    private Vector3 lastLossyScale;
    private Dictionary<Transform, Vector3> originalColliderPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Vector3> originalColliderScales = new Dictionary<Transform, Vector3>();

    void Awake()
    {
        StoreOriginalColliderStates();
        Initialize();
    }
    void StoreOriginalColliderStates()
    {
        originalColliderPositions.Clear();
        originalColliderScales.Clear();

        foreach (PolygonCollider2D poly in GetComponentsInChildren<PolygonCollider2D>())
        {
            Transform colliderTransform = poly.transform;
            originalColliderPositions[colliderTransform] = colliderTransform.localPosition;
            originalColliderScales[colliderTransform] = colliderTransform.localScale;
        }
    }

    void RestoreOriginalColliderStates()
    {
        foreach (var entry in originalColliderPositions)
        {
            if (entry.Key != null)
            {
                entry.Key.localPosition = entry.Value;
            }
        }

        foreach (var entry in originalColliderScales)
        {
            if (entry.Key != null)
            {
                entry.Key.localScale = entry.Value;
            }
        }

        Physics2D.SyncTransforms(); // Ensure physics updates properly
    }


    void Start() => Initialize();

    #if UNITY_EDITOR
    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            RestoreOriginalColliderStates();
        }
        EditorApplication.update += EditorUpdate;

    }
    void OnDisable() => EditorApplication.update -= EditorUpdate;

    void EditorUpdate()
    {
        if (!Application.isPlaying)
        {
            DetectAndResize(); // Wait for UI changes to settle
        }
    }
    #endif

    void OnValidate()
    {
        Initialize();
        DetectAndResize();
    }

    #if UNITY_EDITOR
    void OnRectTransformDimensionsChange()
    {
        if (BuildPipeline.isBuildingPlayer) return; // Skip execution during build
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += DetectAndResize; // Wait for UI changes to settle
        }
    }
    #endif

    void DetectAndResize()
    {
        if (BuildPipeline.isBuildingPlayer) return;
        if (rectTransform == null) return;

        Vector2 currentSize = rectTransform.rect.size;
        Vector3 currentLossyScale = rectTransform.lossyScale;

        if (currentSize != lastSize || currentLossyScale != lastLossyScale)
        {
            Vector2 scaleFactor = new Vector2(
                currentSize.x / lastSize.x,
                currentSize.y / lastSize.y
            );
            if(scaleFactor.x==Mathf.Infinity || scaleFactor.y==Mathf.Infinity) return;
            ResizeColliders(scaleFactor);
            lastSize = currentSize;
            lastLossyScale = currentLossyScale;
        }
    }

    void Initialize()
    {
        rectTransform = GetComponent<RectTransform>();
        lastSize = rectTransform.rect.size;
        lastLossyScale = rectTransform.lossyScale;
    }

    void ResizeColliders(Vector2 scaleFactor)
    {
        // if (!Application.isPlaying) return; // Prevents resizing in Edit mode
        if(this==null) return;
        foreach (PolygonCollider2D poly in GetComponentsInChildren<PolygonCollider2D>())
        {
            Transform colliderTransform = poly.transform;
            
            // Scale the Transform
            Vector3 newScale = new Vector3(
                colliderTransform.localScale.x * scaleFactor.x,
                colliderTransform.localScale.y * scaleFactor.y,
                1 // Z remains unchanged
            );
            colliderTransform.localScale = newScale;

            // Adjust position to maintain relative placement
            Vector3 newPosition = new Vector3(
                colliderTransform.localPosition.x * scaleFactor.x,
                colliderTransform.localPosition.y * scaleFactor.y,
                colliderTransform.localPosition.z
            );
            colliderTransform.localPosition = newPosition;

            // Force Unity to refresh physics calculations
            Physics2D.SyncTransforms();
        }
    }

}
#endif