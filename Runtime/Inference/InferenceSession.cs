// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

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
        protected SessionTensorPool tensorPool = new();
        private bool _disposed = false;
        public abstract IEnumerator Infer<T>(ModelInputType input, InferenceConfig config, Action<ThespeonDataPacket<T>> callback, string sessionID, bool asyncDownload = true) where T : unmanaged;

        /// <summary>
        /// Disposes the session and releases any resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            tensorPool.Dispose();
            _disposed = true;
        }
    }

}
