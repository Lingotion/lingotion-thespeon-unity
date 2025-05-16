// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using UnityEngine;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.Linq;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using Lingotion.Thespeon.API;



namespace Lingotion.Thespeon.API

{
    /// <summary>
    /// Represents the tags associated with an actor.
    /// </summary>
    [Serializable]
    public class ActorTags
    {
        #nullable enable
        [JsonProperty("quality")]
        [JsonConverter(typeof(StringEnumConverter))]
        public Quality? quality { get; set; }
        #nullable disable

        /// <summary>
        /// Initializes a new instance of the ActorTags class.
        /// </summary>
        public ActorTags(){} // Default constructor for JSON deserialization

        /// <summary>
        /// Initializes a new instance of the ActorTags class with the specified quality.
        /// </summary>
        /// <param name="quality">The quality of the model.</param>
        public ActorTags(string quality)

        {
            if(quality == null)
            {
                this.quality = null; // or set to a default value
                return;
            }
            if (Enum.TryParse(typeof(Quality), quality, true, out var parsedQuality))
            {
                this.quality = (Quality)parsedQuality;
            }
            else
            {
                Debug.LogError($"Invalid quality value: {quality}");
                this.quality = null; // or set to a default value
            }
        }

        /// <summary>
        /// Enumeration representing the quality of the models.
        /// This is used to categorize the models based on their quality.
        /// </summary>
        public enum Quality
        {
            Ultralow,
            Low,
            Mid,
            High,
            Ultrahigh
        }

        /// <summary>
        /// Returns a string representation of the ActorTags object.
        /// </summary>
        public override string ToString()
        {
            return $"quality: {quality}";
        }

        /// <summary>
        /// Compares this ActorTags object with another object for equality.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is ActorTags tags &&
                   quality == tags.quality;
        }

        /// <summary>
        /// Returns the hash code for this ActorTags object.
        /// </summary>
        /// <returns>A hash code for the current ActorTags object.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(quality);
        }
    }
}
