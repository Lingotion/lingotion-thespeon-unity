// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Unity.InferenceEngine;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Character;
using Metaonnx;
using UnityEngine;

namespace Lingotion.Thespeon.Inference
{
    /// <summary>
    /// Executes MetaGraph protocol buffer graphs for inference.
    /// Provides flexible, graph-driven model execution.
    /// </summary>
    public class MetaGraphRunner
    {
        private readonly SessionTensorPool _tensorPool;
        private readonly CharacterModule _characterModule;
        private readonly InferenceConfig _config;
        private readonly Action<ThespeonDataPacket> _callback;
        private readonly Func<bool> _shouldStop;

        private Dictionary<string, HostValue> _hostValues;

        private Dictionary<string, SymExprParser.VarDictEntry> _varDict;

        private Dictionary<string, Node> _nodeById;
        private Dictionary<string, Condition> _conditionById;
        private Dictionary<string, Loop> _loopById;

        private double _budgetConsumed;

        private bool _verbose;

        // Operators used in symbolic expressions - skip variable extraction if expression contains these
        private static readonly string[] Operators = new[] { "*", "+", "-", "/" };

        /// <summary>
        /// Creates a new MetaGraphRunner instance.
        /// </summary>
        /// <param name="tensorPool">The session tensor pool for tensor storage.</param>
        /// <param name="characterModule">The character module containing model definitions.</param>
        /// <param name="config">Inference configuration settings.</param>
        /// <param name="callback">Callback for streaming audio data packets.</param>
        /// <param name="shouldStop">Function to check if inference should be aborted.</param>
        public MetaGraphRunner(
            SessionTensorPool tensorPool,
            CharacterModule characterModule,
            InferenceConfig config,
            Action<ThespeonDataPacket> callback,
            Func<bool> shouldStop)
        {
            _tensorPool = tensorPool;
            _characterModule = characterModule;
            _config = config;
            _callback = callback;
            _shouldStop = shouldStop;
            _hostValues = new Dictionary<string, HostValue>();
            _varDict = new Dictionary<string, SymExprParser.VarDictEntry>();
            _nodeById = new Dictionary<string, Node>();
            _conditionById = new Dictionary<string, Condition>();
            _loopById = new Dictionary<string, Loop>();
            _budgetConsumed = 0;
        }

        /// <summary>
        /// Executes the MetaGraph from start to finish.
        /// </summary>
        /// <param name="graph">The MetaGraph to execute.</param>
        /// <param name="verbose">Whether to enable verbose logging.</param>
        /// <returns>IEnumerator for coroutine execution.</returns>
        public IEnumerator Run(MetaGraph graph, bool verbose = false)
        {
            _verbose = verbose;

            foreach (var node in graph.Nodes)
            {
                _nodeById[node.Id] = node;
                // Only build symbolic dimension dictionary in verbose mode - expensive calculation with no gain in Sentis
                if (_verbose)
                {
                    BuildVarDictFromNode(node);
                }
            }

            foreach (var condition in graph.Conditions)
            {
                _conditionById[condition.Id] = condition;
            }

            foreach (var loop in graph.Loops)
            {
                _loopById[loop.Id] = loop;
            }


            LingotionLogger.Info($"MetaGraphRunner: Starting execution of MetaGraph v{graph.MajorVersion}.{graph.MinorVersion}.{graph.PatchVersion}");
            LingotionLogger.Debug($"MetaGraphRunner: {graph.Nodes.Count} nodes, {graph.Conditions.Count} conditions, {graph.Loops.Count} loops");


            foreach (var item in graph.Graph)
            {
                if (_shouldStop != null && _shouldStop())
                {
                    LingotionLogger.Info("MetaGraphRunner: Execution aborted by shouldStop");
                    yield break;
                }

                var executeItem = ExecuteGraphItem(item);
                while (executeItem.MoveNext()) { yield return executeItem.Current; }

                if (_tensorPool.IsDisposed())
                {
                    LingotionLogger.Error("MetaGraphRunner: Tensor pool disposed during execution");
                    yield break;
                }
            }

            LingotionLogger.Info("MetaGraphRunner: Execution completed successfully");

        }

        /// <summary>
        /// Builds the symbolic variable dictionary from a node's input/output bindings.
        /// </summary>
        private void BuildVarDictFromNode(Node node)
        {
            ProcessInputsForSymbolicDims(node.Inputs);
            ProcessOutputsForSymbolicDims(node.Outputs);
        }

        /// <summary>
        /// Processes input bindings to extract symbolic dimension variables.
        /// </summary>
        private void ProcessInputsForSymbolicDims(IEnumerable<Node.Types.InputBinding> inputs)
        {
            foreach (var input in inputs)
            {
                foreach (var dim in input.Dims)
                {
                    if (dim.Type != DimEntry.Types.DimType.DimSymbolic || string.IsNullOrEmpty(dim.Expression))
                        continue;

                    string varName = dim.Expression.Trim();

                    if (_varDict.ContainsKey(varName))
                        continue;

                    if (Operators.Any(op => varName.Contains(op)))
                        continue;

                    int dimIndex = input.Dims.IndexOf(dim);
                    _varDict[varName] = new SymExprParser.VarDictEntry
                    {
                        TargetTensor = input.TensorName,
                        Dim = dimIndex
                    };
                }
            }
        }

        /// <summary>
        /// Processes output bindings to extract symbolic dimension variables.
        /// </summary>
        private void ProcessOutputsForSymbolicDims(IEnumerable<Node.Types.OutputBinding> outputs)
        {
            foreach (var output in outputs)
            {
                foreach (var dim in output.Dims)
                {
                    if (dim.Type != DimEntry.Types.DimType.DimSymbolic || string.IsNullOrEmpty(dim.Expression))
                        continue;

                    string varName = dim.Expression.Trim();

                    if (_varDict.ContainsKey(varName))
                        continue;

                    if (Operators.Any(op => varName.Contains(op)))
                        continue;

                    int dimIndex = output.Dims.IndexOf(dim);
                    _varDict[varName] = new SymExprParser.VarDictEntry
                    {
                        TargetTensor = output.TensorName,
                        Dim = dimIndex
                    };
                }
            }
        }

        /// <summary>
        /// Executes a single graph item (node, loop, action, or conditional).
        /// </summary>
        private IEnumerator ExecuteGraphItem(GraphItem item)
        {
            switch (item.KindCase)
            {
                case GraphItem.KindOneofCase.NodeId:
                    var runNode = RunNode(item.NodeId);
                    while (runNode.MoveNext()) { yield return runNode.Current; }
                    break;

                case GraphItem.KindOneofCase.LoopId:
                    var runLoop = RunLoop(item.LoopId);
                    while (runLoop.MoveNext()) { yield return runLoop.Current; }
                    break;

                case GraphItem.KindOneofCase.HostAction:
                    EvalHostAction(item.HostAction);
                    break;

                case GraphItem.KindOneofCase.Conditional:
                    var runConditional = RunConditional(item.Conditional);
                    while (runConditional.MoveNext()) { yield return runConditional.Current; }
                    break;

                default:
                    LingotionLogger.Warning($"MetaGraphRunner: Unknown graph item kind: {item.KindCase}");
                    break;
            }
        }

        /// <summary>
        /// Executes a list of host actions with error handling.
        /// </summary>
        private void ExecuteHostActions(IEnumerable<HostAction> actions, string nodeId)
        {
            int actionIndex = 0;
            foreach (var action in actions)
            {
                try
                {
                    EvalHostAction(action);
                }
                catch (Exception e)
                {
                    LingotionLogger.Error($"MetaGraphRunner: Error in action {actionIndex} ({action.ActionCase}) for node '{nodeId}': {e.Message}");
                    throw;
                }
                actionIndex++;
            }
        }

        /// <summary>
        /// Executes a model node with pre/post actions.
        /// </summary>
        private IEnumerator RunNode(string nodeId)
        {
            if (!_nodeById.TryGetValue(nodeId, out Node node))
            {
                LingotionLogger.Error($"MetaGraphRunner: Node '{nodeId}' not found");
                yield break;
            }

            if (!string.IsNullOrEmpty(node.ConditionId))
            {
                if (!EvaluateCondition(node.ConditionId))
                {
                    LingotionLogger.Debug($"MetaGraphRunner: Skipping node '{nodeId}' due to false condition '{node.ConditionId}'");
                    yield break;
                }
            }

            LingotionLogger.Debug($"MetaGraphRunner: Running node '{nodeId}' (model: {node.ModelPath})");


            ExecuteHostActions(node.PreActions, nodeId);

            foreach (var input in node.Inputs)
            {
                if (input.InputName != input.TensorName && _tensorPool.ContainsTensor(input.TensorName))
                {
                    _tensorPool.TryRenameTensor(input.TensorName, input.InputName);
                }
            }

            string modelMD5 = ResolveWorkloadForNode(node);
            if (string.IsNullOrEmpty(modelMD5))
            {
                LingotionLogger.Error($"MetaGraphRunner: Could not resolve workload for node '{nodeId}'");
                yield break;
            }
            string modelWorkloadID = Module.GetWorkloadID(modelMD5, _config.PreferredBackendType);
            InferenceWorkload workload = null;
            if (!InferenceWorkloadManager.Instance.AcquireWorkload(modelWorkloadID, ref workload))
            {
                yield return new WaitUntil(() => InferenceWorkloadManager.Instance.AcquireWorkload(modelWorkloadID, ref workload));
                yield return new WaitForEndOfFrame();
                _budgetConsumed = 0;
            }
            var infer = workload.Infer(
                _tensorPool,
                _config,
                (consumed) => _budgetConsumed = consumed,
                debugName: node.Id,
                budgetConsumed: _budgetConsumed
            );
            while (infer.MoveNext()) { yield return infer.Current; }

            InferenceWorkloadManager.Instance.ReleaseWorkload(modelWorkloadID);

            if (_tensorPool.IsDisposed())
            {
                LingotionLogger.Error($"MetaGraphRunner: Tensor pool disposed in node '{nodeId}'");
                yield break;
            }

            foreach (var output in node.Outputs)
            {
                if (output.OutputName != output.TensorName && _tensorPool.ContainsTensor(output.OutputName))
                {
                    _tensorPool.TryRenameTensor(output.OutputName, output.TensorName);
                }
            }

            ExecuteHostActions(node.PostActions, nodeId);
        }

        /// <summary>
        /// Executes a loop construct.
        /// </summary>
        private IEnumerator RunLoop(string loopId)
        {
            if (!_loopById.TryGetValue(loopId, out Loop loop))
            {
                LingotionLogger.Error($"MetaGraphRunner: Loop '{loopId}' not found");
                yield break;
            }

            LingotionLogger.Debug($"MetaGraphRunner: Starting loop '{loopId}' (max iterations: {loop.MaxIterations})");


            uint iteration = 0;
            uint maxIterations = loop.MaxIterations > 0 ? loop.MaxIterations : uint.MaxValue;

            while (iteration < maxIterations)
            {
                if (!string.IsNullOrEmpty(loop.IterationVarName))
                {
                    _hostValues[loop.IterationVarName] = HostValue.FromInt64((long)iteration);
                }

                if (!string.IsNullOrEmpty(loop.ConditionId))
                {
                    if (!EvaluateCondition(loop.ConditionId))
                    {
                        LingotionLogger.Debug($"MetaGraphRunner: Loop '{loopId}' exiting at iteration {iteration} due to exit condition '{loop.ConditionId}'");
                        break;
                    }
                }

                foreach (var item in loop.Subgraph)
                {
                    if (_shouldStop != null && _shouldStop())
                    {
                        yield break;
                    }

                    var executeLoopItem = ExecuteGraphItem(item);
                    while (executeLoopItem.MoveNext()) { yield return executeLoopItem.Current; }

                    if (_tensorPool.IsDisposed())
                    {
                        yield break;
                    }
                }

                iteration++;
            }
            LingotionLogger.Debug($"MetaGraphRunner: Loop '{loopId}' completed after {iteration} iterations");
        }

        /// <summary>
        /// Executes a conditional branch.
        /// </summary>
        private IEnumerator RunConditional(ConditionalBranch conditional)
        {
            bool conditionResult = EvaluateCondition(conditional.ConditionId);

            var flow = conditionResult ? conditional.ThenFlow : conditional.ElseFlow;

            foreach (var item in flow)
            {
                if (_shouldStop != null && _shouldStop())
                {
                    yield break;
                }

                var executeCondItem = ExecuteGraphItem(item);
                while (executeCondItem.MoveNext()) { yield return executeCondItem.Current; }

                if (_tensorPool.IsDisposed())
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// Evaluates a condition by its ID.
        /// </summary>
        private bool EvaluateCondition(string conditionId)
        {
            if (!_conditionById.TryGetValue(conditionId, out Condition condition))
            {
                LingotionLogger.Error($"MetaGraphRunner: Condition '{conditionId}' not found");
                return false;
            }

            if (_verbose)
            {
                string leftDesc = DescribeValueRef(condition.Left);
                string rightDesc = DescribeValueRef(condition.Right);
                LingotionLogger.Debug($"MetaGraphRunner: Evaluating condition '{conditionId}': {leftDesc} {condition.Comparison} {rightDesc}");
            }

            float leftVal = ResolveValueRefAsFloat(condition.Left);
            float rightVal = ResolveValueRefAsFloat(condition.Right);

            bool result = condition.Comparison switch
            {
                Condition.Types.Comparison.CmpEq => Math.Abs(leftVal - rightVal) < float.Epsilon,
                Condition.Types.Comparison.CmpNe => Math.Abs(leftVal - rightVal) >= float.Epsilon,
                Condition.Types.Comparison.CmpLt => leftVal < rightVal,
                Condition.Types.Comparison.CmpLe => leftVal <= rightVal,
                Condition.Types.Comparison.CmpGt => leftVal > rightVal,
                Condition.Types.Comparison.CmpGe => leftVal >= rightVal,
                _ => false
            };

            return result;
        }

        /// <summary>
        /// Returns a human-readable description of a ValueRef for logging.
        /// </summary>
        private string DescribeValueRef(ValueRef valueRef)
        {
            return valueRef.KindCase switch
            {
                ValueRef.KindOneofCase.Host => $"Host({valueRef.Host.Name})",
                ValueRef.KindOneofCase.Tensor => valueRef.Tensor.HasDim
                    ? $"Tensor({valueRef.Tensor.Name}).dim[{valueRef.Tensor.Dim}]"
                    : $"Tensor({valueRef.Tensor.Name})",
                ValueRef.KindOneofCase.Literal => $"Literal({valueRef.Literal})",
                _ => $"Unknown({valueRef.KindCase})"
            };
        }

        /// <summary>
        /// Evaluates a host action.
        /// </summary>
        private void EvalHostAction(HostAction action)
        {
            switch (action.ActionCase)
            {
                case HostAction.ActionOneofCase.TensorToHost:
                    EvalTensorToHost(action.TensorToHost);
                    break;

                case HostAction.ActionOneofCase.HostToTensor:
                    EvalHostToTensor(action.HostToTensor);
                    break;

                case HostAction.ActionOneofCase.TensorCopy:
                    EvalTensorCopy(action.TensorCopy);
                    break;

                case HostAction.ActionOneofCase.TensorRename:
                    EvalTensorRename(action.TensorRename);
                    break;

                case HostAction.ActionOneofCase.HostBinop:
                    EvalHostBinop(action.HostBinop);
                    break;

                case HostAction.ActionOneofCase.HostSet:
                    EvalHostSet(action.HostSet);
                    break;

                case HostAction.ActionOneofCase.HostRemove:
                    EvalHostRemove(action.HostRemove);
                    break;

                case HostAction.ActionOneofCase.TensorCreate:
                    EvalTensorCreate(action.TensorCreate);
                    break;

                case HostAction.ActionOneofCase.Callback:
                    EvalCallback(action.Callback);
                    break;

                default:
                    LingotionLogger.Error($"MetaGraphRunner: Unknown host action type: {action.ActionCase}");
                    break;
            }
        }

        /// <summary>
        /// Copies tensor data to a host variable.
        /// </summary>
        private void EvalTensorToHost(TensorToHost action)
        {
            string tensorName = action.SrcTensor.Name;
            string hostName = action.DestHost;

            if (!_tensorPool.TryGetTensor(tensorName, out Tensor tensor))
            {
                LingotionLogger.Error($"MetaGraphRunner: TensorToHost - tensor '{tensorName}' not found");
                return;
            }

            HostValue hostValue;

            // If dim is specified, get that dimension's size
            if (action.SrcTensor.HasDim)
            {
                int dim = (int)action.SrcTensor.Dim;
                if (dim >= 0 && dim < tensor.shape.rank)
                {
                    hostValue = HostValue.FromInt64(tensor.shape[dim]);
                }
                else
                {
                    LingotionLogger.Error($"MetaGraphRunner: TensorToHost - invalid dim {dim} for tensor '{tensorName}'");
                    return;
                }
            }
            else
            {
                hostValue = HostValue.FromTensor(tensor);
            }

            _hostValues[hostName] = hostValue;

        }

        /// <summary>
        /// Copies host variable to a tensor.
        /// </summary>
        private void EvalHostToTensor(HostToTensor action)
        {
            string hostName = action.SrcHost;
            string tensorName = action.DestTensor.Name;

            if (!_hostValues.TryGetValue(hostName, out HostValue hostValue))
            {
                LingotionLogger.Error($"MetaGraphRunner: HostToTensor - host variable '{hostName}' not found");
                return;
            }


            Tensor tensor;
            try
            {
                if (hostValue.IsTensor)
                {
                    tensor = hostValue.AsTensor;
                }
                else if (hostValue.IsInt64)
                {
                    long val = hostValue.AsInt64;
                    tensor = new Tensor<int>(new TensorShape(1), new[] { (int)val });
                }
                else if (hostValue.IsFloat)
                {
                    tensor = new Tensor<float>(new TensorShape(1), new[] { hostValue.AsFloat });
                }
                else if (hostValue.IsInt64Array)
                {
                    var arr = hostValue.AsInt64Array;
                    int[] intArr = new int[arr.Length];
                    for (int i = 0; i < arr.Length; i++)
                    {
                        intArr[i] = (int)arr[i];
                    }
                    tensor = new Tensor<int>(new TensorShape(arr.Length), intArr);
                }
                else if (hostValue.IsFloatArray)
                {
                    var arr = hostValue.AsFloatArray;
                    tensor = new Tensor<float>(new TensorShape(arr.Length), arr);
                }
                else
                {
                    LingotionLogger.Error($"MetaGraphRunner: HostToTensor - cannot convert {hostValue.Type} to tensor");
                    return;
                }
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"MetaGraphRunner: HostToTensor - error creating tensor from {hostValue.Type}: {e.Message}");
                return;
            }

            _tensorPool.SetTensor(tensorName, tensor);

        }

        /// <summary>
        /// Copies a tensor to another tensor name.
        /// </summary>
        private void EvalTensorCopy(TensorCopy action)
        {
            string srcName = action.SrcTensor.Name;
            string destName = action.DestTensor.Name;

            if (!_tensorPool.TryGetTensor(srcName, out Tensor srcTensor))
            {
                LingotionLogger.Error($"MetaGraphRunner: TensorCopy - source tensor '{srcName}' not found");
                return;
            }

            Tensor destTensor = srcTensor switch
            {
                Tensor<float> floatTensor => new Tensor<float>(floatTensor.shape, floatTensor.DownloadToArray()),
                Tensor<int> intTensor => new Tensor<int>(intTensor.shape, intTensor.DownloadToArray()),
                Tensor<long> longTensor => new Tensor<long>(longTensor.shape, longTensor.DownloadToArray()),
                _ => throw new InvalidOperationException("MetaGraphRunner: TensorCopy - unsupported tensor type")
            };

            _tensorPool.SetTensor(destName, destTensor);
        }

        /// <summary>
        /// Renames a tensor in the pool.
        /// </summary>
        private void EvalTensorRename(TensorRename action)
        {
            string srcName = action.SrcTensor;
            string destName = action.DestTensor;

            if (!_tensorPool.TryRenameTensor(srcName, destName))
            {
                LingotionLogger.Error($"MetaGraphRunner: TensorRename - failed to rename '{srcName}' to '{destName}'");
                return;
            }
        }

        /// <summary>
        /// Performs a binary operation on host variables.
        /// </summary>
        private void EvalHostBinop(HostBinaryOp action)
        {
            float leftVal = ResolveValueRefAsFloat(action.Left);
            float rightVal = ResolveValueRefAsFloat(action.Right);

            float result = action.Op switch
            {
                HostBinaryOp.Types.Op.Add => leftVal + rightVal,
                HostBinaryOp.Types.Op.Sub => leftVal - rightVal,
                HostBinaryOp.Types.Op.Mul => leftVal * rightVal,
                HostBinaryOp.Types.Op.Div => rightVal != 0 ? leftVal / rightVal : 0,
                HostBinaryOp.Types.Op.Mod => rightVal != 0 ? leftVal % rightVal : 0,
                _ => 0
            };

            // Determine result type based on inputs
            bool isInteger = IsIntegerValueRef(action.Left) && IsIntegerValueRef(action.Right);
            _hostValues[action.DestHost] = isInteger
                ? HostValue.FromInt64((long)result)
                : HostValue.FromFloat(result);

        }

        /// <summary>
        /// Sets a host variable to a value.
        /// </summary>
        private void EvalHostSet(HostSetVariable action)
        {
            HostValue value = ResolveValueRef(action.Value);
            _hostValues[action.DestHost] = value;

        }

        /// <summary>
        /// Removes a host variable.
        /// </summary>
        private void EvalHostRemove(HostVarRef action)
        {
            _hostValues.Remove(action.Name);
        }

        /// <summary>
        /// Creates a new tensor with specified dimensions and fill pattern.
        /// </summary>
        private void EvalTensorCreate(TensorCreate action)
        {
            string tensorName = action.DestTensor.Name;
            string dtype = action.Dtype.ToLowerInvariant();

            int[] dims = new int[action.DimsLiteral.Count];
            for (int i = 0; i < action.DimsLiteral.Count; i++)
            {
                dims[i] = (int)action.DimsLiteral[i];
            }

            TensorShape shape = dims.Length switch
            {
                1 => new TensorShape(dims[0]),
                2 => new TensorShape(dims[0], dims[1]),
                3 => new TensorShape(dims[0], dims[1], dims[2]),
                4 => new TensorShape(dims[0], dims[1], dims[2], dims[3]),
                _ => new TensorShape(dims)
            };

            Tensor tensor;
            int totalSize = shape.length;

            switch (dtype)
            {
                case "float32":
                case "float":
                    float[] floatData = new float[totalSize];
                    float floatFill = action.Fill switch
                    {
                        TensorCreate.Types.Fill.Zeros => 0f,
                        TensorCreate.Types.Fill.Ones => 1f,
                        TensorCreate.Types.Fill.Value => action.Value?.F32 ?? 0f,
                        _ => 0f
                    };
                    Array.Fill(floatData, floatFill);
                    tensor = new Tensor<float>(shape, floatData);
                    break;

                case "int64":
                case "long":
                    long[] longData = new long[totalSize];
                    long longFill = action.Fill switch
                    {
                        TensorCreate.Types.Fill.Zeros => 0L,
                        TensorCreate.Types.Fill.Ones => 1L,
                        TensorCreate.Types.Fill.Value => action.Value?.I64 ?? 0L,
                        _ => 0L
                    };
                    Array.Fill(longData, longFill);
                    tensor = new Tensor<long>(shape, longData);
                    break;

                case "int32":
                case "int":
                    int[] intData = new int[totalSize];
                    int intFill = action.Fill switch
                    {
                        TensorCreate.Types.Fill.Zeros => 0,
                        TensorCreate.Types.Fill.Ones => 1,
                        TensorCreate.Types.Fill.Value => (int)(action.Value?.I64 ?? 0),
                        _ => 0
                    };
                    Array.Fill(intData, intFill);
                    tensor = new Tensor<int>(shape, intData);
                    break;

                default:
                    LingotionLogger.Error($"MetaGraphRunner: TensorCreate - unsupported dtype '{dtype}'");
                    return;
            }

            _tensorPool.SetTensor(tensorName, tensor);

        }

        /// <summary>
        /// Executes a callback action (audio streaming, etc.).
        /// </summary>
        private void EvalCallback(HostCallback action)
        {
            ThespeonDataPacket resultPacket = null;
            switch (action.CallbackType)
            {
                case CallbackType.CbAudio:
                    resultPacket = EvalAudioCallback(action);
                    break;

                case CallbackType.CbTriggersample:
                    resultPacket = EvalTriggerSampleCallback(action);
                    break;

                case CallbackType.CbError:
                    string errorMsg = action.CallbackName ?? "Unknown error";
                    LingotionLogger.Error($"MetaGraphRunner: Error callback - {errorMsg}");
                    break;

                default:
                    LingotionLogger.Error($"MetaGraphRunner: Unhandled callback type: {action.CallbackType}");
                    break;
            }

            if(resultPacket == null)
            {
                return;
            }

            for (int i = 0; i < action.Metadata.Count; i++)
            {
                HostValue literalValue = ScalarLiteralToHostValue(action.Metadata[i].Value);
                PacketMetadataValue metadataValue;
                switch (literalValue.Type)
                {
                    case HostValueType.Int64:
                        metadataValue = PacketMetadataValue.Create(literalValue.AsInt64);
                        break;

                    case HostValueType.Float:
                        metadataValue = PacketMetadataValue.Create(literalValue.AsFloat);
                        break;

                    case HostValueType.Bool:
                        metadataValue = PacketMetadataValue.Create(literalValue.AsBool);
                        break;

                    case HostValueType.String:
                        metadataValue = PacketMetadataValue.Create(literalValue.AsString);
                        break;

                    default:
                        LingotionLogger.Error($"Illegal metadata type specified: {literalValue.Type}");
                        continue;
                }

                resultPacket.Metadata.Add(action.Metadata[i].Key, metadataValue);
            }

            _callback?.Invoke(resultPacket);
        }

        /// <summary>
        /// Handles audio data callback - streams synthesized audio to the callback.
        /// Based on Unreal implementation pattern: resolve all args and append to result array.
        /// </summary>
        private ThespeonDataPacket EvalAudioCallback(HostCallback action)
        {
            if (action.Args.Count == 0)
            {
                LingotionLogger.Error("Audio callback without args specified, ignoring.");
                return null;
            }

            var resultData = new List<float>();
            for (int i = 0; i < action.Args.Count; i++)
            {
                HostValue resolved = ResolveValueRef(action.Args[i]);
                if (resolved.Type == HostValueType.None)
                {
                    LingotionLogger.Error($"Audio callback - failed to resolve arg {i}");
                    return null;
                }

                if (resolved.IsTensor)
                {
                    Tensor tensor = resolved.AsTensor;
                    if (tensor is Tensor<float> floatTensor)
                    {
                       resultData.AddRange(floatTensor.DownloadToArray());
                    }
                    else
                    {
                       LingotionLogger.Error($"Audio callback - tensor arg {i} is not float tensor");
                       return null;
                    }
                }
                else if (resolved.IsFloatArray)
                {
                    resultData.AddRange(resolved.AsFloatArray);
                }
                else
                {
                    LingotionLogger.Error($"Audio callback - unsuitable data type for audio callback arg {i}");
                    return null;
                }
            }
            return new ThespeonDataPacket(SynthCallbackType.CB_AUDIO, PacketPayload.Create(resultData.ToArray()));
        }

        /// <summary>
        /// Handles audio sample request callback.
        /// </summary>
        private ThespeonDataPacket EvalTriggerSampleCallback(HostCallback action)
        {
            if (action.Args.Count == 0)
            {
                LingotionLogger.Error("Audio sample request callback without args specified, ignoring.");
                return null;
            }

            var resultData = new List<long>();
            for (int i = 0; i < action.Args.Count; i++)
            {
                HostValue resolved = ResolveValueRef(action.Args[i]);
                if (resolved.Type == HostValueType.None)
                {
                    LingotionLogger.Error($"Audio sample request callback failed to resolve arg {i}");
                    return null;
                }

                if (resolved.IsTensor)
                {
                    Tensor tensor = resolved.AsTensor;
                    if (tensor is Tensor<int> intTensor)
                    {
                        int[] tensorIntValues = intTensor.DownloadToArray();
                        long[] resultValues = new long[tensorIntValues.Length];
                        for (int j = 0; j < tensorIntValues.Length; j++)
                            resultValues[j] = tensorIntValues[j];
                        
                        resultData.AddRange(resultValues);
                    }
                    else
                    {
                        LingotionLogger.Error($"Audio sample request callback tensor arg {i} is not integer tensor");
                        return null;
                    }
                }
                else if (resolved.IsInt64Array)
                {
                    resultData.AddRange(resolved.AsInt64Array);
                }
                else
                {
                    LingotionLogger.Error($"Unsuitable data type for audio sample request callback arg {i}");
                    return null;
                }
            }
            return new ThespeonDataPacket(SynthCallbackType.CB_TRIGGERSAMPLE, PacketPayload.Create(resultData.ToArray()));
        }

        /// <summary>
        /// Resolves a ValueRef to a HostValue.
        /// </summary>
        private HostValue ResolveValueRef(ValueRef valueRef)
        {
            switch (valueRef.KindCase)
            {
                case ValueRef.KindOneofCase.Host:
                    if (_hostValues.TryGetValue(valueRef.Host.Name, out HostValue hostValue))
                    {
                        return hostValue;
                    }
                    LingotionLogger.Error($"MetaGraphRunner: Host variable '{valueRef.Host.Name}' not found");
                    return new HostValue();

                case ValueRef.KindOneofCase.Tensor:
                    if (_tensorPool.TryGetTensor(valueRef.Tensor.Name, out Tensor tensor))
                    {
                        if (valueRef.Tensor.HasDim)
                        {
                            int dim = (int)valueRef.Tensor.Dim;
                            return HostValue.FromInt64(tensor.shape[dim]);
                        }
                        return HostValue.FromTensor(tensor);
                    }
                    LingotionLogger.Error($"MetaGraphRunner: Tensor '{valueRef.Tensor.Name}' not found");
                    return new HostValue();

                case ValueRef.KindOneofCase.Literal:
                    return ScalarLiteralToHostValue(valueRef.Literal);

                case ValueRef.KindOneofCase.None:
                default:
                    LingotionLogger.Error($"MetaGraphRunner: ValueRef has no value set (KindCase: {valueRef.KindCase})");
                    return new HostValue();
            }
        }

        /// <summary>
        /// Resolves a ValueRef to a float value.
        /// </summary>
        private float ResolveValueRefAsFloat(ValueRef valueRef)
        {
            HostValue hv = ResolveValueRef(valueRef);
            if (hv.Type == HostValueType.None)
            {
                LingotionLogger.Error($"MetaGraphRunner: Could not resolve ValueRef to float, returning 0. Kind: {valueRef.KindCase}");
                return 0f;
            }
            return hv.ToFloat();
        }

        /// <summary>
        /// Checks if a ValueRef represents an integer value.
        /// </summary>
        private bool IsIntegerValueRef(ValueRef valueRef)
        {
            switch (valueRef.KindCase)
            {
                case ValueRef.KindOneofCase.Literal:
                    return valueRef.Literal.ValueCase == ScalarLiteral.ValueOneofCase.I64;

                case ValueRef.KindOneofCase.Host:
                    if (_hostValues.TryGetValue(valueRef.Host.Name, out HostValue hv))
                    {
                        return hv.IsInt64;
                    }
                    return false;

                case ValueRef.KindOneofCase.Tensor:
                    return valueRef.Tensor.HasDim;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Converts a ScalarLiteral to a HostValue.
        /// </summary>
        private HostValue ScalarLiteralToHostValue(ScalarLiteral literal)
        {
            return literal.ValueCase switch
            {
                ScalarLiteral.ValueOneofCase.I64 => HostValue.FromInt64(literal.I64),
                ScalarLiteral.ValueOneofCase.F32 => HostValue.FromFloat(literal.F32),
                ScalarLiteral.ValueOneofCase.B => HostValue.FromBool(literal.B),
                ScalarLiteral.ValueOneofCase.S => HostValue.FromString(literal.S),
                _ => new HostValue()
            };
        }

        /// <summary>
        /// Resolves the workload MD5 for a given node based on its model path.
        /// </summary>
        private string ResolveWorkloadForNode(Node node)
        {
            string modelPath = node.ModelPath;

            var candidates = new[]
            {
                modelPath,
                System.IO.Path.GetFileNameWithoutExtension(modelPath)
            };

            foreach (var candidate in candidates)
            {
                if (TryGetInternalModelID(candidate, out string modelId))
                {
                    return modelId;
                }
            }

            foreach (var candidate in candidates)
            {
                if (_characterModule.HasModelMD5(candidate))
                {
                    return candidate;
                }
            }

            LingotionLogger.Error($"MetaGraphRunner: Could not find model mapping for '{modelPath}'");
            return null;
        }

        /// <summary>
        /// Tries to get the internal model ID for a given path.
        /// </summary>
        private bool TryGetInternalModelID(string path, out string modelId)
        {
            try
            {
                modelId = _characterModule.GetInternalModelID(path);
                return true;
            }
            catch (KeyNotFoundException)
            {
                modelId = null;
                return false;
            }
        }
    }
}
