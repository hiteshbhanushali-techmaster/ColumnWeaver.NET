using System;
using System.Collections.Generic;
using System.Linq;
using ColumnWeaver.NET.Application.Abstractions;
using ColumnWeaver.NET.Application.Exceptions;
using ColumnWeaver.NET.Application.Execution;
using ColumnWeaver.NET.Application.Pipeline;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Interfaces;
using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET.Application.Validation
{
    /// <summary>
    /// Provides static validation methods for <see cref="ParserConfiguration"/> instances,
    /// verifying column definitions, variable bindings, dependencies, and expressions.
    /// </summary>
    public static class ConfigurationValidator
    {
        /// <summary>
        /// Validates a <see cref="ParserConfiguration"/> against optional source type, resolver, engine, and function registry instances.
        /// </summary>
        /// <param name="config">The parser configuration to validate. Must not be null.</param>
        /// <param name="sourceType">The optional source input type for property path checking.</param>
        /// <param name="resolver">The optional property resolver to check property paths.</param>
        /// <param name="engine">The optional expression engine to validate syntax.</param>
        /// <param name="registry">The optional custom function registry to check registered functions.</param>
        /// <returns>A <see cref="ValidationResult"/> containing errors, warnings, and informational diagnostics.</returns>
        public static ValidationResult Validate(
            ParserConfiguration      config,
            Type?                    sourceType,
            IPropertyResolver?       resolver,
            IExpressionEngine?       engine,
            ICustomFunctionRegistry? registry)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var errors   = new List<ValidationError>();
            var warnings = new List<ValidationError>();
            var infos    = new List<ValidationError>();

            if (config.Columns == null)
            {
                errors.Add(ValidationError.Error(
                    "NULL_COLUMNS_LIST",
                    "ParserConfiguration.Columns must not be null."));
                return ValidationResult.From(errors, warnings, infos);
            }

            if (config.Columns.Count == 0)
            {
                infos.Add(ValidationError.Info(
                    string.Empty,
                    "EMPTY_COLUMNS_LIST",
                    "ParserConfiguration.Columns is empty. Parsing will produce an empty string."));
            }

            var seenIds = new HashSet<Guid>();
            foreach (var col in config.Columns)
            {
                if (col == null) continue;
                if (!seenIds.Add(col.Id))
                    errors.Add(ValidationError.Error(
                        col.DisplayName, "DUPLICATE_ID",
                        $"Duplicate column Id '{col.Id:N}'. Each column must have a unique Id."));
            }

            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var col in config.Columns)
            {
                if (col == null) continue;

                if (!string.IsNullOrWhiteSpace(col.Name)) allColumnNames.Add(col.Name);
                if (!string.IsNullOrWhiteSpace(col.Alias)) allColumnNames.Add(col.Alias!);

                if (!col.IsEnabled) continue;

                if (!string.IsNullOrWhiteSpace(col.Name) && !seenNames.Add(col.Name))
                    errors.Add(ValidationError.Error(
                        col.Name, "DUPLICATE_NAME",
                        $"Duplicate column Name '{col.Name}' among enabled columns."));

                if (!string.IsNullOrWhiteSpace(col.Alias) && !seenNames.Add(col.Alias!))
                    errors.Add(ValidationError.Error(
                        col.DisplayName, "DUPLICATE_ALIAS",
                        $"Duplicate column Alias '{col.Alias}' among enabled columns."));
            }

            if (config.Variables != null)
            {
                var seenVarNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var idRegex = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
                foreach (var kvp in config.Variables)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key))
                    {
                        errors.Add(ValidationError.Error("Variables", "INVALID_VARIABLE_NAME", "Variable name cannot be null or empty."));
                        continue;
                    }
                    if (!idRegex.IsMatch(kvp.Key))
                    {
                        errors.Add(ValidationError.Error("Variables", "INVALID_VARIABLE_NAME", $"Variable '{kvp.Key}' is not a valid identifier."));
                    }
                    if (!seenVarNames.Add(kvp.Key))
                    {
                        errors.Add(ValidationError.Error("Variables", "DUPLICATE_VARIABLE_NAME", $"Duplicate variable name '{kvp.Key}'."));
                    }
                    if (allColumnNames.Contains(kvp.Key))
                    {
                        errors.Add(ValidationError.Error("Variables", "VARIABLE_COLLISION", $"Variable '{kvp.Key}' collides with an existing column Name or Alias."));
                    }
                }
            }

            var nameIndex = BuildNameIndex(config);

            foreach (var col in config.Columns)
            {
                if (col == null || !col.IsEnabled) continue;

                ValidateColumnFields(
                    col, sourceType, resolver, engine, registry,
                    nameIndex, errors, warnings, infos);
            }

            try
            {
                var enabled = new List<ColumnDefinition>();
                foreach (var col in config.Columns)
                    if (col != null && col.IsEnabled) enabled.Add(col);

                new DependencyResolver().Sort(enabled);
            }
            catch (CircularDependencyException ex)
            {
                errors.Add(ValidationError.Error(
                    "CIRCULAR_DEPENDENCY",
                    ex.Message));
            }

            if (engine != null && engine.Capabilities.SupportsCompilation)
            {
                infos.Add(ValidationError.Info(
                    string.Empty,
                    "COMPILATION_ENABLED",
                    "Expression engine supports compilation — expressions will be pre-compiled to delegates."));
            }

            return ValidationResult.From(errors, warnings, infos);
        }

        private static void ValidateColumnFields(
            ColumnDefinition         col,
            Type?                    sourceType,
            IPropertyResolver?       resolver,
            IExpressionEngine?       engine,
            ICustomFunctionRegistry? registry,
            HashSet<string>          nameIndex,
            List<ValidationError>    errors,
            List<ValidationError>    warnings,
            List<ValidationError>    infos)
        {
            switch (col)
            {
                case PropertyColumn pc:
                    ValidatePropertyColumn(pc, sourceType, resolver, errors, warnings);
                    break;

                case FormulaColumn fc:
                    ValidateFormulaColumn(fc, engine, nameIndex, errors, warnings);
                    break;

                case ConditionalColumn cc:
                    ValidateConditionalColumn(cc, engine, nameIndex, errors, warnings);
                    break;

                case CustomFunctionColumn cfc:
                    ValidateCustomFunctionColumn(cfc, registry, nameIndex, errors, warnings);
                    break;

                case FixedTextColumn ft:
                    if (string.IsNullOrEmpty(ft.Text))
                        infos.Add(ValidationError.Info(
                            col.DisplayName, "EMPTY_FIXED_TEXT",
                            "FixedTextColumn.Text is empty."));
                    break;

                default:
                    warnings.Add(ValidationError.Warning(
                        col.DisplayName, "UNKNOWN_COLUMN_TYPE",
                        $"Column type '{col.GetType().Name}' is unrecognised by the validator."));
                    break;
            }
        }

        private static void ValidatePropertyColumn(
            PropertyColumn        col,
            Type?                sourceType,
            IPropertyResolver?   resolver,
            List<ValidationError> errors,
            List<ValidationError> warnings)
        {
            if (string.IsNullOrWhiteSpace(col.PropertyPath))
            {
                errors.Add(ValidationError.Error(
                    col.DisplayName, "MISSING_FIELD",
                    "PropertyColumn.PropertyPath cannot be null or empty."));
                return;
            }

            if (sourceType != null && resolver != null)
            {
                if (!resolver.CanResolve(sourceType, col.PropertyPath))
                {
                    errors.Add(ValidationError.Error(
                        col.DisplayName, "INVALID_PROPERTY_PATH",
                        $"Property path '{col.PropertyPath}' cannot be resolved on " +
                        $"type '{sourceType.Name}'."));
                }
                else
                {
                    var resolvedType = resolver.ResolveType(sourceType, col.PropertyPath);
                    if (resolvedType != null)
                    {
                        col.OutputType = resolvedType;
                        if (resolvedType == typeof(object))
                            warnings.Add(ValidationError.Warning(
                                col.DisplayName, "TYPE_UNRESOLVED",
                                $"Property path '{col.PropertyPath}' resolved to 'object' — " +
                                "type inference could not determine a more specific type."));
                    }
                }
            }
        }

        private static void ValidateFormulaColumn(
            FormulaColumn         col,
            IExpressionEngine?    engine,
            HashSet<string>      nameIndex,
            List<ValidationError> errors,
            List<ValidationError> warnings)
        {
            if (string.IsNullOrWhiteSpace(col.Expression))
            {
                errors.Add(ValidationError.Error(
                    col.DisplayName, "MISSING_FIELD",
                    "FormulaColumn.Expression cannot be null or empty."));
                return;
            }

            if (engine != null && !engine.ValidateExpression(col.Expression))
                errors.Add(ValidationError.Error(
                    col.DisplayName, "INVALID_EXPRESSION",
                    $"Expression '{col.Expression}' failed syntax validation. " +
                    "Expected a valid NCalc expression (e.g., 'UnitPrice * Quantity')."));

            ValidateColumnReferences(col.DisplayName, col.Expression, nameIndex, errors);
        }

        private static void ValidateConditionalColumn(
            ConditionalColumn    col,
            IExpressionEngine?   engine,
            HashSet<string>     nameIndex,
            List<ValidationError> errors,
            List<ValidationError> warnings)
        {
            if (string.IsNullOrWhiteSpace(col.ConditionExpression))
            {
                errors.Add(ValidationError.Error(
                    col.DisplayName, "MISSING_FIELD",
                    "ConditionalColumn.ConditionExpression cannot be null or empty."));
            }
            else
            {
                if (engine != null && !engine.ValidateExpression(col.ConditionExpression))
                    errors.Add(ValidationError.Error(
                        col.DisplayName, "INVALID_EXPRESSION",
                        $"ConditionExpression '{col.ConditionExpression}' is not a valid expression."));

                ValidateColumnReferences(col.DisplayName, col.ConditionExpression, nameIndex, errors);
            }

            ValidateBranchValue(col, col.TrueValue,  "TrueValue",  engine, nameIndex, errors);
            ValidateBranchValue(col, col.FalseValue, "FalseValue", engine, nameIndex, errors);

            bool trueIsExpr  = !string.IsNullOrEmpty(col.TrueValue)  && col.TrueValue.StartsWith("=");
            bool falseIsExpr = !string.IsNullOrEmpty(col.FalseValue) && col.FalseValue.StartsWith("=");
            if (trueIsExpr != falseIsExpr)
                warnings.Add(ValidationError.Warning(
                    col.DisplayName, "BRANCH_TYPE_MISMATCH",
                    "One branch is a dynamic expression and the other is a literal string. " +
                    "OutputType will be 'object' at runtime."));
        }

        private static void ValidateBranchValue(
            ConditionalColumn    col,
            string?              branchValue,
            string               branchName,
            IExpressionEngine?   engine,
            HashSet<string>     nameIndex,
            List<ValidationError> errors)
        {
            if (string.IsNullOrEmpty(branchValue)) return;

            ValidateColumnReferences(col.DisplayName, branchValue!, nameIndex, errors);

            if (branchValue!.StartsWith("=", StringComparison.Ordinal))
            {
                var expr = branchValue.Substring(1).Trim();
                if (!string.IsNullOrEmpty(expr) && engine != null && !engine.ValidateExpression(expr))
                {
                    errors.Add(ValidationError.Error(
                        col.DisplayName, "INVALID_EXPRESSION",
                        $"{branchName} expression '{expr}' is not valid."));
                }
            }
        }

        private static void ValidateCustomFunctionColumn(
            CustomFunctionColumn   col,
            ICustomFunctionRegistry? registry,
            HashSet<string>       nameIndex,
            List<ValidationError> errors,
            List<ValidationError> warnings)
        {
            if (string.IsNullOrWhiteSpace(col.FunctionName))
            {
                errors.Add(ValidationError.Error(
                    col.DisplayName, "MISSING_FIELD",
                    "CustomFunctionColumn.FunctionName cannot be null or empty."));
                return;
            }

            if (registry != null && !registry.IsRegistered(col.FunctionName))
            {
                errors.Add(ValidationError.Error(
                    col.DisplayName, "UNKNOWN_FUNCTION",
                    $"Function '{col.FunctionName}' is not found in the custom function registry."));
                return;
            }

            if (registry != null)
            {
                var meta = registry.GetMetadata(col.FunctionName);
                if (meta != null)
                {
                    if (meta.ParameterTypes.Count > 0 && col.Parameters.Count != meta.ParameterTypes.Count)
                    {
                        errors.Add(ValidationError.Error(
                            col.DisplayName, "PARAM_COUNT_MISMATCH",
                            $"Function '{col.FunctionName}' expects {meta.ParameterTypes.Count} " +
                            $"parameter(s) but {col.Parameters.Count} were provided. " +
                            $"Expected types: [{string.Join(", ", meta.ParameterTypes.Select(TypeGraph.GetTypeName))}]."));
                    }

                    if (meta.ReturnType == typeof(object))
                        warnings.Add(ValidationError.Warning(
                            col.DisplayName, "RETURN_TYPE_OBJECT",
                            $"Function '{col.FunctionName}' returns 'object' — " +
                            "type-specific formatting may not be available."));
                }
            }

            foreach (var param in col.Parameters)
                ValidateColumnReferences(col.DisplayName, param, nameIndex, errors);
        }

        private static void ValidateColumnReferences(
            string                columnName,
            string                expression,
            HashSet<string>       nameIndex,
            List<ValidationError> errors)
        {
            var refs = ColumnReferenceParser.ExtractColumnReferences(expression);
            foreach (var refName in refs)
            {
                if (!nameIndex.Contains(refName))
                    errors.Add(ValidationError.Error(
                        columnName, "UNKNOWN_COLUMN_REF",
                        $"Expression references '${{{refName}}}' but no enabled column or variable " +
                        $"named '{refName}' exists."));
            }
        }

        private static HashSet<string> BuildNameIndex(
            ParserConfiguration config)
        {
            var index = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in config.Columns)
            {
                if (col == null || !col.IsEnabled) continue;
                if (!string.IsNullOrWhiteSpace(col.Name))  index.Add(col.Name);
                if (!string.IsNullOrWhiteSpace(col.Alias)) index.Add(col.Alias!);
            }
            if (config.Variables != null)
            {
                foreach (var paramName in config.Variables.Keys)
                {
                    if (!string.IsNullOrWhiteSpace(paramName)) index.Add(paramName);
                }
            }
            return index;
        }
    }
}
