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
        /// <summary>
        /// Gets the unique identifier of the column definition.
        /// </summary>
        public Guid ColumnId { get; }

        /// <summary>
        /// Gets the name of the column.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Gets the zero-based index of the column in the plan.
        /// </summary>
        public int ColumnIndex { get; }

        /// <summary>
        /// Gets the execution phase description (e.g., Evaluation or Assembly).
        /// </summary>
        public string Phase { get; }

        /// <summary>
        /// Gets the elapsed duration in stopwatch ticks spent executing this node.
        /// </summary>
        public long ElapsedTicks { get; }

        /// <summary>
        /// Gets the input value received by this node, or <c>null</c>.
        /// </summary>
        public object? InputValue { get; }

        /// <summary>
        /// Gets the output value produced by this node, or <c>null</c>.
        /// </summary>
        public object? OutputValue { get; }

        /// <summary>
        /// Gets the error message if node execution failed; otherwise, <c>null</c>.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets a value indicating whether execution succeeded without errors.
        /// </summary>
        public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Gets the elapsed execution time in microseconds.
        /// </summary>
        public double ElapsedMicroseconds => ElapsedTicks * 1_000_000.0 / Stopwatch.Frequency;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionNodeRecord"/> class.
        /// </summary>
        /// <param name="columnId">The column unique identifier.</param>
        /// <param name="columnName">The column name.</param>
        /// <param name="columnIndex">The zero-based column index.</param>
        /// <param name="phase">The execution phase name.</param>
        /// <param name="elapsedTicks">The elapsed stopwatch ticks.</param>
        /// <param name="inputValue">The node input value.</param>
        /// <param name="outputValue">The node output value.</param>
        /// <param name="errorMessage">The error message, or <c>null</c>.</param>
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
        /// <summary>
        /// Gets the source input object parsed during this execution.
        /// </summary>
        public object? Source { get; }

        /// <summary>
        /// Gets the UTC start time of the execution.
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// Gets the UTC end time of the execution.
        /// </summary>
        public DateTime EndTime { get; private set; }

        /// <summary>
        /// Gets the total elapsed ticks spent during execution.
        /// </summary>
        public long TotalTicks { get; private set; }

        /// <summary>
        /// Gets a value indicating whether execution completed successfully.
        /// </summary>
        public bool WasSuccessful { get; private set; }

        /// <summary>
        /// Gets the final formatted output string, or <c>null</c> if execution failed before assembly.
        /// </summary>
        public string? FinalOutput { get; private set; }

        /// <summary>
        /// Gets the top-level error message if execution failed; otherwise, <c>null</c>.
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// Gets the ordered list of execution records for each node visited during execution.
        /// </summary>
        public IReadOnlyList<ExecutionNodeRecord> NodeRecords { get; }

        /// <summary>
        /// Gets the count of execution passes per column index.
        /// </summary>
        public IReadOnlyDictionary<int, int> ExecutionCounts { get; }

        /// <summary>
        /// Gets the list of variable values present during execution.
        /// </summary>
        public IReadOnlyList<object?> Variables { get; }

        private readonly List<ExecutionNodeRecord> _records;
        private readonly Dictionary<int, int> _counts;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionTrace"/> class.
        /// </summary>
        /// <param name="source">The source object being parsed.</param>
        /// <param name="variables">The optional variables collection.</param>
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

        /// <summary>
        /// Records execution details for an individual execution node.
        /// </summary>
        /// <param name="columnId">The column unique identifier.</param>
        /// <param name="columnName">The column name.</param>
        /// <param name="columnIndex">The zero-based column index.</param>
        /// <param name="phase">The execution phase name.</param>
        /// <param name="elapsedTicks">The elapsed duration in stopwatch ticks.</param>
        /// <param name="inputValue">The node input value.</param>
        /// <param name="outputValue">The node output value.</param>
        /// <param name="errorMessage">The error message, or <c>null</c>.</param>
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

        /// <summary>
        /// Marks the execution trace as completed with the specified output string and optional error message.
        /// </summary>
        /// <param name="finalOutput">The final assembled output string.</param>
        /// <param name="errorMessage">The optional error message.</param>
        public void Complete(string? finalOutput, string? errorMessage = null)
        {
            EndTime      = DateTime.UtcNow;
            TotalTicks   = (EndTime - StartTime).Ticks;
            FinalOutput  = finalOutput;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Marks the execution trace as failed with the specified error message.
        /// </summary>
        /// <param name="errorMessage">The error message describing the failure.</param>
        public void MarkFailed(string errorMessage)
        {
            WasSuccessful = false;
            ErrorMessage  = errorMessage;
            EndTime      = DateTime.UtcNow;
            TotalTicks   = (EndTime - StartTime).Ticks;
        }

        /// <summary>
        /// Gets the total execution duration in microseconds.
        /// </summary>
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
