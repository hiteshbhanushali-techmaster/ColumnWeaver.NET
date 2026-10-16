using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using ColumnWeaver.NET;
using ColumnWeaver.NET.Application.Engine;
using ColumnWeaver.NET.Application.Factory;
using ColumnWeaver.NET.Application.Validation;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Enums;
using ColumnWeaver.NET.Core.Models;
using ColumnWeaver.NET.Core.ParserConfiguration;
using ColumnWeaver.NET.Infrastructure;
using ColumnWeaver.NET.Infrastructure.ExpressionEngine;
using ColumnWeaver.NET.Infrastructure.PropertyResolution;

namespace ColumnWeaver.NET.Benchmarks
{
    public class BenchmarkOrder
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double UnitPrice { get; set; }
        public int Quantity { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
        public string RawCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public CustomerInfo Customer { get; set; } = new CustomerInfo();
    }

    public class CustomerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net48, warmupCount: 3, iterationCount: 6)]
    public class ParserBenchmarks
    {
        private BenchmarkOrder _sampleItem = null!;
        private BenchmarkOrder[] _batch10K = null!;
        private BenchmarkOrder[] _conditionalItems = null!;
        private BenchmarkOrder[] _nullItems = null!;

        private int _condHkIndex;
        private int _condCcIndex;
        private int _condNcIndex;

        private int _nullHkIndex;
        private int _nullCcIndex;
        private int _nullNcIndex;

        // Configurations
        private ParserConfiguration _propConfig = null!;
        private ParserConfiguration _formulaConfig = null!;
        private ParserConfiguration _condConfig = null!;
        private ParserConfiguration _varConfig = null!;
        private ParserConfiguration _fixedConfig = null!;
        private ParserConfiguration _funcConfig = null!;
        private ParserConfiguration _nullConfig = null!;
        private ParserConfiguration _formattingConfig = null!;
        private ParserConfiguration _replacementConfig = null!;
        private ParserConfiguration _disabledConfig = null!;
        private ParserConfiguration _depConfig = null!;
        private ParserConfiguration _repConfig = null!;

        // Compiled Clients
        private ColumnWeaverClient _propCompiledClient = null!;
        private ColumnWeaverClient _formulaCompiledClient = null!;
        private ColumnWeaverClient _condCompiledClient = null!;
        private ColumnWeaverClient _varCompiledClient = null!;
        private ColumnWeaverClient _fixedCompiledClient = null!;
        private ColumnWeaverClient _funcCompiledClient = null!;
        private ColumnWeaverClient _nullCompiledClient = null!;
        private ColumnWeaverClient _formattingCompiledClient = null!;
        private ColumnWeaverClient _replacementCompiledClient = null!;
        private ColumnWeaverClient _disabledCompiledClient = null!;
        private ColumnWeaverClient _depCompiledClient = null!;
        private ColumnWeaverClient _repCompiledClient = null!;

        // Interpreted Engine (ParserEngine + NCalc)
        private ReflectionPropertyResolver _resolver = null!;
        private NCalcExpressionEngine _ncalcExpr = null!;
        private DefaultFunctionRegistry _registry = null!;
        private ParserEngine _ncalcEngine = null!;

        [GlobalSetup]
        public void Setup()
        {
            _sampleItem = new BenchmarkOrder
            {
                Id = 1001,
                Name = "Widget Pro",
                UnitPrice = 50.0,
                Quantity = 5,
                IsActive = true,
                Notes = null,
                RawCode = "  item-code  ",
                Status = "PENDING",
                Customer = new CustomerInfo
                {
                    Name = "ACME Global",
                    Region = "North America",
                    City = "New York"
                }
            };

            _batch10K = new BenchmarkOrder[10000];
            for (int i = 0; i < _batch10K.Length; i++)
            {
                _batch10K[i] = new BenchmarkOrder
                {
                    Id = 1000 + i,
                    Name = "Item #" + i,
                    UnitPrice = 10.0 + (i % 50),
                    Quantity = 1 + (i % 10),
                    IsActive = (i % 2 == 0),
                    Notes = null,
                    RawCode = "  code-" + i + "  ",
                    Status = (i % 2 == 0) ? "PENDING" : "APPROVED",
                    Customer = new CustomerInfo
                    {
                        Name = "Customer " + (i % 100),
                        Region = "Region " + (i % 5),
                        City = "City " + (i % 10)
                    }
                };
            }

            _conditionalItems = new[]
            {
                new BenchmarkOrder { Id = 101, IsActive = true, Status = "PENDING" },
                new BenchmarkOrder { Id = 102, IsActive = false, Status = "PENDING" },
                new BenchmarkOrder { Id = 103, IsActive = true, Status = "PENDING" },
                new BenchmarkOrder { Id = 104, IsActive = false, Status = "PENDING" }
            };

            _nullItems = new[]
            {
                new BenchmarkOrder { Id = 201, Notes = "Valid Note", Status = "PENDING" },
                new BenchmarkOrder { Id = 202, Notes = null, Status = "PENDING" },
                new BenchmarkOrder { Id = 203, Notes = "Another Note", Status = "PENDING" },
                new BenchmarkOrder { Id = 204, Notes = null, Status = "PENDING" }
            };

            // 1. Property Extraction (Flat and Nested)
            _propConfig = new ParserConfiguration
            {
                Name = "PropertyScenario",
                GlobalSeparator = "|",
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 1 },
                    new PropertyColumn { Name = "CustomerName", PropertyPath = "Customer.Name", Order = 2 },
                    new PropertyColumn { Name = "City", PropertyPath = "Customer.City", Order = 3 }
                }
            };

            // 2. Formula Calculation
            _formulaConfig = new ParserConfiguration
            {
                Name = "FormulaScenario",
                Columns = new List<ColumnDefinition>
                {
                    new FormulaColumn
                    {
                        Name = "Gross",
                        Order = 1,
                        Expression = "UnitPrice * Quantity",
                        Formatting = new FormattingOptions { FormatString = "F2" }
                    }
                }
            };

            // 3. Conditional Column (Full End-to-End Row with Id + Status)
            _condConfig = new ParserConfiguration
            {
                Name = "ConditionalScenario",
                GlobalSeparator = "|",
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 1 },
                    new ConditionalColumn
                    {
                        Name = "Status",
                        Order = 2,
                        ConditionExpression = "IsActive == true",
                        TrueValue = "Active",
                        FalseValue = "Inactive"
                    }
                }
            };

            // 4. Variables (Numeric & String)
            _varConfig = new ParserConfiguration
            {
                Name = "VariablesScenario",
                GlobalSeparator = "|",
                Variables = new Dictionary<string, object?>
                {
                    { "TaxRate", 0.18 },
                    { "Currency", "USD" }
                },
                Columns = new List<ColumnDefinition>
                {
                    new FormulaColumn
                    {
                        Name = "Tax",
                        Order = 1,
                        Expression = "UnitPrice * Quantity * ${TaxRate}",
                        Formatting = new FormattingOptions { FormatString = "F2" }
                    },
                    new ConditionalColumn
                    {
                        Name = "Tier",
                        Order = 2,
                        ConditionExpression = "${Currency} == 'USD'",
                        TrueValue = "US-Domestic",
                        FalseValue = "Foreign"
                    }
                }
            };

            // 5. Fixed Text / Labels
            _fixedConfig = new ParserConfiguration
            {
                Name = "FixedTextScenario",
                GlobalSeparator = "|",
                Columns = new List<ColumnDefinition>
                {
                    new FixedTextColumn { Text = "RECORD", Order = 1 },
                    new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 2 },
                    new FixedTextColumn { Text = "END", Order = 3 }
                }
            };

            // 6. Custom Functions
            _funcConfig = new ParserConfiguration
            {
                Name = "CustomFunctionScenario",
                GlobalSeparator = "|",
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Name", PropertyPath = "Name", Order = 1 },
                    new CustomFunctionColumn
                    {
                        Name = "UpperName",
                        Order = 2,
                        FunctionName = "Upper",
                        Parameters = new List<string> { "${Name}" }
                    }
                }
            };

            // 7. Null Handling (Full End-to-End Row with Id + Notes)
            _nullConfig = new ParserConfiguration
            {
                Name = "NullHandlingScenario",
                GlobalSeparator = "|",
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 1 },
                    new PropertyColumn
                    {
                        Name = "Notes",
                        PropertyPath = "Notes",
                        Order = 2,
                        NullHandling = new NullHandlingConfig
                        {
                            Strategy = NullHandlingStrategy.UseDefault,
                            DefaultValue = "(none)"
                        }
                    }
                }
            };

            // 8. Formatting Options (Trim, Upper, Pad, Prefix, Suffix)
            _formattingConfig = new ParserConfiguration
            {
                Name = "FormattingScenario",
                Columns = new List<ColumnDefinition>
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

            // 9. Replacement Rules
            _replacementConfig = new ParserConfiguration
            {
                Name = "ReplacementScenario",
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn
                    {
                        Name = "Status",
                        PropertyPath = "Status",
                        ReplacementRules = new List<ReplacementRule>
                        {
                            new ReplacementRule
                            {
                                SourceValue = "PENDING",
                                TargetValue = "Awaiting Approval",
                                IsCaseSensitive = true
                            }
                        }
                    }
                }
            };

            // 10. Disabled Columns & Separator Slot
            _disabledConfig = new ParserConfiguration
            {
                Name = "DisabledColumnScenario",
                GlobalSeparator = "|",
                PreserveSeparatorForDisabledColumns = true,
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 1 },
                    new PropertyColumn { Name = "Notes", PropertyPath = "Notes", Order = 2, IsEnabled = false },
                    new PropertyColumn { Name = "Status", PropertyPath = "Status", Order = 3 }
                }
            };

            // 11. Dependency-Heavy Formulas
            _depConfig = new ParserConfiguration
            {
                Name = "DependencyHeavyScenario",
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

            // 12. Representative Configuration (for Batch benchmark)
            _repConfig = new ParserConfiguration
            {
                Name = "RepresentativeBatchScenario",
                GlobalSeparator = "|",
                Variables = new Dictionary<string, object?> { { "TaxRate", 0.18 } },
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", Order = 1, PropertyPath = "Id" },
                    new PropertyColumn { Name = "Customer", Order = 2, PropertyPath = "Customer.Name" },
                    new FormulaColumn { Name = "Gross", Order = 3, Expression = "UnitPrice * Quantity", Formatting = new FormattingOptions { FormatString = "F2" } },
                    new ConditionalColumn { Name = "Status", Order = 4, ConditionExpression = "IsActive == true", TrueValue = "Active", FalseValue = "Inactive" },
                    new PropertyColumn { Name = "Region", Order = 5, PropertyPath = "Customer.Region" },
                    new FormulaColumn { Name = "Tax", Order = 6, Expression = "UnitPrice * Quantity * ${TaxRate}", Formatting = new FormattingOptions { FormatString = "F2" } }
                }
            };

            // Initialize Compiled Clients (using high-throughput precompiled client)
            _propCompiledClient = ColumnWeaverClient.CreatePrecompiled(_propConfig, typeof(BenchmarkOrder));
            _formulaCompiledClient = ColumnWeaverClient.CreatePrecompiled(_formulaConfig, typeof(BenchmarkOrder));
            _condCompiledClient = ColumnWeaverClient.CreatePrecompiled(_condConfig, typeof(BenchmarkOrder));
            _varCompiledClient = ColumnWeaverClient.CreatePrecompiled(_varConfig, typeof(BenchmarkOrder));
            _fixedCompiledClient = ColumnWeaverClient.CreatePrecompiled(_fixedConfig, typeof(BenchmarkOrder));
            _funcCompiledClient = ColumnWeaverClient.CreatePrecompiled(_funcConfig, typeof(BenchmarkOrder));
            _nullCompiledClient = ColumnWeaverClient.CreatePrecompiled(_nullConfig, typeof(BenchmarkOrder));
            _formattingCompiledClient = ColumnWeaverClient.CreatePrecompiled(_formattingConfig, typeof(BenchmarkOrder));
            _replacementCompiledClient = ColumnWeaverClient.CreatePrecompiled(_replacementConfig, typeof(BenchmarkOrder));
            _disabledCompiledClient = ColumnWeaverClient.CreatePrecompiled(_disabledConfig, typeof(BenchmarkOrder));
            _depCompiledClient = ColumnWeaverClient.CreatePrecompiled(_depConfig, typeof(BenchmarkOrder));
            _repCompiledClient = ColumnWeaverClient.CreatePrecompiled(_repConfig, typeof(BenchmarkOrder));

            // Initialize Interpreted Engine (ParserEngine + NCalc)
            _resolver = new ReflectionPropertyResolver();
            _ncalcExpr = new NCalcExpressionEngine(_resolver);
            _registry = new DefaultFunctionRegistry();
            var factory = new ColumnEvaluatorFactory(_resolver, _ncalcExpr, _registry);
            _ncalcEngine = new ParserEngine(factory);

            // Correctness & Equivalence Verification
            VerifyEquivalence("Property Extraction", Property_Hardcoded(), Property_CompiledClient(), Property_InterpretedNCalc());
            VerifyEquivalence("Formula Calculation", Formula_Hardcoded(), Formula_CompiledClient(), Formula_InterpretedNCalc());

            // Parity check across all conditional inputs (exercising both true and false branches on full row)
            for (int i = 0; i < _conditionalItems.Length; i++)
            {
                var item = _conditionalItems[i];
                string hc = item.Id.ToString(CultureInfo.InvariantCulture) + "|" + (item.IsActive ? "Active" : "Inactive");
                string cc = _condCompiledClient.Parse(item);
                string nc = _ncalcEngine.Parse(_condConfig, item);
                VerifyEquivalence($"Conditional Column [case {i}: IsActive={item.IsActive}]", hc, cc, nc);
            }

            VerifyEquivalence("Variables (Numeric & String)", Variables_Hardcoded(), Variables_CompiledClient(), Variables_InterpretedNCalc());
            VerifyEquivalence("Fixed Text / Labels", FixedText_Hardcoded(), FixedText_CompiledClient(), FixedText_InterpretedNCalc());
            VerifyEquivalence("Custom Function", CustomFunction_Hardcoded(), CustomFunction_CompiledClient(), CustomFunction_InterpretedNCalc());

            // Parity check across all null-handling inputs (exercising both non-null and null fallback branches on full row)
            for (int i = 0; i < _nullItems.Length; i++)
            {
                var item = _nullItems[i];
                string hc = item.Id.ToString(CultureInfo.InvariantCulture) + "|" + (item.Notes ?? "(none)");
                string cc = _nullCompiledClient.Parse(item);
                string nc = _ncalcEngine.Parse(_nullConfig, item);
                VerifyEquivalence($"Null Handling [case {i}: Notes={(item.Notes == null ? "null" : item.Notes)}]", hc, cc, nc);
            }

            VerifyEquivalence("Formatting Options", Formatting_Hardcoded(), Formatting_CompiledClient(), Formatting_InterpretedNCalc());
            VerifyEquivalence("Replacement Rules", Replacement_Hardcoded(), Replacement_CompiledClient(), Replacement_InterpretedNCalc());
            VerifyEquivalence("Disabled Column Slot", DisabledColumn_Hardcoded(), DisabledColumn_CompiledClient(), DisabledColumn_InterpretedNCalc());
            VerifyEquivalence("Dependency-Heavy Formulas", DependencyHeavy_Hardcoded(), DependencyHeavy_CompiledClient(), DependencyHeavy_InterpretedNCalc());

            // Batch Equivalence Check (Full record-by-record comparison across all 10,000 items)
            int batchIndex = 0;
            using (var hcEnum = GenerateHardcodedBatchOutputs().GetEnumerator())
            using (var ccEnum = _repCompiledClient.ParseMany(_batch10K).GetEnumerator())
            using (var ncEnum = GenerateNCalcBatchOutputs().GetEnumerator())
            {
                while (hcEnum.MoveNext())
                {
                    if (!ccEnum.MoveNext())
                        throw new InvalidOperationException($"Batch count mismatch: Compiled produced fewer items than Hardcoded at index {batchIndex}.");
                    if (!ncEnum.MoveNext())
                        throw new InvalidOperationException($"Batch count mismatch: NCalc produced fewer items than Hardcoded at index {batchIndex}.");

                    if (hcEnum.Current != ccEnum.Current)
                        throw new InvalidOperationException($"Batch content mismatch at index {batchIndex}:\nHardcoded: '{hcEnum.Current}'\nCompiled:  '{ccEnum.Current}'");
                    if (hcEnum.Current != ncEnum.Current)
                        throw new InvalidOperationException($"Batch content mismatch at index {batchIndex}:\nHardcoded: '{hcEnum.Current}'\nNCalc:     '{ncEnum.Current}'");

                    batchIndex++;
                }

                if (ccEnum.MoveNext() || ncEnum.MoveNext())
                    throw new InvalidOperationException("Batch count mismatch: Engine produced more items than Hardcoded.");
            }
        }

        private IEnumerable<string> GenerateHardcodedBatchOutputs()
        {
            for (int i = 0; i < _batch10K.Length; i++)
            {
                var item = _batch10K[i];
                double gross = item.UnitPrice * item.Quantity;
                double tax = gross * 0.18;
                yield return item.Id.ToString(CultureInfo.InvariantCulture) + "|" +
                             item.Customer.Name + "|" +
                             gross.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                             (item.IsActive ? "Active" : "Inactive") + "|" +
                             item.Customer.Region + "|" +
                             tax.ToString("F2", CultureInfo.InvariantCulture);
            }
        }

        private IEnumerable<string> GenerateNCalcBatchOutputs()
        {
            for (int i = 0; i < _batch10K.Length; i++)
            {
                yield return _ncalcEngine.Parse(_repConfig, _batch10K[i]);
            }
        }

        private static void VerifyEquivalence(string scenario, string hardcoded, string compiled, string ncalc)
        {
            if (hardcoded != compiled)
                throw new InvalidOperationException($"Scenario '{scenario}' mismatch between Hardcoded ('{hardcoded}') and CompiledClient ('{compiled}').");
            if (hardcoded != ncalc)
                throw new InvalidOperationException($"Scenario '{scenario}' mismatch between Hardcoded ('{hardcoded}') and NCalc ('{ncalc}').");
        }

        // -------------------------------------------------------------
        // Scenario 1: Property Extraction (Flat and Nested)
        // -------------------------------------------------------------
        [Benchmark(Description = "Property Extraction (Hard-coded C#)")]
        public string Property_Hardcoded()
        {
            return _sampleItem.Id.ToString(CultureInfo.InvariantCulture) + "|" +
                   _sampleItem.Customer.Name + "|" +
                   _sampleItem.Customer.City;
        }

        [Benchmark(Description = "Property Extraction (Compiled Client)")]
        public string Property_CompiledClient()
        {
            return _propCompiledClient.Parse(_sampleItem);
        }

        [Benchmark(Description = "Property Extraction (NCalc / Interpreted)")]
        public string Property_InterpretedNCalc()
        {
            return _ncalcEngine.Parse(_propConfig, _sampleItem);
        }

        // -------------------------------------------------------------
        // Scenario 2: Formula Calculations
        // -------------------------------------------------------------
        [Benchmark(Description = "Formula Calculations (Hard-coded C#)")]
        public string Formula_Hardcoded()
        {
            return (_sampleItem.UnitPrice * _sampleItem.Quantity).ToString("F2", CultureInfo.InvariantCulture);
        }

        [Benchmark(Description = "Formula Calculations (Compiled Client)")]
        public string Formula_CompiledClient()
        {
            return _formulaCompiledClient.Parse(_sampleItem);
        }

        [Benchmark(Description = "Formula Calculations (NCalc / Interpreted)")]
        public string Formula_InterpretedNCalc()
        {
            return _ncalcEngine.Parse(_formulaConfig, _sampleItem);
        }

        // -------------------------------------------------------------
        // Scenario 3: Conditional Columns
        // -------------------------------------------------------------
        [Benchmark(Description = "Conditional Branch (Hard-coded C# Microbenchmark)")]
        public string Conditional_Micro_Hardcoded()
        {
            var item = _conditionalItems[_condHkIndex & 3];
            return item.IsActive ? "Active" : "Inactive";
        }

        [Benchmark(Description = "Conditional Columns (Hard-coded C#)")]
        public string Conditional_Hardcoded()
        {
            var item = _conditionalItems[_condHkIndex++ & 3];
            return item.Id.ToString(CultureInfo.InvariantCulture) + "|" + (item.IsActive ? "Active" : "Inactive");
        }

        [Benchmark(Description = "Conditional Columns (Compiled Client)")]
        public string Conditional_CompiledClient()
        {
            var item = _conditionalItems[_condCcIndex++ & 3];
            return _condCompiledClient.Parse(item);
        }

        [Benchmark(Description = "Conditional Columns (NCalc / Interpreted)")]
        public string Conditional_InterpretedNCalc()
        {
            var item = _conditionalItems[_condNcIndex++ & 3];
            return _ncalcEngine.Parse(_condConfig, item);
        }

        // -------------------------------------------------------------
        // Scenario 4: Variables (Numeric & String)
        // -------------------------------------------------------------
        [Benchmark(Description = "Variables (Hard-coded C#)")]
        public string Variables_Hardcoded()
        {
            return (_sampleItem.UnitPrice * _sampleItem.Quantity * 0.18).ToString("F2", CultureInfo.InvariantCulture) +
                   "|" + ("USD" == "USD" ? "US-Domestic" : "Foreign");
        }

        [Benchmark(Description = "Variables (Compiled Client)")]
        public string Variables_CompiledClient()
        {
            return _varCompiledClient.Parse(_sampleItem);
        }

        [Benchmark(Description = "Variables (NCalc / Interpreted)")]
        public string Variables_InterpretedNCalc()
        {
            return _ncalcEngine.Parse(_varConfig, _sampleItem);
        }

        // -------------------------------------------------------------
        // Scenario 5: Fixed Text / Labels
        // -------------------------------------------------------------
        [Benchmark(Description = "Fixed Text / Labels (Hard-coded C#)")]
        public string FixedText_Hardcoded()
        {
            return "RECORD|" + _sampleItem.Id.ToString(CultureInfo.InvariantCulture) + "|END";
        }

        [Benchmark(Description = "Fixed Text / Labels (Compiled Client)")]
        public string FixedText_CompiledClient()
        {
            return _fixedCompiledClient.Parse(_sampleItem);
        }

        [Benchmark(Description = "Fixed Text / Labels (NCalc / Interpreted)")]
        public string FixedText_InterpretedNCalc()
        {
            return _ncalcEngine.Parse(_fixedConfig, _sampleItem);
        }

        // -------------------------------------------------------------
        // Scenario 6: Custom Functions
        // -------------------------------------------------------------
        [Benchmark(Description = "Custom Functions (Hard-coded C#)")]
        public string CustomFunction_Hardcoded()
        {
            return _sampleItem.Name + "|" + _sampleItem.Name.ToUpperInvariant();
        }

        [Benchmark(Description = "Custom Functions (Compiled Client)")]
        public string CustomFunction_CompiledClient()
        {
            return _funcCompiledClient.Parse(_sampleItem);
        }

        [Benchmark(Description = "Custom Functions (NCalc / Interpreted)")]
        public string CustomFunction_InterpretedNCalc()
        {
            return _ncalcEngine.Parse(_funcConfig, _sampleItem);
        }

        // -------------------------------------------------------------
        // Scenario 7: Null Handling
        // -------------------------------------------------------------
        [Benchmark(Description = "Null Coalescing (Hard-coded C# Microbenchmark)")]
        public string NullHandling_Micro_Hardcoded()
        {
            var item = _nullItems[_nullHkIndex & 3];
            return item.Notes ?? "(none)";
        }

        [Benchmark(Description = "Null Handling (Hard-coded C#)")]
        public string NullHandling_Hardcoded()
        {
            var item = _nullItems[_nullHkIndex++ & 3];
            return item.Id.ToString(CultureInfo.InvariantCulture) + "|" + (item.Notes ?? "(none)");
        }

        [Benchmark(Description = "Null Handling (Compiled Client)")]
        public string NullHandling_CompiledClient()
        {
            var item = _nullItems[_nullCcIndex++ & 3];
            return _nullCompiledClient.Parse(item);
        }

        [Benchmark(Description = "Null Handling (NCalc / Interpreted)")]
        public string NullHandling_InterpretedNCalc()
        {
            var item = _nullItems[_nullNcIndex++ & 3];
            return _ncalcEngine.Parse(_nullConfig, item);
        }

        // -------------------------------------------------------------
        // Scenario 8: Formatting Options (Trim, Upper, Pad, Prefix, Suffix)
        // -------------------------------------------------------------
        [Benchmark(Description = "Formatting Options (Hard-coded C#)")]
        public string Formatting_Hardcoded()
        {
            return "[" + _sampleItem.RawCode.Trim().ToUpperInvariant().PadRight(12, ' ') + "]";
        }

        [Benchmark(Description = "Formatting Options (Compiled Client)")]
        public string Formatting_CompiledClient()
        {
            return _formattingCompiledClient.Parse(_sampleItem);
        }

        [Benchmark(Description = "Formatting Options (NCalc / Interpreted)")]
        public string Formatting_InterpretedNCalc()
        {
            return _ncalcEngine.Parse(_formattingConfig, _sampleItem);
        }

        // -------------------------------------------------------------
        // Scenario 9: Replacement Rules
        // -------------------------------------------------------------
        [Benchmark(Description = "Replacement Rules (Hard-coded C#)")]
        public string Replacement_Hardcoded()
        {
            return _sampleItem.Status.Replace("PENDING", "Awaiting Approval");
        }

        [Benchmark(Description = "Replacement Rules (Compiled Client)")]
        public string Replacement_CompiledClient()
        {
            return _replacementCompiledClient.Parse(_sampleItem);
        }

        [Benchmark(Description = "Replacement Rules (NCalc / Interpreted)")]
        public string Replacement_InterpretedNCalc()
        {
            return _ncalcEngine.Parse(_replacementConfig, _sampleItem);
        }

        // -------------------------------------------------------------
        // Scenario 10: Disabled Columns & Separator Slot
        // -------------------------------------------------------------
        [Benchmark(Description = "Disabled Columns (Hard-coded C#)")]
        public string DisabledColumn_Hardcoded()
        {
            return _sampleItem.Id.ToString(CultureInfo.InvariantCulture) + "||" + _sampleItem.Status;
        }

        [Benchmark(Description = "Disabled Columns (Compiled Client)")]
        public string DisabledColumn_CompiledClient()
        {
            return _disabledCompiledClient.Parse(_sampleItem);
        }

        [Benchmark(Description = "Disabled Columns (NCalc / Interpreted)")]
        public string DisabledColumn_InterpretedNCalc()
        {
            return _ncalcEngine.Parse(_disabledConfig, _sampleItem);
        }

        // -------------------------------------------------------------
        // Scenario 11: Dependency-Heavy Formulas
        // -------------------------------------------------------------
        [Benchmark(Description = "Dependency-Heavy (Hard-coded C#)")]
        public string DependencyHeavy_Hardcoded()
        {
            double gross = _sampleItem.UnitPrice * _sampleItem.Quantity;
            double discount = gross * 0.10;
            double subTotal = gross - discount;
            double tax = subTotal * 0.18;
            double finalTotal = subTotal + tax;
            string tier = finalTotal > 200 ? "VIP" : "Standard";
            return gross.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                   discount.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                   subTotal.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                   tax.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                   finalTotal.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                   tier;
        }

        [Benchmark(Description = "Dependency-Heavy (Compiled Client)")]
        public string DependencyHeavy_CompiledClient()
        {
            return _depCompiledClient.Parse(_sampleItem);
        }

        [Benchmark(Description = "Dependency-Heavy (NCalc / Interpreted)")]
        public string DependencyHeavy_InterpretedNCalc()
        {
            return _ncalcEngine.Parse(_depConfig, _sampleItem);
        }

        // -------------------------------------------------------------
        // Scenario 12: Batch Processing (10,000 items)
        // -------------------------------------------------------------
        [Benchmark(Description = "Batch 10k Items (Hard-coded C#)")]
        public int Hardcoded_Batch10K()
        {
            int count = 0;
            for (int i = 0; i < _batch10K.Length; i++)
            {
                var item = _batch10K[i];
                double gross = item.UnitPrice * item.Quantity;
                double tax = gross * 0.18;
                var s = item.Id.ToString(CultureInfo.InvariantCulture) + "|" +
                        item.Customer.Name + "|" +
                        gross.ToString("F2", CultureInfo.InvariantCulture) + "|" +
                        (item.IsActive ? "Active" : "Inactive") + "|" +
                        item.Customer.Region + "|" +
                        tax.ToString("F2", CultureInfo.InvariantCulture);
                count += s.Length;
            }
            return count;
        }

        [Benchmark(Description = "Batch 10k Items (Compiled Client)")]
        public int CompiledClient_Batch10K()
        {
            int count = 0;
            foreach (var s in _repCompiledClient.ParseMany(_batch10K))
            {
                count += s.Length;
            }
            return count;
        }

        [Benchmark(Description = "Batch 10k Items (NCalc / Interpreted)")]
        public int NCalc_Batch10K()
        {
            int count = 0;
            for (int i = 0; i < _batch10K.Length; i++)
            {
                var s = _ncalcEngine.Parse(_repConfig, _batch10K[i]);
                count += s.Length;
            }
            return count;
        }

        // -------------------------------------------------------------
        // Scenario 13: Cold Start vs Steady State
        // -------------------------------------------------------------
        [Benchmark(Description = "Cold Start (Clone & Compile & Parse)")]
        public string ColdStart_CompilationAndParse()
        {
            var config = new ParserConfiguration
            {
                Name = "ColdStart_" + Guid.NewGuid().ToString("N"),
                GlobalSeparator = "|",
                Columns = new List<ColumnDefinition>
                {
                    new PropertyColumn { Name = "Id", PropertyPath = "Id", Order = 1 },
                    new PropertyColumn { Name = "CustomerName", PropertyPath = "Customer.Name", Order = 2 },
                    new PropertyColumn { Name = "City", PropertyPath = "Customer.City", Order = 3 }
                }
            };
            return ColumnWeaverClient.Create(config).Parse(_sampleItem);
        }

        [Benchmark(Description = "Steady State (Precompiled Parse)")]
        public string SteadyState_Precompiled()
        {
            return _propCompiledClient.Parse(_sampleItem);
        }

        // -------------------------------------------------------------
        // Scenario 14: Configuration Validation (Separate from Parsing)
        // -------------------------------------------------------------
        [Benchmark(Description = "Configuration Validation (Representative)")]
        public ValidationResult Validation_RepresentativeConfig()
        {
            return ConfigurationValidator.Validate(_repConfig, typeof(BenchmarkOrder), _resolver, _ncalcExpr, _registry);
        }
    }

    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("===============================================================================");
            Console.WriteLine("ColumnWeaver.NET Benchmark & Profiling Suite");
            Console.WriteLine("===============================================================================");
            Console.WriteLine($"Date/Time:        {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Machine Name:     {Environment.MachineName}");
            Console.WriteLine($"OS Version:       {Environment.OSVersion}");
            Console.WriteLine($"64-Bit OS:        {Environment.Is64BitOperatingSystem}");
            Console.WriteLine($"Processor Count:  {Environment.ProcessorCount}");
            Console.WriteLine($"CLR Version:      {Environment.Version}");
            Console.WriteLine($"Runtime Target:   .NET Framework 4.8 (net48)");
            Console.WriteLine("===============================================================================\n");

            var bench = new ParserBenchmarks();
            Console.WriteLine("Initializing configurations and running output equivalence checks...");
            bench.Setup();
            Console.WriteLine("✓ All scenario equivalence assertions passed successfully.\n");

            bool runBdn = args.Length > 0 && args[0].Equals("--bdn", StringComparison.OrdinalIgnoreCase);

            if (runBdn)
            {
                Console.WriteLine("===============================================================================");
                Console.WriteLine("Launching BenchmarkDotNet full run (Job: net48, MemoryDiagnoser)...");
                Console.WriteLine("===============================================================================");
                Console.WriteLine("Note: Setup and equivalence assertions ran outside timed methods.\n");

                var summary = BenchmarkRunner.Run<ParserBenchmarks>();

                string resultsDir = summary.ResultsDirectoryPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BenchmarkDotNet.Artifacts", "results");
                string reportPath = Path.Combine(resultsDir, "ColumnWeaver.NET.Benchmarks.ParserBenchmarks-report-github.md");

                Console.WriteLine("\n===============================================================================");
                Console.WriteLine("BenchmarkDotNet run complete!");
                Console.WriteLine($"Generated Markdown Report: {reportPath}");
                Console.WriteLine("===============================================================================");
            }
            else
            {
                Console.WriteLine("===============================================================================");
                Console.WriteLine("Running Quick Profiling Mode (Stopwatch Loop)");
                Console.WriteLine("NOTE: Memory shown is 'Retained Heap Delta' via GC.GetTotalMemory, measuring");
                Console.WriteLine("net heap change over the run, not per-operation allocations.");
                Console.WriteLine("For official BenchmarkDotNet harness with MemoryDiagnoser, execute:");
                Console.WriteLine("  dotnet run --project benchmarks/ColumnWeaver.NET.Benchmarks/ColumnWeaver.NET.Benchmarks.csproj -c Release -- --bdn");
                Console.WriteLine("===============================================================================\n");

                RunPreciseProfiling(bench);

                Console.WriteLine("\n===============================================================================");
                Console.WriteLine("To run the full multi-iteration BenchmarkDotNet harness, execute:");
                Console.WriteLine("  dotnet run --project benchmarks/ColumnWeaver.NET.Benchmarks/ColumnWeaver.NET.Benchmarks.csproj -c Release -- --bdn");
                Console.WriteLine("===============================================================================");
            }
        }

        private static void RunPreciseProfiling(ParserBenchmarks bench)
        {
            const int warmupIterations = 5000;
            const int measurementIterations = 30000;

            Console.WriteLine("{0,-45} | {1,14} | {2,12} | {3,22} | {4,10}",
                "Scenario / Engine", "Time per Op", "Throughput", "Retained Heap Delta", "Gen0 / Gen1");
            Console.WriteLine(new string('-', 112));

            // Scenario 1: Property
            ProfileScenario("1. Property (Hard-coded C#)", () => bench.Property_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("1. Property (Compiled Client)", () => bench.Property_CompiledClient(), warmupIterations, measurementIterations);
            ProfileScenario("1. Property (NCalc / Interpreted)", () => bench.Property_InterpretedNCalc(), warmupIterations, measurementIterations / 2);
            Console.WriteLine(new string('-', 112));

            // Scenario 2: Formula
            ProfileScenario("2. Formula (Hard-coded C#)", () => bench.Formula_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("2. Formula (Compiled Client)", () => bench.Formula_CompiledClient(), warmupIterations, measurementIterations);
            ProfileScenario("2. Formula (NCalc / Interpreted)", () => bench.Formula_InterpretedNCalc(), 1000, 5000);
            Console.WriteLine(new string('-', 112));

            // Scenario 3: Conditional
            ProfileScenario("3. Cond Branch (Hard-coded Micro)", () => bench.Conditional_Micro_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("3. Conditional (Hard-coded C#)", () => bench.Conditional_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("3. Conditional (Compiled Client)", () => bench.Conditional_CompiledClient(), warmupIterations, measurementIterations);
            ProfileScenario("3. Conditional (NCalc / Interpreted)", () => bench.Conditional_InterpretedNCalc(), 1000, 5000);
            Console.WriteLine(new string('-', 112));

            // Scenario 4: Variables
            ProfileScenario("4. Variables (Hard-coded C#)", () => bench.Variables_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("4. Variables (Compiled Client)", () => bench.Variables_CompiledClient(), warmupIterations, measurementIterations);
            ProfileScenario("4. Variables (NCalc / Interpreted)", () => bench.Variables_InterpretedNCalc(), 1000, 5000);
            Console.WriteLine(new string('-', 112));

            // Scenario 5: Fixed Text
            ProfileScenario("5. Fixed Text (Hard-coded C#)", () => bench.FixedText_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("5. Fixed Text (Compiled Client)", () => bench.FixedText_CompiledClient(), warmupIterations, measurementIterations);
            ProfileScenario("5. Fixed Text (NCalc / Interpreted)", () => bench.FixedText_InterpretedNCalc(), warmupIterations, measurementIterations / 2);
            Console.WriteLine(new string('-', 112));

            // Scenario 6: Custom Functions
            ProfileScenario("6. Custom Function (Hard-coded C#)", () => bench.CustomFunction_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("6. Custom Function (Compiled Client)", () => bench.CustomFunction_CompiledClient(), warmupIterations, measurementIterations);
            ProfileScenario("6. Custom Function (NCalc / Interpreted)", () => bench.CustomFunction_InterpretedNCalc(), warmupIterations, measurementIterations / 2);
            Console.WriteLine(new string('-', 112));

            // Scenario 7: Null Handling
            ProfileScenario("7. Null Coalesce (Hard-coded Micro)", () => bench.NullHandling_Micro_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("7. Null Handling (Hard-coded C#)", () => bench.NullHandling_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("7. Null Handling (Compiled Client)", () => bench.NullHandling_CompiledClient(), warmupIterations, measurementIterations);
            ProfileScenario("7. Null Handling (NCalc / Interpreted)", () => bench.NullHandling_InterpretedNCalc(), warmupIterations, measurementIterations / 2);
            Console.WriteLine(new string('-', 112));

            // Scenario 8: Formatting Options
            ProfileScenario("8. Formatting Options (Hard-coded C#)", () => bench.Formatting_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("8. Formatting Options (Compiled Client)", () => bench.Formatting_CompiledClient(), warmupIterations, measurementIterations);
            ProfileScenario("8. Formatting Options (NCalc / Interpreted)", () => bench.Formatting_InterpretedNCalc(), warmupIterations, measurementIterations / 2);
            Console.WriteLine(new string('-', 112));

            // Scenario 9: Replacement Rules
            ProfileScenario("9. Replacement Rules (Hard-coded C#)", () => bench.Replacement_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("9. Replacement Rules (Compiled Client)", () => bench.Replacement_CompiledClient(), warmupIterations, measurementIterations);
            ProfileScenario("9. Replacement Rules (NCalc / Interpreted)", () => bench.Replacement_InterpretedNCalc(), warmupIterations, measurementIterations / 2);
            Console.WriteLine(new string('-', 112));

            // Scenario 10: Disabled Column Slot
            ProfileScenario("10. Disabled Column Slot (Hard-coded C#)", () => bench.DisabledColumn_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("10. Disabled Column Slot (Compiled Client)", () => bench.DisabledColumn_CompiledClient(), warmupIterations, measurementIterations);
            ProfileScenario("10. Disabled Column Slot (NCalc / Interpreted)", () => bench.DisabledColumn_InterpretedNCalc(), warmupIterations, measurementIterations / 2);
            Console.WriteLine(new string('-', 112));

            // Scenario 11: Dependency-Heavy Formulas
            ProfileScenario("11. Dep-Heavy (Hard-coded C#)", () => bench.DependencyHeavy_Hardcoded(), warmupIterations, measurementIterations);
            ProfileScenario("11. Dep-Heavy (Compiled Client)", () => bench.DependencyHeavy_CompiledClient(), warmupIterations, measurementIterations);
            ProfileScenario("11. Dep-Heavy (NCalc / Interpreted)", () => bench.DependencyHeavy_InterpretedNCalc(), 1000, 5000);
            Console.WriteLine(new string('-', 112));

            // Scenario 12: Batch Processing
            Console.WriteLine("\nBatch Processing (10,000 items):");
            Console.WriteLine("{0,-45} | {1,14} | {2,12} | {3,22} | {4,10}",
                "Scenario / Engine", "Batch Time", "Throughput", "Retained Heap Delta", "Gen0 / Gen1");
            Console.WriteLine(new string('-', 112));
            ProfileBatch("12. Batch 10k (Hard-coded C#)", () => bench.Hardcoded_Batch10K(), 10000);
            ProfileBatch("12. Batch 10k (Compiled Client)", () => bench.CompiledClient_Batch10K(), 10000);
            ProfileBatch("12. Batch 10k (NCalc / Interpreted)", () => bench.NCalc_Batch10K(), 10000);
            Console.WriteLine(new string('-', 112));

            // Scenario 13: Cold Start vs Steady State
            Console.WriteLine("\nLifecycle: Cold Start vs Steady State:");
            Console.WriteLine("{0,-45} | {1,14} | {2,12} | {3,22} | {4,10}",
                "Scenario / Engine", "Time per Op", "Throughput", "Retained Heap Delta", "Gen0 / Gen1");
            Console.WriteLine(new string('-', 112));
            ProfileScenario("13. Cold Start (Clone & Compile & Parse)", () => bench.ColdStart_CompilationAndParse(), 50, 500);
            ProfileScenario("13. Steady State (Precompiled Parse)", () => bench.SteadyState_Precompiled(), warmupIterations, measurementIterations);
            Console.WriteLine(new string('-', 112));

            // Scenario 14: Validation
            Console.WriteLine("\nConfiguration Validation (Separate from Parsing):");
            Console.WriteLine("{0,-45} | {1,14} | {2,12} | {3,22} | {4,10}",
                "Scenario / Engine", "Time per Op", "Throughput", "Retained Heap Delta", "Gen0 / Gen1");
            Console.WriteLine(new string('-', 112));
            ProfileScenario("14. Configuration Validation", () => bench.Validation_RepresentativeConfig().IsValid ? "Valid" : "Invalid", 1000, 10000);
            Console.WriteLine(new string('-', 112));
        }

        private static void ProfileScenario(string label, Func<string> action, int warmup, int iterations)
        {
            for (int i = 0; i < warmup; i++) action();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startMem = GC.GetTotalMemory(true);
            int gen0Start = GC.CollectionCount(0);
            int gen1Start = GC.CollectionCount(1);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                action();
            }
            sw.Stop();

            long endMem = GC.GetTotalMemory(false);
            int gen0 = GC.CollectionCount(0) - gen0Start;
            int gen1 = GC.CollectionCount(1) - gen1Start;

            double totalMs = sw.Elapsed.TotalMilliseconds;
            double usPerOp = (totalMs / iterations) * 1000.0;
            double opsPerSec = iterations / sw.Elapsed.TotalSeconds;
            double retainedKb = (endMem - startMem) / 1024.0;

            string timeStr = usPerOp < 0.05
                ? $"{usPerOp * 1000.0,8:N1} ns"
                : $"{usPerOp,8:N2} µs";

            Console.WriteLine("{0,-45} | {1,14} | {2,12:N0}/s | {3,18:N1} KB | {4,4} / {5,-4}",
                label, timeStr, opsPerSec, retainedKb, gen0, gen1);
        }

        private static void ProfileBatch(string label, Func<int> batchAction, int batchSize)
        {
            batchAction();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startMem = GC.GetTotalMemory(true);
            int gen0Start = GC.CollectionCount(0);
            int gen1Start = GC.CollectionCount(1);

            var sw = Stopwatch.StartNew();
            batchAction();
            sw.Stop();

            long endMem = GC.GetTotalMemory(false);
            int gen0 = GC.CollectionCount(0) - gen0Start;
            int gen1 = GC.CollectionCount(1) - gen1Start;

            double ms = sw.Elapsed.TotalMilliseconds;
            double opsPerSec = batchSize / sw.Elapsed.TotalSeconds;
            double retainedMb = (endMem - startMem) / (1024.0 * 1024.0);

            Console.WriteLine("{0,-45} | {1,11:N2} ms | {2,10:N0}/s | {3,18:N2} MB | {4,4} / {5,-4}",
                label, ms, opsPerSec, retainedMb, gen0, gen1);
        }
    }
}
