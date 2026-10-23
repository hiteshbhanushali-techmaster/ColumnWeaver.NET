using System;
using System.Collections.Generic;
using System.Linq;
using ColumnWeaver.NET.Application.Engine;
using ColumnWeaver.NET.Application.Exceptions;
using ColumnWeaver.NET.Application.Execution;
using ColumnWeaver.NET.Application.PostProcessing;
using ColumnWeaver.NET.Application.Validation;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Models;
using ColumnWeaver.NET.Core.ParserConfiguration;
using ColumnWeaver.NET.Infrastructure.ExpressionEngine;
using ColumnWeaver.NET.Infrastructure.PropertyResolution;
using Xunit;

namespace ColumnWeaver.NET.Tests
{
    public class WarningRemediationRegressionTests
    {
        private readonly ReflectionPropertyResolver _resolver = new ReflectionPropertyResolver();
        private readonly NCalcExpressionEngine _ncalc;
        private readonly LambdaExpressionEngine _lambda;
        private readonly CompiledParserEngine _engine;

        public WarningRemediationRegressionTests()
        {
            _ncalc = new NCalcExpressionEngine(_resolver);
            _lambda = new LambdaExpressionEngine(_ncalc);
            _engine = new CompiledParserEngine(_resolver, _lambda);
        }

        #region 1. ExecutionPlanCache Signed Order and Enabled Independence

        [Theory]
        [InlineData(-100)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        public void Fingerprint_Differs_When_IsEnabled_Differs_Across_Orders(int order)
        {
            var configEnabled = new ParserConfiguration
            {
                Name = "ConfigTest",
                GlobalSeparator = ",",
                Columns =
                {
                    new PropertyColumn
                    {
                        Id = Guid.NewGuid(),
                        Name = "Col1",
                        PropertyPath = "Id",
                        Order = order,
                        IsEnabled = true
                    }
                }
            };

            var configDisabled = new ParserConfiguration
            {
                Name = "ConfigTest",
                GlobalSeparator = ",",
                Columns =
                {
                    new PropertyColumn
                    {
                        Id = configEnabled.Columns[0].Id,
                        Name = "Col1",
                        PropertyPath = "Id",
                        Order = order,
                        IsEnabled = false
                    }
                }
            };

            ulong fpEnabled = ExecutionPlanCache.ComputeFastFingerprint(configEnabled, typeof(BenchmarkTestOrder));
            ulong fpDisabled = ExecutionPlanCache.ComputeFastFingerprint(configDisabled, typeof(BenchmarkTestOrder));

            Assert.NotEqual(fpEnabled, fpDisabled);

            string hashEnabled = ExecutionPlanCache.ComputeHash(configEnabled, typeof(BenchmarkTestOrder));
            string hashDisabled = ExecutionPlanCache.ComputeHash(configDisabled, typeof(BenchmarkTestOrder));

            Assert.NotEqual(hashEnabled, hashDisabled);
        }

        [Theory]
        [InlineData(-50)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(50)]
        public void Cache_Retrieval_Returns_Correct_Plan_For_Negative_Zero_And_Positive_Orders(int order)
        {
            var cache = new ExecutionPlanCache();

            var configEnabled = new ParserConfiguration
            {
                Name = $"Config_{order}_Enabled",
                GlobalSeparator = "|",
                Columns =
                {
                    new PropertyColumn
                    {
                        Id = Guid.NewGuid(),
                        Name = "ColA",
                        PropertyPath = "Id",
                        Order = order,
                        IsEnabled = true
                    }
                }
            };

            var configDisabled = new ParserConfiguration
            {
                Name = $"Config_{order}_Disabled",
                GlobalSeparator = "|",
                Columns =
                {
                    new PropertyColumn
                    {
                        Id = configEnabled.Columns[0].Id,
                        Name = "ColA",
                        PropertyPath = "Id",
                        Order = order,
                        IsEnabled = false
                    }
                }
            };

            var compiler = new ExecutionPlanCompiler(_resolver, _lambda);
            var planEnabled = compiler.Compile(configEnabled, typeof(BenchmarkTestOrder));
            var planDisabled = compiler.Compile(configDisabled, typeof(BenchmarkTestOrder));

            Assert.NotSame(planEnabled, planDisabled);

            cache.Put(configEnabled, planEnabled, typeof(BenchmarkTestOrder));
            cache.Put(configDisabled, planDisabled, typeof(BenchmarkTestOrder));

            // Verify content hash lookup
            var retrievedEnabled = cache.TryGet(configEnabled, typeof(BenchmarkTestOrder));
            var retrievedDisabled = cache.TryGet(configDisabled, typeof(BenchmarkTestOrder));

            Assert.NotNull(retrievedEnabled);
            Assert.NotNull(retrievedDisabled);
            Assert.Same(planEnabled, retrievedEnabled);
            Assert.Same(planDisabled, retrievedDisabled);

            // Verify fast fingerprint lookup
            ulong fpEnabled = ExecutionPlanCache.ComputeFastFingerprint(configEnabled, typeof(BenchmarkTestOrder));
            ulong fpDisabled = ExecutionPlanCache.ComputeFastFingerprint(configDisabled, typeof(BenchmarkTestOrder));

            Assert.NotEqual(fpEnabled, fpDisabled);
            Assert.Same(planEnabled, cache.TryGet(fpEnabled));
            Assert.Same(planDisabled, cache.TryGet(fpDisabled));
        }

        [Fact]
        public void Engine_Parse_Negative_Order_Enabled_Vs_Disabled_Produces_Distinct_Results()
        {
            var id = Guid.NewGuid();
            var configEnabled = new ParserConfiguration
            {
                GlobalSeparator = ",",
                Columns =
                {
                    new PropertyColumn { Id = id, Name = "Name", PropertyPath = "Name", Order = -1, IsEnabled = true },
                    new PropertyColumn { Name = "Qty", PropertyPath = "Quantity", Order = 0, IsEnabled = true }
                }
            };

            var configDisabled = new ParserConfiguration
            {
                GlobalSeparator = ",",
                Columns =
                {
                    new PropertyColumn { Id = id, Name = "Name", PropertyPath = "Name", Order = -1, IsEnabled = false },
                    new PropertyColumn { Name = "Qty", PropertyPath = "Quantity", Order = 0, IsEnabled = true }
                }
            };

            var item = new BenchmarkTestOrder { Name = "Widgets", Quantity = 12 };

            var outEnabled = _engine.Parse(configEnabled, item);
            var outDisabled = _engine.Parse(configDisabled, item);

            Assert.Equal("Widgets,12", outEnabled);
            Assert.Equal("12", outDisabled);
        }

        #endregion

        #region 2. Nullable Flow for Formulas, Conditionals, and Replacements

        [Fact]
        public void FormulaColumn_NullOrBlank_Expression_Fails_Validation()
        {
            var configNull = new ParserConfiguration
            {
                Columns = { new FormulaColumn { Name = "F1", Expression = null! } }
            };
            var resultNull = ConfigurationValidator.Validate(configNull, typeof(BenchmarkTestOrder), _resolver, _lambda, null);
            Assert.False(resultNull.IsValid);
            Assert.Contains(resultNull.Errors, e => e.Code == "MISSING_FIELD");

            var configBlank = new ParserConfiguration
            {
                Columns = { new FormulaColumn { Name = "F1", Expression = "   " } }
            };
            var resultBlank = ConfigurationValidator.Validate(configBlank, typeof(BenchmarkTestOrder), _resolver, _lambda, null);
            Assert.False(resultBlank.IsValid);
            Assert.Contains(resultBlank.Errors, e => e.Code == "MISSING_FIELD");
        }

        [Fact]
        public void ConditionalColumn_NullOrBlank_ConditionExpression_Fails_Validation()
        {
            var configNull = new ParserConfiguration
            {
                Columns = { new ConditionalColumn { Name = "C1", ConditionExpression = null!, TrueValue = "Yes", FalseValue = "No" } }
            };
            var resultNull = ConfigurationValidator.Validate(configNull, typeof(BenchmarkTestOrder), _resolver, _lambda, null);
            Assert.False(resultNull.IsValid);
            Assert.Contains(resultNull.Errors, e => e.Code == "MISSING_FIELD");

            var configBlank = new ParserConfiguration
            {
                Columns = { new ConditionalColumn { Name = "C1", ConditionExpression = "  \t  ", TrueValue = "Yes", FalseValue = "No" } }
            };
            var resultBlank = ConfigurationValidator.Validate(configBlank, typeof(BenchmarkTestOrder), _resolver, _lambda, null);
            Assert.False(resultBlank.IsValid);
            Assert.Contains(resultBlank.Errors, e => e.Code == "MISSING_FIELD");
        }

        [Fact]
        public void ConditionalColumn_NullAndBlank_BranchValues_Are_Valid_And_Output_EmptyString()
        {
            var config = new ParserConfiguration
            {
                GlobalSeparator = ",",
                Columns =
                {
                    new ConditionalColumn
                    {
                        Name = "FlagTrueNull",
                        ConditionExpression = "Quantity > 0",
                        TrueValue = null!,
                        FalseValue = "NonZero",
                        Order = 1
                    },
                    new ConditionalColumn
                    {
                        Name = "FlagFalseEmpty",
                        ConditionExpression = "Quantity < 0",
                        TrueValue = "Negative",
                        FalseValue = "",
                        Order = 2
                    }
                }
            };

            var valResult = ConfigurationValidator.Validate(config, typeof(BenchmarkTestOrder), _resolver, _lambda, null);
            Assert.True(valResult.IsValid);

            var item = new BenchmarkTestOrder { Quantity = 10 };
            var output = _engine.Parse(config, item);

            // FlagTrueNull condition is true -> outputs empty string.
            // FlagFalseEmpty condition is false -> outputs empty string.
            Assert.Equal(",", output);
        }

        [Fact]
        public void ConditionalColumn_BlankExpressionBranch_Throws_ColumnEvaluationException()
        {
            // Branch starts with '=' but expression is blank: "=  "
            // Under configuration contract, blank expression is invalid; runtime evaluation fails as expected.
            var config = new ParserConfiguration
            {
                GlobalSeparator = ",",
                Columns =
                {
                    new ConditionalColumn
                    {
                        Name = "CondBlankExprBranch",
                        ConditionExpression = "Quantity > 0",
                        TrueValue = "=  ",
                        FalseValue = "FallbackVal",
                        Order = 1
                    }
                }
            };

            var item = new BenchmarkTestOrder { Quantity = 5 };
            Assert.Throws<ColumnEvaluationException>(() => _engine.Parse(config, item));
        }

        [Fact]
        public void ReplacementApplicator_NullRules_Returns_OriginalValue()
        {
            var applicator = new ReplacementApplicator();
            var result = applicator.Apply("Hello World", null);
            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void ReplacementApplicator_EmptyRules_Returns_OriginalValue()
        {
            var applicator = new ReplacementApplicator();
            var result = applicator.Apply("Hello World", new List<ReplacementRule>());
            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void ReplacementApplicator_NullTargetValue_ReplacesWithEmptyString()
        {
            var applicator = new ReplacementApplicator();
            var rules = new List<ReplacementRule>
            {
                new ReplacementRule { SourceValue = "World", TargetValue = null! }
            };
            var result = applicator.Apply("Hello World", rules);
            Assert.Equal("Hello ", result);
        }

        [Fact]
        public void ReplacementApplicator_NullSourceValue_IsSilentlySkipped()
        {
            var applicator = new ReplacementApplicator();
            var rules = new List<ReplacementRule>
            {
                new ReplacementRule { SourceValue = null!, TargetValue = "Replacement" }
            };
            var result = applicator.Apply("Hello World", rules);
            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void Column_With_Null_ReplacementRules_List_Executes_Without_Error()
        {
            var col = new PropertyColumn
            {
                Name = "ColWithNullReplacements",
                PropertyPath = "Name",
                ReplacementRules = null!
            };

            var config = new ParserConfiguration
            {
                GlobalSeparator = "|",
                Columns = { col }
            };

            var item = new BenchmarkTestOrder { Name = "Alpha" };
            var output = _engine.Parse(config, item);
            Assert.Equal("Alpha", output);
        }

        #endregion
    }
}
