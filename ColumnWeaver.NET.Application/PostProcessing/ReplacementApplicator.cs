using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ColumnWeaver.NET.Core.Models;

namespace ColumnWeaver.NET.Application.PostProcessing
{
    /// <summary>
    /// Applies an ordered list of <see cref="ReplacementRule"/> entries to a
    /// formatted column value. Rules are executed sequentially in list order.
    /// </summary>
    /// <remarks>
    /// Supports both literal string replacement (case-sensitive or -insensitive)
    /// and regular expression replacement, controlled per rule via
    /// <see cref="ReplacementRule.UseRegex"/> and <see cref="ReplacementRule.IsCaseSensitive"/>.
    /// </remarks>
    public sealed class ReplacementApplicator
    {
        /// <summary>
        /// Applies all replacement rules in order to the provided string value.
        /// </summary>
        /// <param name="value">The string value to process. Must not be null.</param>
        /// <param name="rules">
        /// The ordered list of replacement rules. May be null or empty (no-op).
        /// </param>
        /// <returns>The value after all replacement rules have been applied.</returns>
        public string Apply(string value, IList<ReplacementRule> rules)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (rules == null || rules.Count == 0) return value;

            foreach (var rule in rules)
            {
                // Skip malformed rules silently — source must be non-empty.
                if (string.IsNullOrEmpty(rule.SourceValue)) continue;

                var target = rule.TargetValue ?? string.Empty;

                value = rule.UseRegex
                    ? ApplyRegexReplacement(value, rule.SourceValue, target, rule.IsCaseSensitive)
                    : ApplyLiteralReplacement(value, rule.SourceValue, target, rule.IsCaseSensitive);
            }

            return value;
        }

        // -----------------------------------------------------------------
        //  Regex replacement
        // -----------------------------------------------------------------

        private static string ApplyRegexReplacement(
            string value, string pattern, string replacement, bool caseSensitive)
        {
            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            return Regex.Replace(value, pattern, replacement, options);
        }

        // -----------------------------------------------------------------
        //  Literal replacement (avoids Regex overhead for plain string cases)
        // -----------------------------------------------------------------

        private static string ApplyLiteralReplacement(
            string value, string oldValue, string newValue, bool caseSensitive)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var comparison = caseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            // Fast path — no occurrence at all.
            if (value.IndexOf(oldValue, comparison) < 0)
                return value;

            var sb         = new StringBuilder(value.Length);
            int startIndex = 0;
            int matchIndex;

            while ((matchIndex = value.IndexOf(oldValue, startIndex, comparison)) >= 0)
            {
                sb.Append(value, startIndex, matchIndex - startIndex);
                sb.Append(newValue);
                startIndex = matchIndex + oldValue.Length;
            }

            // Append the remaining tail.
            sb.Append(value, startIndex, value.Length - startIndex);
            return sb.ToString();
        }
    }
}
