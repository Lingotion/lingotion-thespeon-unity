// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System.Collections.Generic;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Specifies the type of synthesis callback a data packet represents.
    /// </summary>
    public enum SynthCallbackType
    {
        /// <summary>Unspecified callback type.</summary>
        CB_UNSPECIFIED = 0,
        /// <summary>Error callback indicating synthesis failure.</summary>
        CB_ERROR = 1,
        /// <summary>Audio data callback containing synthesized audio samples.</summary>
        CB_AUDIO = 2,
        /// <summary>Trigger sample callback containing requested audio sample indices.</summary>
        CB_TRIGGERSAMPLE = 3
    }

    /// <summary>
    /// Specifies the data type stored in a <see cref="PacketPayload"/>.
    /// </summary>
    public enum PacketPayloadType
    {
        /// <summary>No payload data.</summary>
        None,
        /// <summary>Payload contains a float array (e.g. audio samples).</summary>
        FloatArray,
        /// <summary>Payload contains a long array (e.g. trigger sample indices).</summary>
        Int64Array,
        /// <summary>Payload contains a string (e.g. error message).</summary>
        String
    }

    /// <summary>
    /// Well-known metadata key constants used in <see cref="ThespeonDataPacket"/> metadata dictionaries.
    /// </summary>
    public static class CommonMetadataKeys
    {
        /// <summary>
        /// Metadata key for the synthesis session identifier.
        /// </summary>
        public const string SessionID = "session_id";
    }

    /// <summary>
    /// A type-safe, immutable payload container for <see cref="ThespeonDataPacket"/>.
    /// Holds exactly one typed value (float[], long[], or string) determined at creation time.
    /// </summary>
    public readonly struct PacketPayload
    {
        /// <summary>
        /// The type of data stored in this payload.
        /// </summary>
        public PacketPayloadType Type { get; }
        private readonly float[] _floats;
        private readonly long[] _longs;
        private readonly string _text;

        private PacketPayload(PacketPayloadType type, float[] floats, long[] longs, string text)
        {
            Type = type;
            _floats = floats;
            _longs = longs;
            _text = text;
        }

        /// <summary>
        /// Creates a payload containing a float array.
        /// </summary>
        /// <param name="v">The float array to store.</param>
        public static PacketPayload Create(float[] v) => new(PacketPayloadType.FloatArray, v, null, null);
        /// <summary>
        /// Creates a payload containing a long array.
        /// </summary>
        /// <param name="v">The long array to store.</param>
        public static PacketPayload Create(long[] v)  => new(PacketPayloadType.Int64Array, null, v, null);
        /// <summary>
        /// Creates a payload containing a string.
        /// </summary>
        /// <param name="v">The string to store.</param>
        public static PacketPayload Create(string v)  => new(PacketPayloadType.String, null, null, v);
        /// <summary>
        /// Creates an empty payload with no data.
        /// </summary>
        public static PacketPayload CreateEmpty()  => new(PacketPayloadType.None, null, null, null);

        /// <summary>
        /// Attempts to retrieve the payload as a float array.
        /// </summary>
        /// <param name="v">When this method returns, contains the float array if the payload type is <see cref="PacketPayloadType.FloatArray"/>; otherwise, null.</param>
        /// <returns>True if the payload contains a float array; otherwise, false.</returns>
        public bool TryGet(out float[] v)
        {
            v = Type == PacketPayloadType.FloatArray ? _floats : null;
            return v != null;
        }
        /// <summary>
        /// Attempts to retrieve the payload as a long array.
        /// </summary>
        /// <param name="v">When this method returns, contains the long array if the payload type is <see cref="PacketPayloadType.Int64Array"/>; otherwise, null.</param>
        /// <returns>True if the payload contains a long array; otherwise, false.</returns>
        public bool TryGet(out long[] v)
        {
            v = Type == PacketPayloadType.Int64Array ? _longs : null;
            return v != null;
        }
        /// <summary>
        /// Attempts to retrieve the payload as a string.
        /// </summary>
        /// <param name="v">When this method returns, contains the string if the payload type is <see cref="PacketPayloadType.String"/>; otherwise, null.</param>
        /// <returns>True if the payload contains a string; otherwise, false.</returns>
        public bool TryGet(out string v)
        {
            v = Type == PacketPayloadType.String ? _text : null;
            return v != null;
        }
    }

    /// <summary>
    /// Specifies the data type stored in a <see cref="PacketMetadataValue"/>.
    /// </summary>
    public enum MetadataType { Int64, Float, Bool, String }

    /// <summary>
    /// A type-safe, immutable container for a single metadata value in a <see cref="ThespeonDataPacket"/>.
    /// Holds exactly one typed value (long, float, bool, or string) determined at creation time.
    /// </summary>
    public readonly struct PacketMetadataValue
    {
        /// <summary>
        /// The type of data stored in this metadata value.
        /// </summary>
        public MetadataType Type { get; }
        private readonly long _i64;
        private readonly float _f;
        private readonly bool _b;
        private readonly string _s;

        private PacketMetadataValue(MetadataType type, long i64, float f, bool b, string s)
        {
            Type = type;
            _i64 = i64;
            _f = f;
            _b = b;
            _s = s;
        }

        /// <summary>Creates a metadata value containing a long integer.</summary>
        /// <param name="v">The long value to store.</param>
        public static PacketMetadataValue Create(long v)   => new(MetadataType.Int64, v, 0, false, null);
        /// <summary>Creates a metadata value containing a float.</summary>
        /// <param name="v">The float value to store.</param>
        public static PacketMetadataValue Create(float v)  => new(MetadataType.Float, 0, v, false, null);
        /// <summary>Creates a metadata value containing a boolean.</summary>
        /// <param name="v">The boolean value to store.</param>
        public static PacketMetadataValue Create(bool v)   => new(MetadataType.Bool, 0, 0, v, null);
        /// <summary>Creates a metadata value containing a string.</summary>
        /// <param name="v">The string value to store.</param>
        public static PacketMetadataValue Create(string v) => new(MetadataType.String, 0, 0, false, v);

        /// <summary>Attempts to retrieve the value as a long integer.</summary>
        /// <param name="v">When this method returns, contains the long value if the type matches; otherwise, default.</param>
        /// <returns>True if the metadata type is <see cref="MetadataType.Int64"/>; otherwise, false.</returns>
        public bool TryGet(out long v)   { v = _i64; return Type == MetadataType.Int64; }
        /// <summary>Attempts to retrieve the value as a float.</summary>
        /// <param name="v">When this method returns, contains the float value if the type matches; otherwise, default.</param>
        /// <returns>True if the metadata type is <see cref="MetadataType.Float"/>; otherwise, false.</returns>
        public bool TryGet(out float v)  { v = _f;   return Type == MetadataType.Float; }
        /// <summary>Attempts to retrieve the value as a boolean.</summary>
        /// <param name="v">When this method returns, contains the boolean value if the type matches; otherwise, default.</param>
        /// <returns>True if the metadata type is <see cref="MetadataType.Bool"/>; otherwise, false.</returns>
        public bool TryGet(out bool v)   { v = _b;   return Type == MetadataType.Bool; }
        /// <summary>Attempts to retrieve the value as a string.</summary>
        /// <param name="v">When this method returns, contains the string value if the type matches; otherwise, null.</param>
        /// <returns>True if the metadata type is <see cref="MetadataType.String"/>; otherwise, false.</returns>
        public bool TryGet(out string v) { v = _s;   return Type == MetadataType.String; }

        /// <summary>
        /// Returns a string representation of the stored value.
        /// </summary>
        public override string ToString()
        {
            switch (Type)
            {
                case MetadataType.Int64:
                    return _i64.ToString();

                case MetadataType.Float:
                    return _f.ToString();

                case MetadataType.Bool:
                    return _b.ToString();

                default:
                    return _s ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Represents a data packet from Thespeon synthesis containing a single data payload and its metadata.
    /// </summary>
    public class ThespeonDataPacket
    {
        /// <summary>
        /// The type of synthesis callback this packet represents.
        /// </summary>
        public SynthCallbackType CallbackType { get; }
        /// <summary>
        /// The data payload of the packet.
        /// </summary>
        public PacketPayload Payload {get; }
        /// <summary>
        /// Key-value metadata associated with this packet.
        /// </summary>
        public Dictionary<string, PacketMetadataValue> Metadata { get; }

        /// <summary>
        /// Initializes a new data packet with the specified callback type and payload. Metadata dictionary is initialized empty.
        /// </summary>
        /// <param name="callbackType">The type of synthesis callback.</param>
        /// <param name="payload">The data payload.</param>
        public ThespeonDataPacket(SynthCallbackType callbackType, PacketPayload payload)
        {
            CallbackType = callbackType;
            Payload = payload;
            Metadata = new Dictionary<string, PacketMetadataValue>();
        }

        /// <summary>
        /// Creates an error packet with the specified error message as its string payload.
        /// </summary>
        /// <param name="errorMessage">The error message to include in the packet.</param>
        /// <returns>A new <see cref="ThespeonDataPacket"/> with <see cref="SynthCallbackType.CB_ERROR"/> callback type.</returns>
        public static ThespeonDataPacket CreateErrorPacket(string errorMessage)
        {
            ThespeonDataPacket errorPacket = new ThespeonDataPacket(SynthCallbackType.CB_ERROR, PacketPayload.Create(errorMessage));
            return errorPacket;
        }

        /// <summary>
        /// Initializes a new data packet with the specified callback type, payload, and metadata.
        /// </summary>
        /// <param name="callbackType">The type of synthesis callback.</param>
        /// <param name="payload">The data payload.</param>
        /// <param name="metadata">Key-value metadata to associate with this packet.</param>
        public ThespeonDataPacket(SynthCallbackType callbackType, PacketPayload payload, Dictionary<string, PacketMetadataValue> metadata)
        {
            CallbackType = callbackType;
            Payload = payload;
            Metadata = metadata;
        }

    }
}
