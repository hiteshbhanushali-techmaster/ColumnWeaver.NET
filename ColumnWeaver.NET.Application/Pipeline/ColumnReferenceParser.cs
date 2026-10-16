using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ColumnWeaver.NET.Application.Pipeline
{
    /// <summary>
    /// Parses and expands cross-column references embedded in expression strings.
    /// </summary>
    /// <remarks>
    /// <b>Reference syntax:</b> <c>${ColumnNameOrAlias}</c>
    /// <para>
    /// Examples:
    /// <code>
    ///   FormulaColumn:   "${Quantity} * ${UnitPrice}"
    ///   ConditionalColumn condition: "${Status} == 'Active'"
    ///   ConditionalColumn true branch: "= ${Price} * 0.9"
    /// </code>
    /// </para>
    /// References are case-insensitive for lookup. If a referenced column has not yet
    /// been evaluated, the token is left unexpanded (the expression engine will then
    /// receive the literal token, typically causing an evaluation error).
    /// </remarks>
    public static class ColumnReferenceParser
    {
        // Matches ${ColumnName} or ${ Column Name } (trims inner whitespace).
        private static readonly Regex ReferencePattern = new Regex(
            @"\$\{([^}]+)\}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // -----------------------------------------------------------------
        //  Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Extracts the list of distinct column names referenced in the expression
        /// via the <c>${...}</c> token syntax.
        /// </summary>
        /// <param name="expression">The expression string to scan. May be null or empty.</param>
        /// <returns>
        /// A read-only list of column name/alias strings found in the expression.
        /// Returns an empty list when the expression is null, empty, or contains no references.
        /// </returns>
        public static IReadOnlyList<string> ExtractColumnReferences(string? expression)
        {
            if (string.IsNullOrEmpty(expression) || !expression!.Contains("${"))
                return Array.Empty<string>();

            var matches = ReferencePattern.Matches(expression);
            if (matches.Count == 0)
                return Array.Empty<string>();

            return matches
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Trim())
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Replaces all <c>${ColumnName}</c> tokens in the expression with the
        /// corresponding resolved column values.
        /// </summary>
        /// <param name="expression">The expression string with potential tokens. May be null.</param>
        /// <param name="columnValues">
        /// A dictionary of resolved column values keyed by Name or Alias (case-insensitive).
        /// </param>
        /// <returns>
        /// The expression with all matched tokens replaced by their resolved values.
        /// Tokens whose column names are not found in <paramref name="columnValues"/>
        /// are left unchanged.
        /// </returns>
        public static string ExpandColumnReferences(
            string? expression,
            IReadOnlyDictionary<string, string> columnValues)
        {
            if (string.IsNullOrEmpty(expression) || !expression!.Contains("${"))
                return expression ?? string.Empty;

            if (columnValues == null || columnValues.Count == 0)
                return expression;

            return ReferencePattern.Replace(expression, match =>
            {
                var referencedName = match.Groups[1].Value.Trim();

                return columnValues.TryGetValue(referencedName, out var resolvedValue)
                    ? resolvedValue
                    : match.Value; // Leave unexpanded — expression engine will handle / error.
            });
        }

        /// <summary>
        /// Expands references in an expression. Cross-column references are replaced with their
        /// resolved string values. Variable references are converted to <c>[VarName]</c> parameter
        /// syntax for safe, typed parameter binding in NCalc without string corruption.
        /// </summary>
        public static string ExpandForExpression(
            string? expression,
            IReadOnlyDictionary<string, string>? columnValues = null,
            IReadOnlyDictionary<string, object?>? variables = null)
        {
            if (string.IsNullOrEmpty(expression) || !expression!.Contains("${"))
                return expression ?? string.Empty;

            bool hasCols = columnValues != null && columnValues.Count > 0;
            bool hasVars = variables != null && variables.Count > 0;

            if (!hasCols && !hasVars)
                return expression;

            return ReferencePattern.Replace(expression, match =>
            {
                var name = match.Groups[1].Value.Trim();

                if (hasVars && variables!.ContainsKey(name))
                    return $"[{name}]";

                if (hasCols && columnValues!.TryGetValue(name, out var colVal))
                    return colVal;

                return match.Value;
            });
        }

        /// <summary>
        /// Expands references in a literal text string or custom function parameter.
        /// Cross-column references and variables are converted to their string representations.
        /// </summary>
        public static string ExpandForText(
            string? text,
            IReadOnlyDictionary<string, string>? columnValues = null,
            IReadOnlyDictionary<string, object?>? variables = null)
        {
            if (string.IsNullOrEmpty(text) || !text!.Contains("${"))
                return text ?? string.Empty;

            bool hasCols = columnValues != null && columnValues.Count > 0;
            bool hasVars = variables != null && variables.Count > 0;

            if (!hasCols && !hasVars)
                return text;

            return ReferencePattern.Replace(text, match =>
            {
                var name = match.Groups[1].Value.Trim();

                if (hasCols && columnValues!.TryGetValue(name, out var colVal))
                    return colVal;

                if (hasVars && variables!.TryGetValue(name, out var varVal))
                    return varVal?.ToString() ?? string.Empty;

                return match.Value;
            });
        }
    }
}
