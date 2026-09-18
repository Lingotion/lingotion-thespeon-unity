// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Unity.InferenceEngine;
using Lingotion.Thespeon.Core;
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
        private readonly Module _module;
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

        // Used where a symbolic expression must resolve against host variables and the tensor pool alone.
        private static readonly Dictionary<string, SymExprParser.VarDictEntry> EmptyVarDict = new();

        /// <summary>
        /// Creates a new MetaGraphRunner instance.
        /// </summary>
        /// <param name="tensorPool">The session tensor pool for tensor storage.</param>
        /// <param name="module">The module owning the graph, resolving its nodes' models.</param>
        /// <param name="config">Inference configuration settings.</param>
        /// <param name="callback">Callback for streaming audio data packets.</param>
        /// <param name="shouldStop">Function to check if inference should be aborted.</param>
        public MetaGraphRunner(
            SessionTensorPool tensorPool,
            Module module,
            InferenceConfig config,
            Action<ThespeonDataPacket> callback,
            Func<bool> shouldStop)
        {
            _tensorPool = tensorPool;
            _module = module;
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

            if (!ValidateAndFillInputs(graph))
            {
                LingotionLogger.Error("MetaGraphRunner: input validation failed, aborting");
                yield break;
            }

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
        /// Maps a declared dtype string onto the Unity tensor DataType a matching tensor must have.
        /// Unity has no 64-bit integer tensor, so int64 graph inputs are represented as Tensor&lt;int&gt;
        /// throughout Thespeon and validate against DataType.Int.
        /// </summary>
        private static bool TryGetExpectedDataType(string dtype, out DataType expected)
        {
            switch (dtype?.ToLowerInvariant())
            {
                case "int64":
                case "long":
                case "int32":
                case "int":
                    expected = DataType.Int;
                    return true;

                case "float32":
                case "float":
                    expected = DataType.Float;
                    return true;

                default:
                    expected = default;
                    return false;
            }
        }

        /// <summary>
        /// Checks the tensor pool against the graph's declared external input contract (presence, dtype and
        /// dimensions), then fills in default values for any missing optional inputs. Every problem found is
        /// collected and logged together so a caller can fix them all in one pass.
        /// </summary>
        /// <param name="graph">The MetaGraph whose input contract should be validated.</param>
        /// <returns>True if all declared inputs are satisfied, false otherwise.</returns>
        private bool ValidateAndFillInputs(MetaGraph graph)
        {
            List<string> errors = new();

            // expression -> (tensorName, dimIndex, size) for every symbolic dim seen so far. Two inputs
            // sharing the same symbolic name (as assigned by the ONNX exporter) are declaring that those
            // dims must be equal at runtime - this is how that co-dependency gets enforced.
            Dictionary<string, (string tensorName, int dimIndex, int size)> symbolicDims = new();

            // Pass 1: presence/dtype/shape only. Must fully complete, and fail on any problem, before
            // filling optional defaults below - a default's dim expression can reference another declared
            // input, which isn't safe to evaluate until we know that input is actually present, regardless
            // of where it falls in graph.Inputs' order.
            foreach (InputBinding declaredInput in graph.Inputs)
            {
                string name = declaredInput.TensorName;

                if (!_tensorPool.TryGetTensor(name, out Tensor tensor) || tensor == null)
                {
                    if (!declaredInput.IsOptional)
                    {
                        errors.Add($"Missing required input '{name}'");
                    }
                    continue;
                }

                if (!TryGetExpectedDataType(declaredInput.Dtype, out DataType expectedType))
                {
                    LingotionLogger.Error($"MetaGraphRunner: input '{name}' declares unsupported dtype: {declaredInput.Dtype}");
                    return false;
                }
                if (tensor.dataType != expectedType)
                {
                    errors.Add($"Input '{name}' has wrong dtype: expected '{declaredInput.Dtype}', got '{tensor.dataType}'");
                }

                if (tensor.shape.rank != declaredInput.Dims.Count)
                {
                    errors.Add($"Input '{name}' has wrong number of dimensions: expected {declaredInput.Dims.Count}, got {tensor.shape.rank}");
                    continue;
                }

                for (int j = 0; j < declaredInput.Dims.Count; j++)
                {
                    DimEntry dim = declaredInput.Dims[j];
                    int size = tensor.shape[j];

                    if (dim.Type == DimEntry.Types.DimType.DimStatic)
                    {
                        if (size != dim.Size)
                        {
                            errors.Add($"Input '{name}' has wrong size at dimension {j}: expected {dim.Size}, got {size}");
                        }
                    }
                    else if (dim.Type == DimEntry.Types.DimType.DimSymbolic)
                    {
                        string expression = dim.Expression;
                        if (symbolicDims.TryGetValue(expression, out var prior))
                        {
                            if (prior.size != size)
                            {
                                errors.Add($"Input '{name}' dimension {j} has size {size}, but must equal input '{prior.tensorName}' " +
                                    $"dimension {prior.dimIndex} (size {prior.size}) - both share symbolic dimension '{expression}'");
                            }
                        }
                        else
                        {
                            symbolicDims[expression] = (name, j, size);
                        }
                    }
                    // Runtime dims are unknowable ahead of time; any size is accepted.
                }
            }

            if (errors.Count > 0)
            {
                foreach (string error in errors)
                {
                    LingotionLogger.Error($"MetaGraphRunner: invalid inputs: {error}");
                }
                return false;
            }

            // Pass 2: every declared input is now confirmed present-and-valid or optional-and-absent, so it
            // is safe to fill missing optional defaults - any tensor their dim expressions reference is
            // guaranteed resolvable.
            Dictionary<string, HostValue> emptyHost = new();
            foreach (InputBinding declaredInput in graph.Inputs)
            {
                string name = declaredInput.TensorName;
                if (_tensorPool.ContainsTensor(name) || !declaredInput.IsOptional || declaredInput.DefaultValue == null)
                {
                    continue;
                }

                Tensor defaultTensor = BuildTensorCreateArray(declaredInput.DefaultValue, emptyHost);
                if (defaultTensor == null)
                {
                    LingotionLogger.Error($"MetaGraphRunner: input '{name}': failed to build default value");
                    return false;
                }
                LingotionLogger.Debug($"MetaGraphRunner: filled optional input '{name}' with its default value {defaultTensor.shape}");
                _tensorPool.SetTensor(name, defaultTensor);
            }

            return true;
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
        private void ProcessInputsForSymbolicDims(IEnumerable<InputBinding> inputs)
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
                // Inputs are renamed into the ONNX input name below and stay under it afterwards,
                // so a tensor bound on an earlier pass through this node - as happens every
                // iteration of a loop - still counts as present.
                bool isBound = _tensorPool.ContainsTensor(input.TensorName)
                    || (!string.IsNullOrEmpty(input.InputName) && _tensorPool.ContainsTensor(input.InputName));

                if (!isBound && !TryFillOptionalInput(input, nodeId))
                {
                    yield break;
                }

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
            string modelWorkloadID = Module.GetWorkloadID(modelMD5, ResolveBackendForNode(node, modelMD5));
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
        /// Fills a node input that is missing from the tensor pool from its declared default value.
        /// A required input that is absent is a hard error, as is an optional one whose graph
        /// declares no default to build from.
        /// </summary>
        /// <param name="input">The node input binding to satisfy.</param>
        /// <param name="nodeId">The node the input belongs to, for error reporting.</param>
        /// <returns>True if the input is now bound, false if the node should be aborted.</returns>
        private bool TryFillOptionalInput(InputBinding input, string nodeId)
        {
            if (!input.IsOptional)
            {
                LingotionLogger.Error($"MetaGraphRunner: Node '{nodeId}' - required input tensor '{input.TensorName}' not found");
                return false;
            }

            if (input.DefaultValue == null)
            {
                LingotionLogger.Error($"MetaGraphRunner: Node '{nodeId}' - optional input '{input.TensorName}' has no default_value");
                return false;
            }

            Tensor tensor = BuildTensorCreateArray(input.DefaultValue, _hostValues);
            if (tensor == null)
            {
                LingotionLogger.Error($"MetaGraphRunner: Node '{nodeId}' - could not build default value for optional input '{input.TensorName}'");
                return false;
            }

            _tensorPool.SetTensor(input.TensorName, tensor);
            LingotionLogger.Debug($"MetaGraphRunner: Node '{nodeId}' - filled optional input '{input.TensorName}' with its default value {tensor.shape}");
            return true;
        }

        /// <summary>
        /// Maps a node's preferred_device onto a Unity backend. The preference is a portable hardware
        /// class rather than a backend name, and the graph allows a runtime that cannot provide the
        /// requested device to fall back, so a pinned backend is only honoured when a workload for it
        /// has actually been registered for this model.
        /// </summary>
        /// <param name="node">The node whose device preference to resolve.</param>
        /// <param name="modelMD5">MD5 of the node's model, used to look up the pinned workload.</param>
        /// <returns>The backend the node's workload should be taken from.</returns>
        private BackendType ResolveBackendForNode(Node node, string modelMD5)
        {
            if (node.PreferredDevice == Metaonnx.DeviceType.DeviceUnspecified)
            {
                return _config.PreferredBackendType;
            }

            BackendType? requested = node.PreferredDevice switch
            {
                Metaonnx.DeviceType.DeviceCpu => BackendType.CPU,
                Metaonnx.DeviceType.DeviceGpu => BackendType.GPUCompute,
                // Unity Inference Engine exposes no NPU backend, so an NPU request has no mapping
                // here and is left to the configured backend.
                _ => null
            };

            if (!requested.HasValue || requested.Value == _config.PreferredBackendType)
            {
                return _config.PreferredBackendType;
            }

            if (!InferenceWorkloadManager.Instance.HasWorkload(Module.GetWorkloadID(modelMD5, requested.Value)))
            {
                LingotionLogger.Warning(
                    $"MetaGraphRunner: Node '{node.Id}' requests {node.PreferredDevice}, but no workload is registered " +
                    $"on {requested.Value} - falling back to {_config.PreferredBackendType}");
                return _config.PreferredBackendType;
            }

            LingotionLogger.Debug($"MetaGraphRunner: Node '{node.Id}' pinned to {requested.Value} by preferred_device");
            return requested.Value;
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

                // a fractional remainder feeds a shape computation that then disagrees with the tensor it is
                // multiplied against by a single element.
                HostBinaryOp.Types.Op.Mod => (int)rightVal != 0 ? (int)leftVal % (int)rightVal : 0,
                _ => 0
            };


            // both to be integers. An int left operand combined with a float right one has to truncate here,
            // because the graph relies on that truncation when it derives lengths (for example the upsampled
            // loudness envelope). Keeping full precision instead lets the rounding happen later and one element
            // further along, which surfaces as a broadcast mismatch in post_process_loudness.
            _hostValues[action.DestHost] = IsIntegerValueRef(action.Left)
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
            Tensor tensor = BuildTensorCreateArray(action, _hostValues);
            if (tensor == null)
            {
                return;
            }
            _tensorPool.SetTensor(action.DestTensor.Name, tensor);
        }

        /// <summary>
        /// Builds a tensor from a TensorCreate spec: resolves each dim (a static size, or a symbolic/runtime
        /// expression evaluated against the host variables and tensor pool), then fills it per the spec's
        /// fill mode. Shared by the TensorCreate host action and optional-input default filling.
        /// </summary>
        /// <param name="action">The TensorCreate spec to build from.</param>
        /// <param name="host">Host variables the dim expressions may reference.</param>
        /// <returns>The created tensor, or null on failure.</returns>
        private Tensor BuildTensorCreateArray(TensorCreate action, Dictionary<string, HostValue> host)
        {
            string dtype = action.Dtype?.ToLowerInvariant();

            List<int> dimList = new(action.Dims.Count);
            foreach (DimEntry dim in action.Dims)
            {
                if (dim.Type == DimEntry.Types.DimType.DimStatic)
                {
                    dimList.Add((int)dim.Size);
                    continue;
                }

                // Symbolic and runtime dims carry an expression resolved against the current host
                // variables and tensor pool rather than a size known ahead of time.
                // No symbolic var dict here: a TensorCreate dim expression is resolved purely against host
                // variables and the tensor pool, not against another node's symbolic dim bindings.
                if (!SymExprParser.Evaluate(dim.Expression, EmptyVarDict, host, _tensorPool, out uint resolved))
                {
                    LingotionLogger.Error($"MetaGraphRunner: TensorCreate - failed to evaluate dim expression '{dim.Expression}'");
                    return null;
                }
                dimList.Add((int)resolved);
            }
            if (dimList.Count == 0)
            {
                dimList.Add(1);
            }

            int[] dims = dimList.ToArray();
            TensorShape shape = dims.Length switch
            {
                1 => new TensorShape(dims[0]),
                2 => new TensorShape(dims[0], dims[1]),
                3 => new TensorShape(dims[0], dims[1], dims[2]),
                4 => new TensorShape(dims[0], dims[1], dims[2], dims[3]),
                _ => new TensorShape(dims)
            };

            if (action.Fill != TensorCreate.Types.Fill.Zeros &&
                action.Fill != TensorCreate.Types.Fill.Ones &&
                action.Fill != TensorCreate.Types.Fill.Value)
            {
                LingotionLogger.Error($"MetaGraphRunner: TensorCreate - unknown fill '{action.Fill}'");
                return null;
            }
            if (action.Fill == TensorCreate.Types.Fill.Value && action.Value == null)
            {
                LingotionLogger.Error("MetaGraphRunner: TensorCreate - VALUE fill requires a scalar value");
                return null;
            }
            float fillValue = action.Fill switch
            {
                TensorCreate.Types.Fill.Ones => 1f,
                TensorCreate.Types.Fill.Value => ScalarLiteralToHostValue(action.Value).ToFloat(),
                _ => 0f
            };

            int totalSize = shape.length;

            switch (dtype)
            {
                case "float32":
                case "float":
                    float[] floatData = new float[totalSize];
                    Array.Fill(floatData, fillValue);
                    return new Tensor<float>(shape, floatData);

                // Unity has no 64-bit integer tensor, so int64 is represented as Tensor<int> here just as
                // it is for every integer tensor Thespeon feeds the graph.
                case "int64":
                case "long":
                case "int32":
                case "int":
                    int[] intData = new int[totalSize];
                    Array.Fill(intData, (int)fillValue);
                    return new Tensor<int>(shape, intData);

                default:
                    LingotionLogger.Error($"MetaGraphRunner: TensorCreate - unsupported dtype '{action.Dtype}'");
                    return null;
            }
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
                if (_module.HasModelMD5(candidate))
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
                modelId = _module.GetInternalModelID(path);
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
