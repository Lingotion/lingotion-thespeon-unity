// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using Lingotion.Thespeon.Core;

namespace Lingotion.Thespeon.Inference
{
    /// <summary>
    /// Parser for symbolic dimension expressions in MetaGraph.
    /// Handles expressions like "B", "T", "B*T", "B + 1", etc.
    /// </summary>
    public static class SymExprParser
    {
        // Operator constants for compile-time safety (matches Unreal implementation)
        private const char PLUS = '+';
        private const char MINUS = '-';
        private const char MUL = '*';
        private const char DIV = '/';
        private const char MOD = '%';

        /// <summary>
        /// Entry mapping a symbolic name to a tensor dimension.
        /// </summary>
        public struct VarDictEntry
        {
            public string TargetTensor;
            public int Dim;
        }

        /// <summary>
        /// Tries to extract an int from a tensor's first element.
        /// </summary>
        private static bool TryGetIntFromTensor(Tensor tensor, out int value)
        {
            try
            {
                value = tensor switch
                {
                    Tensor<int> intTensor => intTensor.DownloadToArray()[0],
                    Tensor<long> longTensor => (int)longTensor.DownloadToArray()[0],
                    Tensor<float> floatTensor => (int)floatTensor.DownloadToArray()[0],
                    _ => throw new InvalidOperationException("Unsupported tensor type")
                };
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Tries to extract an int from a HostValue.
        /// </summary>
        private static bool TryGetIntFromHostValue(HostValue hostValue, out int value)
        {
            // Try tensor
            if (hostValue.TryGetTensor(out Tensor tensor))
            {
                return TryGetIntFromTensor(tensor, out value);
            }

            // Try Int64
            if (hostValue.TryGetInt64(out long int64Val))
            {
                value = (int)int64Val;
                return true;
            }

            // Try Float
            if (hostValue.TryGetFloat(out float floatVal))
            {
                value = (int)floatVal;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Evaluates a symbolic expression and returns the computed value.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <param name="varDict">Dictionary mapping symbolic names to tensor dimensions.</param>
        /// <param name="host">Host variable map.</param>
        /// <param name="tensorPool">Tensor pool for looking up tensors.</param>
        /// <param name="outValue">The computed result.</param>
        /// <returns>True if evaluation succeeded, false otherwise.</returns>
        public static bool Evaluate(
            string expression,
            Dictionary<string, VarDictEntry> varDict,
            Dictionary<string, HostValue> host,
            SessionTensorPool tensorPool,
            out uint outValue)
        {
            outValue = 0;
            int index = 0;
            int length = expression.Length;

            if (!ParseExpression(expression, length, ref index, varDict, host, tensorPool, out int result) || result < 0)
            {
                return false;
            }

            outValue = (uint)result;
            return true;
        }

        private static void SkipWhitespace(string expression, int length, ref int index)
        {
            while (index < length && char.IsWhiteSpace(expression[index]))
            {
                index++;
            }
        }

        private static bool ParseNumber(string expression, int length, ref int index, out int outValue)
        {
            outValue = 0;
            bool hasDigit = false;

            while (index < length && char.IsDigit(expression[index]))
            {
                hasDigit = true;
                outValue = outValue * 10 + (expression[index] - '0');
                index++;
            }

            return hasDigit;
        }

        private static bool ParseVariable(
            string expression,
            int length,
            ref int index,
            Dictionary<string, VarDictEntry> varDict,
            Dictionary<string, HostValue> host,
            SessionTensorPool tensorPool,
            out int outValue)
        {
            outValue = 0;
            int start = index;

            while (index < length && (char.IsLetterOrDigit(expression[index]) || expression[index] == '_'))
            {
                index++;
            }

            if (start == index)
            {
                return false;
            }

            string name = expression.Substring(start, index - start);

            // Check if the name is in the symbolic dict
            if (varDict.TryGetValue(name, out VarDictEntry entry))
            {
                // Check host first, as we want to be able to "override" a tensor dim value if necessary.
                if (host.TryGetValue(entry.TargetTensor, out HostValue hostValue))
                {
                    if (hostValue.TryGetTensor(out Tensor tensor))
                    {
                        outValue = tensor.shape[entry.Dim];
                        return true;
                    }
                }

                if (tensorPool.TryGetTensor(entry.TargetTensor, out Tensor poolTensor))
                {
                    outValue = poolTensor.shape[entry.Dim];
                    return true;
                }

                LingotionLogger.Error($"SymExprParser: invalid tensor {entry.TargetTensor}");
                return false;
            }

            // If the entry is not in the symbolic dict, look through host and device dicts for matches
            if (host.TryGetValue(name, out HostValue directHostValue))
            {
                if (TryGetIntFromHostValue(directHostValue, out outValue))
                {
                    return true;
                }
                LingotionLogger.Error($"SymExprParser: invalid host value type with name {name}");
                return false;
            }

            if (tensorPool.TryGetTensor(name, out Tensor poolTensorDirect))
            {
                if (TryGetIntFromTensor(poolTensorDirect, out outValue))
                {
                    return true;
                }
            }

            LingotionLogger.Error($"SymExprParser: invalid tensor {name}");
            return false;
        }

        private static bool EvaluateNextFactor(
            string expression,
            int length,
            ref int index,
            Dictionary<string, VarDictEntry> varDict,
            Dictionary<string, HostValue> host,
            SessionTensorPool tensorPool,
            out int outValue)
        {
            outValue = 0;
            SkipWhitespace(expression, length, ref index);

            // Handle unary + or -
            if (index < length && (expression[index] == PLUS || expression[index] == MINUS))
            {
                bool negative = expression[index] == MINUS;
                index++;;

                if (!EvaluateNextFactor(expression, length, ref index, varDict, host, tensorPool, out outValue))
                {
                    return false;
                }

                if (negative)
                {
                    outValue = -outValue;
                }
                return true;
            }

            // Handle parentheses
            if (index < length && expression[index] == '(')
            {
                index++;

                if (!ParseExpression(expression, length, ref index, varDict, host, tensorPool, out outValue))
                {
                    return false;
                }

                SkipWhitespace(expression, length, ref index);
                if (index >= length || expression[index] != ')')
                {
                    return false;
                }

                index++;
                return true;
            }

            // Handle number literal
            if (index < length && char.IsDigit(expression[index]))
            {
                return ParseNumber(expression, length, ref index, out outValue);
            }

            // Handle variable
            return ParseVariable(expression, length, ref index, varDict, host, tensorPool, out outValue);
        }

        private static bool EvaluateNextTerm(
            string expression,
            int length,
            ref int index,
            Dictionary<string, VarDictEntry> varDict,
            Dictionary<string, HostValue> host,
            SessionTensorPool tensorPool,
            out int outValue)
        {
            outValue = 0;

            if (!EvaluateNextFactor(expression, length, ref index, varDict, host, tensorPool, out outValue))
            {
                return false;
            }

            while (true)
            {
                SkipWhitespace(expression, length, ref index);

                if (index >= length ||
                    (expression[index] != MUL && expression[index] != DIV && expression[index] != MOD))
                {
                    break;
                }

                char op = expression[index++];

                if (!EvaluateNextFactor(expression, length, ref index, varDict, host, tensorPool, out int rhs))
                {
                    return false;
                }

                switch (op)
                {
                    case MUL:
                        outValue = outValue * rhs;
                        break;
                    case DIV:
                        if (rhs == 0)
                        {
                            LingotionLogger.Error("SymExprParser: division by zero");
                            return false;
                        }
                        outValue = outValue / rhs;
                        break;
                    case MOD:
                        if (rhs == 0)
                        {
                            LingotionLogger.Error("SymExprParser: modulo by zero");
                            return false;
                        }
                        outValue = outValue % rhs;
                        break;
                }
            }

            return true;
        }

        private static bool ParseExpression(
            string expression,
            int length,
            ref int index,
            Dictionary<string, VarDictEntry> varDict,
            Dictionary<string, HostValue> host,
            SessionTensorPool tensorPool,
            out int outValue)
        {
            outValue = 0;

            if (!EvaluateNextTerm(expression, length, ref index, varDict, host, tensorPool, out outValue))
            {
                return false;
            }

            while (true)
            {
                SkipWhitespace(expression, length, ref index);

                if (index >= length || (expression[index] != PLUS && expression[index] != MINUS))
                {
                    break;
                }

                char op = expression[index++];

                if (!EvaluateNextTerm(expression, length, ref index, varDict, host, tensorPool, out int rhs))
                {
                    return false;
                }

                outValue = (op == PLUS) ? (outValue + rhs) : (outValue - rhs);
            }

            return true;
        }
    }
}
