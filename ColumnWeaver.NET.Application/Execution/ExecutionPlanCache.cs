using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET.Application.Execution
{
    /// <summary>
    /// V2.5 — Structural execution plan cache that avoids recompilation for
    /// logically identical configurations by generating a deterministic hash.
    /// </summary>
    /// <remarks>
    /// Unlike the reference-equality cache in <c>CompiledParserEngine</c>, this
    /// cache uses a content-based hash so that two different
    /// <see cref="ParserConfiguration"/> instances with the same logical content
    /// share the same <see cref="ExecutionPlan"/>.
    /// </remarks>
    public sealed class ExecutionPlanCache
    {
        private readonly Dictionary<string, ExecutionPlan> _plans
            = new Dictionary<string, ExecutionPlan>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, ExecutionPlan> _fastPlans
            = new Dictionary<ulong, ExecutionPlan>();

        private readonly object _lock = new object();

        /// <summary>
        /// Gets the number of cached execution plans.
        /// </summary>
        public int Count
        {
            get { lock (_lock) { return _plans.Count; } }
        }

        /// <summary>
        /// Attempts to retrieve a cached <see cref="ExecutionPlan"/> for the given configuration.
        /// </summary>
        /// <param name="config">The parser configuration.</param>
        /// <returns>The cached execution plan, or <c>null</c> if not present.</returns>
        public ExecutionPlan? TryGet(ParserConfiguration config) => TryGet(config, null);

        /// <summary>
        /// Attempts to retrieve a cached <see cref="ExecutionPlan"/> by its 64-bit fingerprint.
        /// </summary>
        /// <param name="fingerprint">The 64-bit fast fingerprint.</param>
        /// <returns>The cached execution plan, or <c>null</c> if not present.</returns>
        public ExecutionPlan? TryGet(ulong fingerprint)
        {
            lock (_lock)
            {
                return _fastPlans.TryGetValue(fingerprint, out var plan) ? plan : null;
            }
        }

        /// <summary>
        /// Attempts to retrieve a cached <see cref="ExecutionPlan"/> for the given
        /// <paramref name="config"/> and <paramref name="sourceType"/>.
        /// </summary>
        public ExecutionPlan? TryGet(ParserConfiguration config, Type? sourceType)
        {
            if (config == null) return null;
            var fp = ComputeFastFingerprint(config, sourceType);
            lock (_lock)
            {
                if (_fastPlans.TryGetValue(fp, out var p)) return p;
                var key = ComputeHash(config, sourceType);
                return _plans.TryGetValue(key, out var plan) ? plan : null;
            }
        }

        /// <summary>
        /// Stores an <see cref="ExecutionPlan"/> in the cache, keyed by the
        /// configuration's content hash.
        /// </summary>
        public void Put(ParserConfiguration config, ExecutionPlan plan) => Put(config, plan, null);

        /// <summary>
        /// Stores an <see cref="ExecutionPlan"/> in the cache keyed by its 64-bit fingerprint.
        /// </summary>
        /// <param name="fingerprint">The 64-bit fast fingerprint.</param>
        /// <param name="plan">The compiled execution plan.</param>
        public void Put(ulong fingerprint, ExecutionPlan plan)
        {
            if (plan == null) return;
            lock (_lock)
            {
                _fastPlans[fingerprint] = plan;
            }
        }

        /// <summary>
        /// Stores an <see cref="ExecutionPlan"/> in the cache, keyed by the
        /// configuration's content hash and source type.
        /// </summary>
        public void Put(ParserConfiguration config, ExecutionPlan plan, Type? sourceType)
        {
            if (config == null || plan == null) return;
            var fp = ComputeFastFingerprint(config, sourceType);
            var key = ComputeHash(config, sourceType);
            lock (_lock)
            {
                _fastPlans[fp] = plan;
                if (!_plans.ContainsKey(key))
                    _plans[key] = plan;
            }
        }

        /// <summary>
        /// Removes all cached plans. Call when configurations are mutated.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _plans.Clear();
                _fastPlans.Clear();
            }
        }

        /// <summary>
        /// Computes a deterministic FNV-1a hash of the configuration's
        /// logical content and optional source type.
        /// </summary>
        public static string ComputeHash(ParserConfiguration config, Type? sourceType = null)
        {
            var sb = new StringBuilder(512);
            AppendConfigHash(sb, config, sourceType);

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return HashFNV1a(bytes);
        }

        private static void AppendConfigHash(StringBuilder sb, ParserConfiguration config, Type? sourceType)
        {
            if (config == null) { sb.Append("null"); return; }

            sb.Append("CFG:");
            sb.Append(config.Name ?? "");
            sb.Append("|GS:"); sb.Append(config.GlobalSeparator ?? "");
            sb.Append("|ASS:"); sb.Append(config.AutoSortByOrder);
            sb.Append("|PSD:"); sb.Append(config.PreserveSeparatorForDisabledColumns);
            sb.Append("|ST:"); sb.Append(sourceType?.AssemblyQualifiedName ?? sourceType?.FullName ?? "null");
            sb.Append("|COLS:"); sb.Append(config.Columns?.Count ?? 0);

            if (config.Columns != null)
            {
                var cols = new List<ColumnDefinition>(config.Columns.Count);
                foreach (var c in config.Columns)
                {
                    if (c != null) cols.Add(c);
                }
                if (config.AutoSortByOrder)
                {
                    cols.Sort((a, b) => a.Order.CompareTo(b.Order));
                }

                foreach (var col in cols)
                    AppendColumnHash(sb, col);
            }

            AppendVariables(sb, config.Variables);
        }

        private static void AppendVariables(StringBuilder sb, Dictionary<string, object?>? variables)
        {
            sb.Append("|VARS:");
            if (variables == null || variables.Count == 0)
            {
                sb.Append("empty");
                return;
            }

            sb.Append("COUNT:").Append(variables.Count);
            // Deterministic case-insensitive ordering of variable names
            var sortedKeys = variables.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
            foreach (var key in sortedKeys)
            {
                var val = variables[key];
                sb.Append(";K:").Append(key.ToLowerInvariant()).Append("=");
                if (val == null)
                {
                    sb.Append("null");
                }
                else
                {
                    var type = val.GetType();
                    sb.Append("[").Append(type.FullName).Append("]");
                    if (val is string s)
                    {
                        sb.Append("LEN:").Append(s.Length).Append(":").Append(s);
                    }
                    else if (val is bool b)
                    {
                        sb.Append(b ? "True" : "False");
                    }
                    else if (val is IFormattable formattable)
                    {
                        sb.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(val.ToString());
                    }
                }
            }
        }

        private static void AppendColumnHash(StringBuilder sb, ColumnDefinition? col)
        {
            if (col == null) { sb.Append(";null;"); return; }

            sb.Append(";COL:");
            sb.Append(col.Id.ToString("N"));
            sb.Append(";N:"); sb.Append(col.Name ?? "");
            sb.Append(";A:"); sb.Append(col.Alias ?? "");
            sb.Append(";O:"); sb.Append(col.Order);
            sb.Append(";E:"); sb.Append(col.IsEnabled);
            sb.Append(";SO:"); sb.Append(col.SeparatorOverride ?? "");
            sb.Append(";OT:"); sb.Append(col.OutputType?.FullName ?? "null");
            AppendNullHandling(sb, col.NullHandling);
            AppendFormatting(sb, col.Formatting);
            AppendReplacementRules(sb, col.ReplacementRules);

            switch (col)
            {
                case PropertyColumn pc:
                    sb.Append(";KIND:Property;PATH:"); sb.Append(pc.PropertyPath ?? "");
                    break;

                case FormulaColumn fc:
                    sb.Append(";KIND:Formula;EXPR:"); sb.Append(fc.Expression ?? "");
                    break;

                case ConditionalColumn cc:
                    sb.Append(";KIND:Conditional;COND:"); sb.Append(cc.ConditionExpression ?? "");
                    sb.Append(";TRUE:"); sb.Append(cc.TrueValue ?? "");
                    sb.Append(";FALSE:"); sb.Append(cc.FalseValue ?? "");
                    break;

                case CustomFunctionColumn cfc:
                    sb.Append(";KIND:CustomFunc;FUNC:"); sb.Append(cfc.FunctionName ?? "");
                    sb.Append(";PARAMS:");
                    if (cfc.Parameters != null)
                    {
                        foreach (var p in cfc.Parameters)
                            sb.Append(p ?? "").Append(',');
                    }
                    break;

                case FixedTextColumn ft:
                    sb.Append(";KIND:FixedText;TEXT:"); sb.Append(ft.Text ?? "");
                    break;

                default:
                    sb.Append(";KIND:Unknown");
                    break;
            }
        }

        private static void AppendNullHandling(StringBuilder sb, Core.Models.NullHandlingConfig nh)
        {
            if (nh == null) { sb.Append(";NH:null"); return; }
            sb.Append(";NH:S:"); sb.Append((int)nh.Strategy);
            sb.Append(";NH:DV:"); sb.Append(nh.DefaultValue ?? "");
            sb.Append(";NH:EM:"); sb.Append(nh.ExceptionMessage ?? "");
        }

        private static void AppendFormatting(StringBuilder sb, Core.Models.FormattingOptions fo)
        {
            if (fo == null) { sb.Append(";FO:null"); return; }
            sb.Append(";FO:FS:"); sb.Append(fo.FormatString ?? "");
            sb.Append(";FO:TB:"); sb.Append((int)fo.TrimBehavior);
            sb.Append(";FO:CT:"); sb.Append((int)fo.CasingTransform);
            sb.Append(";FO:ML:"); sb.Append(fo.MaxLength);
            sb.Append(";FO:PW:"); sb.Append(fo.PaddedWidth);
            sb.Append(";FO:PC:"); sb.Append(fo.PaddingChar);
            sb.Append(";FO:PA:"); sb.Append((int)fo.PaddingAlignment);
            sb.Append(";FO:PF:"); sb.Append(fo.Prefix ?? "");
            sb.Append(";FO:SF:"); sb.Append(fo.Suffix ?? "");
        }

        private static void AppendReplacementRules(StringBuilder sb, IList<Core.Models.ReplacementRule> rules)
        {
            sb.Append(";RR:");
            if (rules == null || rules.Count == 0) { sb.Append("empty"); return; }
            foreach (var r in rules)
            {
                if (r == null) continue;
                sb.Append(r.SourceValue ?? "");
                sb.Append("->");
                sb.Append(r.TargetValue ?? "");
                sb.Append(";CS:").Append(r.IsCaseSensitive);
                sb.Append(";RX:").Append(r.UseRegex).Append("|");
            }
        }

        private static string HashFNV1a(byte[] data)
        {
            const ulong FNV_OFFSET_BASIS = 14695981039346656037UL;
            const ulong FNV_PRIME = 1099511628211UL;

            ulong hash = FNV_OFFSET_BASIS;
            foreach (byte b in data)
            {
                hash ^= b;
                hash *= FNV_PRIME;
            }

            return hash.ToString("x16");
        }

        /// <summary>
        /// Computes a zero-allocation 64-bit FNV-1a fingerprint of the configuration's
        /// logical content and optional source type.
        /// </summary>
        public static ulong ComputeFastFingerprint(ParserConfiguration config, Type? sourceType = null)
        {
            if (config == null) return 0UL;

            const ulong FNV_OFFSET_BASIS = 14695981039346656037UL;
            const ulong FNV_PRIME = 1099511628211UL;

            ulong hash = FNV_OFFSET_BASIS;

            hash = HashString(hash, config.Name);
            hash = HashString(hash, config.GlobalSeparator);
            hash ^= (config.AutoSortByOrder ? 1UL : 0UL) | ((config.PreserveSeparatorForDisabledColumns ? 1UL : 0UL) << 1);
            hash *= FNV_PRIME;

            if (sourceType != null)
            {
                hash ^= (ulong)(uint)sourceType.GetHashCode();
                hash *= FNV_PRIME;
            }

            var columns = config.Columns;
            if (columns != null)
            {
                hash ^= (ulong)columns.Count;
                hash *= FNV_PRIME;

                for (int i = 0; i < columns.Count; i++)
                {
                    var col = columns[i];
                    if (col == null)
                    {
                        hash ^= 0xFF;
                        hash *= FNV_PRIME;
                        continue;
                    }

                    hash = HashGuid(hash, col.Id);
                    hash = HashString(hash, col.Name);
                    if (col.Alias != null) hash = HashString(hash, col.Alias);
                    // Keep the signed order's 32-bit representation separate from
                    // the enabled flag. A direct int-to-ulong conversion sign-extends
                    // negative orders and can overwrite the flag bit.
                    hash ^= (ulong)(uint)col.Order | ((col.IsEnabled ? 1UL : 0UL) << 32);
                    hash *= FNV_PRIME;
                    if (col.SeparatorOverride != null) hash = HashString(hash, col.SeparatorOverride);

                    if (col.OutputType != null)
                    {
                        hash ^= (ulong)(uint)col.OutputType.GetHashCode();
                        hash *= FNV_PRIME;
                    }

                    // NullHandling
                    var nh = col.NullHandling;
                    if (nh != null)
                    {
                        hash ^= (ulong)nh.Strategy;
                        hash *= FNV_PRIME;
                        if (nh.DefaultValue != null) hash = HashString(hash, nh.DefaultValue);
                        if (nh.ExceptionMessage != null) hash = HashString(hash, nh.ExceptionMessage);
                    }

                    // Formatting
                    var fo = col.Formatting;
                    if (fo != null)
                    {
                        if (fo.FormatString != null) hash = HashString(hash, fo.FormatString);
                        if (fo.Prefix != null) hash = HashString(hash, fo.Prefix);
                        if (fo.Suffix != null) hash = HashString(hash, fo.Suffix);
                        if (fo.MaxLength != 0 || fo.PaddedWidth != 0 || fo.TrimBehavior != 0 || fo.CasingTransform != 0 || fo.PaddingAlignment != 0)
                        {
                            hash ^= (ulong)fo.TrimBehavior | ((ulong)fo.CasingTransform << 8) | ((ulong)fo.PaddingAlignment << 16) | ((ulong)fo.PaddingChar << 24);
                            hash *= FNV_PRIME;
                            hash ^= (ulong)(uint)fo.MaxLength | ((ulong)(uint)fo.PaddedWidth << 32);
                            hash *= FNV_PRIME;
                        }
                    }

                    // Replacement rules
                    var rr = col.ReplacementRules;
                    if (rr != null && rr.Count > 0)
                    {
                        hash ^= (ulong)rr.Count;
                        hash *= FNV_PRIME;
                        for (int rIdx = 0; rIdx < rr.Count; rIdx++)
                        {
                            var r = rr[rIdx];
                            if (r == null) continue;
                            hash = HashString(hash, r.SourceValue);
                            hash = HashString(hash, r.TargetValue);
                            hash ^= (r.IsCaseSensitive ? 1UL : 0UL) | ((r.UseRegex ? 1UL : 0UL) << 1);
                            hash *= FNV_PRIME;
                        }
                    }

                    switch (col)
                    {
                        case PropertyColumn pc:
                            hash ^= 1; hash *= FNV_PRIME;
                            hash = HashString(hash, pc.PropertyPath);
                            break;
                        case FormulaColumn fc:
                            hash ^= 2; hash *= FNV_PRIME;
                            hash = HashString(hash, fc.Expression);
                            break;
                        case ConditionalColumn cc:
                            hash ^= 3; hash *= FNV_PRIME;
                            hash = HashString(hash, cc.ConditionExpression);
                            hash = HashString(hash, cc.TrueValue);
                            hash = HashString(hash, cc.FalseValue);
                            break;
                        case CustomFunctionColumn cfc:
                            hash ^= 4; hash *= FNV_PRIME;
                            hash = HashString(hash, cfc.FunctionName);
                            if (cfc.Parameters != null)
                            {
                                for (int pIdx = 0; pIdx < cfc.Parameters.Count; pIdx++)
                                    hash = HashString(hash, cfc.Parameters[pIdx]);
                            }
                            break;
                        case FixedTextColumn ft:
                            hash ^= 5; hash *= FNV_PRIME;
                            hash = HashString(hash, ft.Text);
                            break;
                    }
                }
            }

            // Variables (XOR-combined for deterministic order-independence)
            var vars = config.Variables;
            if (vars != null && vars.Count > 0)
            {
                hash ^= (ulong)vars.Count;
                hash *= FNV_PRIME;

                ulong combinedVarHash = 0;
                foreach (var kvp in vars)
                {
                    ulong vh = FNV_OFFSET_BASIS;
                    vh = HashStringLower(vh, kvp.Key);
                    var val = kvp.Value;
                    if (val == null)
                    {
                        vh ^= 0xFF; vh *= FNV_PRIME;
                    }
                    else
                    {
                        vh ^= (ulong)(uint)val.GetType().GetHashCode(); vh *= FNV_PRIME;
                        if (val is string s)
                        {
                            vh = HashString(vh, s);
                        }
                        else if (val is bool b)
                        {
                            vh ^= (b ? 1UL : 0UL); vh *= FNV_PRIME;
                        }
                        else if (val is int itg)
                        {
                            vh ^= (ulong)itg; vh *= FNV_PRIME;
                        }
                        else if (val is long lng)
                        {
                            vh ^= (ulong)lng; vh *= FNV_PRIME;
                        }
                        else if (val is double d)
                        {
                            vh ^= (ulong)BitConverter.DoubleToInt64Bits(d); vh *= FNV_PRIME;
                        }
                        else if (val is float flt)
                        {
                            var fb = new FloatBits { FloatValue = flt };
                            vh ^= (ulong)(uint)fb.IntValue; vh *= FNV_PRIME;
                        }
                        else if (val is decimal dec)
                        {
                            var db = new DecimalBits { DecimalValue = dec };
                            vh ^= (ulong)(uint)db.Low | ((ulong)(uint)db.Mid << 32); vh *= FNV_PRIME;
                            vh ^= (ulong)(uint)db.High | ((ulong)(uint)db.Flags << 32); vh *= FNV_PRIME;
                        }
                        else if (val is DateTime dt)
                        {
                            vh ^= (ulong)dt.Ticks; vh *= FNV_PRIME;
                        }
                        else if (val is IFormattable formattable)
                        {
                            vh = HashString(vh, formattable.ToString(null, CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            vh = HashString(vh, val.ToString());
                        }
                    }
                    combinedVarHash ^= vh;
                }
                hash ^= combinedVarHash;
                hash *= FNV_PRIME;
            }

            return hash;
        }

        private static ulong HashString(ulong hash, string? str)
        {
            if (str == null) return (hash ^ 0xFF) * 1099511628211UL;
            const ulong FNV_PRIME = 1099511628211UL;
            hash ^= (ulong)(uint)str.GetHashCode();
            hash *= FNV_PRIME;
            hash ^= (ulong)str.Length;
            hash *= FNV_PRIME;
            return hash;
        }

        private static ulong HashStringLower(ulong hash, string? str)
        {
            if (str == null) return (hash ^ 0xFF) * 1099511628211UL;
            const ulong FNV_PRIME = 1099511628211UL;
            hash ^= (ulong)(uint)StringComparer.OrdinalIgnoreCase.GetHashCode(str);
            hash *= FNV_PRIME;
            hash ^= (ulong)str.Length;
            hash *= FNV_PRIME;
            return hash;
        }

        private static ulong HashGuid(ulong hash, Guid guid)
        {
            const ulong FNV_PRIME = 1099511628211UL;
            hash ^= (ulong)(uint)guid.GetHashCode();
            hash *= FNV_PRIME;
            return hash;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct FloatBits
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public float FloatValue;
            [System.Runtime.InteropServices.FieldOffset(0)]
            public int IntValue;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct DecimalBits
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public decimal DecimalValue;
            [System.Runtime.InteropServices.FieldOffset(0)]
            public int Low;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public int Mid;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public int High;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public int Flags;
        }
    }
}
