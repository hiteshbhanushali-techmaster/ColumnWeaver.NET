using System;
using System.Collections.Generic;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Interfaces;
using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET.Application.Engine
{
    /// <summary>
    /// Stores all intermediate column evaluation results for a single
    /// <see cref="ParserEngine.Parse"/> invocation. Acts as the shared memory
    /// between the evaluation phase and the assembly phase of the pipeline.
    /// </summary>
    /// <remarks>
    /// Thread-safety: A new context instance is created per <c>Parse</c> call.
    /// This class is not intended for concurrent access.
    /// </remarks>
    public sealed class ColumnEvaluationContext : IExecutionContext
    {
        // -----------------------------------------------------------------
        //  Internal state
        // -----------------------------------------------------------------

        private readonly Dictionary<Guid, string>          _resultsByColumnId;
        private readonly Dictionary<string, string>        _resultsByName;
        private readonly HashSet<Guid>                     _inProgress;
        private readonly HashSet<Guid>                     _skippedColumns;

        // -----------------------------------------------------------------
        //  Constructor
        // -----------------------------------------------------------------

        /// <summary>
        /// Initializes a new evaluation context for a single parse operation.
        /// </summary>
        /// <param name="source">
        /// The root source object being parsed. May be null.
        /// </param>
        /// <param name="configuration">
        /// The <see cref="ParserConfiguration"/> driving this parse. Must not be null.
        /// </param>
        public ColumnEvaluationContext(object? source, ParserConfiguration configuration)
        {
            Source        = source;
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _resultsByColumnId = new Dictionary<Guid, string>();
            _resultsByName     = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _inProgress        = new HashSet<Guid>();
            _skippedColumns    = new HashSet<Guid>();
        }

        // -----------------------------------------------------------------
        //  Public read-only surface
        // -----------------------------------------------------------------

        /// <summary>Gets the root source object being parsed.</summary>
        public object? Source { get; }

        /// <summary>Gets the configuration that governs this parse operation.</summary>
        public ParserConfiguration Configuration { get; }

        /// <summary>
        /// Attempts to retrieve the value of a configuration variable by name.
        /// </summary>
        /// <param name="name">The name of the variable to look up.</param>
        /// <param name="value">When this method returns, contains the variable value if found; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if the variable exists in the configuration; otherwise, <c>false</c>.</returns>
        public bool TryGetVariable(string name, out object? value)
        {
            if (Configuration.Variables != null && Configuration.Variables.TryGetValue(name, out value))
            {
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Gets the evaluated result for the specified column index, returning <c>null</c> in this context implementation.
        /// </summary>
        /// <param name="index">The zero-based column index.</param>
        /// <returns><c>null</c> for this context implementation.</returns>
        public object? GetResult(int index) => null;

        /// <summary>
        /// Gets the configuration variables array for this context implementation.
        /// </summary>
        public object?[] Variables { get; } = Array.Empty<object?>();


        // -----------------------------------------------------------------
        //  Lifecycle management
        // -----------------------------------------------------------------

        /// <summary>
        /// Marks a column as being currently evaluated. Used at runtime
        /// to detect column cycles that escaped the static dependency sort.
        /// </summary>
        public void BeginEvaluation(ColumnDefinition column)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));
            _inProgress.Add(column.Id);
        }

        /// <summary>
        /// Stores the final evaluated result for a column and removes it
        /// from the in-progress set. Must be called once per <see cref="BeginEvaluation"/>.
        /// </summary>
        public void CompleteEvaluation(ColumnDefinition column, string result)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));

            _inProgress.Remove(column.Id);
            _resultsByColumnId[column.Id] = result;

            // Register under both Name and Alias for cross-column reference lookup.
            if (!string.IsNullOrWhiteSpace(column.Name))
                _resultsByName[column.Name] = result;

            if (!string.IsNullOrWhiteSpace(column.Alias))
                _resultsByName[column.Alias!] = result;
        }

        /// <summary>
        /// Marks a column as skipped (due to <see cref="Core.Enums.NullHandlingStrategy.Skip"/>)
        /// and removes it from the in-progress set. Skipped columns are omitted from output.
        /// </summary>
        public void MarkSkipped(ColumnDefinition column)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));

            _inProgress.Remove(column.Id);
            _skippedColumns.Add(column.Id);
        }

        /// <summary>
        /// Removes a column from the in-progress set without storing a result.
        /// Called when evaluation throws an exception so state remains consistent.
        /// </summary>
        public void CancelEvaluation(ColumnDefinition column)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));
            _inProgress.Remove(column.Id);
        }

        // -----------------------------------------------------------------
        //  Query methods
        // -----------------------------------------------------------------

        /// <summary>Returns true if the column is currently being evaluated (cycle guard).</summary>
        public bool IsInProgress(Guid columnId)  => _inProgress.Contains(columnId);

        /// <summary>Returns true if the column has a stored result.</summary>
        public bool HasResult(Guid columnId)      => _resultsByColumnId.ContainsKey(columnId);

        /// <summary>Returns true if the column was skipped (null-skip strategy).</summary>
        public bool IsSkipped(Guid columnId)      => _skippedColumns.Contains(columnId);

        /// <summary>
        /// Attempts to retrieve the stored result for a column by its Id.
        /// Returns false when the column has not been evaluated yet or was skipped.
        /// </summary>
        public bool TryGetResultById(Guid columnId, out string? result)
            => _resultsByColumnId.TryGetValue(columnId, out result);

        /// <summary>
        /// Attempts to retrieve the stored result for a column by its Name or Alias
        /// (case-insensitive). Used during cross-column reference resolution.
        /// </summary>
        public bool TryGetResultByName(string name, out string? result)
            => _resultsByName.TryGetValue(name, out result);

        /// <summary>
        /// Returns a snapshot of all currently resolved column results,
        /// keyed by Name and Alias (case-insensitive).
        /// Used to expand <c>${ColumnName}</c> tokens in formula expressions.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetAllResults()
            => _resultsByName;
    }
}
