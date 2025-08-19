// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

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
        /// Metadata associated with the packet, including session ID, character name, and module type.
        /// </summary>
        public PacketMetadata metadata;

        public ThespeonDataPacket(T[] data, string sessionID, bool isFinalPacket = false, string characterName = null, ModuleType moduleType = ModuleType.None)
        {
            this.data = data;
            this.isFinalPacket = isFinalPacket;
            metadata = new PacketMetadata(sessionID, characterName, moduleType);
        }
    }

    /// <summary>
    /// Metadata for Thespeon data packets.
    /// This includes its origin session ID, and which character name and module type was used.
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

        public PacketMetadata(string sessionID, string characterName = null, ModuleType moduleType = ModuleType.None)
        {
            this.sessionID = sessionID;
            this.characterName = characterName;
            this.moduleType = moduleType;
        }
    }

}
