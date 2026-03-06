// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

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

        /// <summary>
        /// Attempts to get a Tensor by its identifier without throwing an exception.
        /// </summary>
        /// <param name="identifier">The identifier of the tensor.</param>
        /// <param name="tensor">The Tensor associated with the identifier, or null if not found.</param>
        /// <returns>True if the tensor was found, false otherwise.</returns>
        public bool TryGetTensor(string identifier, out Tensor tensor)
        {
            return _tensorObjects.TryGetValue(identifier, out tensor) && tensor != null;
        }

        /// <summary>
        /// Attempts to rename a tensor in the pool.
        /// </summary>
        /// <param name="oldName">The current name of the tensor.</param>
        /// <param name="newName">The new name for the tensor.</param>
        /// <returns>True if the rename was successful, false otherwise.</returns>
        public bool TryRenameTensor(string oldName, string newName)
        {
            if (!_tensorObjects.TryGetValue(oldName, out var tensor) || tensor == null)
            {
                LingotionLogger.Error($"TryRenameTensor: Tensor '{oldName}' not found.");
                return false;
            }

            if (_tensorObjects.ContainsKey(newName))
            {
                _tensorObjects[newName]?.Dispose();
            }

            _tensorObjects[newName] = tensor;
            _tensorObjects.Remove(oldName);
            return true;
        }

        /// <summary>
        /// Removes a tensor from the pool and disposes it.
        /// </summary>
        /// <param name="identifier">The identifier of the tensor to remove.</param>
        public void Remove(string identifier)
        {
            if (_tensorObjects.TryGetValue(identifier, out var tensor))
            {
                tensor?.Dispose();
                _tensorObjects.Remove(identifier);
            }
        }

        /// <summary>
        /// Checks if a tensor with the given identifier exists in the pool.
        /// </summary>
        /// <param name="identifier">The identifier to check.</param>
        /// <returns>True if the tensor exists, false otherwise.</returns>
        public bool ContainsTensor(string identifier)
        {
            return _tensorObjects.ContainsKey(identifier) && _tensorObjects[identifier] != null;
        }
    }

}
