using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Application.Execution
{
    internal enum TokenKind
    {
        End,
        Number,
        String,
        Identifier,
        Plus,
        Minus,
        Multiply,
        Divide,
        Modulo,
        Equal,          // == or =
        NotEqual,       // != or <>
        Less,           // <
        LessOrEqual,    // <=
        Greater,        // >
        GreaterOrEqual, // >=
        And,            // && or and
        Or,             // || or or
        Not,            // ! or not
        Question,       // ?
        Colon,          // :
        OpenParen,      // (
        CloseParen,     // )
        True,           // true
        False,          // false
        Null            // null
    }

    internal sealed class Token
    {
        public TokenKind Kind { get; }
        public object? Value { get; }

        public Token(TokenKind kind, object? value = null)
        {
            Kind = kind;
            Value = value;
        }
    }

    /// <summary>
    /// Recursive-descent expression parser and LINQ expression tree compiler for
    /// formulas, variables, and conditions. Supports both runtime IExecutionContext
    /// delegates and direct strongly-typed pipeline compilation.
    /// </summary>
    public static class CompiledExpressionBuilder
    {
        private static readonly MethodInfo _getResultMethod
            = typeof(IExecutionContext).GetMethod(nameof(IExecutionContext.GetResult))
              ?? throw new InvalidOperationException("IExecutionContext.GetResult not found.");

        /// <summary>
        /// Compiles an expression string into an executable delegate that accepts an execution context and returns the evaluated result.
        /// </summary>
        /// <param name="expression">The expression string to compile.</param>
        /// <param name="sourceType">The optional source input type.</param>
        /// <param name="variableIndexes">The map of variable names to variable array indexes.</param>
        /// <param name="columnIndexes">The map of column names to column result array indexes.</param>
        /// <param name="variableValues">The list of compiled variable values.</param>
        /// <returns>A compiled delegate, or <c>null</c> if compilation fails.</returns>
        public static Func<IExecutionContext, object?>? Compile(
            string expression,
            Type? sourceType,
            IReadOnlyDictionary<string, int>? variableIndexes,
            IReadOnlyDictionary<string, int>? columnIndexes,
            IReadOnlyList<object?>? variableValues)
        {
            if (string.IsNullOrWhiteSpace(expression)) return null;

            try
            {
                var tokens = Tokenize(expression);
                if (tokens == null || tokens.Count == 0) return null;

                var ctxParam = Expression.Parameter(typeof(IExecutionContext), "ctx");
                var parser = new Parser(tokens, sourceType, variableIndexes, columnIndexes, variableValues, ctxParam);
                var body = parser.ParseExpression();

                if (body == null || !parser.IsAtEnd) return null;

                var converted = Expression.Convert(body, typeof(object));
                return Expression.Lambda<Func<IExecutionContext, object?>>(converted, ctxParam).Compile();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Compiles a condition expression string into an executable delegate that evaluates to a boolean value.
        /// </summary>
        /// <param name="conditionExpression">The boolean condition expression string to compile.</param>
        /// <param name="sourceType">The optional source input type.</param>
        /// <param name="variableIndexes">The map of variable names to variable array indexes.</param>
        /// <param name="columnIndexes">The map of column names to column result array indexes.</param>
        /// <param name="variableValues">The list of compiled variable values.</param>
        /// <returns>A compiled boolean delegate, or <c>null</c> if compilation fails.</returns>
        public static Func<IExecutionContext, bool>? CompileCondition(
            string conditionExpression,
            Type? sourceType,
            IReadOnlyDictionary<string, int>? variableIndexes,
            IReadOnlyDictionary<string, int>? columnIndexes,
            IReadOnlyList<object?>? variableValues)
        {
            if (string.IsNullOrWhiteSpace(conditionExpression)) return null;

            try
            {
                var tokens = Tokenize(conditionExpression);
                if (tokens == null || tokens.Count == 0) return null;

                var ctxParam = Expression.Parameter(typeof(IExecutionContext), "ctx");
                var parser = new Parser(tokens, sourceType, variableIndexes, columnIndexes, variableValues, ctxParam);
                var body = parser.ParseExpression();

                if (body == null || !parser.IsAtEnd) return null;

                Expression boolBody;
                if (body.Type == typeof(bool))
                {
                    boolBody = body;
                }
                else
                {
                    boolBody = Expression.Call(
                        typeof(CompiledExpressionCoercion),
                        nameof(CompiledExpressionCoercion.ToBoolean),
                        null,
                        Expression.Convert(body, typeof(object)));
                }

                return Expression.Lambda<Func<IExecutionContext, bool>>(boolBody, ctxParam).Compile();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parses an expression and constructs a strongly-typed LINQ <see cref="Expression"/> tree.
        /// </summary>
        /// <param name="expression">The expression string to parse.</param>
        /// <param name="sourceType">The optional source input type.</param>
        /// <param name="sourceExpr">The source object parameter expression.</param>
        /// <param name="columnExpressions">A dictionary of pre-built expressions for named columns.</param>
        /// <param name="variableValuesByName">A dictionary of variable names and values.</param>
        /// <returns>A strongly-typed expression tree, or <c>null</c> if building fails.</returns>
        public static Expression? BuildTypedExpression(
            string expression,
            Type? sourceType,
            Expression? sourceExpr,
            IReadOnlyDictionary<string, Expression>? columnExpressions,
            IReadOnlyDictionary<string, object?>? variableValuesByName)
        {
            if (string.IsNullOrWhiteSpace(expression)) return null;

            try
            {
                var tokens = Tokenize(expression);
                if (tokens == null || tokens.Count == 0) return null;

                var parser = new Parser(tokens, sourceType, sourceExpr, columnExpressions, variableValuesByName);
                var body = parser.ParseExpression();

                if (body == null || !parser.IsAtEnd) return null;

                return body;
            }
            catch
            {
                return null;
            }
        }

        internal static List<Token>? Tokenize(string expr)
        {
            var tokens = new List<Token>();
            int i = 0;
            int len = expr.Length;

            while (i < len)
            {
                char c = expr[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                // Reference ${...}
                if (c == '$' && i + 1 < len && expr[i + 1] == '{')
                {
                    int start = i + 2;
                    int end = expr.IndexOf('}', start);
                    if (end < 0) return null;
                    string name = expr.Substring(start, end - start).Trim();
                    tokens.Add(new Token(TokenKind.Identifier, name));
                    i = end + 1;
                    continue;
                }

                // Reference [...]
                if (c == '[')
                {
                    int start = i + 1;
                    int end = expr.IndexOf(']', start);
                    if (end < 0) return null;
                    string name = expr.Substring(start, end - start).Trim();
                    tokens.Add(new Token(TokenKind.Identifier, name));
                    i = end + 1;
                    continue;
                }

                // String literals '...' or "..."
                if (c == '\'' || c == '"')
                {
                    char quote = c;
                    i++;
                    var sb = new System.Text.StringBuilder();
                    bool closed = false;

                    while (i < len)
                    {
                        if (expr[i] == quote)
                        {
                            if (i + 1 < len && expr[i + 1] == quote)
                            {
                                sb.Append(quote);
                                i += 2;
                                continue;
                            }
                            closed = true;
                            i++;
                            break;
                        }
                        if (expr[i] == '\\' && i + 1 < len)
                        {
                            i++;
                            char esc = expr[i];
                            if (esc == 'n') sb.Append('\n');
                            else if (esc == 'r') sb.Append('\r');
                            else if (esc == 't') sb.Append('\t');
                            else sb.Append(esc);
                            i++;
                            continue;
                        }
                        sb.Append(expr[i]);
                        i++;
                    }

                    if (!closed) return null;
                    tokens.Add(new Token(TokenKind.String, sb.ToString()));
                    continue;
                }

                // Numbers: digits or . followed by digit
                if (char.IsDigit(c) || (c == '.' && i + 1 < len && char.IsDigit(expr[i + 1])))
                {
                    int start = i;
                    bool hasDot = false;
                    bool hasExp = false;

                    while (i < len)
                    {
                        char nc = expr[i];
                        if (char.IsDigit(nc))
                        {
                            i++;
                        }
                        else if (nc == '.' && !hasDot && !hasExp)
                        {
                            hasDot = true;
                            i++;
                        }
                        else if ((nc == 'e' || nc == 'E') && !hasExp)
                        {
                            hasExp = true;
                            hasDot = true;
                            i++;
                            if (i < len && (expr[i] == '+' || expr[i] == '-'))
                                i++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    char suffix = '\0';
                    if (i < len && (expr[i] == 'm' || expr[i] == 'M' || expr[i] == 'd' || expr[i] == 'D' ||
                                    expr[i] == 'f' || expr[i] == 'F' || expr[i] == 'l' || expr[i] == 'L'))
                    {
                        suffix = char.ToLowerInvariant(expr[i]);
                        i++;
                    }

                    string numStr = expr.Substring(start, i - start - (suffix != '\0' ? 1 : 0));
                    object numVal;

                    if (suffix == 'm')
                    {
                        if (!decimal.TryParse(numStr, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var dec)) return null;
                        numVal = dec;
                    }
                    else if (suffix == 'f')
                    {
                        if (!float.TryParse(numStr, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var flt)) return null;
                        numVal = flt;
                    }
                    else if (hasDot || hasExp || suffix == 'd')
                    {
                        if (!double.TryParse(numStr, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var dbl)) return null;
                        numVal = dbl;
                    }
                    else if (suffix == 'l')
                    {
                        if (!long.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lng)) return null;
                        numVal = lng;
                    }
                    else
                    {
                        if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var itg))
                            numVal = itg;
                        else if (long.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lng))
                            numVal = lng;
                        else if (double.TryParse(numStr, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var dbl))
                            numVal = dbl;
                        else return null;
                    }

                    tokens.Add(new Token(TokenKind.Number, numVal));
                    continue;
                }

                // Identifiers or keywords
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < len && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_' || expr[i] == '.'))
                    {
                        i++;
                    }
                    string word = expr.Substring(start, i - start);
                    string wordLower = word.ToLowerInvariant();

                    if (wordLower == "true") tokens.Add(new Token(TokenKind.True, true));
                    else if (wordLower == "false") tokens.Add(new Token(TokenKind.False, false));
                    else if (wordLower == "null") tokens.Add(new Token(TokenKind.Null, null));
                    else if (wordLower == "and") tokens.Add(new Token(TokenKind.And));
                    else if (wordLower == "or") tokens.Add(new Token(TokenKind.Or));
                    else if (wordLower == "not") tokens.Add(new Token(TokenKind.Not));
                    else tokens.Add(new Token(TokenKind.Identifier, word));
                    continue;
                }

                // Operators
                switch (c)
                {
                    case '+': tokens.Add(new Token(TokenKind.Plus)); i++; break;
                    case '-': tokens.Add(new Token(TokenKind.Minus)); i++; break;
                    case '*': tokens.Add(new Token(TokenKind.Multiply)); i++; break;
                    case '/': tokens.Add(new Token(TokenKind.Divide)); i++; break;
                    case '%': tokens.Add(new Token(TokenKind.Modulo)); i++; break;
                    case '(': tokens.Add(new Token(TokenKind.OpenParen)); i++; break;
                    case ')': tokens.Add(new Token(TokenKind.CloseParen)); i++; break;
                    case '?': tokens.Add(new Token(TokenKind.Question)); i++; break;
                    case ':': tokens.Add(new Token(TokenKind.Colon)); i++; break;

                    case '=':
                        if (i + 1 < len && expr[i + 1] == '=') { tokens.Add(new Token(TokenKind.Equal)); i += 2; }
                        else { tokens.Add(new Token(TokenKind.Equal)); i++; }
                        break;

                    case '!':
                        if (i + 1 < len && expr[i + 1] == '=') { tokens.Add(new Token(TokenKind.NotEqual)); i += 2; }
                        else { tokens.Add(new Token(TokenKind.Not)); i++; }
                        break;

                    case '<':
                        if (i + 1 < len && expr[i + 1] == '>') { tokens.Add(new Token(TokenKind.NotEqual)); i += 2; }
                        else if (i + 1 < len && expr[i + 1] == '=') { tokens.Add(new Token(TokenKind.LessOrEqual)); i += 2; }
                        else { tokens.Add(new Token(TokenKind.Less)); i++; }
                        break;

                    case '>':
                        if (i + 1 < len && expr[i + 1] == '=') { tokens.Add(new Token(TokenKind.GreaterOrEqual)); i += 2; }
                        else { tokens.Add(new Token(TokenKind.Greater)); i++; }
                        break;

                    case '&':
                        if (i + 1 < len && expr[i + 1] == '&') { tokens.Add(new Token(TokenKind.And)); i += 2; }
                        else return null;
                        break;

                    case '|':
                        if (i + 1 < len && expr[i + 1] == '|') { tokens.Add(new Token(TokenKind.Or)); i += 2; }
                        else return null;
                        break;

                    default:
                        return null;
                }
            }

            tokens.Add(new Token(TokenKind.End));
            return tokens;
        }

        private sealed class Parser
        {
            private readonly List<Token> _tokens;
            private readonly Type? _sourceType;
            private readonly Expression? _sourceExpr;
            private readonly IReadOnlyDictionary<string, Expression>? _columnExpressions;
            private readonly IReadOnlyDictionary<string, object?>? _variableValuesByName;
            private readonly IReadOnlyDictionary<string, int>? _variableIndexes;
            private readonly IReadOnlyDictionary<string, int>? _columnIndexes;
            private readonly IReadOnlyList<object?>? _variableValues;
            private readonly ParameterExpression? _ctxParam;
            private int _pos;

            public Parser(
                List<Token> tokens,
                Type? sourceType,
                IReadOnlyDictionary<string, int>? variableIndexes,
                IReadOnlyDictionary<string, int>? columnIndexes,
                IReadOnlyList<object?>? variableValues,
                ParameterExpression ctxParam)
            {
                _tokens = tokens;
                _sourceType = sourceType;
                _variableIndexes = variableIndexes;
                _columnIndexes = columnIndexes;
                _variableValues = variableValues;
                _ctxParam = ctxParam;
            }

            public Parser(
                List<Token> tokens,
                Type? sourceType,
                Expression? sourceExpr,
                IReadOnlyDictionary<string, Expression>? columnExpressions,
                IReadOnlyDictionary<string, object?>? variableValuesByName)
            {
                _tokens = tokens;
                _sourceType = sourceType;
                _sourceExpr = sourceExpr;
                _columnExpressions = columnExpressions;
                _variableValuesByName = variableValuesByName;
            }

            public bool IsAtEnd => Peek.Kind == TokenKind.End;
            private Token Peek => _pos < _tokens.Count ? _tokens[_pos] : new Token(TokenKind.End);

            private Token Advance()
            {
                var t = Peek;
                if (_pos < _tokens.Count) _pos++;
                return t;
            }

            private bool Match(TokenKind kind)
            {
                if (Peek.Kind == kind)
                {
                    Advance();
                    return true;
                }
                return false;
            }

            public Expression? ParseExpression()
            {
                return ParseTernary();
            }

            private Expression? ParseTernary()
            {
                var cond = ParseOr();
                if (cond == null) return null;

                if (Match(TokenKind.Question))
                {
                    var trueExpr = ParseTernary();
                    if (trueExpr == null || !Match(TokenKind.Colon)) return null;
                    var falseExpr = ParseTernary();
                    if (falseExpr == null) return null;

                    Expression boolCond = cond.Type == typeof(bool)
                        ? cond
                        : Expression.Call(typeof(CompiledExpressionCoercion),
                            nameof(CompiledExpressionCoercion.ToBoolean), null,
                            Expression.Convert(cond, typeof(object)));

                    if (trueExpr.Type == falseExpr.Type)
                    {
                        return Expression.Condition(boolCond, trueExpr, falseExpr);
                    }

                    return Expression.Condition(boolCond,
                        Expression.Convert(trueExpr, typeof(object)),
                        Expression.Convert(falseExpr, typeof(object)));
                }

                return cond;
            }

            private Expression? ParseOr()
            {
                var expr = ParseAnd();
                if (expr == null) return null;

                while (Match(TokenKind.Or))
                {
                    var right = ParseAnd();
                    if (right == null) return null;

                    var leftBool = expr.Type == typeof(bool) ? expr :
                        Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToBoolean), null, Expression.Convert(expr, typeof(object)));
                    var rightBool = right.Type == typeof(bool) ? right :
                        Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToBoolean), null, Expression.Convert(right, typeof(object)));

                    expr = Expression.OrElse(leftBool, rightBool);
                }

                return expr;
            }

            private Expression? ParseAnd()
            {
                var expr = ParseEquality();
                if (expr == null) return null;

                while (Match(TokenKind.And))
                {
                    var right = ParseEquality();
                    if (right == null) return null;

                    var leftBool = expr.Type == typeof(bool) ? expr :
                        Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToBoolean), null, Expression.Convert(expr, typeof(object)));
                    var rightBool = right.Type == typeof(bool) ? right :
                        Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToBoolean), null, Expression.Convert(right, typeof(object)));

                    expr = Expression.AndAlso(leftBool, rightBool);
                }

                return expr;
            }

            private Expression? ParseEquality()
            {
                var expr = ParseRelational();
                if (expr == null) return null;

                while (Peek.Kind == TokenKind.Equal || Peek.Kind == TokenKind.NotEqual)
                {
                    var op = Advance().Kind;
                    var right = ParseRelational();
                    if (right == null) return null;

                    expr = op == TokenKind.Equal
                        ? BuildEquality(expr, right)
                        : Expression.Not(BuildEquality(expr, right));
                }

                return expr;
            }

            private Expression? ParseRelational()
            {
                var expr = ParseAdditive();
                if (expr == null) return null;

                while (Peek.Kind == TokenKind.Less || Peek.Kind == TokenKind.LessOrEqual ||
                       Peek.Kind == TokenKind.Greater || Peek.Kind == TokenKind.GreaterOrEqual)
                {
                    var op = Advance().Kind;
                    var right = ParseAdditive();
                    if (right == null) return null;

                    expr = BuildRelational(expr, right, op);
                }

                return expr;
            }

            private Expression? ParseAdditive()
            {
                var expr = ParseMultiplicative();
                if (expr == null) return null;

                while (Peek.Kind == TokenKind.Plus || Peek.Kind == TokenKind.Minus)
                {
                    var op = Advance().Kind;
                    var right = ParseMultiplicative();
                    if (right == null) return null;

                    expr = op == TokenKind.Plus
                        ? BuildAdd(expr, right)
                        : BuildSubtract(expr, right);
                }

                return expr;
            }

            private Expression? ParseMultiplicative()
            {
                var expr = ParseUnary();
                if (expr == null) return null;

                while (Peek.Kind == TokenKind.Multiply || Peek.Kind == TokenKind.Divide || Peek.Kind == TokenKind.Modulo)
                {
                    var op = Advance().Kind;
                    var right = ParseUnary();
                    if (right == null) return null;

                    expr = op switch
                    {
                        TokenKind.Multiply => BuildMultiply(expr, right),
                        TokenKind.Divide => BuildDivide(expr, right),
                        TokenKind.Modulo => BuildModulo(expr, right),
                        _ => null
                    };

                    if (expr == null) return null;
                }

                return expr;
            }

            private Expression? ParseUnary()
            {
                if (Match(TokenKind.Minus))
                {
                    var right = ParseUnary();
                    if (right == null) return null;
                    return BuildNegate(right);
                }

                if (Match(TokenKind.Plus))
                {
                    return ParseUnary();
                }

                if (Match(TokenKind.Not))
                {
                    var right = ParseUnary();
                    if (right == null) return null;
                    var boolRight = right.Type == typeof(bool) ? right :
                        Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToBoolean), null, Expression.Convert(right, typeof(object)));
                    return Expression.Not(boolRight);
                }

                return ParsePrimary();
            }

            private Expression? ParsePrimary()
            {
                var t = Peek;

                if (Match(TokenKind.Number))
                {
                    return Expression.Constant(t.Value, t.Value!.GetType());
                }

                if (Match(TokenKind.String))
                {
                    return Expression.Constant((string)t.Value!, typeof(string));
                }

                if (Match(TokenKind.True))
                {
                    return Expression.Constant(true, typeof(bool));
                }

                if (Match(TokenKind.False))
                {
                    return Expression.Constant(false, typeof(bool));
                }

                if (Match(TokenKind.Null))
                {
                    return Expression.Constant(null, typeof(object));
                }

                if (Match(TokenKind.OpenParen))
                {
                    var inner = ParseExpression();
                    if (inner == null || !Match(TokenKind.CloseParen)) return null;
                    return inner;
                }

                if (Match(TokenKind.Identifier))
                {
                    string name = (string)t.Value!;
                    return ResolveIdentifier(name);
                }

                return null;
            }

            private Expression? ResolveIdentifier(string name)
            {
                // Mode 1: Direct typed resolution (for SpecializedPipelineBuilder)
                if (_sourceExpr != null || _columnExpressions != null || _variableValuesByName != null)
                {
                    if (_variableValuesByName != null && _variableValuesByName.TryGetValue(name, out var vVal))
                    {
                        if (vVal == null) return Expression.Constant(null, typeof(object));
                        return Expression.Constant(vVal, vVal.GetType());
                    }

                    if (_columnExpressions != null && _columnExpressions.TryGetValue(name, out var colExpr))
                    {
                        return colExpr;
                    }

                    if (_sourceType != null && _sourceExpr != null)
                    {
                        var propExpr = PropertyAccessorBuilder.BuildPropertyExpression(_sourceType, name, _sourceExpr);
                        if (propExpr != null) return propExpr;
                    }

                    return null;
                }

                // Mode 2: Context parameter resolution (for IExecutionContext delegates)
                if (_ctxParam != null)
                {
                    if (_variableIndexes != null && _variableIndexes.TryGetValue(name, out int vIdx))
                    {
                        var varArr = Expression.Property(_ctxParam, nameof(IExecutionContext.Variables));
                        var vAccess = Expression.ArrayIndex(varArr, Expression.Constant(vIdx));

                        if (_variableValues != null && vIdx >= 0 && vIdx < _variableValues.Count && _variableValues[vIdx] != null)
                        {
                            var vVal = _variableValues[vIdx];
                            var vType = vVal!.GetType();

                            if (vType == typeof(double))
                                return Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDouble), null, vAccess);
                            if (vType == typeof(decimal))
                                return Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDecimal), null, vAccess);
                            if (vType == typeof(int))
                                return Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToInt32), null, vAccess);
                            if (vType == typeof(long))
                                return Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToInt64), null, vAccess);
                            if (vType == typeof(bool))
                                return Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToBoolean), null, vAccess);
                            if (vType == typeof(string))
                                return Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToString), null, vAccess);
                        }

                        return vAccess;
                    }

                    if (_columnIndexes != null && _columnIndexes.TryGetValue(name, out int cIdx))
                    {
                        return Expression.Call(_ctxParam, _getResultMethod, Expression.Constant(cIdx));
                    }

                    if (_sourceType != null)
                    {
                        var srcExpr = Expression.Property(_ctxParam, nameof(IExecutionContext.Source));
                        var propExpr = PropertyAccessorBuilder.BuildPropertyExpression(_sourceType, name, srcExpr);
                        if (propExpr != null) return propExpr;
                    }
                }

                return null;
            }

            private static Expression BuildAdd(Expression left, Expression right)
            {
                if (left.Type == typeof(string) || right.Type == typeof(string))
                {
                    var leftStr = left.Type == typeof(string) ? left :
                        Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToString), null, Expression.Convert(left, typeof(object)));
                    var rightStr = right.Type == typeof(string) ? right :
                        Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToString), null, Expression.Convert(right, typeof(object)));

                    var concatMethod = typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string) })!;
                    return Expression.Add(leftStr, rightStr, concatMethod);
                }

                if (CompiledExpressionCoercion.IsNumericType(left.Type) && CompiledExpressionCoercion.IsNumericType(right.Type))
                {
                    var commonType = CompiledExpressionCoercion.GetCommonNumericType(left.Type, right.Type);
                    var lConv = left.Type == commonType ? left : Expression.Convert(left, commonType);
                    var rConv = right.Type == commonType ? right : Expression.Convert(right, commonType);
                    return Expression.Add(lConv, rConv);
                }

                if (CompiledExpressionCoercion.IsNumericType(left.Type))
                {
                    var rDbl = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDouble), null, Expression.Convert(right, typeof(object)));
                    return Expression.Add(Expression.Convert(left, typeof(double)), rDbl);
                }
                if (CompiledExpressionCoercion.IsNumericType(right.Type))
                {
                    var lDbl = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDouble), null, Expression.Convert(left, typeof(object)));
                    return Expression.Add(lDbl, Expression.Convert(right, typeof(double)));
                }

                return Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.Add), null,
                    Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
            }

            private static Expression BuildSubtract(Expression left, Expression right)
            {
                if (CompiledExpressionCoercion.IsNumericType(left.Type) && CompiledExpressionCoercion.IsNumericType(right.Type))
                {
                    var commonType = CompiledExpressionCoercion.GetCommonNumericType(left.Type, right.Type);
                    var lConv = left.Type == commonType ? left : Expression.Convert(left, commonType);
                    var rConv = right.Type == commonType ? right : Expression.Convert(right, commonType);
                    return Expression.Subtract(lConv, rConv);
                }

                if (CompiledExpressionCoercion.IsNumericType(left.Type))
                {
                    var rDbl = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDouble), null, Expression.Convert(right, typeof(object)));
                    return Expression.Subtract(Expression.Convert(left, typeof(double)), rDbl);
                }
                if (CompiledExpressionCoercion.IsNumericType(right.Type))
                {
                    var lDbl = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDouble), null, Expression.Convert(left, typeof(object)));
                    return Expression.Subtract(lDbl, Expression.Convert(right, typeof(double)));
                }

                return Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.Subtract), null,
                    Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
            }

            private static Expression BuildMultiply(Expression left, Expression right)
            {
                if (CompiledExpressionCoercion.IsNumericType(left.Type) && CompiledExpressionCoercion.IsNumericType(right.Type))
                {
                    var commonType = CompiledExpressionCoercion.GetCommonNumericType(left.Type, right.Type);
                    var lConv = left.Type == commonType ? left : Expression.Convert(left, commonType);
                    var rConv = right.Type == commonType ? right : Expression.Convert(right, commonType);
                    return Expression.Multiply(lConv, rConv);
                }

                if (CompiledExpressionCoercion.IsNumericType(left.Type))
                {
                    var rDbl = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDouble), null, Expression.Convert(right, typeof(object)));
                    return Expression.Multiply(Expression.Convert(left, typeof(double)), rDbl);
                }
                if (CompiledExpressionCoercion.IsNumericType(right.Type))
                {
                    var lDbl = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDouble), null, Expression.Convert(left, typeof(object)));
                    return Expression.Multiply(lDbl, Expression.Convert(right, typeof(double)));
                }

                return Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.Multiply), null,
                    Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
            }

            private static Expression BuildDivide(Expression left, Expression right)
            {
                if (CompiledExpressionCoercion.IsNumericType(left.Type) && CompiledExpressionCoercion.IsNumericType(right.Type))
                {
                    var commonType = CompiledExpressionCoercion.GetCommonNumericType(left.Type, right.Type);
                    var lConv = left.Type == commonType ? left : Expression.Convert(left, commonType);
                    var rConv = right.Type == commonType ? right : Expression.Convert(right, commonType);
                    return Expression.Divide(lConv, rConv);
                }

                if (CompiledExpressionCoercion.IsNumericType(left.Type))
                {
                    var rDbl = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDouble), null, Expression.Convert(right, typeof(object)));
                    return Expression.Divide(Expression.Convert(left, typeof(double)), rDbl);
                }
                if (CompiledExpressionCoercion.IsNumericType(right.Type))
                {
                    var lDbl = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDouble), null, Expression.Convert(left, typeof(object)));
                    return Expression.Divide(lDbl, Expression.Convert(right, typeof(double)));
                }

                return Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.Divide), null,
                    Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
            }

            private static Expression BuildModulo(Expression left, Expression right)
            {
                if (CompiledExpressionCoercion.IsNumericType(left.Type) && CompiledExpressionCoercion.IsNumericType(right.Type))
                {
                    var commonType = CompiledExpressionCoercion.GetCommonNumericType(left.Type, right.Type);
                    var lConv = left.Type == commonType ? left : Expression.Convert(left, commonType);
                    var rConv = right.Type == commonType ? right : Expression.Convert(right, commonType);
                    return Expression.Modulo(lConv, rConv);
                }

                return Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.Modulo), null,
                    Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
            }

            private static Expression BuildNegate(Expression expr)
            {
                if (CompiledExpressionCoercion.IsNumericType(expr.Type))
                {
                    return Expression.Negate(expr);
                }

                var dbl = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDouble), null, Expression.Convert(expr, typeof(object)));
                return Expression.Negate(dbl);
            }

            private static Expression BuildEquality(Expression left, Expression right)
            {
                if (CompiledExpressionCoercion.IsNumericType(left.Type) && CompiledExpressionCoercion.IsNumericType(right.Type))
                {
                    var commonType = CompiledExpressionCoercion.GetCommonNumericType(left.Type, right.Type);
                    var lConv = left.Type == commonType ? left : Expression.Convert(left, commonType);
                    var rConv = right.Type == commonType ? right : Expression.Convert(right, commonType);
                    return Expression.Equal(lConv, rConv);
                }

                if (left.Type == typeof(string) && right.Type == typeof(string))
                {
                    var strEquals = typeof(string).GetMethod(nameof(string.Equals), new[] { typeof(string), typeof(string), typeof(StringComparison) })!;
                    return Expression.Call(strEquals, left, right, Expression.Constant(StringComparison.OrdinalIgnoreCase));
                }

                if (left.Type == typeof(string))
                {
                    var rStr = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToString), null, Expression.Convert(right, typeof(object)));
                    var strEquals = typeof(string).GetMethod(nameof(string.Equals), new[] { typeof(string), typeof(string), typeof(StringComparison) })!;
                    return Expression.Call(strEquals, left, rStr, Expression.Constant(StringComparison.OrdinalIgnoreCase));
                }
                if (right.Type == typeof(string))
                {
                    var lStr = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToString), null, Expression.Convert(left, typeof(object)));
                    var strEquals = typeof(string).GetMethod(nameof(string.Equals), new[] { typeof(string), typeof(string), typeof(StringComparison) })!;
                    return Expression.Call(strEquals, lStr, right, Expression.Constant(StringComparison.OrdinalIgnoreCase));
                }

                if (left.Type == typeof(bool) && right.Type == typeof(bool))
                {
                    return Expression.Equal(left, right);
                }

                if (left.Type == typeof(bool))
                {
                    var rBool = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToBoolean), null, Expression.Convert(right, typeof(object)));
                    return Expression.Equal(left, rBool);
                }
                if (right.Type == typeof(bool))
                {
                    var lBool = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToBoolean), null, Expression.Convert(left, typeof(object)));
                    return Expression.Equal(lBool, right);
                }

                var areEqualCall = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.AreEqual), null,
                    Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
                return areEqualCall;
            }

            private static Expression BuildRelational(Expression left, Expression right, TokenKind op)
            {
                if (CompiledExpressionCoercion.IsNumericType(left.Type) && CompiledExpressionCoercion.IsNumericType(right.Type))
                {
                    var commonType = CompiledExpressionCoercion.GetCommonNumericType(left.Type, right.Type);
                    var lConv = left.Type == commonType ? left : Expression.Convert(left, commonType);
                    var rConv = right.Type == commonType ? right : Expression.Convert(right, commonType);
                    return op switch
                    {
                        TokenKind.Less => Expression.LessThan(lConv, rConv),
                        TokenKind.LessOrEqual => Expression.LessThanOrEqual(lConv, rConv),
                        TokenKind.Greater => Expression.GreaterThan(lConv, rConv),
                        TokenKind.GreaterOrEqual => Expression.GreaterThanOrEqual(lConv, rConv),
                        _ => Expression.Constant(false)
                    };
                }

                if (CompiledExpressionCoercion.IsNumericType(left.Type))
                {
                    var rDbl = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDouble), null, Expression.Convert(right, typeof(object)));
                    var lDbl = Expression.Convert(left, typeof(double));
                    return op switch
                    {
                        TokenKind.Less => Expression.LessThan(lDbl, rDbl),
                        TokenKind.LessOrEqual => Expression.LessThanOrEqual(lDbl, rDbl),
                        TokenKind.Greater => Expression.GreaterThan(lDbl, rDbl),
                        TokenKind.GreaterOrEqual => Expression.GreaterThanOrEqual(lDbl, rDbl),
                        _ => Expression.Constant(false)
                    };
                }
                if (CompiledExpressionCoercion.IsNumericType(right.Type))
                {
                    var lDbl = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToDouble), null, Expression.Convert(left, typeof(object)));
                    var rDbl = Expression.Convert(right, typeof(double));
                    return op switch
                    {
                        TokenKind.Less => Expression.LessThan(lDbl, rDbl),
                        TokenKind.LessOrEqual => Expression.LessThanOrEqual(lDbl, rDbl),
                        TokenKind.Greater => Expression.GreaterThan(lDbl, rDbl),
                        TokenKind.GreaterOrEqual => Expression.GreaterThanOrEqual(lDbl, rDbl),
                        _ => Expression.Constant(false)
                    };
                }

                var compCall = Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.Compare), null,
                    Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
                var zero = Expression.Constant(0);
                return op switch
                {
                    TokenKind.Less => Expression.LessThan(compCall, zero),
                    TokenKind.LessOrEqual => Expression.LessThanOrEqual(compCall, zero),
                    TokenKind.Greater => Expression.GreaterThan(compCall, zero),
                    TokenKind.GreaterOrEqual => Expression.GreaterThanOrEqual(compCall, zero),
                    _ => Expression.Constant(false)
                };
            }
        }
    }
}
