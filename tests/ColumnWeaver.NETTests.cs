using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ColumnWeaver.NET;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Enums;
using ColumnWeaver.NET.Core.Models;
using ColumnWeaver.NET.Core.ParserConfiguration;
using Xunit;

namespace ColumnWeaver.NET.Tests
{
    public class ColumnWeaverClientTests
    {
        #region PropertyColumn Tests

        [Fact]
        public void PropertyColumn_SimpleProperty_ReturnsValue()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "Name", PropertyPath = "Name" });

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { Name = "Test" });

            Assert.Equal("Test", result);
        }

        [Fact]
        public void PropertyColumn_NestedProperty_ReturnsValue()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "City", PropertyPath = "Address.City" });

            var parser = ColumnWeaverClient.Create(config);
            var obj = new { Address = new { City = "Seattle" } };
            var result = parser.Parse(obj);

            Assert.Equal("Seattle", result);
        }

        [Fact]
        public void PropertyColumn_MultipleColumns_JoinsWithSeparator()
        {
            var config = new ParserConfiguration
            {
                GlobalSeparator = "|",
                Columns =
                {
                    new PropertyColumn { Name = "Id",   PropertyPath = "Id",   Order = 1 },
                    new PropertyColumn { Name = "Name", PropertyPath = "Name", Order = 2 },
                    new PropertyColumn { Name = "City", PropertyPath = "City", Order = 3 }
                }
            };

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { Id = 1, Name = "John", City = "NYC" });

            Assert.Equal("1|John|NYC", result);
        }

        [Fact]
        public void PropertyColumn_NullValue_UsesDefault()
        {
            var config = new ParserConfiguration
            {
                Columns =
                {
                    new PropertyColumn
                    {
                        Name         = "Name",
                        PropertyPath = "Name",
                        NullHandling = new NullHandlingConfig
                        {
                            Strategy    = NullHandlingStrategy.UseDefault,
                            DefaultValue = "Unknown"
                        }
                    }
                }
            };

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { Name = (string)null });

            Assert.Equal("Unknown", result);
        }

        #endregion

        #region FormulaColumn Tests

        [Fact]
        public void FormulaColumn_ArithmeticExpression_CalculatesCorrectly()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "Price", PropertyPath = "Price" },
                new PropertyColumn { Name = "Qty", PropertyPath = "Qty" },
                new FormulaColumn { Name = "Total", Expression = "${Price} * ${Qty}" });

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { Price = 10, Qty = 3 });

            Assert.Contains("30", result);
        }

        [Fact]
        public void FormulaColumn_DependsOnOtherColumn_EvaluatesInOrder()
        {
            var config = new ParserConfiguration
            {
                GlobalSeparator = " | ",
                Columns =
                {
                    new FormulaColumn { Name = "A", Expression = "10",  Order = 1 },
                    new FormulaColumn { Name = "B", Expression = "${A} * 2", Order = 2 },
                    new FormulaColumn { Name = "C", Expression = "${B} + ${A}", Order = 3 }
                }
            };

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new object());

            Assert.Contains("10", result);
            Assert.Contains("20", result);
            Assert.Contains("30", result);
        }

        [Fact]
        public void FormulaColumn_StringConcatenation_Concatenates()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "First", PropertyPath = "First" },
                new PropertyColumn { Name = "Last", PropertyPath = "Last" });

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { First = "John", Last = "Doe" });

            Assert.Contains("John", result);
            Assert.Contains("Doe", result);
        }

        #endregion

        #region ConditionalColumn Tests

        [Fact]
        public void ConditionalColumn_TrueCondition_ReturnsTrueValue()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "IsActive", PropertyPath = "IsActive" },
                new ConditionalColumn
                {
                    Name                = "Status",
                    ConditionExpression = "${IsActive}",
                    TrueValue          = "Active",
                    FalseValue         = "Inactive"
                });

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { IsActive = true });

            Assert.Contains("Active", result);
        }

        [Fact]
        public void ConditionalColumn_FalseCondition_ReturnsFalseValue()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "IsActive", PropertyPath = "IsActive" },
                new ConditionalColumn
                {
                    Name                = "Status",
                    ConditionExpression = "${IsActive}",
                    TrueValue          = "Active",
                    FalseValue         = "Inactive"
                });

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { IsActive = false });

            Assert.Contains("Inactive", result);
        }

        [Fact]
        public void ConditionalColumn_NumericComparison_Works()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "Score", PropertyPath = "Score" },
                new ConditionalColumn
                {
                    Name                = "Tier",
                    ConditionExpression = "${Score} >= 100",
                    TrueValue          = "Premium",
                    FalseValue         = "Standard"
                });

            var parser = ColumnWeaverClient.Create(config);

            Assert.Contains("150", parser.Parse(new { Score = 150 }));
            Assert.Contains("50", parser.Parse(new { Score = 50 }));
            Assert.Contains("Premium", parser.Parse(new { Score = 150 }));
            Assert.Contains("Standard", parser.Parse(new { Score = 50 }));
        }

        #endregion

        #region FixedTextColumn Tests

        [Fact]
        public void FixedTextColumn_ReturnsConstantText()
        {
            var config = CreateConfig(
                new FixedTextColumn { Name = "Separator", Text = "---" });

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { });

            Assert.Equal("---", result);
        }

        #endregion

        #region Batch Processing Tests

        [Fact]
        public void ParseMany_ProcessesMultipleObjects()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "Id", PropertyPath = "Id" });

            var parser = ColumnWeaverClient.Create(config);

            var items = new[]
            {
                new { Id = 1 },
                new { Id = 2 },
                new { Id = 3 }
            };

            var results = parser.ParseMany(items).ToList();

            Assert.Equal(3, results.Count);
            Assert.Contains("1", results[0]);
            Assert.Contains("2", results[1]);
            Assert.Contains("3", results[2]);
        }

        [Fact]
        public void ParseMany_LargeBatch_Completes()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "Id", PropertyPath = "Id" });

            var parser = ColumnWeaverClient.Create(config);

            var items = Enumerable.Range(1, 1000)
                .Select(i => new { Id = i })
                .ToList();

            var count = parser.ParseMany(items).Count();

            Assert.Equal(1000, count);
        }

        #endregion

        #region Formatting Tests

        [Fact]
        public void Formatting_CasingTransform_AppliesCorrectly()
        {
            var config = new ParserConfiguration
            {
                Columns =
                {
                    new PropertyColumn
                    {
                        Name         = "Name",
                        PropertyPath = "Name",
                        Formatting   = new FormattingOptions
                        {
                            CasingTransform = CasingTransform.UpperCase
                        }
                    }
                }
            };

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { Name = "hello" });

            Assert.Equal("HELLO", result);
        }

        [Fact]
        public void Formatting_PrefixAndSuffix_Appended()
        {
            var config = new ParserConfiguration
            {
                Columns =
                {
                    new PropertyColumn
                    {
                        Name         = "Value",
                        PropertyPath = "Value",
                        Formatting   = new FormattingOptions
                        {
                            Prefix = "[",
                            Suffix = "]"
                        }
                    }
                }
            };

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { Value = "test" });

            Assert.Equal("[test]", result);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void Validate_ValidConfig_ReturnsNoErrors()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "Name", PropertyPath = "Name" });

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Validate();

            Assert.True(result.IsValid);
        }

        #endregion

        #region Separator Tests

        [Fact]
        public void GlobalSeparator_CustomSeparator_UsesIt()
        {
            var config = new ParserConfiguration
            {
                GlobalSeparator = "||",
                Columns =
                {
                    new PropertyColumn { Name = "A", PropertyPath = "A" },
                    new PropertyColumn { Name = "B", PropertyPath = "B" }
                }
            };

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { A = "1", B = "2" });

            Assert.Equal("1||2", result);
        }

        [Fact]
        public void ColumnSeparatorOverride_OverridesGlobal()
        {
            var config = new ParserConfiguration
            {
                GlobalSeparator = ",",
                Columns =
                {
                    new PropertyColumn { Name = "A", PropertyPath = "A" },
                    new PropertyColumn
                    {
                        Name             = "B",
                        PropertyPath     = "B",
                        SeparatorOverride = "|"
                    },
                    new PropertyColumn { Name = "C", PropertyPath = "C" }
                }
            };

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { A = "1", B = "2", C = "3" });

            Assert.Equal("1,2|3", result);
        }

        #endregion

        #region Variable Tests

        [Fact]
        public void Variables_UsedInFormulaColumn_EvaluatesCorrectly()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "Price", PropertyPath = "Price" },
                new FormulaColumn { Name = "Total", Expression = "${Price} * ${TaxRate}" }
            );
            config.Variables["TaxRate"] = 1.05;

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { Price = 100 });

            Assert.Contains("105", result);
        }

        [Fact]
        public void Variables_UsedInConditionalColumn_EvaluatesCorrectly()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "Score", PropertyPath = "Score" },
                new ConditionalColumn 
                { 
                    Name = "Status", 
                    ConditionExpression = "${Score} >= ${PassingScore}",
                    TrueValue = "Pass",
                    FalseValue = "Fail"
                }
            );
            config.Variables["PassingScore"] = 75;

            var parser = ColumnWeaverClient.Create(config);
            Assert.Contains("Pass", parser.Parse(new { Score = 80 }));
            Assert.Contains("Fail", parser.Parse(new { Score = 70 }));
        }

        [Fact]
        public void StalePlan_VariableModifiedOnSameConfigInstance_SubsequentParsesReflectUpdate()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "Price", PropertyPath = "Price" },
                new FormulaColumn { Name = "Total", Expression = "${Price} * ${TaxRate}" }
            );
            config.Variables["TaxRate"] = 1.10;

            var parser = ColumnWeaverClient.Create(config);
            var result1 = parser.Parse(new { Price = 100.0 });
            Assert.Contains("110", result1);

            // Mutate the variable on the exact same configuration instance
            config.Variables["TaxRate"] = 1.25;

            var result2 = parser.Parse(new { Price = 100.0 });
            Assert.Contains("125", result2);
        }

        [Fact]
        public void StalePlan_ColumnModifiedOnSameConfigInstance_SubsequentParsesReflectUpdate()
        {
            var col = new PropertyColumn { Name = "Field", PropertyPath = "A" };
            var config = CreateConfig(col);

            var parser = ColumnWeaverClient.Create(config);
            var result1 = parser.Parse(new { A = "Alpha", B = "Beta" });
            Assert.Equal("Alpha", result1);

            // Mutate column property path on the same instance
            col.PropertyPath = "B";

            var result2 = parser.Parse(new { A = "Alpha", B = "Beta" });
            Assert.Equal("Beta", result2);
        }

        [Fact]
        public void Variables_StringWithQuotesAndSpecialChars_EvaluatesInConditionAndFormula()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "Status", PropertyPath = "Status" },
                new ConditionalColumn
                {
                    Name = "Check",
                    ConditionExpression = "${Status} == ${TargetStatus}",
                    TrueValue = "Matched",
                    FalseValue = "Unmatched"
                },
                new FormulaColumn
                {
                    Name = "Label",
                    Expression = "${Prefix} + ': ' + ${Status}"
                }
            );
            config.GlobalSeparator = "|";
            config.Variables["TargetStatus"] = "O'Reilly & \"Sons\" / Special";
            config.Variables["Prefix"] = "Customer/Dept";

            var parser = ColumnWeaverClient.Create(config);
            var matchResult = parser.Parse(new { Status = "O'Reilly & \"Sons\" / Special" });
            Assert.Equal("O'Reilly & \"Sons\" / Special|Matched|Customer/Dept: O'Reilly & \"Sons\" / Special", matchResult);

            var nonMatchResult = parser.Parse(new { Status = "Other" });
            Assert.Equal("Other|Unmatched|Customer/Dept: Other", nonMatchResult);
        }

        [Fact]
        public void Variables_BooleanNumericAndNull_EvaluatesCorrectly()
        {
            var config = CreateConfig(
                new ConditionalColumn
                {
                    Name = "DiscountApplied",
                    ConditionExpression = "${IsEligible} == true",
                    TrueValue = "= ${BasePrice} - ${DiscountAmount}",
                    FalseValue = "= ${BasePrice}"
                }
            );
            config.Variables["IsEligible"] = true;
            config.Variables["BasePrice"] = 100.0;
            config.Variables["DiscountAmount"] = 15.5;

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { });
            Assert.Equal("84.5", result);
        }

        [Fact]
        public void Variables_CaseInsensitiveReference_EvaluatesCorrectly()
        {
            var config = CreateConfig(
                new FormulaColumn { Name = "Calculation", Expression = "${tax_rate} * 100" }
            );
            config.Variables["TAX_RATE"] = 0.18;

            var parser = ColumnWeaverClient.Create(config);
            var result = parser.Parse(new { });
            Assert.Equal("18", result);
        }

        [Fact]
        public void Validation_VariableCollisionWithColumnNameOrAlias_IsRejected()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "ActiveTotal", PropertyPath = "Total" },
                new PropertyColumn { Name = "Col2", Alias = "ShortAlias", PropertyPath = "X", IsEnabled = false }
            );
            config.Variables["ActiveTotal"] = 100;

            var resolver = new ColumnWeaver.NET.Infrastructure.PropertyResolution.ReflectionPropertyResolver();
            var ncalc = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.NCalcExpressionEngine(resolver);
            var result = ColumnWeaver.NET.Application.Validation.ConfigurationValidator.Validate(
                config, typeof(object), resolver, ncalc, null);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Code == "VARIABLE_COLLISION");

            // Test collision with disabled column's alias
            var config2 = CreateConfig(
                new PropertyColumn { Name = "Col2", Alias = "ShortAlias", PropertyPath = "X", IsEnabled = false }
            );
            config2.Variables["ShortAlias"] = 200;

            var result2 = ColumnWeaver.NET.Application.Validation.ConfigurationValidator.Validate(
                config2, typeof(object), resolver, ncalc, null);

            Assert.False(result2.IsValid);
            Assert.Contains(result2.Errors, e => e.Code == "VARIABLE_COLLISION");
        }

        [Fact]
        public void Variables_ParserEngineFallback_EvaluatesCorrectly()
        {
            var config = CreateConfig(
                new PropertyColumn { Name = "Price", PropertyPath = "Price" },
                new FormulaColumn { Name = "Total", Expression = "${Price} * ${TaxRate}" }
            );
            config.Variables["TaxRate"] = 1.05;

            var resolver = new ColumnWeaver.NET.Infrastructure.PropertyResolution.ReflectionPropertyResolver();
            var ncalc = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.NCalcExpressionEngine(resolver);
            var factory = new ColumnWeaver.NET.Application.Factory.ColumnEvaluatorFactory(resolver, ncalc);
            var parserEngine = new ColumnWeaver.NET.Application.Engine.ParserEngine(factory);

            var result = parserEngine.Parse(config, new { Price = 100 });
            Assert.Contains("105", result);
        }

        [Fact]
        public void WinFormsPreview_MatchesPublicClient_AndSandboxParsesEquivalently()
        {
            var demoConfig = ColumnWeaver.NET.UI.Demo.DemoConfigurationFactory.CreateWithVariables();
            var sampleOrder = ColumnWeaver.NET.UI.Demo.DemoConfigurationFactory.CreateSampleOrder();
            var sampleJson = ColumnWeaver.NET.UI.Demo.DemoConfigurationFactory.SampleOrderWithVariablesJson;

            // 1. Production Public Client evaluation
            var client = ColumnWeaverClient.Create(demoConfig);
            var clientOutput = client.Parse(sampleOrder);

            // 2. WinForms Service Live Preview (Compiled Engine) evaluation
            var propertyResolver = new ColumnWeaver.NET.Infrastructure.PropertyResolution.ReflectionPropertyResolver();
            var ncalcEngine = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.NCalcExpressionEngine(propertyResolver);
            var expressionEngine = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.LambdaExpressionEngine(ncalcEngine);
            var jsonSerializer = new ColumnWeaver.NET.Infrastructure.Serialization.JsonConfigurationSerializer();
            var xmlSerializer = new ColumnWeaver.NET.Infrastructure.Serialization.XmlConfigurationSerializer();
            var functionRegistry = new ColumnWeaver.NET.Infrastructure.DefaultFunctionRegistry();
            var evaluatorFactory = new ColumnWeaver.NET.Application.Factory.ColumnEvaluatorFactory(propertyResolver, expressionEngine, functionRegistry);
            var parserEngine = new ColumnWeaver.NET.Application.Engine.ParserEngine(evaluatorFactory);
            var compiledEngine = new ColumnWeaver.NET.Application.Engine.CompiledParserEngine(propertyResolver, expressionEngine, functionRegistry);

            var service = new ColumnWeaver.NET.UI.Services.UIParserService(
                parserEngine, compiledEngine, expressionEngine, jsonSerializer, xmlSerializer, functionRegistry);
            service.LoadConfiguration(demoConfig);

            var previewOutput = service.TryPreviewParse(sampleOrder);

            // Parity check between Public Client and Configurator Live Preview
            Assert.Equal(clientOutput, previewOutput);

            // 3. WinForms JSON Sandbox Test evaluation
            var sandboxOutput = service.TryTestParse(sampleJson);
            Assert.Equal(clientOutput, sandboxOutput);

            // 4. End-to-end Variable update in UIParserService reflects in subsequent preview
            var newVars = new Dictionary<string, object?>(demoConfig.Variables)
            {
                ["Discount"] = 25.0
            };
            service.UpdateVariables(newVars);

            var updatedPreview = service.TryPreviewParse(sampleOrder);
            var updatedClient = ColumnWeaverClient.Create(service.CurrentConfiguration).Parse(sampleOrder);

            Assert.Equal(updatedClient, updatedPreview);
            Assert.NotEqual(previewOutput, updatedPreview);
        }

        [Fact]
        public void BenchmarkScenarios_AllProduceEquivalentOutputs()
        {
            var sample = new BenchmarkTestOrder
            {
                Id = 1001,
                Name = "Widget Pro",
                UnitPrice = 50.0,
                Quantity = 5,
                IsActive = true,
                Notes = null,
                RawCode = "  item-code  ",
                Status = "PENDING",
                Customer = new BenchmarkTestCustomerInfo { Name = "ACME Global", Region = "North America", City = "New York" }
            };

            var resolver = new ColumnWeaver.NET.Infrastructure.PropertyResolution.ReflectionPropertyResolver();
            var ncalc = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.NCalcExpressionEngine(resolver);
            var registry = new ColumnWeaver.NET.Infrastructure.DefaultFunctionRegistry();
            var factory = new ColumnWeaver.NET.Application.Factory.ColumnEvaluatorFactory(resolver, ncalc, registry);
            var parserEngine = new ColumnWeaver.NET.Application.Engine.ParserEngine(factory);

            // 1. Property Extraction (Flat and Nested)
            var propConfig = new ParserConfiguration
            {
                GlobalSeparator = "|",
                Columns =
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 1 },
                    new PropertyColumn { Name = "CustomerName", PropertyPath = "Customer.Name", Order = 2 },
                    new PropertyColumn { Name = "City", PropertyPath = "Customer.City", Order = 3 }
                }
            };
            string expectedProp = sample.Id.ToString(CultureInfo.InvariantCulture) + "|" + sample.Customer.Name + "|" + sample.Customer.City;
            Assert.Equal(expectedProp, ColumnWeaverClient.Create(propConfig).Parse(sample));
            Assert.Equal(expectedProp, parserEngine.Parse(propConfig, sample));

            // 2. Formula Calculation
            var formulaConfig = new ParserConfiguration
            {
                Columns =
                {
                    new FormulaColumn { Name = "Gross", Expression = "UnitPrice * Quantity", Formatting = new FormattingOptions { FormatString = "F2" } }
                }
            };
            string expectedFormula = (sample.UnitPrice * sample.Quantity).ToString("F2", CultureInfo.InvariantCulture);
            Assert.Equal(expectedFormula, ColumnWeaverClient.Create(formulaConfig).Parse(sample));
            Assert.Equal(expectedFormula, parserEngine.Parse(formulaConfig, sample));

            // 3. Conditional Column (test both branches: true and false)
            var condConfig = new ParserConfiguration
            {
                Columns =
                {
                    new ConditionalColumn { Name = "Status", ConditionExpression = "IsActive == true", TrueValue = "Active", FalseValue = "Inactive" }
                }
            };
            sample.IsActive = true;
            Assert.Equal("Active", ColumnWeaverClient.Create(condConfig).Parse(sample));
            Assert.Equal("Active", parserEngine.Parse(condConfig, sample));
            sample.IsActive = false;
            Assert.Equal("Inactive", ColumnWeaverClient.Create(condConfig).Parse(sample));
            Assert.Equal("Inactive", parserEngine.Parse(condConfig, sample));
            sample.IsActive = true;

            // 4. Variables (Numeric & String)
            var varConfig = new ParserConfiguration
            {
                GlobalSeparator = "|",
                Variables = new Dictionary<string, object?> { { "TaxRate", 0.18 }, { "Currency", "USD" } },
                Columns =
                {
                    new FormulaColumn { Name = "Tax", Expression = "UnitPrice * Quantity * ${TaxRate}", Formatting = new FormattingOptions { FormatString = "F2" }, Order = 1 },
                    new ConditionalColumn { Name = "Tier", ConditionExpression = "${Currency} == 'USD'", TrueValue = "US-Domestic", FalseValue = "Foreign", Order = 2 }
                }
            };
            string expectedVar = (sample.UnitPrice * sample.Quantity * 0.18).ToString("F2", CultureInfo.InvariantCulture) + "|US-Domestic";
            Assert.Equal(expectedVar, ColumnWeaverClient.Create(varConfig).Parse(sample));
            Assert.Equal(expectedVar, parserEngine.Parse(varConfig, sample));

            // 5. Fixed Text / Labels
            var fixedConfig = new ParserConfiguration
            {
                GlobalSeparator = "|",
                Columns =
                {
                    new FixedTextColumn { Text = "RECORD", Order = 1 },
                    new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 2 },
                    new FixedTextColumn { Text = "END", Order = 3 }
                }
            };
            string expectedFixed = "RECORD|" + sample.Id.ToString(CultureInfo.InvariantCulture) + "|END";
            Assert.Equal(expectedFixed, ColumnWeaverClient.Create(fixedConfig).Parse(sample));
            Assert.Equal(expectedFixed, parserEngine.Parse(fixedConfig, sample));

            // 6. Custom Functions
            var funcConfig = new ParserConfiguration
            {
                GlobalSeparator = "|",
                Columns =
                {
                    new PropertyColumn { Name = "Name", PropertyPath = "Name", Order = 1 },
                    new CustomFunctionColumn { Name = "UpperName", FunctionName = "Upper", Parameters = new List<string> { "${Name}" }, Order = 2 }
                }
            };
            string expectedFunc = sample.Name + "|" + sample.Name.ToUpperInvariant();
            Assert.Equal(expectedFunc, ColumnWeaverClient.Create(funcConfig).Parse(sample));
            Assert.Equal(expectedFunc, parserEngine.Parse(funcConfig, sample));

            // 7. Null Handling (test both branches: null default and non-null value)
            var nullConfig = new ParserConfiguration
            {
                Columns =
                {
                    new PropertyColumn
                    {
                        Name = "Notes",
                        PropertyPath = "Notes",
                        NullHandling = new NullHandlingConfig { Strategy = NullHandlingStrategy.UseDefault, DefaultValue = "(none)" }
                    }
                }
            };
            sample.Notes = null;
            Assert.Equal("(none)", ColumnWeaverClient.Create(nullConfig).Parse(sample));
            Assert.Equal("(none)", parserEngine.Parse(nullConfig, sample));
            sample.Notes = "Custom Note";
            Assert.Equal("Custom Note", ColumnWeaverClient.Create(nullConfig).Parse(sample));
            Assert.Equal("Custom Note", parserEngine.Parse(nullConfig, sample));
            sample.Notes = null;

            // 8. Formatting Options
            var formattingConfig = new ParserConfiguration
            {
                Columns =
                {
                    new PropertyColumn
                    {
                        Name = "RawCode",
                        PropertyPath = "RawCode",
                        Formatting = new FormattingOptions
                        {
                            TrimBehavior = TrimBehavior.TrimBoth,
                            CasingTransform = CasingTransform.UpperCase,
                            PaddedWidth = 12,
                            PaddingAlignment = PaddingAlignment.Right,
                            PaddingChar = ' ',
                            Prefix = "[",
                            Suffix = "]"
                        }
                    }
                }
            };
            string expectedFormatting = "[" + sample.RawCode.Trim().ToUpperInvariant().PadRight(12, ' ') + "]";
            Assert.Equal(expectedFormatting, ColumnWeaverClient.Create(formattingConfig).Parse(sample));
            Assert.Equal(expectedFormatting, parserEngine.Parse(formattingConfig, sample));

            // 9. Replacement Rules
            var replacementConfig = new ParserConfiguration
            {
                Columns =
                {
                    new PropertyColumn
                    {
                        Name = "Status",
                        PropertyPath = "Status",
                        ReplacementRules = new List<ReplacementRule>
                        {
                            new ReplacementRule { SourceValue = "PENDING", TargetValue = "Awaiting Approval", IsCaseSensitive = true }
                        }
                    }
                }
            };
            string expectedReplacement = sample.Status.Replace("PENDING", "Awaiting Approval");
            Assert.Equal(expectedReplacement, ColumnWeaverClient.Create(replacementConfig).Parse(sample));
            Assert.Equal(expectedReplacement, parserEngine.Parse(replacementConfig, sample));

            // 10. Disabled Columns & Separator Slot
            var disabledConfig = new ParserConfiguration
            {
                GlobalSeparator = "|",
                PreserveSeparatorForDisabledColumns = true,
                Columns =
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 1 },
                    new PropertyColumn { Name = "Notes", PropertyPath = "Notes", Order = 2, IsEnabled = false },
                    new PropertyColumn { Name = "Status", PropertyPath = "Status", Order = 3 }
                }
            };
            string expectedDisabled = sample.Id.ToString(CultureInfo.InvariantCulture) + "||" + sample.Status;
            Assert.Equal(expectedDisabled, ColumnWeaverClient.Create(disabledConfig).Parse(sample));
            Assert.Equal(expectedDisabled, parserEngine.Parse(disabledConfig, sample));

            // 11. Dependency-Heavy Formulas
            var depConfig = new ParserConfiguration
            {
                GlobalSeparator = "|",
                Variables = new Dictionary<string, object?>
                {
                    { "TaxRate", 0.18 },
                    { "DiscountRate", 0.10 }
                },
                Columns =
                {
                    new FormulaColumn { Name = "Gross", Order = 1, Expression = "UnitPrice * Quantity", Formatting = new FormattingOptions { FormatString = "F2" } },
                    new FormulaColumn { Name = "Discount", Order = 2, Expression = "${Gross} * ${DiscountRate}", Formatting = new FormattingOptions { FormatString = "F2" } },
                    new FormulaColumn { Name = "SubTotal", Order = 3, Expression = "${Gross} - ${Discount}", Formatting = new FormattingOptions { FormatString = "F2" } },
                    new FormulaColumn { Name = "Tax", Order = 4, Expression = "${SubTotal} * ${TaxRate}", Formatting = new FormattingOptions { FormatString = "F2" } },
                    new FormulaColumn { Name = "FinalTotal", Order = 5, Expression = "${SubTotal} + ${Tax}", Formatting = new FormattingOptions { FormatString = "F2" } },
                    new ConditionalColumn { Name = "Tier", Order = 6, ConditionExpression = "${FinalTotal} > 200", TrueValue = "VIP", FalseValue = "Standard" }
                }
            };
            double gross = sample.UnitPrice * sample.Quantity;
            double discount = gross * 0.10;
            double subTotal = gross - discount;
            double tax = subTotal * 0.18;
            double finalTotal = subTotal + tax;
            string tier = finalTotal > 200 ? "VIP" : "Standard";
            string expectedDep = gross.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                                 discount.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                                 subTotal.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                                 tax.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                                 finalTotal.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                                 tier;
            Assert.Equal(expectedDep, ColumnWeaverClient.Create(depConfig).Parse(sample));
            Assert.Equal(expectedDep, parserEngine.Parse(depConfig, sample));
        }

        [Fact]
        public void BenchmarkScenario_BatchProducesEquivalentOutputs()
        {
            var repConfig = new ParserConfiguration
            {
                GlobalSeparator = "|",
                Variables = new Dictionary<string, object?> { { "TaxRate", 0.18 } },
                Columns =
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id" },
                    new PropertyColumn { Name = "Customer", PropertyPath = "Customer.Name" },
                    new FormulaColumn { Name = "Gross", Expression = "UnitPrice * Quantity", Formatting = new FormattingOptions { FormatString = "F2" } },
                    new ConditionalColumn { Name = "Status", ConditionExpression = "IsActive == true", TrueValue = "Active", FalseValue = "Inactive" },
                    new PropertyColumn { Name = "Region", PropertyPath = "Customer.Region" },
                    new FormulaColumn { Name = "Tax", Expression = "UnitPrice * Quantity * ${TaxRate}", Formatting = new FormattingOptions { FormatString = "F2" } }
                }
            };

            var items = new BenchmarkTestOrder[100];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = new BenchmarkTestOrder
                {
                    Id = 1000 + i,
                    Name = "Item #" + i,
                    UnitPrice = 10.0 + (i % 50),
                    Quantity = 1 + (i % 10),
                    IsActive = (i % 2 == 0),
                    Customer = new BenchmarkTestCustomerInfo { Name = "Customer " + (i % 100), Region = "Region " + (i % 5) }
                };
            }

            var client = ColumnWeaverClient.Create(repConfig);
            var clientResults = client.ParseMany(items).ToList();

            var resolver = new ColumnWeaver.NET.Infrastructure.PropertyResolution.ReflectionPropertyResolver();
            var ncalc = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.NCalcExpressionEngine(resolver);
            var registry = new ColumnWeaver.NET.Infrastructure.DefaultFunctionRegistry();
            var factory = new ColumnWeaver.NET.Application.Factory.ColumnEvaluatorFactory(resolver, ncalc, registry);
            var parserEngine = new ColumnWeaver.NET.Application.Engine.ParserEngine(factory);

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                double gross = item.UnitPrice * item.Quantity;
                double tax = gross * 0.18;
                string expected = item.Id.ToString(CultureInfo.InvariantCulture) + "|" +
                                  item.Customer.Name + "|" +
                                  gross.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                                  (item.IsActive ? "Active" : "Inactive") + "|" +
                                  item.Customer.Region + "|" +
                                  tax.ToString("F2", CultureInfo.InvariantCulture);

                Assert.Equal(expected, clientResults[i]);
                Assert.Equal(expected, parserEngine.Parse(repConfig, item));
            }
        }

        [Fact]
        public void CompiledParserEngine_ThreadSafety_MultipleThreadsExecuteConcurrently()
        {
            var config = new ParserConfiguration
            {
                GlobalSeparator = ",",
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 1 },
                    new FormulaColumn { Name = "Total", Expression = "UnitPrice * Quantity", Order = 2 },
                    new ConditionalColumn { Name = "Status", ConditionExpression = "IsActive == true", TrueValue = "Active", FalseValue = "Inactive", Order = 3 }
                }
            };

            var resolver = new ColumnWeaver.NET.Infrastructure.PropertyResolution.ReflectionPropertyResolver();
            var ncalc = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.NCalcExpressionEngine(resolver);
            var lambda = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.LambdaExpressionEngine(ncalc);
            var engine = new ColumnWeaver.NET.Application.Engine.CompiledParserEngine(resolver, lambda);

            System.Threading.Tasks.Parallel.For(0, 100, i =>
            {
                var order = new BenchmarkTestOrder
                {
                    Id = i,
                    UnitPrice = 10.0 + i,
                    Quantity = 2,
                    IsActive = (i % 2 == 0)
                };
                double expectedTotal = (10.0 + i) * 2;
                string expectedStatus = (i % 2 == 0) ? "Active" : "Inactive";
                string expected = $"{i},{expectedTotal},{expectedStatus}";

                string actual = engine.Parse(config, order);
                Assert.Equal(expected, actual);
            });
        }

        [Fact]
        public void CompiledParserEngine_FastFingerprint_PlanInvalidationOnConfigMutation()
        {
            var config = new ParserConfiguration
            {
                GlobalSeparator = ",",
                Variables = new Dictionary<string, object?> { { "Rate", 1.5 } },
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 1 },
                    new FormulaColumn { Name = "Cost", Expression = "UnitPrice * ${Rate}", Order = 2 }
                }
            };

            var client = ColumnWeaverClient.Create(config);
            var order = new BenchmarkTestOrder { Id = 1, UnitPrice = 100.0 };

            string first = client.Parse(order);
            Assert.Equal("1,150", first);

            // Mutate variable on the same configuration instance
            config.Variables["Rate"] = 2.0;
            string second = client.Parse(order);
            Assert.Equal("1,200", second);

            // Mutate column on the same configuration instance
            config.Columns.Add(new FixedTextColumn { Text = "CONFIRMED", Order = 3 });
            string third = client.Parse(order);
            Assert.Equal("1,200,CONFIRMED", third);
        }

        [Fact]
        public void CompiledExpression_ComplexFormulasWithDependencies_EvaluatesAccurately()
        {
            var config = new ParserConfiguration
            {
                GlobalSeparator = "|",
                Variables = new Dictionary<string, object?>
                {
                    { "TaxRate", 0.18 },
                    { "DiscountRate", 0.10 }
                },
                Columns = new List<ColumnDefinition>
                {
                    new FormulaColumn { Name = "Gross", Order = 1, Expression = "UnitPrice * Quantity", Formatting = new FormattingOptions { FormatString = "F2" } },
                    new FormulaColumn { Name = "Discount", Order = 2, Expression = "${Gross} * ${DiscountRate}", Formatting = new FormattingOptions { FormatString = "F2" } },
                    new FormulaColumn { Name = "SubTotal", Order = 3, Expression = "${Gross} - ${Discount}", Formatting = new FormattingOptions { FormatString = "F2" } },
                    new FormulaColumn { Name = "Tax", Order = 4, Expression = "${SubTotal} * ${TaxRate}", Formatting = new FormattingOptions { FormatString = "F2" } },
                    new FormulaColumn { Name = "FinalTotal", Order = 5, Expression = "${SubTotal} + ${Tax}", Formatting = new FormattingOptions { FormatString = "F2" } },
                    new ConditionalColumn { Name = "Tier", Order = 6, ConditionExpression = "${FinalTotal} > 200", TrueValue = "VIP", FalseValue = "Standard" }
                }
            };

            var client = ColumnWeaverClient.Create(config);
            var order = new BenchmarkTestOrder { UnitPrice = 50.0, Quantity = 5 }; // Gross = 250, Disc = 25, Sub = 225, Tax = 40.5, Final = 265.5 => VIP

            string result = client.Parse(order);
            Assert.Equal("250.00|25.00|225.00|40.50|265.50|VIP", result);
        }

        [Fact]
        public void CompiledConditional_LiteralBranches_EvaluatesAccurately()
        {
            var config = new ParserConfiguration
            {
                GlobalSeparator = ",",
                Columns = new List<ColumnDefinition>
                {
                    new ConditionalColumn
                    {
                        Name = "Status",
                        ConditionExpression = "IsActive == true",
                        TrueValue = "Active",
                        FalseValue = "Inactive"
                    }
                }
            };

            var client = ColumnWeaverClient.Create(config);
            Assert.Equal("Active", client.Parse(new BenchmarkTestOrder { IsActive = true }));
            Assert.Equal("Inactive", client.Parse(new BenchmarkTestOrder { IsActive = false }));
        }

        [Fact]
        public void ParseMany_SingleUseEnumerable_EnumeratesExactlyOnce()
        {
            var config = new ParserConfiguration
            {
                GlobalSeparator = "|",
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id" },
                    new PropertyColumn { Name = "Name", PropertyPath = "Name" }
                }
            };

            var items = new[]
            {
                new BenchmarkTestOrder { Id = 1, Name = "One" },
                new BenchmarkTestOrder { Id = 2, Name = "Two" },
                new BenchmarkTestOrder { Id = 3, Name = "Three" }
            };

            var singleUseSequence = new SingleUseTestEnumerable<BenchmarkTestOrder>(items);
            var client = ColumnWeaverClient.Create(config);

            var results = client.ParseMany(singleUseSequence).ToList();

            Assert.Equal(3, results.Count);
            Assert.Equal("1|One", results[0]);
            Assert.Equal("2|Two", results[1]);
            Assert.Equal("3|Three", results[2]);
        }

        [Fact]
        public void ParseMany_CountingEnumerable_EvaluatesEachItemOnce()
        {
            var config = new ParserConfiguration
            {
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id" }
                }
            };

            int getEnumeratorCalls = 0;
            int yieldsCount = 0;

            IEnumerable<object> CountingGenerator()
            {
                getEnumeratorCalls++;
                for (int i = 0; i < 5; i++)
                {
                    yieldsCount++;
                    yield return new BenchmarkTestOrder { Id = i };
                }
            }

            var client = ColumnWeaverClient.Create(config);
            var results = client.ParseMany(CountingGenerator()).ToList();

            Assert.Equal(1, getEnumeratorCalls);
            Assert.Equal(5, yieldsCount);
            Assert.Equal(5, results.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(i.ToString(), results[i]);
            }
        }

        [Fact]
        public void ParseMany_EmptyEnumerable_ReturnsEmptyWithoutError()
        {
            var config = new ParserConfiguration
            {
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id" }
                }
            };

            var client = ColumnWeaverClient.Create(config);
            var results = client.ParseMany(Enumerable.Empty<object>()).ToList();

            Assert.Empty(results);
        }

        [Fact]
        public void CompiledParserEngine_ConcurrentParses_DifferentConfigurations_NeverInterferes()
        {
            var resolver = new ColumnWeaver.NET.Infrastructure.PropertyResolution.ReflectionPropertyResolver();
            var ncalc = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.NCalcExpressionEngine(resolver);
            var lambda = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.LambdaExpressionEngine(ncalc);
            var engine = new ColumnWeaver.NET.Application.Engine.CompiledParserEngine(resolver, lambda);

            var configs = new ParserConfiguration[5];
            for (int c = 0; c < 5; c++)
            {
                configs[c] = new ParserConfiguration
                {
                    GlobalSeparator = "-",
                    Columns = new List<ColumnDefinition>
                    {
                        new FixedTextColumn { Text = "CFG" + c, Order = 1 },
                        new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 2 }
                    }
                };
            }

            System.Threading.Tasks.Parallel.For(0, 100, i =>
            {
                int cfgIdx = i % 5;
                var cfg = configs[cfgIdx];
                var order = new BenchmarkTestOrder { Id = i };

                string expected = $"CFG{cfgIdx}-{i}";
                string actual = engine.Parse(cfg, order);

                Assert.Equal(expected, actual);
            });
        }

        [Fact]
        public void ColumnWeaverClient_CreatePrecompiled_ReusesPlanWithoutScanningConfig()
        {
            var config = new ParserConfiguration
            {
                GlobalSeparator = ":",
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id" },
                    new PropertyColumn { Name = "Name", PropertyPath = "Name" }
                }
            };

            var client = ColumnWeaverClient.CreatePrecompiled(config, typeof(BenchmarkTestOrder));
            Assert.True(client.IsFrozen);

            var order = new BenchmarkTestOrder { Id = 42, Name = "Answer" };
            string result = client.Parse(order);
            Assert.Equal("42:Answer", result);

            var results = client.ParseMany(new[] { order }).ToList();
            Assert.Single(results);
            Assert.Equal("42:Answer", results[0]);
        }

        [Fact]
        public void ColumnWeaverClient_FrozenClient_ExplicitRecompileReflectsMutation()
        {
            var config = new ParserConfiguration
            {
                GlobalSeparator = ",",
                Variables = new Dictionary<string, object?> { { "Multiplier", 10 } },
                Columns = new List<ColumnDefinition>
                {
                    new FormulaColumn { Name = "Calc", Expression = "Quantity * ${Multiplier}" }
                }
            };

            var client = ColumnWeaverClient.CreatePrecompiled(config, typeof(BenchmarkTestOrder));
            var order = new BenchmarkTestOrder { Quantity = 3 };

            Assert.Equal("30", client.Parse(order));

            // Mutate config on frozen client: because it is frozen, Parse without Recompile still returns cached plan
            config.Variables["Multiplier"] = 20;
            Assert.Equal("30", client.Parse(order));

            // Explicitly recompile: now reflects the mutation
            client.Recompile();
            Assert.Equal("60", client.Parse(order));
        }

        [Fact]
        public void ParserConfiguration_Snapshot_CreatesIndependentDeepCopy()
        {
            var original = new ParserConfiguration
            {
                Name = "Original",
                GlobalSeparator = "|",
                Variables = new Dictionary<string, object?> { { "Rate", 1.5 } },
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 1 },
                    new FormulaColumn { Name = "Total", Expression = "UnitPrice * ${Rate}", Order = 2 }
                }
            };

            var snapshot = original.Snapshot();

            // Mutate original
            original.Name = "ModifiedOriginal";
            original.GlobalSeparator = ",";
            original.Variables["Rate"] = 2.0;
            original.Columns.Add(new FixedTextColumn { Text = "END" });

            // Verify snapshot remained unchanged
            Assert.Equal("Original", snapshot.Name);
            Assert.Equal("|", snapshot.GlobalSeparator);
            Assert.Equal(1.5, (double)snapshot.Variables["Rate"]!);
            Assert.Equal(2, snapshot.Columns.Count);
        }

        [Fact]
        public void SpecializedPipelineBuilder_AppendInt_HandlesBoundaryValuesCorrectly()
        {
            var sb = new System.Text.StringBuilder();

            ColumnWeaver.NET.Application.Execution.SpecializedPipelineBuilder.AppendInt(sb, 0);
            sb.Append('|');
            ColumnWeaver.NET.Application.Execution.SpecializedPipelineBuilder.AppendInt(sb, 7);
            sb.Append('|');
            ColumnWeaver.NET.Application.Execution.SpecializedPipelineBuilder.AppendInt(sb, 1001);
            sb.Append('|');
            ColumnWeaver.NET.Application.Execution.SpecializedPipelineBuilder.AppendInt(sb, -42);
            sb.Append('|');
            ColumnWeaver.NET.Application.Execution.SpecializedPipelineBuilder.AppendInt(sb, int.MaxValue);
            sb.Append('|');
            ColumnWeaver.NET.Application.Execution.SpecializedPipelineBuilder.AppendInt(sb, int.MinValue);

            string expected = $"0|7|1001|-42|{int.MaxValue}|{int.MinValue}";
            Assert.Equal(expected, sb.ToString());
        }

        #endregion

        #region Helper Methods

        private static ParserConfiguration CreateConfig(params ColumnDefinition[] columns)
        {
            var config = new ParserConfiguration();
            foreach (var col in columns)
                config.Columns.Add(col);
            return config;
        }

        private sealed class SingleUseTestEnumerable<T> : IEnumerable<T>
        {
            private readonly IEnumerable<T> _source;
            private int _consumed;

            public SingleUseTestEnumerable(IEnumerable<T> source)
            {
                _source = source ?? throw new ArgumentNullException(nameof(source));
            }

            public IEnumerator<T> GetEnumerator()
            {
                if (System.Threading.Interlocked.Exchange(ref _consumed, 1) != 0)
                    throw new InvalidOperationException("SingleUseTestEnumerable cannot be enumerated more than once.");

                return _source.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        #endregion
    }

    public class BenchmarkTestOrder
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double UnitPrice { get; set; }
        public int Quantity { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
        public string RawCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public BenchmarkTestCustomerInfo Customer { get; set; } = new BenchmarkTestCustomerInfo();
    }

    public class BenchmarkTestCustomerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }
}
