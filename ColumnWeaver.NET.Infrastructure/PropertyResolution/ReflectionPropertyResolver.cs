using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Infrastructure.PropertyResolution
{
    /// <summary>
    /// Reflection-based implementation of <see cref="IPropertyResolver"/> that
    /// navigates dot-separated property paths on arbitrary CLR objects.
    /// </summary>
    /// <remarks>
    /// <b>Supported path syntax:</b>
    /// <list type="table">
    ///   <item><term><c>Name</c></term><description>Simple property</description></item>
    ///   <item><term><c>Customer.Address.City</c></term><description>Nested navigation</description></item>
    ///   <item><term><c>Orders[0].Amount</c></term><description>List/array index then property</description></item>
    ///   <item><term><c>Items[2].Tags[0]</c></term><description>Chained indexers</description></item>
    /// </list>
    ///
    /// <b>Performance:</b> On the first call for a given <c>(Type, path)</c> pair,
    /// the full accessor chain is compiled into a <c>Func&lt;object, object?&gt;</c>
    /// delegate by walking reflection metadata. All subsequent calls for the same pair
    /// invoke only the cached delegate — no repeated reflection overhead.
    ///
    /// <b>Thread safety:</b> The internal caches use <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// and are safe for concurrent reads and first-write races (worst case: the chain is
    /// built twice for the same key, both builds are idempotent).
    /// </remarks>
    public sealed class ReflectionPropertyResolver : IPropertyResolver
    {
        // -----------------------------------------------------------------
        //  Constants
        // -----------------------------------------------------------------

        private const BindingFlags PropertyFlags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

        // -----------------------------------------------------------------
        //  Caches
        // -----------------------------------------------------------------

        /// <summary>
        /// Primary cache: (AssemblyQualifiedTypeName::path) → compiled accessor delegate.
        /// The delegate encapsulates the full reflection chain for zero-overhead repeated calls.
        /// </summary>
        private readonly ConcurrentDictionary<string, Func<object, object?>> _accessorCache;

        /// <summary>
        /// Secondary cache: same key → whether the path is resolvable on the type.
        /// Prevents repeated attempt-and-fail reflection for invalid paths.
        /// </summary>
        private readonly ConcurrentDictionary<string, bool> _resolvabilityCache;

        // -----------------------------------------------------------------
        //  Constructor
        // -----------------------------------------------------------------

        /// <summary>Initializes a new <see cref="ReflectionPropertyResolver"/>.</summary>
        public ReflectionPropertyResolver()
        {
            _accessorCache      = new ConcurrentDictionary<string, Func<object, object?>>(StringComparer.OrdinalIgnoreCase);
            _resolvabilityCache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }

        // -----------------------------------------------------------------
        //  IPropertyResolver
        // -----------------------------------------------------------------

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">When <paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentException">When <paramref name="propertyPath"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// When the path cannot be resolved on the source type (e.g., property does not exist).
        /// </exception>
        public object? Resolve(object source, string propertyPath)
        {
            if (source      == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(propertyPath))
                throw new ArgumentException("Property path cannot be null or empty.", nameof(propertyPath));

            var key      = CacheKey(source.GetType(), propertyPath);
            var accessor = _accessorCache.GetOrAdd(key, _ => CompileAccessor(source.GetType(), propertyPath));

            return accessor(source);
        }

        /// <inheritdoc />
        public bool CanResolve(Type sourceType, string propertyPath)
        {
            if (sourceType == null || string.IsNullOrWhiteSpace(propertyPath))
                return false;

            var key = CacheKey(sourceType, propertyPath);

            return _resolvabilityCache.GetOrAdd(key, _ =>
            {
                try
                {
                    // Building the accessor validates every segment of the path.
                    // If successful, store the accessor to avoid rebuilding it later.
                    _accessorCache.TryAdd(key, CompileAccessor(sourceType, propertyPath));
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        // ── V2: ResolveType ──────────────────────────────────────────────────

        /// <inheritdoc />
        /// <remarks>
        /// Parses the property path using the same <c>BuildChain</c> logic as
        /// <see cref="Resolve"/>, then returns the <c>PropertyType</c> of the
        /// last segment in the chain. Any parse or reflection error causes a
        /// <c>null</c> return rather than an exception.
        /// </remarks>
        public Type? ResolveType(Type sourceType, string propertyPath)
        {
            if (sourceType == null || string.IsNullOrWhiteSpace(propertyPath))
                return null;

            try
            {
                var steps = ParsePathSegments(propertyPath);
                var chain = BuildChain(sourceType, steps);

                if (chain.Length == 0) return null;

                var lastStep = chain[chain.Length - 1];
                // If the last segment has an indexer, return the collection's element type.
                return lastStep.Index.HasValue
                    ? GetElementType(lastStep.Property.PropertyType, lastStep.Property.Name)
                    : lastStep.Property.PropertyType;
            }
            catch
            {
                return null;
            }
        }

        // -----------------------------------------------------------------
        //  Cache key
        // -----------------------------------------------------------------

        private static string CacheKey(Type type, string path)
            => $"{type.AssemblyQualifiedName}::{path}";

        // -----------------------------------------------------------------
        //  Accessor compilation
        // -----------------------------------------------------------------

        /// <summary>
        /// Parses the path and validates every segment against type metadata,
        /// then builds a closure that navigates the chain at runtime.
        /// </summary>
        private static Func<object, object?> CompileAccessor(Type rootType, string propertyPath)
        {
            var steps = ParsePathSegments(propertyPath);
            var chain = BuildChain(rootType, steps);

            // Capture the immutable chain array in a closure — zero boxing for the step tuple.
            return root =>
            {
                object? current = root;

                foreach (var step in chain)
                {
                    if (current == null) return null;

                    // Navigate the property.
                    current = step.Property.GetValue(current);

                    // Apply integer index if this segment had an indexer (e.g., Orders[0]).
                    if (step.Index.HasValue && current != null)
                        current = ApplyIndex(current, step.Index.Value);
                }

                return current;
            };
        }

        // -----------------------------------------------------------------
        //  Path segment parsing
        // -----------------------------------------------------------------

        /// <summary>
        /// Splits a property path into individual segments, extracting optional
        /// integer indexers. E.g. <c>"Items[2].Tags[0]"</c> →
        /// <c>[("Items",2), ("Tags",0)]</c>.
        /// </summary>
        private static List<(string Name, int? Index)> ParsePathSegments(string path)
        {
            var segments = path.Split('.');
            var steps    = new List<(string, int?)>(segments.Length);

            foreach (var segment in segments)
            {
                var bracketStart = segment.IndexOf('[');

                if (bracketStart >= 0)
                {
                    // Validate position of closing bracket.
                    var bracketEnd = segment.IndexOf(']', bracketStart);
                    if (bracketEnd < 0)
                        throw new FormatException(
                            $"Malformed indexer in path segment '{segment}': missing closing ']'.");

                    var name     = segment.Substring(0, bracketStart);
                    var indexStr = segment.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);

                    if (!int.TryParse(indexStr, out var idx))
                        throw new FormatException(
                            $"Non-integer indexer '{indexStr}' in path segment '{segment}'. " +
                            "Only integer indexers are supported (e.g., Orders[0]).");

                    steps.Add((name, idx));
                }
                else
                {
                    steps.Add((segment, null));
                }
            }

            return steps;
        }

        // -----------------------------------------------------------------
        //  Chain building (validates against type metadata at compile time)
        // -----------------------------------------------------------------

        /// <summary>Represents one step in the resolved property-accessor chain.</summary>
        private readonly struct ChainStep
        {
            public PropertyInfo Property { get; }
            public int?         Index    { get; }

            public ChainStep(PropertyInfo property, int? index)
            {
                Property = property;
                Index    = index;
            }
        }

        private static ChainStep[] BuildChain(Type rootType, List<(string Name, int? Index)> steps)
        {
            var chain       = new ChainStep[steps.Count];
            var currentType = rootType;

            for (int i = 0; i < steps.Count; i++)
            {
                var (name, index) = steps[i];
                var prop = currentType.GetProperty(name, PropertyFlags);

                if (prop == null)
                    throw new InvalidOperationException(
                        $"Property '{name}' was not found on type '{currentType.FullName}'. " +
                        "Ensure the property is public and accessible.");

                chain[i] = new ChainStep(prop, index);

                // Advance the current type for the next segment.
                currentType = index.HasValue
                    ? GetElementType(prop.PropertyType, name)
                    : prop.PropertyType;
            }

            return chain;
        }

        /// <summary>
        /// Returns the element type of a collection property (used to type-check
        /// the next segment after an indexer).
        /// </summary>
        private static Type GetElementType(Type collectionType, string propertyName)
        {
            if (collectionType.IsArray)
            {
                var elementType = collectionType.GetElementType();
                return elementType ?? typeof(object);
            }

            if (collectionType.IsGenericType)
            {
                var args = collectionType.GetGenericArguments();
                if (args.Length > 0) return args[0];
            }

            // Cannot determine element type — use object so the chain continues.
            return typeof(object);
        }

        // -----------------------------------------------------------------
        //  Runtime index application
        // -----------------------------------------------------------------

        private static object? ApplyIndex(object collection, int index)
        {
            // Fast path — IList (covers List<T>, arrays, etc.)
            if (collection is IList list)
                return index >= 0 && index < list.Count ? list[index] : null;

            // Fallback — any IEnumerable (slower but universal)
            if (collection is IEnumerable enumerable)
            {
                int position = 0;
                foreach (var item in enumerable)
                {
                    if (position == index) return item;
                    position++;
                }
                return null;
            }

            return null;
        }
    }
}
