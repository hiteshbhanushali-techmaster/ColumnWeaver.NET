using System;
using System.Collections.Generic;
using System.Linq;
using ColumnWeaver.NET.Application.Engine;
using ColumnWeaver.NET.Application.Exceptions;
using ColumnWeaver.NET.Application.Factory;
using ColumnWeaver.NET.Application.Pipeline;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET.Application.Engine
{
    /// <summary>
    /// The top-level application service that converts any object into a configured
    /// string representation using a <see cref="ParserConfiguration"/> pipeline.
    /// </summary>
    /// <remarks>
    /// <b>Usage:</b>
    /// <code>
    ///   var factory = new ColumnEvaluatorFactory(myPropertyResolver, myExpressionEngine);
    ///   var engine  = new ParserEngine(factory);
    ///
    ///   var config  = new ParserConfiguration
    ///   {
    ///       GlobalSeparator = "|",
    ///       Columns =
    ///       {
    ///           new PropertyColumn { Name = "OrderId",  Order = 1, PropertyPath = "Id"       },
    ///           new PropertyColumn { Name = "Customer", Order = 2, PropertyPath = "Customer.Name" },
    ///           new FormulaColumn  { Name = "Total",    Order = 3, Expression   = "${Price} * ${Qty}" }
    ///       }
    ///   };
    ///
    ///   string result = engine.Parse(config, myOrder);
    ///   // → "1001|ACME Corp|2500.00"
    /// </code>
    ///
    /// <b>Pipeline execution order:</b>
    /// <list type="number">
    ///   <item>Validate configuration (duplicate IDs, null column list).</item>
    ///   <item>Sort all columns by <see cref="ColumnDefinition.Order"/> if <see cref="ParserConfiguration.AutoSortByOrder"/> is true.</item>
    ///   <item>Filter to enabled columns.</item>
    ///   <item>Topologically sort enabled columns by dependency graph (detect circular refs).</item>
    ///   <item>Execute two-phase <see cref="ColumnPipeline"/>.</item>
    /// </list>
    ///
    /// <b>Thread safety:</b> <see cref="ParserEngine"/> is stateless and thread-safe.
    /// A new <see cref="ColumnEvaluationContext"/> is created per <see cref="Parse"/> call.
    /// </remarks>
    public sealed class ParserEngine
    {
        private readonly IColumnEvaluatorFactory _evaluatorFactory;
        private readonly DependencyResolver      _dependencyResolver;
        private readonly ColumnPipeline          _pipeline;

        // -----------------------------------------------------------------
        //  Constructor
        // -----------------------------------------------------------------

        /// <summary>
        /// Initializes the parser engine with the evaluator factory.
        /// </summary>
        /// <param name="evaluatorFactory">
        /// The factory used to resolve a column evaluator for each column type.
        /// Must not be null.
        /// </param>
        public ParserEngine(IColumnEvaluatorFactory evaluatorFactory)
        {
            _evaluatorFactory   = evaluatorFactory ?? throw new ArgumentNullException(nameof(evaluatorFactory));
            _dependencyResolver = new DependencyResolver();
            _pipeline           = new ColumnPipeline(_evaluatorFactory);
        }

        // -----------------------------------------------------------------
        //  Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Converts the <paramref name="source"/> object to a string using the
        /// provided <paramref name="config"/> pipeline.
        /// </summary>
        /// <param name="config">
        /// The parser configuration defining columns, separators, and evaluation
        /// behaviour. Must not be null.
        /// </param>
        /// <param name="source">
        /// The source object to parse. May be null; columns handle null sources
        /// according to their individual <see cref="Core.Models.NullHandlingConfig"/>.
        /// </param>
        /// <returns>
        /// The assembled output string produced by joining all enabled, non-skipped
        /// column outputs with the configured separators.
        /// </returns>
        /// <exception cref="ParserConfigurationException">
        /// Thrown when the configuration is structurally invalid (duplicate IDs, null list).
        /// </exception>
        /// <exception cref="CircularDependencyException">
        /// Thrown when a circular cross-column dependency is detected.
        /// </exception>
        /// <exception cref="ColumnEvaluationException">
        /// Thrown when an individual column fails to evaluate.
        /// </exception>
        public string Parse(ParserConfiguration config, object? source)
        {
            ValidateConfiguration(config);

            // ── Step 1: Collect non-null columns — single pass, no LINQ ──────────
            var allColumns = new List<ColumnDefinition>(config.Columns.Count);
            foreach (var col in config.Columns)
            {
                if (col != null) allColumns.Add(col);
            }

            // ── Step 2: Sort by Order if configured ───────────────────────────────
            if (config.AutoSortByOrder)
                allColumns = allColumns.OrderBy(a => a.Order).ToList();

            // ── Step 3: Separate enabled vs. disabled (no intermediate enumerator) ─
            var enabledColumns = allColumns.FindAll(c => c.IsEnabled);

            // ── Step 4: Topological sort for formula dependency resolution ─────────
            IReadOnlyList<ColumnDefinition> evaluationOrder =
                _dependencyResolver.Sort(enabledColumns);

            // ── Step 5: Build assembly order ──────────────────────────────────────
            // When PreserveSeparatorForDisabledColumns = true, disabled columns
            // occupy a slot in the output (outputting empty string + separator).
            IReadOnlyList<ColumnDefinition> assemblyOrder =
                config.PreserveSeparatorForDisabledColumns
                    ? allColumns                // All columns — disabled ones get empty string.
                    : enabledColumns;           // Only enabled columns — gaps collapsed.

            // ── Step 6: Create a fresh context for this parse operation ────────────
            var context = new ColumnEvaluationContext(source, config);

            // ── Step 7: Run the two-phase pipeline ────────────────────────────────
            return _pipeline.Execute(evaluationOrder, assemblyOrder, context, config);
        }

        // -----------------------------------------------------------------
        //  Configuration validation
        // -----------------------------------------------------------------

        /// <summary>
        /// Validates the <paramref name="config"/> before any column processing begins.
        /// Throws <see cref="ParserConfigurationException"/> for structural violations.
        /// </summary>
        private static void ValidateConfiguration(ParserConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (config.Columns == null)
                throw new ParserConfigurationException(
                    "ParserConfiguration.Columns must not be null. " +
                    "Provide an empty list if no columns are configured.");

            // ── Guard: duplicate column IDs ─────────────────────────────────
            var seenIds = new HashSet<Guid>();
            foreach (var col in config.Columns)
            {
                if (col == null) continue;

                if (!seenIds.Add(col.Id))
                    throw new ParserConfigurationException(
                        $"Duplicate column Id detected: '{col.Id}' " +
                        $"(Column: '{col.DisplayName}'). Each column must have a unique Id.");
            }

            // ── Guard: duplicate column names among enabled columns ───────────
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in config.Columns)
            {
                if (col == null || !col.IsEnabled) continue;

                if (!string.IsNullOrWhiteSpace(col.Name) && !seenNames.Add(col.Name))
                    throw new ParserConfigurationException(
                        $"Duplicate column Name '{col.Name}' in enabled columns. " +
                        "Column names must be unique to allow unambiguous cross-column references.");

                if (!string.IsNullOrWhiteSpace(col.Alias) && !seenNames.Add(col.Alias!))
                    throw new ParserConfigurationException(
                        $"Duplicate column Alias '{col.Alias}' in enabled columns. " +
                        "Column aliases must be unique to allow unambiguous cross-column references.");
            }

            // ── Guard: per-column-type required fields ───────────────────────
            // Catch missing required fields at configuration time so the error
            // message is clear and points directly to the offending column.
            foreach (var col in config.Columns)
            {
                if (col == null || !col.IsEnabled) continue;

                switch (col)
                {
                    case PropertyColumn propCol
                        when string.IsNullOrWhiteSpace(propCol.PropertyPath):
                        throw new ParserConfigurationException(
                            $"Enabled column '{col.Name}' (PropertyColumn) has an empty " +
                            "PropertyPath. Set a valid dot-path (e.g., 'Customer.Name').");

                    case FormulaColumn formulaCol
                        when string.IsNullOrWhiteSpace(formulaCol.Expression):
                        throw new ParserConfigurationException(
                            $"Enabled column '{col.Name}' (FormulaColumn) has an empty " +
                            "Expression. Set a valid NCalc expression (e.g., 'UnitPrice * Quantity').");

                    case ConditionalColumn condCol
                        when string.IsNullOrWhiteSpace(condCol.ConditionExpression):
                        throw new ParserConfigurationException(
                            $"Enabled column '{col.Name}' (ConditionalColumn) has an empty " +
                            "ConditionExpression. Set a boolean NCalc expression (e.g., 'IsActive').");
                }
            }
        }
    }
}
