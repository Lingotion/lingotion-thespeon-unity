// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

#if UNITY_EDITOR
using System.Collections.Generic;
using System;
using System.Linq;
using Lingotion.Thespeon.Core;
using Newtonsoft.Json;

namespace Lingotion.Thespeon.Inference
{

    public class CacheData
    {
        public Dictionary<string, Dictionary<string, int>> Data { get; set; }

        public CacheData(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                Data = new();
                return;
            }
            try
            {
                Data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json) ?? new Dictionary<string, Dictionary<string, int>>();
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error parsing data cache from JSON: {e.Message}");
                Data = new Dictionary<string, Dictionary<string, int>>();
            }
        }

        public CacheData(string moduleID, Dictionary<string, int> moduleData)
        {
            Data = new Dictionary<string, Dictionary<string, int>> { { moduleID, moduleData } };
        }

        public CacheData(Dictionary<string, Dictionary<string, int>> data)
        {
            Data = data ?? new();
        }

        public CacheData AddContent(CacheData other)
        {
            CacheData result = new(
                Data.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value != null ? new Dictionary<string, int>(kvp.Value) : new Dictionary<string, int>())
            );
            if (other?.Data == null) return result;
            foreach (var kvp in other.Data)
            {
                if (!result.Data.TryGetValue(kvp.Key, out var existingInner) || existingInner == null)
                {
                    result.Data[kvp.Key] = kvp.Value != null
                        ? new Dictionary<string, int>(kvp.Value)
                        : new Dictionary<string, int>();
                    continue;
                }
                if (kvp.Value == null) continue;
                foreach (var innerKvp in kvp.Value)
                {
                    if (existingInner.TryGetValue(innerKvp.Key, out var existingValue))
                    {
                        existingInner[innerKvp.Key] = existingValue + innerKvp.Value;
                    }
                    else
                    {
                        existingInner[innerKvp.Key] = innerKvp.Value;
                    }
                }
            }
            return result;
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(Data, Formatting.Indented);
        }
    }

    /// <summary>
    /// Possible cache keys along with Emotions enum names as lowercase strings.
    /// </summary>
    public enum DataCacheKeys
    {
        nbrSynths = 0, //intentional lower case
    }
    public static class InferenceEditorSignals
    {
        public static Action<CacheData> OnSynthesisDataSignal;
    }
}
#endif