// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System.Collections.Generic;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Represents a data packet from Thespeon synthesis containing raw data, metadata and a flag for if it is the final packet of a synthesis.
    /// </summary>
    public struct ThespeonDataPacket<T>
    {
        /// <summary>
        /// The raw data contained in the packet.
        /// </summary>
        public T[] data;
        /// <summary>
        /// Indicates if this is the final packet of a synthesis.
        /// </summary>
        public bool isFinalPacket;
        /// <summary>
        /// Metadata associated with the packet, including session ID, character name, module type and any eventual requested audio indices.
        /// </summary>
        public PacketMetadata metadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThespeonDataPacket{T}"/> struct.
        /// </summary>
        public ThespeonDataPacket(T[] data, string sessionID, bool isFinalPacket = false, string characterName = null, ModuleType moduleType = ModuleType.None, Queue<int> requestedAudioIndices = null)
        {
            this.data = data;
            this.isFinalPacket = isFinalPacket;
            metadata = new PacketMetadata(sessionID, characterName, moduleType, requestedAudioIndices);
        }
    }

    /// <summary>
    /// Metadata for Thespeon data packets.
    /// This includes its origin session ID, and which character name and module type was used, and any eventual requested audio indices.
    /// </summary>
    public struct PacketMetadata
    {
        /// <summary>
        /// The session ID associated with the session the packet originates from.
        /// </summary>
        public string sessionID;
        /// <summary>
        /// The name of the character/actor used during the session.
        /// </summary>
        public string characterName;
        /// <summary>
        /// The module type used during the session.
        /// </summary>
        public ModuleType moduleType;

        /// <summary>
        /// A queue of audio indices corresponding to requested positions in input text in chronological order. Is null if none were requested.
        /// </summary>
        public Queue<int> requestedAudioIndices;

        /// <summary>
        /// Initializes a new instance of the <see cref="PacketMetadata"/> struct.
        /// </summary>
        public PacketMetadata(string sessionID, string characterName = null, ModuleType moduleType = ModuleType.None, Queue<int> requestedAudioIndices = null)
        {
            this.sessionID = sessionID;
            this.characterName = characterName;
            this.moduleType = moduleType;
            this.requestedAudioIndices = requestedAudioIndices;
        }
    }

}
