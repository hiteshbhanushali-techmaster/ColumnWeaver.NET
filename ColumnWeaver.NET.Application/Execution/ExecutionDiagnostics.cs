using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ColumnWeaver.NET.Application.Exceptions;

namespace ColumnWeaver.NET.Application.Execution
{
    /// <summary>
    /// V2.5 — Global execution options that control runtime behaviour.
    /// </summary>
    public sealed class ExecutionOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether execution tracing is enabled.
        /// When <c>false</c>, all tracing code paths are eliminated by the JIT
        /// and carry zero overhead.
        /// Default is <c>false</c>.
        /// </summary>
        public bool DebugMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether execution should allow warnings
        /// during validation without blocking compilation/execution.
        /// When <c>false</c> (default), only errors block execution.
        /// </summary>
        public bool AllowWarnings { get; set; }

        /// <summary>
        /// Gets or sets the maximum duration allowed for a single row parse operation
        /// (per item in a batch). A value of <see cref="Timeout.InfiniteTimeSpan"/>
        /// means no timeout. Default is <see cref="Timeout.InfiniteTimeSpan"/>.
        /// </summary>
        /// <remarks>
        /// Timeout enforcement operates cooperatively by checking deadlines before and after
        /// each column node. A slow column causes a <see cref="TimeoutException"/> to be reported
        /// after that column returns; in-flight synchronous expressions or custom functions
        /// cannot be preemptively aborted while running.
        /// </remarks>
        public TimeSpan Timeout { get; set; } = System.Threading.Timeout.InfiniteTimeSpan;

        /// <summary>
        /// Gets or sets an optional cancellation token to cooperatively cancel execution
        /// between column evaluations and between rows in batch processing.
        /// In-flight synchronous expressions, custom functions, and property resolutions
        /// cannot be preemptively interrupted mid-execution.
        /// Default is <see cref="CancellationToken.None"/>.
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        /// <summary>
        /// Creates a default <see cref="ExecutionOptions"/> with all debug features disabled.
        /// </summary>
        public static ExecutionOptions Default => new ExecutionOptions();

        /// <summary>
        /// Creates an <see cref="ExecutionOptions"/> with tracing enabled for
        /// diagnostics and debugging.
        /// </summary>
        public static ExecutionOptions WithDiagnostics()
            => new ExecutionOptions { DebugMode = true };
    }

    /// <summary>
    /// V2.5 — A structured record of a single <see cref="ExecutionNode"/>'s
    /// execution within an <see cref="ExecutionTrace"/>.
    /// </summary>
    public sealed class ExecutionNodeRecord
    {
        public Guid ColumnId { get; }
        public string ColumnName { get; }
        public int ColumnIndex { get; }
        public string Phase { get; }
        public long ElapsedTicks { get; }
        public object? InputValue { get; }
        public object? OutputValue { get; }
        public string? ErrorMessage { get; }
        public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);

        public double ElapsedMicroseconds => ElapsedTicks * 1_000_000.0 / Stopwatch.Frequency;

        public ExecutionNodeRecord(
            Guid       columnId,
            string     columnName,
            int        columnIndex,
            string     phase,
            long       elapsedTicks,
            object?    inputValue,
            object?    outputValue,
            string?    errorMessage)
        {
            ColumnId      = columnId;
            ColumnName    = columnName    ?? "";
            ColumnIndex   = columnIndex;
            Phase         = phase         ?? "";
            ElapsedTicks  = elapsedTicks;
            InputValue    = inputValue;
            OutputValue   = outputValue;
            ErrorMessage  = errorMessage;
        }
    }

    /// <summary>
    /// V2.5 — Complete execution trace captured when <see cref="ExecutionOptions.DebugMode"/>
    /// is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Capture is entirely conditional — when DebugMode is <c>false</c>, no data is
    /// collected and no allocations occur. The trace is frozen after execution completes
    /// and cannot be modified.
    /// </remarks>
    public sealed class ExecutionTrace
    {
        public object? Source { get; }
        public DateTime StartTime { get; }
        public DateTime EndTime { get; private set; }
        public long TotalTicks { get; private set; }
        public bool WasSuccessful { get; private set; }
        public string? FinalOutput { get; private set; }
        public string? ErrorMessage { get; private set; }
        public IReadOnlyList<ExecutionNodeRecord> NodeRecords { get; }
        public IReadOnlyDictionary<int, int> ExecutionCounts { get; }
        public IReadOnlyList<object?> Variables { get; }

        private readonly List<ExecutionNodeRecord> _records;
        private readonly Dictionary<int, int> _counts;

        public ExecutionTrace(object? source, IReadOnlyList<object?>? variables = null)
        {
            Source            = source;
            StartTime         = DateTime.UtcNow;
            _records           = new List<ExecutionNodeRecord>();
            _counts            = new Dictionary<int, int>();
            NodeRecords        = _records;
            ExecutionCounts    = _counts;
            Variables          = variables ?? Array.Empty<object?>();
            WasSuccessful      = true;
        }

        public void RecordNodeExecution(
            Guid       columnId,
            string     columnName,
            int        columnIndex,
            string     phase,
            long       elapsedTicks,
            object?    inputValue,
            object?    outputValue,
            string?    errorMessage)
        {
            _records.Add(new ExecutionNodeRecord(
                columnId, columnName, columnIndex, phase,
                elapsedTicks, inputValue, outputValue, errorMessage));

            if (!_counts.ContainsKey(columnIndex))
                _counts[columnIndex] = 0;
            _counts[columnIndex]++;

            if (!string.IsNullOrEmpty(errorMessage))
                WasSuccessful = false;
        }

        public void Complete(string? finalOutput, string? errorMessage = null)
        {
            EndTime      = DateTime.UtcNow;
            TotalTicks   = (EndTime - StartTime).Ticks;
            FinalOutput  = finalOutput;
            ErrorMessage = errorMessage;
        }

        public void MarkFailed(string errorMessage)
        {
            WasSuccessful = false;
            ErrorMessage  = errorMessage;
            EndTime      = DateTime.UtcNow;
            TotalTicks   = (EndTime - StartTime).Ticks;
        }

        public double TotalMicroseconds => TotalTicks * 1_000_000.0 / Stopwatch.Frequency;
    }

    /// <summary>
    /// V2.5 — Scoped execution tracer that automatically captures timing
    /// and records to an <see cref="ExecutionTrace"/> when DebugMode is enabled.
    /// </summary>
    /// <remarks>
    /// Designed to be allocation-free when tracing is disabled — the JIT eliminates
    /// all fields and calls in release builds.
    /// </remarks>
    internal sealed class ExecutionTracer
    {
        private readonly ExecutionTrace? _trace;
        private readonly bool _enabled;

        private Guid _columnId;
        private string _columnName = "";
        private int _columnIndex;
        private string _phase = "";
        private object? _inputValue;
        private Stopwatch? _sw;

        public ExecutionTracer(ExecutionOptions options, object? source, IReadOnlyList<object?>? variables = null)
        {
            _enabled = options?.DebugMode ?? false;
            _trace   = _enabled ? new ExecutionTrace(source, variables) : null;
            _sw      = _enabled ? Stopwatch.StartNew() : null;
        }

        public ExecutionTrace? Trace => _trace;

        public void BeginNode(Guid columnId, string columnName, int columnIndex, string phase, object? inputValue)
        {
            if (!_enabled) return;
            _columnId    = columnId;
            _columnName  = columnName;
            _columnIndex = columnIndex;
            _phase       = phase;
            _inputValue  = inputValue;
            _sw          = Stopwatch.StartNew();
        }

        public void EndNode(object? outputValue, string? errorMessage = null)
        {
            if (!_enabled || _trace == null || _sw == null) return;
            _sw.Stop();
            _trace.RecordNodeExecution(
                _columnId, _columnName, _columnIndex, _phase,
                _sw.ElapsedTicks, _inputValue, outputValue, errorMessage);
        }

        public void EndWithError(string errorMessage, object? outputValue = null)
        {
            if (!_enabled || _trace == null || _sw == null) return;
            _sw.Stop();
            _trace.RecordNodeExecution(
                _columnId, _columnName, _columnIndex, _phase,
                _sw.ElapsedTicks, _inputValue, outputValue, errorMessage);
        }
    }
}
