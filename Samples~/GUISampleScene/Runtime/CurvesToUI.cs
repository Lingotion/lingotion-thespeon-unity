using System;
using UnityEngine;


namespace Lingotion.Thespeon.CurvesToUI
{
    [ExecuteAlways]
    public class CurvesToUI : MonoBehaviour
    {
        [Header("Curves")]
        public AnimationCurve speedCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
        public AnimationCurve loudnessCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));

        [Header("Graph Settings")]
        public RectTransform graphArea;
        public LineRenderer speedRenderer;
        public LineRenderer loudnessRenderer;
        public int resolution = 50;
        private int lastHash;
        public event Action OnCurvesChanged;


        void Start()
        {
            lastHash = GetCurvesHash();
        }
        void Update()
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

        private void UpdateCurveDisplay(LineRenderer lineRenderer, AnimationCurve curve)
        {
            if (lineRenderer == null || curve == null)
                return;

            Vector3[] positions = new Vector3[resolution];

            for (int i = 0; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1);
                float curveValue = curve.Evaluate(t) * 0.5f;

                float xPos = Mathf.Lerp(0, graphArea.rect.width, t);
                float yPos = Mathf.Lerp(0, graphArea.rect.height, curveValue);

                positions[i] = new Vector3(xPos, yPos, 0);
            }

            lineRenderer.positionCount = resolution;
            lineRenderer.SetPositions(positions);
        }
        public void SetAnimationCurve(AnimationCurve curve, string index)
        {
            if (index == "speed")
            {
                speedCurve=new AnimationCurve();
                foreach(Keyframe kf in curve.keys)
                {
                    speedCurve.AddKey(kf);
                }
            }
            else if (index == "loudness")
            {
                loudnessCurve=new AnimationCurve();
                foreach(Keyframe kf in curve.keys)
                {
                    loudnessCurve.AddKey(kf);
                }
            }
        }

        private int GetCurvesHash()
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