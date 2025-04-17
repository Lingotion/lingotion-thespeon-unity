using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace Lingotion.Thespeon.CurvesToUI
{
    [ExecuteAlways]
    public class CurvesToUI : MonoBehaviour
    {
        [Header("Curves")]
        public AnimationCurve speedCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
        public AnimationCurve loudnessCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));

        [Header("Graph Settings")]
        public RectTransform graphArea; // UI space for rendering the graph
        public LineRenderer speedRenderer; // First curve
        public LineRenderer loudnessRenderer; // Second curve
        public int resolution = 50; // Number of points to sample
        private int lastHash;
        public event Action OnCurvesChanged;


        private void Start()
        {
            lastHash = GetCurvesHash();
        }
        private void Update()
        {
            
            if (graphArea != null)
            {
                UpdateCurveDisplay(speedRenderer, speedCurve);
                UpdateCurveDisplay(loudnessRenderer, loudnessCurve);
            }
            int currentHash = GetCurvesHash();
            if (currentHash != lastHash)
            {
                lastHash = currentHash;
                OnCurvesChanged?.Invoke();
            }

        }

        void UpdateCurveDisplay(LineRenderer lineRenderer, AnimationCurve curve)
        {
            if (lineRenderer == null || curve == null)
                return;

            Vector3[] positions = new Vector3[resolution];

            for (int i = 0; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1); // Normalized time (0 to 1)
                float curveValue = curve.Evaluate(t) * 0.5f; // Get value from AnimationCurve

                // Directly use local UI coordinates
                float xPos = Mathf.Lerp(0, graphArea.rect.width, t);
                float yPos = Mathf.Lerp(0, graphArea.rect.height, curveValue);

                // Assign local positions instead of world space
                positions[i] = new Vector3(xPos, yPos, 0);
            }

            lineRenderer.positionCount = resolution;
            lineRenderer.SetPositions(positions);
        }
        public void SetAnimationCurve(List<double> values, string index)
        {
            AnimationCurve curve = new AnimationCurve();
            for (int i = 0; i < values.Count; i++)
            {
                curve.AddKey(new Keyframe(i / (float)(values.Count - 1), (float) values[i]));
            }
            if (index == "speed")
            {
                speedCurve = curve;
            }
            else if (index == "loudness")
            {
                loudnessCurve = curve;
            }
        }


        public int GetCurvesHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + GetCurveHash(speedCurve);
                hash = hash * 31 + GetCurveHash(loudnessCurve);
                return hash;
            }
        }

        private int GetCurveHash(AnimationCurve curve)
        {
            if (curve == null) return 0;
            int hash = curve.length;
            foreach (var key in curve.keys)
            {
                hash = hash * 31 + key.time.GetHashCode();
                hash = hash * 31 + key.value.GetHashCode();
                hash = hash * 31 + key.inTangent.GetHashCode();
                hash = hash * 31 + key.outTangent.GetHashCode();
            }
            return hash;
        }


    }
}