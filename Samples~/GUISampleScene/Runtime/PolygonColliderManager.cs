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
    private Dictionary<Transform, Vector3> originalColliderPositions = new();
    private Dictionary<Transform, Vector3> originalColliderScales = new();

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

        Physics2D.SyncTransforms();
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
            DetectAndResize();
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
        if (BuildPipeline.isBuildingPlayer) return;
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += DetectAndResize;
        }
    }
    #endif

    private void DetectAndResize()
    {
        if (BuildPipeline.isBuildingPlayer) return;
        if (rectTransform == null) return;

        Vector2 currentSize = rectTransform.rect.size;
        Vector3 currentLossyScale = rectTransform.lossyScale;

        if (currentSize != lastSize || currentLossyScale != lastLossyScale)
        {
            Vector2 scaleFactor = new(
                currentSize.x / lastSize.x,
                currentSize.y / lastSize.y
            );
            if(scaleFactor.x==Mathf.Infinity || scaleFactor.y==Mathf.Infinity) return;
            ResizeColliders(scaleFactor);
            lastSize = currentSize;
            lastLossyScale = currentLossyScale;
        }
    }

    private void Initialize()
    {
        rectTransform = GetComponent<RectTransform>();
        lastSize = rectTransform.rect.size;
        lastLossyScale = rectTransform.lossyScale;
    }

    private void ResizeColliders(Vector2 scaleFactor)
    {
        if(this==null) return;
        foreach (PolygonCollider2D poly in GetComponentsInChildren<PolygonCollider2D>())
        {
            Transform colliderTransform = poly.transform;
            Vector3 newScale = new(
                colliderTransform.localScale.x * scaleFactor.x,
                colliderTransform.localScale.y * scaleFactor.y,
                1
            );
            colliderTransform.localScale = newScale;

            Vector3 newPosition = new(
                colliderTransform.localPosition.x * scaleFactor.x,
                colliderTransform.localPosition.y * scaleFactor.y,
                colliderTransform.localPosition.z
            );
            colliderTransform.localPosition = newPosition;

            Physics2D.SyncTransforms();
        }
    }

}
#endif