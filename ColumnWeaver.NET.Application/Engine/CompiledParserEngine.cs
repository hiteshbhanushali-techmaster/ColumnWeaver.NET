using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using ColumnWeaver.NET.Application.Abstractions;
using ColumnWeaver.NET.Application.Execution;
using ColumnWeaver.NET.Application.Validation;
using ColumnWeaver.NET.Core.Interfaces;
using ColumnWeaver.NET.Core.ParserConfiguration;
using AppExecution = ColumnWeaver.NET.Application.Execution;

namespace ColumnWeaver.NET.Application.Engine
{
    /// <summary>
    /// V2.5 high-performance parser engine built on a pre-compiled
    /// <see cref="ExecutionPlan"/>.
    /// </summary>
    /// <remarks>
    /// <b>V2.5 enhancements over V2:</b>
    /// <list type="bullet">
    ///   <item>Structural <see cref="ExecutionPlanCache"/> — plans shared across
    ///         logically identical configurations.</item>
    ///   <item><see cref="ExecutionOptions"/> — DebugMode tracing, timeout support,
    ///         and warning-allowance.</item>
    ///   <item><see cref="ExecutionTrace"/> — per-node timing and value capture
    ///         when DebugMode is enabled (zero overhead otherwise).</item>
    ///   <item>Optimised context reset for <c>ParseMany</c>.</item>
    ///   <item>Thread-safe plan cache.</item>
    /// </list>
    ///
    /// <b>Thread safety:</b> The cached <see cref="ExecutionPlan"/> is immutable and
    /// thread-safe. Multiple threads can call <see cref="Parse"/> concurrently
    /// because each call creates its own <see cref="AppExecution.ExecutionContext"/>.
    /// Do NOT call <see cref="ParseMany"/> from multiple threads on the same instance.
    /// </remarks>
    public sealed class CompiledParserEngine
    {
        private readonly ExecutionPlanCompiler   _compiler;
        private readonly ExecutionPlanCache      _planCache;

        private sealed class PlanCacheSlot
        {
            public readonly ulong Fingerprint;
            public readonly ExecutionPlan Plan;
            public readonly Type? SourceType;

            public PlanCacheSlot(ulong fingerprint, ExecutionPlan plan, Type? sourceType)
            {
                Fingerprint = fingerprint;
                Plan = plan;
                SourceType = sourceType;
            }
        }

        private volatile PlanCacheSlot? _lastSlot;

        [ThreadStatic]
        private static AppExecution.ExecutionContext? _threadContext;
        [ThreadStatic]
        private static StringBuilder? _threadStringBuilder;
        [ThreadStatic]
        private static bool _threadContextInUse;

        public ExecutionOptions Options { get; set; } = ExecutionOptions.Default;

        public CompiledParserEngine(
            IPropertyResolver         resolver,
            IExpressionEngine        engine,
            ICustomFunctionRegistry? registry = null)
        {
            _compiler   = new ExecutionPlanCompiler(resolver, engine, registry);
            _planCache  = new ExecutionPlanCache();
        }

        public string Parse(
            ParserConfiguration config,
            object?             source,
            Type?               sourceType = null)
        {
            if (sourceType == null && source != null)
                sourceType = source.GetType();

            var plan = GetOrCompilePlan(config, sourceType);
            return ParsePlan(plan, source);
        }

        /// <summary>
        /// Retrieves or compiles the <see cref="ExecutionPlan"/> for the given configuration and source type.
        /// </summary>
        public ExecutionPlan GetPlan(ParserConfiguration config, Type? sourceType = null)
        {
            return GetOrCompilePlan(config, sourceType);
        }

        /// <summary>
        /// Parses a source object using a pre-resolved <see cref="ExecutionPlan"/>,
        /// bypassing configuration fingerprinting and cache lookups.
        /// </summary>
        public string ParsePlan(ExecutionPlan plan, object? source)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            if (plan.FastRowExecutor != null && !Options.DebugMode && Options.Timeout == Timeout.InfiniteTimeSpan)
            {
                var sb = _threadStringBuilder ?? (_threadStringBuilder = new StringBuilder(256));
                sb.Clear();
                var fastResult = plan.FastRowExecutor(source, sb);
                if (fastResult != null) return fastResult;
            }

            if (!_threadContextInUse && _threadContext != null && _threadContext.ColumnCount >= plan.TotalColumnCount)
            {
                _threadContextInUse = true;
                try
                {
                    _threadContext.Reset(source, plan.CompiledVariables, plan.VariableIndexes);
                    ExecutePlan(plan, _threadContext, Options, source);

                    var sb = _threadStringBuilder ?? (_threadStringBuilder = new StringBuilder(256));
                    sb.Clear();
                    AssembleOutput(plan, _threadContext, sb, Options, source);
                    return sb.ToString();
                }
                finally
                {
                    _threadContextInUse = false;
                }
            }

            var context = plan.CreateContext();
            if (!_threadContextInUse)
            {
                _threadContext = context;
            }
            context.Reset(source);
            return ExecuteAndAssemble(plan, context, Options, source);
        }

        public string ParseWithTrace(
            ParserConfiguration  config,
            object?               source,
            Type?                sourceType,
            out ExecutionTrace   trace)
        {
            var plan    = GetOrCompilePlan(config, sourceType);
            var context = plan.CreateContext();
            context.Reset(source);

            var opts = new ExecutionOptions { DebugMode = true };
            trace = ExecuteAndAssembleWithTrace(plan, context, opts, source);
            return trace.FinalOutput ?? string.Empty;
        }

        public ValidationResult Validate(ParserConfiguration config, Type? sourceType = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return ConfigurationValidator.Validate(config, sourceType, null, null, null);
        }

        public ValidationResult ValidateFull(
            ParserConfiguration config,
            IPropertyResolver   resolver,
            IExpressionEngine  engine,
            Type?               sourceType = null)
        {
            if (config   == null) throw new ArgumentNullException(nameof(config));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (engine   == null) throw new ArgumentNullException(nameof(engine));

            return ConfigurationValidator.Validate(config, sourceType, resolver, engine, null);
        }

        public IEnumerable<string> ParseMany(
            ParserConfiguration  config,
            IEnumerable<object?> inputs,
            Type?                sourceType = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            return ParseManyCore(config, inputs, sourceType);
        }

        private IEnumerable<string> ParseManyCore(
            ParserConfiguration  config,
            IEnumerable<object?> inputs,
            Type?                sourceType)
        {
            using (var enumerator = inputs.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    yield break;

                var first = enumerator.Current;
                var resolvedSourceType = sourceType ?? first?.GetType();
                var plan = GetOrCompilePlan(config, resolvedSourceType);
                var sb = new StringBuilder(256);
                var opts = Options;

                yield return ProcessRow(plan, first, sb, opts);

                while (enumerator.MoveNext())
                {
                    yield return ProcessRow(plan, enumerator.Current, sb, opts);
                }
            }
        }

        /// <summary>
        /// Parses a batch of input objects using a pre-resolved <see cref="ExecutionPlan"/>,
        /// avoiding per-batch plan compilation or configuration fingerprinting.
        /// Enumerates the input sequence exactly once without buffering.
        /// </summary>
        public IEnumerable<string> ParseManyWithPlan(
            ExecutionPlan        plan,
            IEnumerable<object?> inputs)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            return ParseManyWithPlanCore(plan, inputs);
        }

        private IEnumerable<string> ParseManyWithPlanCore(
            ExecutionPlan        plan,
            IEnumerable<object?> inputs)
        {
            using (var enumerator = inputs.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    yield break;

                var sb = new StringBuilder(256);
                var opts = Options;

                yield return ProcessRow(plan, enumerator.Current, sb, opts);

                while (enumerator.MoveNext())
                {
                    yield return ProcessRow(plan, enumerator.Current, sb, opts);
                }
            }
        }

        private static string ProcessRow(
            ExecutionPlan    plan,
            object?          source,
            StringBuilder   sb,
            ExecutionOptions opts)
        {
            if (plan.FastRowExecutor != null && !opts.DebugMode && opts.Timeout == Timeout.InfiniteTimeSpan)
            {
                sb.Clear();
                var fastResult = plan.FastRowExecutor(source, sb);
                if (fastResult != null) return fastResult;
            }

            var fallbackCtx = plan.CreateContext();
            fallbackCtx.Reset(source);
            ExecutePlan(plan, fallbackCtx, opts, source);
            sb.Clear();
            AssembleOutput(plan, fallbackCtx, sb, opts, source);
            return sb.ToString();
        }

        private ExecutionPlan GetOrCompilePlan(ParserConfiguration config, Type? sourceType)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var fp = ExecutionPlanCache.ComputeFastFingerprint(config, sourceType);
            var slot = _lastSlot;
            if (slot != null && slot.Fingerprint == fp && slot.SourceType == sourceType)
                return slot.Plan;

            var cached = _planCache.TryGet(fp);
            if (cached != null)
            {
                _lastSlot = new PlanCacheSlot(fp, cached, sourceType);
                return cached;
            }

            var plan = _compiler.Compile(config, sourceType);
            _planCache.Put(fp, plan);
            _planCache.Put(config, plan, sourceType);
            _lastSlot = new PlanCacheSlot(fp, plan, sourceType);
            return plan;
        }

        private static string ExecuteAndAssemble(
            ExecutionPlan    plan,
            AppExecution.ExecutionContext context,
            ExecutionOptions opts,
            object?          source)
        {
            ExecutePlan(plan, context, opts, source);

            var sb = new StringBuilder(256);
            AssembleOutput(plan, context, sb, opts, source);
            return sb.ToString();
        }

        private static ExecutionTrace ExecuteAndAssembleWithTrace(
            ExecutionPlan    plan,
            AppExecution.ExecutionContext context,
            ExecutionOptions opts,
            object?          source)
        {
            var tracer = new ExecutionTracer(opts, source, context.Variables);
            ExecutePlanWithTrace(plan, context, opts, tracer);
            var sb = new StringBuilder(256);
            AssembleOutput(plan, context, sb, opts, source);
            var result = sb.ToString();
            tracer.Trace?.Complete(result);
            return tracer.Trace!;
        }

        private static void ExecutePlan(
            ExecutionPlan    plan,
            AppExecution.ExecutionContext context,
            ExecutionOptions opts,
            object?          source)
        {
            var timeout = opts?.Timeout ?? Timeout.InfiniteTimeSpan;
            var deadline = timeout == Timeout.InfiniteTimeSpan
                ? DateTime.MaxValue
                : DateTime.UtcNow.Add(timeout);

            foreach (var node in plan.EvaluationOrder)
            {
                if (!node.IsEnabled) continue;

                if (deadline != DateTime.MaxValue && DateTime.UtcNow > deadline)
                    throw new TimeoutException(
                        $"Execution timeout of {timeout} exceeded at column '{node.ColumnName}'.");

                ExecuteNode(node, context);
            }
        }

        private static void ExecutePlanWithTrace(
            ExecutionPlan     plan,
            AppExecution.ExecutionContext  context,
            ExecutionOptions  opts,
            ExecutionTracer   tracer)
        {
            var timeout  = opts?.Timeout ?? Timeout.InfiniteTimeSpan;
            var deadline = timeout == Timeout.InfiniteTimeSpan
                ? DateTime.MaxValue
                : DateTime.UtcNow.Add(timeout);

            foreach (var node in plan.EvaluationOrder)
            {
                if (!node.IsEnabled) continue;

                if (deadline != DateTime.MaxValue && DateTime.UtcNow > deadline)
                    throw new TimeoutException(
                        $"Execution timeout of {timeout} exceeded at column '{node.ColumnName}'.");

                var sw = Stopwatch.StartNew();
                object? result = null;
                string? error  = null;

                try
                {
                    tracer.BeginNode(node.ColumnId, node.ColumnName, node.ColumnIndex, "Execute", context.Source);
                    result = node.Execute(context);
                }
                catch (Exceptions.ColumnEvaluationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                finally
                {
                    sw.Stop();
                    if (error != null)
                        tracer.EndWithError(error, result);
                    else
                        tracer.EndNode(result);
                }
            }
        }

        private static void ExecuteNode(ExecutionNode node, AppExecution.ExecutionContext context)
        {
            try
            {
                node.Execute(context);
            }
            catch (Exceptions.ColumnEvaluationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exceptions.ColumnEvaluationException(
                    node.ColumnId, node.ColumnName,
                    $"Unhandled exception during compiled execution: {ex.Message}", ex);
            }
        }

        private static void AssembleOutput(
            ExecutionPlan    plan,
            AppExecution.ExecutionContext context,
            StringBuilder   sb,
            ExecutionOptions opts,
            object?          source)
        {
            var assemblyOrder = plan.AssemblyOrder;
            int count         = assemblyOrder.Count;

            int lastActiveIdx = -1;
            for (int i = count - 1; i >= 0; i--)
            {
                if (!context.IsSkipped(assemblyOrder[i].ColumnIndex))
                {
                    lastActiveIdx = i;
                    break;
                }
            }

            if (lastActiveIdx < 0) return;

            for (int i = 0; i <= lastActiveIdx; i++)
            {
                var node = assemblyOrder[i];
                if (context.IsSkipped(node.ColumnIndex)) continue;

                sb.Append(context.GetFormatted(node.ColumnIndex));

                if (i < lastActiveIdx)
                {
                    var sep = node.SeparatorOverride ?? plan.GlobalSeparator;
                    if (!string.IsNullOrEmpty(sep))
                        sb.Append(sep);
                }
            }
        }
    }
}
