// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections.Generic;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Builds the piecewise-linear keypoint curves that drive emotion blending, speed and loudness.
    /// Each segment contributes up to two keypoints - one at its first character and one at its last - placed on a
    /// global character position axis. Every segment boundary is then resampled from that curve, so values stay
    /// continuous across segment splits, are flat-held outside the outermost keypoints and may be discontinuous
    /// where one segment ends and the next begins at the same character.
    /// </summary>
    public static class KeypointUtils
    {
        private struct CurvePoint<TValue>
        {
            public int Position;
            public TValue Value;
        }

        /// <summary>
        /// Removes <see cref="Emotion.None"/>, clamps every weight to [0,1], drops non-positive weights and normalizes the rest to sum to 1.
        /// The blend is mutated in place.
        /// </summary>
        /// <param name="blend">The emotion blend to sanitize. May be null.</param>
        /// <returns>True if the blend holds at least one meaningful emotion after sanitization, false if it collapsed to empty ("no opinion").</returns>
        public static bool SanitizeEmotionBlend(Dictionary<Emotion, float> blend)
        {
            if (blend == null)
            {
                return false;
            }
            blend.Remove(Emotion.None);

            float weightSum = 0f;
            foreach (Emotion emotion in new List<Emotion>(blend.Keys))
            {
                float originalWeight = blend[emotion];
                float clampedWeight;
                if (float.IsNaN(originalWeight) || float.IsInfinity(originalWeight))
                {
                    clampedWeight = !float.IsNaN(originalWeight) && originalWeight > 0f ? 1f : 0f;
                }
                else
                {
                    clampedWeight = Math.Clamp(originalWeight, 0f, 1f);
                }

                if (!(Math.Abs(originalWeight - clampedWeight) < 1e-4f))
                {
                    LingotionLogger.Warning($"Emotion weight for {emotion} must be between 0 and 1. Clamping {originalWeight} to {clampedWeight}.");
                }

                if (clampedWeight <= 0f)
                {
                    blend.Remove(emotion);
                    continue;
                }
                blend[emotion] = clampedWeight;
                weightSum += clampedWeight;
            }

            if (weightSum <= 0f)
            {
                blend.Clear();
                return false;
            }

            foreach (Emotion emotion in new List<Emotion>(blend.Keys))
            {
                blend[emotion] /= weightSum;
            }
            return true;
        }

        /// <summary>
        /// Linearly interpolates between two emotion blends over the union of their keys, treating a missing key as weight 0.
        /// </summary>
        /// <param name="startBlend">The blend at alpha 0.</param>
        /// <param name="endBlend">The blend at alpha 1.</param>
        /// <param name="alpha">The interpolation factor, clamped to [0,1].</param>
        /// <returns>A new normalized blend.</returns>
        public static Dictionary<Emotion, float> InterpolateEmotionKeypoints(Dictionary<Emotion, float> startBlend, Dictionary<Emotion, float> endBlend, float alpha)
        {
            float clampedAlpha = Math.Clamp(alpha, 0f, 1f);
            Dictionary<Emotion, float> result = new();
            if (startBlend != null)
            {
                foreach (KeyValuePair<Emotion, float> pair in startBlend)
                {
                    result[pair.Key] = pair.Value * (1f - clampedAlpha);
                }
            }
            if (endBlend != null)
            {
                foreach (KeyValuePair<Emotion, float> pair in endBlend)
                {
                    result.TryGetValue(pair.Key, out float existing);
                    result[pair.Key] = existing + pair.Value * clampedAlpha;
                }
            }

            SanitizeEmotionBlend(result);
            return result;
        }

        /// <summary>
        /// Resamples every segment's start and end emotion blends from the curve formed by all supplied blends.
        /// </summary>
        /// <param name="segments">The segments to populate. Modified in place.</param>
        /// <param name="defaultEmotion">The fallback emotion used when no segment supplies a blend. <see cref="Emotion.None"/> means there is no valid fallback.</param>
        /// <returns>True on success, false if population failed and the request should be aborted.</returns>
        public static bool PopulateEmotionKeypoints<TSegment>(IReadOnlyList<TSegment> segments, Emotion defaultEmotion) where TSegment : ModelInputSegment
        {
            bool defaultValueIsValid = defaultEmotion != Emotion.None;
            Dictionary<Emotion, float> defaultBlend = new();
            if (defaultValueIsValid)
            {
                defaultBlend[defaultEmotion] = 1f;
            }

            return PopulateKeypointCurve<TSegment, Dictionary<Emotion, float>>(
                segments,
                segment => segment.StartEmotion,
                (segment, value) => segment.StartEmotion = value,
                segment => segment.EndEmotion,
                (segment, value) => segment.EndEmotion = value,
                SanitizeEmotionBlend,
                InterpolateEmotionKeypoints,
                ModelInputSegment.CopyBlend,
                defaultBlend,
                defaultValueIsValid,
                "emotion"
            );
        }

        /// <summary>
        /// Resamples every segment's start and end speed from the curve formed by all supplied speed values.
        /// </summary>
        /// <param name="segments">The segments to populate. Modified in place.</param>
        /// <returns>True on success, false if population failed and the request should be aborted.</returns>
        public static bool PopulateSpeedKeypoints<TSegment>(IReadOnlyList<TSegment> segments) where TSegment : ModelInputSegment
        {
            return PopulateKeypointCurve<TSegment, float>(
                segments,
                segment => segment.StartSpeed,
                (segment, value) => segment.StartSpeed = value,
                segment => segment.EndSpeed,
                (segment, value) => segment.EndSpeed = value,
                SanitizeScalar,
                InterpolateScalar,
                value => value,
                1f,
                true,
                "speed"
            );
        }

        /// <summary>
        /// Resamples every segment's start and end loudness from the curve formed by all supplied loudness values.
        /// </summary>
        /// <param name="segments">The segments to populate. Modified in place.</param>
        /// <returns>True on success, false if population failed and the request should be aborted.</returns>
        public static bool PopulateLoudnessKeypoints<TSegment>(IReadOnlyList<TSegment> segments) where TSegment : ModelInputSegment
        {
            return PopulateKeypointCurve<TSegment, float>(
                segments,
                segment => segment.StartLoudness,
                (segment, value) => segment.StartLoudness = value,
                segment => segment.EndLoudness,
                (segment, value) => segment.EndLoudness = value,
                SanitizeScalar,
                InterpolateScalar,
                value => value,
                1f,
                true,
                "loudness"
            );
        }

        private static bool SanitizeScalar(float value)
        {
            return true;
        }

        private static float InterpolateScalar(float start, float end, float alpha)
        {
            return start + (end - start) * Math.Clamp(alpha, 0f, 1f);
        }

        /// <summary>
        /// Generic engine shared by every keypoint curve type. Builds a piecewise-linear curve from each segment's
        /// start/end boundary values and resamples every segment's boundaries at its global character position.
        /// </summary>
        /// <param name="sanitize">Validates/clamps a value in place; returning false excludes it from the curve (used for emotion's "no opinion" state - scalar curves always return true).</param>
        /// <param name="interpolate">Blends between two curve values at an alpha in [0,1].</param>
        /// <param name="copy">Copies a value, so curve points and segments never alias the same instance.</param>
        /// <param name="defaultValue">Seeds a single keypoint at position 0 when the curve ends up empty.</param>
        /// <param name="defaultValueIsValid">Whether defaultValue may be used as that fallback; if false and the curve is empty, population fails.</param>
        /// <param name="curveName">Human-readable curve name used in log messages.</param>
        private static bool PopulateKeypointCurve<TSegment, TValue>(
            IReadOnlyList<TSegment> segments,
            Func<TSegment, TValue> getStart,
            Action<TSegment, TValue> setStart,
            Func<TSegment, TValue> getEnd,
            Action<TSegment, TValue> setEnd,
            Func<TValue, bool> sanitize,
            Func<TValue, TValue, float, TValue> interpolate,
            Func<TValue, TValue> copy,
            TValue defaultValue,
            bool defaultValueIsValid,
            string curveName
        ) where TSegment : ModelInputSegment
        {
            List<CurvePoint<TValue>> curve = new();
            List<int> segmentStartPositions = new(segments.Count);
            List<int> segmentEndPositions = new(segments.Count);

            int globalPosition = 0;
            foreach (TSegment segment in segments)
            {
                if (string.IsNullOrEmpty(segment.Text))
                {
                    LingotionLogger.Error($"Cannot populate {curveName} keypoints for an empty segment.");
                    return false;
                }

                int startPosition = globalPosition;
                int endPosition = startPosition + segment.Text.Length - 1;
                segmentStartPositions.Add(startPosition);
                segmentEndPositions.Add(endPosition);

                if (sanitize(getStart(segment)))
                {
                    curve.Add(new CurvePoint<TValue> { Position = startPosition, Value = copy(getStart(segment)) });
                }
                if (sanitize(getEnd(segment)))
                {
                    curve.Add(new CurvePoint<TValue> { Position = endPosition, Value = copy(getEnd(segment)) });
                }

                globalPosition += segment.Text.Length;
            }

            if (curve.Count == 0)
            {
                if (!defaultValueIsValid)
                {
                    LingotionLogger.Error($"No {curveName} keypoints were supplied and no valid default value was given.");
                    return false;
                }
                curve.Add(new CurvePoint<TValue> { Position = 0, Value = copy(defaultValue) });
            }

            for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                setStart(segments[segmentIndex], copy(SampleCurve(curve, segmentStartPositions[segmentIndex], false, interpolate)));
                setEnd(segments[segmentIndex], copy(SampleCurve(curve, segmentEndPositions[segmentIndex], true, interpolate)));
            }

            return true;
        }

        /// <summary>
        /// Samples a piecewise-linear curve at a global character position.
        /// Positions outside the curve are flat-held. When the position lands exactly on a keypoint and two coincident
        /// keypoints share it (a discontinuity between adjacent segments), the left one is returned unless sampleRightSide is set.
        /// </summary>
        private static TValue SampleCurve<TValue>(List<CurvePoint<TValue>> curve, int position, bool sampleRightSide, Func<TValue, TValue, float, TValue> interpolate)
        {
            if (position < curve[0].Position)
            {
                return curve[0].Value;
            }
            if (position > curve[^1].Position)
            {
                return curve[^1].Value;
            }

            int firstAtOrAfter = 0;
            while (firstAtOrAfter < curve.Count && curve[firstAtOrAfter].Position < position)
            {
                firstAtOrAfter++;
            }
            if (firstAtOrAfter < curve.Count && curve[firstAtOrAfter].Position == position)
            {
                int lastAtPosition = firstAtOrAfter;
                while (lastAtPosition + 1 < curve.Count && curve[lastAtPosition + 1].Position == position)
                {
                    lastAtPosition++;
                }
                return curve[sampleRightSide ? lastAtPosition : firstAtOrAfter].Value;
            }

            CurvePoint<TValue> startPoint = curve[firstAtOrAfter - 1];
            CurvePoint<TValue> endPoint = curve[firstAtOrAfter];
            float alpha = (position - startPoint.Position) / (float)(endPoint.Position - startPoint.Position);
            return interpolate(startPoint.Value, endPoint.Value, alpha);
        }
    }
}
