// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using Lingotion.Thespeon.Core;

namespace Lingotion.Thespeon.Inference
{
    /// <summary>
    /// A collection of Tensor objects passed around to Workloads during an InferenceSession. 
    /// Handles all tensors as copies.
    /// </summary>
    public class SessionTensorPool : IDisposable
    {
        private Dictionary<string, Tensor> _tensorObjects = new();
        private bool _disposed = false;

        /// <summary>
        /// Gets a Tensor by its identifier.
        /// </summary>
        /// <param name="identifier">The identifier of the tensor.</param>
        /// <returns>The Tensor associated with the identifier.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the tensor is not found.</exception>
        public Tensor GetTensor(string identifier)
        {
            if (!_tensorObjects.TryGetValue(identifier, out var tensor) || tensor == null)
                throw new InvalidOperationException($"Tensor not found: '{identifier}'");

            return _tensorObjects[identifier];
        }

        /// <summary>
        /// Sets a Tensor in the pool, replacing any existing tensor with the same identifier.
        /// </summary>
        /// <param name="identifier">The identifier for the tensor.</param>
        /// <param name="targetValue">The Tensor to set.</param>
        /// <exception cref="InvalidOperationException">Thrown if an error occurs while setting the tensor.</exception>
        public void SetTensor(string identifier, Tensor targetValue)
        {
            try
            {
                if (_tensorObjects.TryGetValue(identifier, out Tensor currentValue))
                {
                    currentValue.Dispose();
                }
                _tensorObjects[identifier] = targetValue;
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error setting tensor '{identifier}': {e.Message}");
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Disposes of all tensors in the pool and clears the collection.
        /// This method releases all resources associated with the tensors.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            foreach (Tensor tensor in _tensorObjects.Values)
            {
                tensor?.Dispose();
            }
            _tensorObjects.Clear();
            _disposed = true;
        }

        /// <summary>
        /// Checks if the tensor pool has been disposed.
        /// </summary>
        /// <returns>True if the pool is disposed, otherwise false.</returns>
        public bool IsDisposed()
        {
            return _disposed;
        }
    }

}
