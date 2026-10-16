using System;
using System.Collections.Generic;
using ColumnWeaver.NET;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Enums;
using ColumnWeaver.NET.Core.Models;
using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET.Examples
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=== ColumnWeaver.NET Examples ===\n");

            Example1_BasicUsage();
            Example2_FormulaWithDependencies();
            Example3_ConditionalColumns();
            Example4_BatchProcessing();
            Example5_FormattingOptions();
            Example6_FluentBuilder();
            Example7_NullHandling();
            Example8_ReplaceValueHelper();
            Example9_VariableStore();
            Example10_CustomFunctionRegister();
        }

        private static void Example1_BasicUsage()
        {
            Console.WriteLine("1. Basic Usage");
            Console.WriteLine("---------------");

            var config = new ParserConfiguration
            {
                GlobalSeparator = "|",
                Columns =
                {
                    new PropertyColumn { Name = "Id",   PropertyPath = "Id",   Order = 1 },
                    new PropertyColumn { Name = "Name", PropertyPath = "Name", Order = 2 },
                    new PropertyColumn { Name = "Email", PropertyPath = "Email", Order = 3 }
                }
            };

            var parser = ColumnWeaverClient.Create(config);

            var person = new { Id = 1, Name = "John Doe", Email = "john@example.com" };
            var result = parser.Parse(person);

            Console.WriteLine($"Input: {person}");
            Console.WriteLine($"Output: {result}\n");
        }

        private static void Example2_FormulaWithDependencies()
        {
            Console.WriteLine("2. Formula with Dependencies");
            Console.WriteLine("----------------------------");

            var config = new ParserConfiguration
            {
                GlobalSeparator = " | ",
                Columns =
                {
                    new PropertyColumn { Name = "Product", PropertyPath = "Product", Order = 1 },
                    new PropertyColumn { Name = "Price",    PropertyPath = "Price",    Order = 2 },
                    new PropertyColumn { Name = "Quantity",PropertyPath = "Qty",      Order = 3 },
                    new FormulaColumn
                    {
                        Name       = "Subtotal",
                        Expression = "${Price} * ${Quantity}",
                        Order      = 4
                    },
                    new FormulaColumn
                    {
                        Name       = "Tax",
                        Expression = "${Subtotal} * 0.1",
                        Order      = 5
                    },
                    new FormulaColumn
                    {
                        Name       = "Total",
                        Expression = "${Subtotal} + ${Tax}",
                        Order      = 6
                    }
                }
            };

            var parser = ColumnWeaverClient.Create(config);

            var order = new { Product = "Widget", Price = 29.99, Qty = 3 };
            var result = parser.Parse(order);

            Console.WriteLine($"Input: {order}");
            Console.WriteLine($"Output: {result}\n");
        }

        private static void Example3_ConditionalColumns()
        {
            Console.WriteLine("3. Conditional Columns");
            Console.WriteLine("-----------------------");

            var config = new ParserConfiguration
            {
                GlobalSeparator = ", ",
                Columns =
                {
                    new PropertyColumn { Name = "Name",     PropertyPath = "Name",     Order = 1 },
                    new PropertyColumn { Name = "Balance", PropertyPath = "Balance", Order = 2 },
                    new ConditionalColumn
                    {
                        Name                = "Status",
                        ConditionExpression = "${Balance} >= 1000",
                        TrueValue           = "VIP",
                        FalseValue          = "Regular",
                        Order               = 3
                    },
                    new ConditionalColumn
                    {
                        Name                = "Tier",
                        ConditionExpression = "${Balance} > 5000",
                        TrueValue           = "Gold",
                        FalseValue          = "${Status}",
                        Order               = 4
                    }
                }
            };

            var parser = ColumnWeaverClient.Create(config);

            var customer1 = new { Name = "Alice",   Balance = 2500 };
            var customer2 = new { Name = "Bob",     Balance = 500  };
            var customer3 = new { Name = "Charlie", Balance = 10000 };

            Console.WriteLine($"Alice:   {parser.Parse(customer1)}");
            Console.WriteLine($"Bob:     {parser.Parse(customer2)}");
            Console.WriteLine($"Charlie: {parser.Parse(customer3)}\n");
        }

        private static void Example4_BatchProcessing()
        {
            Console.WriteLine("4. Batch Processing");
            Console.WriteLine("---------------------");

            var config = new ParserConfiguration
            {
                GlobalSeparator = "|",
                Columns =
                {
                    new PropertyColumn { Name = "Id",    PropertyPath = "Id",    Order = 1 },
                    new PropertyColumn { Name = "Value", PropertyPath = "Value", Order = 2 },
                    new FormulaColumn { Name = "Doubled", Expression = "${Value} * 2", Order = 3 }
                }
            };

            var parser = ColumnWeaverClient.Create(config);

            var items = new[]
            {
                new { Id = 1, Value = 100 },
                new { Id = 2, Value = 200 },
                new { Id = 3, Value = 300 }
            };

            Console.WriteLine("Processing batch:");
            foreach (var line in parser.ParseMany(items))
                Console.WriteLine($"  {line}");

            Console.WriteLine();
        }

        private static void Example5_FormattingOptions()
        {
            Console.WriteLine("5. Formatting Options");
            Console.WriteLine("-----------------------");

            var config = new ParserConfiguration
            {
                GlobalSeparator = " | ",
                Columns =
                {
                    new PropertyColumn
                    {
                        Name        = "Name",
                        PropertyPath = "Name",
                        Formatting  = new FormattingOptions
                        {
                            CasingTransform = CasingTransform.TitleCase,
                            Prefix          = "[",
                            Suffix          = "]"
                        }
                    },
                    new PropertyColumn
                    {
                        Name        = "Amount",
                        PropertyPath = "Amount",
                        Formatting  = new FormattingOptions
                        {
                            FormatString      = "C2",
                            PaddedWidth       = 15,
                            PaddingAlignment  = PaddingAlignment.Right,
                            PaddingChar       = ' '
                        }
                    },
                    new PropertyColumn
                    {
                        Name        = "Date",
                        PropertyPath = "Date",
                        Formatting  = new FormattingOptions
                        {
                            FormatString = "yyyy-MM-dd"
                        }
                    }
                }
            };

            var parser = ColumnWeaverClient.Create(config);

            var record = new
            {
                Name   = "john doe",
                Amount = 1234.56,
                Date   = DateTime.Now
            };

            var result = parser.Parse(record);
            Console.WriteLine($"Output: {result}\n");
        }

        private static void Example6_FluentBuilder()
        {
            Console.WriteLine("6. Fluent Builder API");
            Console.WriteLine("----------------------");

            var config = ParserConfigurationBuilder.Create()
                .WithGlobalSeparator(" | ")
                .WithName("Order Report")
                .AddProperty("Order ID", "Id")
                .AddProperty("Product",  "Product")
                .AddProperty("Price",    "Price")
                .AddProperty("Quantity", "Qty")
                .AddFormula("Subtotal",  "${Price} * ${Quantity}",  f => f.WithFormat("N2"))
                .AddFormula("Tax",       "${Subtotal} * 0.1", f => f.WithFormat("N2"))
                .AddFormula("Total",     "${Subtotal} + ${Tax}", f => f.WithFormat("N2"))
                .AddConditional("Label", "${Quantity} >= 10", "Bulk", "Standard", l => l.WithSeparator(""))
                .Build();

            var parser = ColumnWeaverClient.Create(config);

            var order = new { Id = 42, Product = "Widget", Price = 19.99, Qty = 15 };
            Console.WriteLine($"Output: {parser.Parse(order)}\n");
        }

        private static void Example7_NullHandling()
        {
            Console.WriteLine("7. Null Handling");
            Console.WriteLine("-----------------");

            var config = new ParserConfiguration
            {
                GlobalSeparator = " | ",
                Columns =
                {
                    new PropertyColumn
                    {
                        Name         = "Name",
                        PropertyPath = "Name",
                        NullHandling = new NullHandlingConfig
                        {
                            Strategy    = NullHandlingStrategy.UseDefault,
                            DefaultValue = "(anonymous)"
                        }
                    },
                    new PropertyColumn
                    {
                        Name         = "Nickname",
                        PropertyPath = "Nickname",
                        NullHandling = new NullHandlingConfig
                        {
                            Strategy = NullHandlingStrategy.Skip
                        }
                    },
                    new PropertyColumn
                    {
                        Name         = "Code",
                        PropertyPath = "Code",
                        NullHandling = new NullHandlingConfig
                        {
                            Strategy = NullHandlingStrategy.LiteralNull
                        }
                    }
                }
            };

            var parser = ColumnWeaverClient.Create(config);

            var person = new { Name = "John", Nickname = (string)null, Code = (string)null };
            Console.WriteLine($"With nulls: {parser.Parse(person)}\n");
        }

        // ----------------------------------------------------------------
        //  Example 8 — Replace Value Helper (ColumnWeaver built-in)
        // ----------------------------------------------------------------

        /// <summary>
        /// Demonstrates the Replace function that is pre-registered in the
        /// ColumnWeaver.NET DefaultFunctionRegistry.
        /// This is NOT a generic C# string.Replace — it is invoked through the
        /// CustomFunctionColumn pipeline so the engine resolves it from the
        /// function registry at evaluation time.
        /// </summary>
        private static void Example8_ReplaceValueHelper()
        {
            Console.WriteLine("8. Replace Value Helper (ColumnWeaver built-in)");
            Console.WriteLine("------------------------------------------------");

            // The Replace function is pre-registered in DefaultFunctionRegistry.
            // It receives parameters as STRINGS. To pass a source-object value, first
            // expose it as a PropertyColumn then reference it via ${ColumnName}.
            var config = new ParserConfiguration
            {
                GlobalSeparator = " | ",
                Columns =
                {
                    // Column 1 — expose the raw code as a named column
                    new PropertyColumn { Name = "Code", PropertyPath = "Code", Order = 1 },

                    // Column 2 — Replace spaces with underscores.
                    // ${Code} resolves to the output of the "Code" column above (not a literal).
                    new CustomFunctionColumn
                    {
                        Name         = "Safe Code",
                        FunctionName = "Replace",
                        Parameters   = { "${Code}", " ", "_" },
                        Order        = 2
                    },

                    // Column 3 — Strip the "PROD " prefix using Replace.
                    new CustomFunctionColumn
                    {
                        Name         = "No Prefix",
                        FunctionName = "Replace",
                        Parameters   = { "${Code}", "PROD ", "" },
                        Order        = 3
                    }
                }
            };

            var parser  = ColumnWeaverClient.Create(config);
            var product = new { Code = "PROD Widget Alpha" };
            var result  = parser.Parse(product);

            Console.WriteLine($"Input:  Code = \"{product.Code}\"");
            Console.WriteLine($"Output: {result}");
            Console.WriteLine($"  ↳ Safe Code  : spaces → underscores");
            Console.WriteLine($"  ↳ No Prefix  : 'PROD ' stripped\n");
        }

        // ----------------------------------------------------------------
        //  Example 9 — Variable Store
        // ----------------------------------------------------------------

        /// <summary>
        /// Demonstrates the Variable Store feature.
        /// Variables defined in <see cref="ParserConfiguration.Variables"/> are
        /// injected into the expression context at parse time.  Any FormulaColumn
        /// or ConditionalColumn can reference them by name using ${VariableName}.
        /// Variables are ideal for environment-specific constants (tax rates,
        /// currency codes, thresholds) that should not be hard-coded in expressions.
        /// </summary>
        private static void Example9_VariableStore()
        {
            Console.WriteLine("9. Variable Store");
            Console.WriteLine("-----------------");

            // Variables are defined once at configuration time and injected into
            // every expression context at parse time.
            //
            // IMPORTANT: source-object PROPERTIES  →  referenced by their plain NCalc name
            //             e.g.  UnitPrice, Quantity
            //
            //            COLUMN outputs               →  referenced with ${ColumnName}
            //            VARIABLES in the store      →  also referenced with ${VariableName}
            var config = new ParserConfiguration
            {
                GlobalSeparator = " | ",
                Variables =
                {
                    ["TaxRate"]  = 0.18,   // 18 % GST  — used in Tax Amount formula
                    ["Discount"] = 50.0    // ₹50 flat  — used in Net Total formula
                },
                Columns =
                {
                    new PropertyColumn { Name = "Product", PropertyPath = "Product", Order = 1 },

                    // UnitPrice and Quantity → source object properties (plain NCalc names)
                    // ${TaxRate}            → variable from Variables store
                    new FormulaColumn
                    {
                        Name       = "Tax Amount",
                        Expression = "UnitPrice * Quantity * ${TaxRate}",
                        Formatting = new ColumnWeaver.NET.Core.Models.FormattingOptions { FormatString = "F2" },
                        Order      = 2
                    },

                    // ${Discount} → variable from Variables store
                    new FormulaColumn
                    {
                        Name       = "Net Total",
                        Expression = "(UnitPrice * Quantity) - ${Discount}",
                        Formatting = new ColumnWeaver.NET.Core.Models.FormattingOptions { FormatString = "F2" },
                        Order      = 3
                    },

                    // Plain fixed text for the currency label (separate from variables)
                    new FixedTextColumn { Name = "Ccy", Text = "INR", Order = 4 }
                }
            };

            var parser = ColumnWeaverClient.Create(config);
            var order  = new { Product = "Widget", UnitPrice = 200.0, Quantity = 3 };
            var result = parser.Parse(order);

            Console.WriteLine($"Variables: TaxRate=0.18, Discount=50.0");
            Console.WriteLine($"Input:     Product=Widget, UnitPrice=200, Quantity=3");
            Console.WriteLine($"Output:    {result}");
            Console.WriteLine($"  ↳ Tax Amount = 200 × 3 × 0.18 = 108.00");
            Console.WriteLine($"  ↳ Net Total  = (200 × 3) − 50  = 550.00\n");
        }

        // ----------------------------------------------------------------
        //  Example 10 — Custom Function: Register and Use
        // ----------------------------------------------------------------

        /// <summary>
        /// Demonstrates how to register a custom domain function in the
        /// ColumnWeaver.NET <see cref="Infrastructure.DefaultFunctionRegistry"/>
        /// and invoke it through a <see cref="CustomFunctionColumn"/>.
        ///
        /// The registry is constructed manually here so the registration call is
        /// explicit.  In production the registry is usually composed at the
        /// application root (e.g., <c>Program.cs</c>) and injected.
        /// </summary>
        private static void Example10_CustomFunctionRegister()
        {
            Console.WriteLine("10. Custom Function — Register & Use");
            Console.WriteLine("-------------------------------------");

            // ── Step 1: Build the registry and Register a custom function ──
            var registry = new ColumnWeaver.NET.Infrastructure.DefaultFunctionRegistry();

            //  Register "PadLeft10": pads any string to 10 chars, left-aligned.
            //  Signature: PadLeft10(propertyValue)  →  string (width 10, space-padded)
            registry.Register(
                name:           "PadLeft10",
                handler:        (source, args) =>
                {
                    var value = args.Count > 0 ? args[0] : string.Empty;
                    return value.PadRight(10);   // right-pad → left-aligned 10-char field
                },
                returnType:     typeof(string),
                parameterTypes: new[] { typeof(string) });

            // Register "Classify": returns a rating label based on a numeric score.
            //  Signature: Classify(Score)  →  "High" | "Medium" | "Low"
            //  No parameterTypes → the validator skips strict type-checking for this
            //  function, which is correct when the input column can be any numeric type.
            registry.Register(
                name:    "Classify",
                handler: (source, args) =>
                {
                    if (args.Count == 0) return "Unknown";
                    return double.TryParse(args[0], out double score)
                        ? score >= 80 ? "High" : score >= 50 ? "Medium" : "Low"
                        : "Unknown";
                },
                returnType:     typeof(string),
                parameterTypes: null);   // accepts any scalar — type-checked at runtime

            // ── Step 2: Wire registry into the engine (infrastructure layer) ──
            var resolver       = new ColumnWeaver.NET.Infrastructure.PropertyResolution.ReflectionPropertyResolver();
            var expressionEng  = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.NCalcExpressionEngine(resolver);
            var lambdaEngine   = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.LambdaExpressionEngine(expressionEng);
            var compiledEngine = new ColumnWeaver.NET.Application.Engine.CompiledParserEngine(resolver, lambdaEngine, registry);

            // ── Step 3: Build a configuration that uses the custom functions ──
            // Custom function parameters are resolved strings. Use ${ColumnName} so the
            // engine substitutes the column's output value before invoking the function.
            var config = new ParserConfiguration
            {
                GlobalSeparator = " | ",
                Columns =
                {
                    // Expose source properties as named columns so we can reference them
                    new PropertyColumn { Name = "Candidate", PropertyPath = "Name",  Order = 1 },
                    new PropertyColumn { Name = "ScoreVal",  PropertyPath = "Score", Order = 2 },

                    // ${Candidate} resolves to the output of the "Candidate" column
                    new CustomFunctionColumn
                    {
                        Name         = "Padded Name",
                        FunctionName = "PadLeft10",
                        Parameters   = { "${Candidate}" },
                        Order        = 3
                    },

                    // ${ScoreVal} resolves to the output of the "ScoreVal" column
                    new CustomFunctionColumn
                    {
                        Name         = "Rating",
                        FunctionName = "Classify",
                        Parameters   = { "${ScoreVal}" },
                        Order        = 4
                    }
                }
            };

            // ── Step 4: Parse using the compiled engine directly ───────────
            var candidates = new[]
            {
                new { Name = "Alice", Score = 92.0 },
                new { Name = "Bob",   Score = 63.0 },
                new { Name = "Carol", Score = 41.0 }
            };

            Console.WriteLine("Results:");
            foreach (var c in candidates)
            {
                var result = compiledEngine.Parse(config, c, c.GetType());
                Console.WriteLine($"  {result}");
            }
            Console.WriteLine();
        }
    }
}
