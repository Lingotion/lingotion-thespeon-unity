// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections;
using Lingotion.Thespeon.Core;

namespace Lingotion.Thespeon.Inference
{
    /// <summary>
    /// Template for a new inference session.
    /// </summary>
    /// <typeparam name="ModelInputType">The specific ModelInput that the implementation expects.</typeparam>
    /// <typeparam name="InputSegmentType">The specific ModelInputSegment that the implementation expects.</typeparam>
    public abstract class InferenceSession<ModelInputType, InputSegmentType> : IDisposable
        where ModelInputType : ModelInput<ModelInputType, InputSegmentType>
        where InputSegmentType : ModelInputSegment

    {
        private bool _disposed = false;
        protected SessionTensorPool TensorPool = new();
        protected string SessionID;
        protected Action<ThespeonDataPacket> OnPacketReadyCallback;

        protected InferenceSession(string sessionID, Action<ThespeonDataPacket> packetCallback)
        {
            SessionID = sessionID;
            OnPacketReadyCallback = packetCallback;
        }

        protected void SendPacketCallback(ThespeonDataPacket packet)
        {
            // Add session metadata
            packet.Metadata[CommonMetadataKeys.SessionID] =  PacketMetadataValue.Create(SessionID);
            OnPacketReadyCallback?.Invoke(packet);
        }
        public abstract IEnumerator Infer(ModelInputType input, InferenceConfig config, bool asyncDownload = true);


        /// <summary>
        /// Disposes the session and releases any resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            TensorPool.Dispose();
            _disposed = true;
        }
    }

}
