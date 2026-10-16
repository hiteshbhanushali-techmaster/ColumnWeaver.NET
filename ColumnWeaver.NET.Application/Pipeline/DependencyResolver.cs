using System;
using System.Collections.Generic;
using System.Linq;
using ColumnWeaver.NET.Application.Exceptions;
using ColumnWeaver.NET.Core.Columns;

namespace ColumnWeaver.NET.Application.Pipeline
{
    /// <summary>
    /// Resolves the correct evaluation order for a set of column definitions
    /// using a depth-first search topological sort with cycle detection.
    /// </summary>
    /// <remarks>
    /// Cross-column dependencies are discovered by scanning <c>${ColumnName}</c>
    /// tokens from the expressions of <see cref="FormulaColumn"/>,
    /// <see cref="ConditionalColumn"/>, and <see cref="CustomFunctionColumn"/> definitions.
    /// <para>
    /// Algorithm: DFS with three-colour marking (White → Gray → Black).
    /// A Gray node encountered during DFS indicates a back-edge, i.e. a cycle.
    /// </para>
    /// </remarks>
    public sealed class DependencyResolver
    {
        // -----------------------------------------------------------------
        //  Colour states for DFS cycle detection
        // -----------------------------------------------------------------

        private enum DfsColor { White, Gray, Black }

        // -----------------------------------------------------------------
        //  Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Returns the columns in a topologically valid evaluation order so that
        /// every column's dependencies are evaluated before the column itself.
        /// </summary>
        /// <param name="columns">
        /// The enabled columns to sort. Columns are assumed to have unique Ids.
        /// </param>
        /// <returns>
        /// A read-only list of <see cref="ColumnDefinition"/> instances in evaluation order.
        /// Columns with no cross-column dependencies retain their relative input order.
        /// </returns>
        /// <exception cref="CircularDependencyException">
        /// Thrown when a circular dependency is detected among column expressions.
        /// </exception>
        public IReadOnlyList<ColumnDefinition> Sort(IReadOnlyList<ColumnDefinition> columns)
        {
            if (columns == null || columns.Count == 0)
                return Array.Empty<ColumnDefinition>();

            // Build a case-insensitive name/alias → column lookup for dependency resolution.
            var nameIndex = BuildNameIndex(columns);

            var colorMap = columns.ToDictionary(c => c.Id, _ => DfsColor.White);
            var result   = new List<ColumnDefinition>(columns.Count);

            foreach (var column in columns)
            {
                if (colorMap[column.Id] == DfsColor.White)
                {
                    var cyclePath = new List<string>();
                    Visit(column, nameIndex, colorMap, result, cyclePath);
                }
            }

            // DFS post-order places dependencies before dependents (dependencies are finished first).
            return result.AsReadOnly();
        }

        // -----------------------------------------------------------------
        //  DFS traversal
        // -----------------------------------------------------------------

        private static void Visit(
            ColumnDefinition column,
            Dictionary<string, ColumnDefinition> nameIndex,
            Dictionary<Guid, DfsColor> colorMap,
            List<ColumnDefinition> result,
            List<string> currentPath)
        {
            // Already fully processed — nothing to do.
            if (colorMap[column.Id] == DfsColor.Black)
                return;

            // Gray = currently on the DFS stack → back edge = cycle.
            if (colorMap[column.Id] == DfsColor.Gray)
            {
                // Append the repeated node to close the cycle visually, e.g. A→B→C→A.
                var cycle = new List<string>(currentPath) { column.DisplayName };
                throw new CircularDependencyException(cycle);
            }

            // Mark as in-progress.
            colorMap[column.Id] = DfsColor.Gray;
            currentPath.Add(column.DisplayName);

            // Recurse into all dependencies.
            foreach (var dependencyName in GetDependencies(column))
            {
                if (nameIndex.TryGetValue(dependencyName, out var dependency))
                {
                    Visit(dependency, nameIndex, colorMap, result, currentPath);
                }
                // Unknown dependencies (referencing a disabled or non-existent column)
                // are ignored here; they will produce a runtime error during evaluation.
            }

            // Mark as fully processed and pop from current path.
            currentPath.RemoveAt(currentPath.Count - 1);
            colorMap[column.Id] = DfsColor.Black;
            result.Add(column); // Post-order addition.
        }

        // -----------------------------------------------------------------
        //  Dependency extraction per column type
        // -----------------------------------------------------------------

        /// <summary>
        /// Returns all column names/aliases that <paramref name="column"/> depends on
        /// via <c>${...}</c> cross-column references in its expressions.
        /// </summary>
        private static IEnumerable<string> GetDependencies(ColumnDefinition column)
        {
            switch (column)
            {
                case FormulaColumn formulaCol:
                    return ColumnReferenceParser.ExtractColumnReferences(formulaCol.Expression);

                case ConditionalColumn conditionalCol:
                    return ColumnReferenceParser
                        .ExtractColumnReferences(conditionalCol.ConditionExpression)
                        .Concat(ColumnReferenceParser.ExtractColumnReferences(conditionalCol.TrueValue))
                        .Concat(ColumnReferenceParser.ExtractColumnReferences(conditionalCol.FalseValue))
                        .Distinct(StringComparer.OrdinalIgnoreCase);

                case CustomFunctionColumn customCol:
                    return customCol.Parameters
                        .SelectMany(p => ColumnReferenceParser.ExtractColumnReferences(p))
                        .Distinct(StringComparer.OrdinalIgnoreCase);

                default:
                    return Array.Empty<string>();
            }
        }

        // -----------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------

        /// <summary>
        /// Builds a case-insensitive name/alias index over the provided column list.
        /// When a Name and Alias clash across columns, the last writer wins (validation
        /// catches ambiguous names before this point).
        /// </summary>
        private static Dictionary<string, ColumnDefinition> BuildNameIndex(
            IReadOnlyList<ColumnDefinition> columns)
        {
            var index = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var col in columns)
            {
                if (!string.IsNullOrWhiteSpace(col.Name))
                    index[col.Name] = col;

                if (!string.IsNullOrWhiteSpace(col.Alias))
                    index[col.Alias!] = col;
            }

            return index;
        }
    }
}
