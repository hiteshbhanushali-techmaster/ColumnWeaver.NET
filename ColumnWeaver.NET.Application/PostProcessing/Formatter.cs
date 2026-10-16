using System;
using System.Globalization;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Enums;
using ColumnWeaver.NET.Core.Models;

namespace ColumnWeaver.NET.Application.PostProcessing
{
    /// <summary>
    /// Applies all <see cref="FormattingOptions"/> from a column definition
    /// to a resolved string value in a deterministic order:
    /// <list type="number">
    ///   <item>Trim whitespace</item>
    ///   <item>Casing transformation</item>
    ///   <item>Truncate to MaxLength</item>
    ///   <item>Pad to fixed width</item>
    ///   <item>Prepend Prefix</item>
    ///   <item>Append Suffix</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <see cref="FormattingOptions.FormatString"/> is intentionally NOT applied here.
    /// Format strings require the original typed value (e.g., DateTime, decimal) for
    /// correct rendering. They are applied by individual column evaluators before the
    /// value is converted to a string, while the type information is still available.
    /// </remarks>
    public sealed class Formatter
    {
        /// <summary>
        /// Applies all configured formatting options to the provided string value.
        /// </summary>
        /// <param name="value">The string value to format. Must not be null.</param>
        /// <param name="column">The column definition supplying formatting options.</param>
        /// <returns>The formatted string.</returns>
        public string Apply(string value, ColumnDefinition column)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (column == null) throw new ArgumentNullException(nameof(column));

            var opts = column.Formatting;

            // Step 1 — Trim
            value = ApplyTrim(value, opts.TrimBehavior);

            // Step 2 — Casing
            value = ApplyCasing(value, opts.CasingTransform);

            // Step 3 — Truncate to MaxLength (0 = no limit)
            if (opts.MaxLength > 0 && value.Length > opts.MaxLength)
                value = value.Substring(0, opts.MaxLength);

            // Step 4 — Padding (only when PaddedWidth > 0 and an alignment is set)
            if (opts.PaddedWidth > 0 && opts.PaddingAlignment != PaddingAlignment.None)
                value = ApplyPadding(value, opts.PaddedWidth, opts.PaddingChar, opts.PaddingAlignment);

            // Step 5 — Prefix
            if (!string.IsNullOrEmpty(opts.Prefix))
                value = opts.Prefix + value;

            // Step 6 — Suffix
            if (!string.IsNullOrEmpty(opts.Suffix))
                value = value + opts.Suffix;

            return value;
        }

        // -----------------------------------------------------------------
        //  Private helpers
        // -----------------------------------------------------------------

        private static string ApplyTrim(string value, TrimBehavior trim)
        {
            switch (trim)
            {
                case TrimBehavior.TrimStart: return value.TrimStart();
                case TrimBehavior.TrimEnd:   return value.TrimEnd();
                case TrimBehavior.TrimBoth:  return value.Trim();
                default:                     return value;
            }
        }

        private static string ApplyCasing(string value, CasingTransform casing)
        {
            switch (casing)
            {
                case CasingTransform.UpperCase:
                    return value.ToUpperInvariant();

                case CasingTransform.LowerCase:
                    return value.ToLowerInvariant();

                case CasingTransform.TitleCase:
                    return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());

                case CasingTransform.SentenceCase:
                    return value.Length == 0
                        ? value
                        : char.ToUpperInvariant(value[0]) + value.Substring(1).ToLowerInvariant();

                default:
                    return value;
            }
        }

        private static string ApplyPadding(
            string value, int width, char padChar, PaddingAlignment alignment)
        {
            if (value.Length >= width) return value;

            int totalPad = width - value.Length;

            switch (alignment)
            {
                case PaddingAlignment.Left:
                    // Left padding = right-align content
                    return value.PadLeft(width, padChar);

                case PaddingAlignment.Right:
                    // Right padding = left-align content
                    return value.PadRight(width, padChar);

                case PaddingAlignment.Center:
                    int leftPad  = totalPad / 2;
                    int rightPad = totalPad - leftPad;
                    return new string(padChar, leftPad) + value + new string(padChar, rightPad);

                default:
                    return value;
            }
        }
    }
}
