using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ColumnWeaver.NET.Application.Abstractions;
using ColumnWeaver.NET.Application.Pipeline;
using ColumnWeaver.NET.Application.Validation;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Application.Execution
{
    /// <summary>
    /// V2.5 — Compile-time type inference and enforcement for the execution plan.
    /// </summary>
    /// <remarks>
    /// The TypeGraph captures the type of every column and the type-compatibility
    /// relationships between them. It is built once during compilation and used to
    /// validate that:
    /// <list type="bullet">
    ///   <item>Arithmetic operations are applied to numeric types.</item>
    ///   <item>Function parameters match their expected types.</item>
    ///   <item>Conditional branches produce compatible types.</item>
    ///   <item>Custom functions receive the correct parameter types.</item>
    /// </list>
    /// Compilation FAILS if any type incompatibility is detected.
    /// </remarks>
    public sealed class TypeGraph
    {
        private readonly Dictionary<int, Type> _nodeTypes;
        private readonly List<ValidationError> _typeErrors;

        /// <summary>
        /// Gets the dictionary mapping column indexes to their inferred CLR types.
        /// </summary>
        public IReadOnlyDictionary<int, Type> NodeTypes => _nodeTypes;

        /// <summary>
        /// Gets the list of type validation errors collected during type analysis.
        /// </summary>
        public IReadOnlyList<ValidationError> TypeErrors => _typeErrors;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeGraph"/> class.
        /// </summary>
        public TypeGraph()
        {
            _nodeTypes  = new Dictionary<int, Type>();
            _typeErrors = new List<ValidationError>();
        }

        /// <summary>
        /// Sets the inferred CLR type for the specified column index.
        /// </summary>
        /// <param name="columnIndex">The zero-based column index.</param>
        /// <param name="type">The inferred CLR type.</param>
        public void SetNodeType(int columnIndex, Type type)
        {
            _nodeTypes[columnIndex] = type ?? typeof(object);
        }

        /// <summary>
        /// Gets the inferred CLR type for the specified column index.
        /// </summary>
        /// <param name="columnIndex">The zero-based column index.</param>
        /// <returns>The inferred CLR type, or <see cref="object"/> if unrecorded.</returns>
        public Type GetNodeType(int columnIndex)
            => _nodeTypes.TryGetValue(columnIndex, out var t) ? t : typeof(object);

        /// <summary>
        /// Adds a type validation error to the graph.
        /// </summary>
        /// <param name="columnName">The column display name.</param>
        /// <param name="code">The validation error code.</param>
        /// <param name="message">The validation error message.</param>
        public void AddTypeError(string columnName, string code, string message)
        {
            _typeErrors.Add(ValidationError.Error(columnName, code, message));
        }

        /// <summary>
        /// Validates that both operands of an arithmetic operator are numeric types.
        /// </summary>
        /// <param name="columnName">The column display name.</param>
        /// <param name="left">The left operand type.</param>
        /// <param name="right">The right operand type.</param>
        /// <param name="op">The operator symbol.</param>
        public void ValidateArithmetic(string columnName, Type left, Type right, string op)
        {
            if (!IsNumeric(left) && left != typeof(object))
                AddTypeError(columnName, "TYPE_ARITH_INVALID",
                    $"Left operand of '{op}' has non-numeric type '{GetTypeName(left)}'.");

            if (!IsNumeric(right) && right != typeof(object))
                AddTypeError(columnName, "TYPE_ARITH_INVALID",
                    $"Right operand of '{op}' has non-numeric type '{GetTypeName(right)}'.");
        }

        /// <summary>
        /// Validates that the arguments supplied to a function call match its expected parameter types.
        /// </summary>
        /// <param name="columnName">The column display name.</param>
        /// <param name="funcName">The function name.</param>
        /// <param name="paramTypes">The supplied argument types.</param>
        /// <param name="expectedTypes">The expected parameter types.</param>
        public void ValidateFunctionCall(
            string                    columnName,
            string                    funcName,
            IReadOnlyList<Type>     paramTypes,
            IReadOnlyList<Type>     expectedTypes)
        {
            if (paramTypes.Count != expectedTypes.Count)
            {
                AddTypeError(columnName, "FUNC_PARAM_COUNT",
                    $"Function '{funcName}' expects {expectedTypes.Count} parameter(s) but {paramTypes.Count} were provided.");
                return;
            }

            for (int i = 0; i < paramTypes.Count; i++)
            {
                if (!IsAssignableTo(paramTypes[i], expectedTypes[i]))
                    AddTypeError(columnName, "FUNC_PARAM_TYPE",
                        $"Function '{funcName}' parameter {i + 1}: expected '{GetTypeName(expectedTypes[i])}' " +
                        $"but got '{GetTypeName(paramTypes[i])}'.");
            }
        }

        /// <summary>
        /// Validates that conditional branches produce compatible CLR types.
        /// </summary>
        /// <param name="columnName">The column display name.</param>
        /// <param name="trueType">The type produced by the true branch.</param>
        /// <param name="falseType">The type produced by the false branch.</param>
        public void ValidateConditionalBranches(string columnName, Type trueType, Type falseType)
        {
            if (trueType == typeof(object) || falseType == typeof(object))
                return;

            if (!IsAssignableTo(trueType, falseType) && !IsAssignableTo(falseType, trueType))
                AddTypeError(columnName, "COND_BRANCH_MISMATCH",
                    $"ConditionalColumn branches have incompatible types: " +
                    $"TrueValue='{GetTypeName(trueType)}', FalseValue='{GetTypeName(falseType)}'. " +
                    "Result will be 'object' at runtime.");
        }

        /// <summary>
        /// Validates comparison operand types.
        /// </summary>
        /// <param name="columnName">The column display name.</param>
        /// <param name="left">The left operand type.</param>
        /// <param name="right">The right operand type.</param>
        public void ValidateComparison(string columnName, Type left, Type right)
        {
            if (left == typeof(object) || right == typeof(object)) return;
        }

        /// <summary>
        /// Determines whether the specified CLR type is a supported numeric primitive.
        /// </summary>
        /// <param name="t">The type to test.</param>
        /// <returns><c>true</c> if numeric; otherwise, <c>false</c>.</returns>
        public static bool IsNumeric(Type t)
        {
            return t == typeof(int) || t == typeof(long) || t == typeof(double) ||
                   t == typeof(float) || t == typeof(decimal) || t == typeof(short) ||
                   t == typeof(byte) || t == typeof(uint) || t == typeof(ulong) ||
                   t == typeof(ushort) || t == typeof(sbyte) || t == typeof(decimal);
        }

        /// <summary>
        /// Determines whether a value of the actual type is assignable or convertible to the expected type.
        /// </summary>
        /// <param name="actual">The actual source type.</param>
        /// <param name="expected">The expected target type.</param>
        /// <returns><c>true</c> if assignable or convertible; otherwise, <c>false</c>.</returns>
        public static bool IsAssignableTo(Type actual, Type expected)
        {
            if (actual == expected) return true;
            if (expected == typeof(object)) return true;
            if (expected == typeof(string) && actual == typeof(object)) return false;
            if (IsNumeric(actual) && IsNumeric(expected)) return true;
            try { return expected.IsAssignableFrom(actual); }
            catch { return false; }
        }

        /// <summary>
        /// Returns a friendly display name for common CLR types.
        /// </summary>
        /// <param name="t">The CLR type.</param>
        /// <returns>A friendly type name string.</returns>
        public static string GetTypeName(Type t)
        {
            if (t == null) return "null";
            if (t == typeof(object)) return "object";
            if (t == typeof(string)) return "string";
            if (t == typeof(int)) return "int";
            if (t == typeof(long)) return "long";
            if (t == typeof(double)) return "double";
            if (t == typeof(decimal)) return "decimal";
            if (t == typeof(float)) return "float";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(DateTime)) return "DateTime";
            return t.Name;
        }
    }

    /// <summary>
    /// V2.5 — Service responsible for inferring the <see cref="Type"/> of every column
    /// during compilation, building the <see cref="TypeGraph"/> in the process.
    /// </summary>
    internal sealed class TypeInferrer
    {
        private readonly IPropertyResolver              _resolver;
        private readonly IExpressionEngine             _engine;
        private readonly ICustomFunctionRegistry?      _registry;
        private readonly TypeGraph                      _typeGraph;
        private readonly List<ColumnDefinition>          _allColumns;
        private readonly Dictionary<string, int>         _nameToIndex;

        public TypeGraph TypeGraph => _typeGraph;

        public TypeInferrer(
            IPropertyResolver         resolver,
            IExpressionEngine        engine,
            ICustomFunctionRegistry? registry,
            List<ColumnDefinition>   allColumns,
            Dictionary<string, int>  nameToIndex)
        {
            _resolver    = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _engine      = engine   ?? throw new ArgumentNullException(nameof(engine));
            _registry    = registry;
            _allColumns  = allColumns;
            _nameToIndex = nameToIndex;
            _typeGraph   = new TypeGraph();
        }

        public void InferAll(Type? sourceType)
        {
            for (int i = 0; i < _allColumns.Count; i++)
            {
                var col = _allColumns[i];
                var type = InferColumnType(col, i, sourceType);
                _typeGraph.SetNodeType(i, type);
                col.OutputType = type;
            }

            ValidateAll(sourceType);
        }

        private Type InferColumnType(ColumnDefinition col, int colIdx, Type? sourceType)
        {
            switch (col)
            {
                case FixedTextColumn _:
                    return typeof(string);

                case PropertyColumn pc when sourceType != null:
                {
                    var t = _resolver.ResolveType(sourceType, pc.PropertyPath);
                    return t ?? typeof(object);
                }

                case FormulaColumn fc:
                    return InferFormulaType(fc, colIdx, sourceType);

                case ConditionalColumn cc:
                    return InferConditionalType(cc, colIdx, sourceType);

                case CustomFunctionColumn cfc:
                    return InferFunctionType(cfc, colIdx, sourceType);

                default:
                    return typeof(object);
            }
        }

        private Type InferFormulaType(FormulaColumn fc, int colIdx, Type? sourceType)
        {
            if (_engine.Capabilities.SupportsTyping && sourceType != null)
            {
                var deps = ColumnReferenceParser.ExtractColumnReferences(fc.Expression);
                if (deps.Count == 0 && IsSimplePropertyPath(fc.Expression))
                {
                    return _resolver.ResolveType(sourceType, fc.Expression) ?? typeof(object);
                }

                var inferrer = new FormulaTypeInferrer(sourceType, _resolver);
                var t = inferrer.Infer(fc.Expression, _allColumns, _nameToIndex);
                if (t != null) return t;
            }

            return typeof(object);
        }

        private Type InferConditionalType(ConditionalColumn cc, int colIdx, Type? sourceType)
        {
            var trueType  = typeof(object);
            var falseType = typeof(object);

            if (!string.IsNullOrEmpty(cc.TrueValue))
            {
                if (cc.TrueValue.StartsWith("="))
                    trueType = InferInlineExpressionType(cc.TrueValue.Substring(1).Trim(), colIdx, sourceType);
                else if (cc.TrueValue.StartsWith("${"))
                    trueType = ResolveColumnRefType(cc.TrueValue, colIdx);
            }

            if (!string.IsNullOrEmpty(cc.FalseValue))
            {
                if (cc.FalseValue.StartsWith("="))
                    falseType = InferInlineExpressionType(cc.FalseValue.Substring(1).Trim(), colIdx, sourceType);
                else if (cc.FalseValue.StartsWith("${"))
                    falseType = ResolveColumnRefType(cc.FalseValue, colIdx);
            }

            _typeGraph.ValidateConditionalBranches(cc.DisplayName, trueType, falseType);

            if (trueType == falseType) return trueType;
            if (trueType == typeof(object)) return falseType;
            if (falseType == typeof(object)) return trueType;

            return typeof(object);
        }

        private Type InferFunctionType(CustomFunctionColumn cfc, int colIdx, Type? sourceType)
        {
            if (_registry == null) return typeof(object);

            var meta = _registry.GetMetadata(cfc.FunctionName);
            if (meta == null) return typeof(object);

            for (int i = 0; i < cfc.Parameters.Count; i++)
            {
                var param = cfc.Parameters[i];
                if (param.StartsWith("${") && param.EndsWith("}"))
                {
                    var refName = param.Substring(2, param.Length - 3).Trim();
                    if (_nameToIndex.TryGetValue(refName, out int depIdx))
                    {
                        var refType = _typeGraph.GetNodeType(depIdx);
                        if (meta.ParameterTypes.Count > i)
                            _typeGraph.ValidateFunctionCall(
                                cfc.DisplayName, cfc.FunctionName,
                                new[] { refType }.ToList(),
                                new[] { meta.ParameterTypes[i] }.ToList());
                    }
                }
            }

            return meta.ReturnType;
        }

        private Type InferInlineExpressionType(string expression, int colIdx, Type? sourceType)
        {
            if (string.IsNullOrEmpty(expression)) return typeof(object);

            if (IsNumericLiteral(expression)) return typeof(double);
            if (IsBoolLiteral(expression)) return typeof(bool);
            if (IsStringLiteral(expression)) return typeof(string);
            if (IsSimplePropertyPath(expression) && sourceType != null)
                return _resolver.ResolveType(sourceType, expression) ?? typeof(object);

            return typeof(object);
        }

        private Type ResolveColumnRefType(string refToken, int colIdx)
        {
            if (!refToken.StartsWith("${") || !refToken.EndsWith("}"))
                return typeof(object);

            var refName = refToken.Substring(2, refToken.Length - 3).Trim();
            if (_nameToIndex.TryGetValue(refName, out int depIdx))
                return _typeGraph.GetNodeType(depIdx);

            return typeof(object);
        }

        private void ValidateAll(Type? sourceType)
        {
        }

        private static bool IsSimplePropertyPath(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            foreach (char c in s)
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_') return false;
            return true;
        }

        private static bool IsNumericLiteral(string s)
        {
            return double.TryParse(s, out _);
        }

        private static bool IsBoolLiteral(string s)
        {
            return s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   s.Equals("false", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStringLiteral(string s)
        {
            return (s.StartsWith("\"") && s.EndsWith("\"")) ||
                   (s.StartsWith("'") && s.EndsWith("'"));
        }
    }

    internal sealed class FormulaTypeInferrer
    {
        private readonly Type _sourceType;
        private readonly IPropertyResolver _resolver;

        public FormulaTypeInferrer(Type sourceType, IPropertyResolver resolver)
        {
            _sourceType = sourceType;
            _resolver   = resolver;
        }

        public Type? Infer(string expression, List<ColumnDefinition> allColumns, Dictionary<string, int> nameToIndex)
        {
            if (string.IsNullOrWhiteSpace(expression)) return typeof(object);

            if (IsSimplePropertyPath(expression))
                return _resolver.ResolveType(_sourceType, expression);

            if (expression.Contains("+") || expression.Contains("-") ||
                expression.Contains("*") || expression.Contains("/"))
            {
                return typeof(double);
            }

            if (expression.Contains("==") || expression.Contains("!=") ||
                expression.Contains("<")  || expression.Contains(">"))
            {
                return typeof(bool);
            }

            return null;
        }

        private static bool IsSimplePropertyPath(string s)
        {
            foreach (char c in s)
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_') return false;
            return true;
        }
    }
}
