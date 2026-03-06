// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using Unity.InferenceEngine;
using Lingotion.Thespeon.Core;

namespace Lingotion.Thespeon.Inference
{
    /// <summary>
    /// Enum representing the type of value stored.
    /// </summary>
    public enum HostValueType
    {
        None,
        Int64,
        Float,
        Bool,
        Tensor,
        FloatArray,
        Int64Array,
        String
    }

    /// <summary>
    /// A discriminated union type that can hold various host-side values used in MetaGraph execution.
    /// Mirrors Unreal's TVariant&lt;int64, float, bool, ModelIOData, TArray&lt;float&gt;, TArray&lt;int64&gt;, FString&gt;.
    /// </summary>
    public class HostValue
    {

        private HostValueType _type;
        private object _value;

        /// <summary>
        /// Gets the type of value currently stored.
        /// </summary>
        public HostValueType Type => _type;

        /// <summary>
        /// Creates an empty HostValue.
        /// </summary>
        public HostValue()
        {
            _type = HostValueType.None;
            _value = null;
        }

        // Generic helper method for creating HostValues
        private static HostValue CreateValue<T>(HostValueType type, T value)
        {
            return new HostValue { _type = type, _value = value };
        }

        /// <summary>
        /// Creates a HostValue from a long value.
        /// </summary>
        public static HostValue FromInt64(long value) => CreateValue(HostValueType.Int64, value);

        /// <summary>
        /// Creates a HostValue from a float value.
        /// </summary>
        public static HostValue FromFloat(float value) => CreateValue(HostValueType.Float, value);

        /// <summary>
        /// Creates a HostValue from a bool value.
        /// </summary>
        public static HostValue FromBool(bool value) => CreateValue(HostValueType.Bool, value);

        /// <summary>
        /// Creates a HostValue from a Tensor.
        /// </summary>
        public static HostValue FromTensor(Tensor value) => CreateValue(HostValueType.Tensor, value);

        /// <summary>
        /// Creates a HostValue from a float array.
        /// </summary>
        public static HostValue FromFloatArray(float[] value) => CreateValue(HostValueType.FloatArray, value);

        /// <summary>
        /// Creates a HostValue from an int64 array.
        /// </summary>
        public static HostValue FromInt64Array(long[] value) => CreateValue(HostValueType.Int64Array, value);

        /// <summary>
        /// Creates a HostValue from a string.
        /// </summary>
        public static HostValue FromString(string value) => CreateValue(HostValueType.String, value);

        // Type checking methods
        public bool IsInt64 => _type == HostValueType.Int64;
        public bool IsFloat => _type == HostValueType.Float;
        public bool IsBool => _type == HostValueType.Bool;
        public bool IsTensor => _type == HostValueType.Tensor;
        public bool IsFloatArray => _type == HostValueType.FloatArray;
        public bool IsInt64Array => _type == HostValueType.Int64Array;
        public bool IsString => _type == HostValueType.String;

        // Generic helper method for type-safe value access
        private T GetValueAs<T>(HostValueType expectedType, string typeName)
        {
            if (_type != expectedType)
                throw new InvalidOperationException($"HostValue is not {typeName}, it is {_type}");
            return (T)_value;
        }

        /// <summary>
        /// Gets the value as long. Throws if not the correct type.
        /// </summary>
        public long AsInt64 => GetValueAs<long>(HostValueType.Int64, "Int64");

        /// <summary>
        /// Gets the value as float. Throws if not the correct type.
        /// </summary>
        public float AsFloat => GetValueAs<float>(HostValueType.Float, "Float");

        /// <summary>
        /// Gets the value as bool. Throws if not the correct type.
        /// </summary>
        public bool AsBool => GetValueAs<bool>(HostValueType.Bool, "Bool");

        /// <summary>
        /// Gets the value as Tensor. Throws if not the correct type.
        /// </summary>
        public Tensor AsTensor => GetValueAs<Tensor>(HostValueType.Tensor, "Tensor");

        /// <summary>
        /// Gets the value as float array. Throws if not the correct type.
        /// </summary>
        public float[] AsFloatArray => GetValueAs<float[]>(HostValueType.FloatArray, "FloatArray");

        /// <summary>
        /// Gets the value as long array. Throws if not the correct type.
        /// </summary>
        public long[] AsInt64Array => GetValueAs<long[]>(HostValueType.Int64Array, "Int64Array");

        /// <summary>
        /// Gets the value as string. Throws if not the correct type.
        /// </summary>
        public string AsString => GetValueAs<string>(HostValueType.String, "String");

        // Generic helper method for TryGet pattern
        private bool TryGetValue<T>(HostValueType expectedType, out T value)
        {
            if (_type == expectedType)
            {
                value = (T)_value;
                return true;
            }
            value = default;
            return false;
        }

        // TryGet methods
        public bool TryGetInt64(out long value) => TryGetValue(HostValueType.Int64, out value);
        public bool TryGetFloat(out float value) => TryGetValue(HostValueType.Float, out value);
        public bool TryGetBool(out bool value) => TryGetValue(HostValueType.Bool, out value);
        public bool TryGetTensor(out Tensor value) => TryGetValue(HostValueType.Tensor, out value);
        public bool TryGetFloatArray(out float[] value) => TryGetValue(HostValueType.FloatArray, out value);
        public bool TryGetInt64Array(out long[] value) => TryGetValue(HostValueType.Int64Array, out value);
        public bool TryGetString(out string value) => TryGetValue(HostValueType.String, out value);

        // Generic helper method for setting values
        private void SetValue<T>(HostValueType type, T value)
        {
            _type = type;
            _value = value;
        }

        // Set methods for modifying the value in place
        public void Set(long value) => SetValue(HostValueType.Int64, value);
        public void Set(float value) => SetValue(HostValueType.Float, value);
        public void Set(bool value) => SetValue(HostValueType.Bool, value);
        public void Set(Tensor value) => SetValue(HostValueType.Tensor, value);
        public void Set(float[] value) => SetValue(HostValueType.FloatArray, value);
        public void Set(long[] value) => SetValue(HostValueType.Int64Array, value);
        public void Set(string value) => SetValue(HostValueType.String, value);

        /// <summary>
        /// Converts the value to float, handling int64, float, bool, and single-element tensors.
        /// </summary>
        public float ToFloat()
        {
            return _type switch
            {
                HostValueType.Int64 => (float)(long)_value,
                HostValueType.Float => (float)_value,
                HostValueType.Bool => (bool)_value ? 1.0f : 0.0f,
                HostValueType.Tensor => GetTensorAsFloat((Tensor)_value),
                _ => throw new InvalidOperationException($"Cannot convert {_type} to float")
            };
        }

        // Helper to extract float from tensor with switch expression
        private static float GetTensorAsFloat(Tensor tensor)
        {
            return tensor switch
            {
                Tensor<float> floatTensor => floatTensor.DownloadToArray()[0],
                Tensor<int> intTensor => (float)intTensor.DownloadToArray()[0],
                Tensor<long> longTensor => (float)longTensor.DownloadToArray()[0],
                _ => throw new InvalidOperationException($"Cannot convert Tensor of type {tensor.GetType()} to float")
            };
        }

        /// <summary>
        /// Converts the value to bool, handling bool, int64, and float.
        /// </summary>
        public bool ToBool()
        {
            switch (_type)
            {
                case HostValueType.Bool:
                    return (bool)_value;
                case HostValueType.Int64:
                    return (long)_value != 0;
                case HostValueType.Float:
                    return (float)_value != 0.0f;
                default:
                    throw new InvalidOperationException($"Cannot convert {_type} to bool");
            }
        }

        /// <summary>
        /// Creates a deep copy of this HostValue.
        /// Note: Tensors are not deep copied (reference is shared).
        /// </summary>
        public HostValue Clone()
        {
            var clone = new HostValue { _type = _type };
            switch (_type)
            {
                case HostValueType.FloatArray:
                    clone._value = ((float[])_value).Clone();
                    break;
                case HostValueType.Int64Array:
                    clone._value = ((long[])_value).Clone();
                    break;
                default:
                    clone._value = _value;
                    break;
            }
            return clone;
        }

        public override string ToString()
        {
            return _type switch
            {
                HostValueType.None => "None",
                HostValueType.Int64 => $"Int64({_value})",
                HostValueType.Float => $"Float({_value})",
                HostValueType.Bool => $"Bool({_value})",
                HostValueType.Tensor => $"Tensor({_value})",
                HostValueType.FloatArray => $"FloatArray[{((float[])_value).Length}]",
                HostValueType.Int64Array => $"Int64Array[{((long[])_value).Length}]",
                HostValueType.String => $"String(\"{_value}\")",
                _ => $"Unknown({_type})"
            };
        }
    }
}
