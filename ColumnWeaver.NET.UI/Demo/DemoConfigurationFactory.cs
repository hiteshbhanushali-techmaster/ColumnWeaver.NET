using System.Collections.Generic;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Models;
using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET.UI.Demo
{
    /// <summary>
    /// Produces a fully populated <see cref="ParserConfiguration"/> and a matching
    /// <see cref="SampleOrder"/> instance that together demonstrate every supported
    /// column type in one parse run.
    /// </summary>
    /// <remarks>
    /// Expected output for the default sample order:
    /// <code>
    ///   ORDER | 1001 | ACME Corp | 149.97 | ACTIVE | Shipped | 2026-04-15
    /// </code>
    ///
    /// Column breakdown:
    /// <list type="table">
    ///   <item><term>ORDER</term><description>FixedTextColumn — literal label</description></item>
    ///   <item><term>1001</term><description>PropertyColumn  — OrderId</description></item>
    ///   <item><term>ACME Corp</term><description>PropertyColumn  — Customer.Name (nested dot-path)</description></item>
    ///   <item><term>149.97</term><description>FormulaColumn   — UnitPrice * Quantity, formatted F2</description></item>
    ///   <item><term>ACTIVE</term><description>ConditionalColumn — IsActive ? "ACTIVE" : "INACTIVE"</description></item>
    ///   <item><term>Shipped</term><description>PropertyColumn  — Status</description></item>
    ///   <item><term>2026-04-15</term><description>PropertyColumn  — OrderDate, formatted yyyy-MM-dd</description></item>
    /// </list>
    /// </remarks>
    public static class DemoConfigurationFactory
    {
        // -----------------------------------------------------------------
        //  Public factory methods
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates the demo <see cref="ParserConfiguration"/> with 7 columns
        /// covering all supported column types.
        /// </summary>
        public static ParserConfiguration Create()
        {
            return new ParserConfiguration
            {
                Name            = "Order Summary",
                GlobalSeparator = " | ",
                Columns         = new List<ColumnDefinition>
                {
                    // ── Column 0 — FixedTextColumn ────────────────────────
                    new FixedTextColumn
                    {
                        Name      = "Label",
                        Order     = 0,
                        IsEnabled = true,
                        Text      = "ORDER"
                    },

                    // ── Column 1 — PropertyColumn (direct property) ───────
                    new PropertyColumn
                    {
                        Name         = "Order ID",
                        Order        = 1,
                        IsEnabled    = true,
                        PropertyPath = "OrderId"
                    },

                    // ── Column 2 — PropertyColumn (nested dot-path) ───────
                    new PropertyColumn
                    {
                        Name         = "Customer",
                        Order        = 2,
                        IsEnabled    = true,
                        PropertyPath = "Customer.Name"
                    },

                    // ── Column 3 — FormulaColumn ──────────────────────────
                    new FormulaColumn
                    {
                        Name       = "Total",
                        Order      = 3,
                        IsEnabled  = true,
                        Expression = "UnitPrice * Quantity",
                        Formatting = new FormattingOptions { FormatString = "F2" }
                    },

                    // ── Column 4 — ConditionalColumn ──────────────────────
                    new ConditionalColumn
                    {
                        Name                = "Status Flag",
                        Order               = 4,
                        IsEnabled           = true,
                        ConditionExpression = "IsActive",
                        TrueValue           = "ACTIVE",
                        FalseValue          = "INACTIVE"
                    },

                    // ── Column 5 — PropertyColumn (lifecycle status) ──────
                    new PropertyColumn
                    {
                        Name         = "Delivery Status",
                        Order        = 5,
                        IsEnabled    = true,
                        PropertyPath = "Status"
                    },

                    // ── Column 6 — PropertyColumn with date formatting ────
                    new PropertyColumn
                    {
                        Name         = "Order Date",
                        Order        = 6,
                        IsEnabled    = true,
                        PropertyPath = "OrderDate",
                        Formatting   = new FormattingOptions { FormatString = "yyyy-MM-dd" }
                    }
                }
            };
        }

        /// <summary>
        /// Creates the matching <see cref="SampleOrder"/> whose properties
        /// are referenced by columns in <see cref="Create"/>.
        /// </summary>
        public static SampleOrder CreateSampleOrder()
        {
            return new SampleOrder
            {
                OrderId   = 1001,
                Status    = "Shipped",
                IsActive  = true,
                UnitPrice = 49.99,
                Quantity  = 3,
                OrderDate = new System.DateTime(2026, 4, 15),
                Customer  = new SampleCustomer
                {
                    Name = "ACME Corp",
                    City = "New York"
                }
            };
        }

        // -----------------------------------------------------------------
        //  Helpers — default JSON for the test-input panel
        // -----------------------------------------------------------------

        /// <summary>
        /// Returns a JSON string that mirrors <see cref="CreateSampleOrder"/>
        /// so that the test-input panel is pre-populated with realistic data.
        /// </summary>
        public static string SampleOrderJson =>
            "{\n" +
            "  \"OrderId\": 1001,\n" +
            "  \"Status\": \"Shipped\",\n" +
            "  \"IsActive\": true,\n" +
            "  \"UnitPrice\": 49.99,\n" +
            "  \"Quantity\": 3,\n" +
            "  \"OrderDate\": \"2026-04-15\",\n" +
            "  \"Customer\": {\n" +
            "    \"Name\": \"ACME Corp\",\n" +
            "    \"City\": \"New York\"\n" +
            "  }\n" +
            "}";

        // -----------------------------------------------------------------
        //  Variable-Store demo — factory methods
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates an extended <see cref="ParserConfiguration"/> that demonstrates
        /// the Variable Store feature of ColumnWeaver.NET.
        /// <para>
        /// Three variables are stored in <see cref="ParserConfiguration.Variables"/>:
        /// <list type="bullet">
        ///   <item><c>TaxRate = 0.18</c>  — used in a FormulaColumn expression.</item>
        ///   <item><c>Discount = 50.0</c> — flat discount applied in Net Total formula.</item>
        ///   <item><c>Currency = "INR"</c> — informational string variable.</item>
        /// </list>
        /// </para>
        /// </summary>
        public static ParserConfiguration CreateWithVariables()
        {
            return new ParserConfiguration
            {
                Name            = "OrderSummary",
                GlobalSeparator = " | ",

                // ── Variable Store ────────────────────────────────────────
                // These key-value pairs are injected into every expression
                // context at parse time and can be referenced with ${Name}.
                Variables = new System.Collections.Generic.Dictionary<string, object?>(
                    System.StringComparer.OrdinalIgnoreCase)
                {
                    ["TaxRate"]  = 0.18,    // 18 % GST — referenced in Tax Amount column
                    ["Discount"] = 50.0,    // ₹50 flat discount — referenced in Net Total
                    ["Currency"] = "INR"    // string variable for display
                },

                Columns = new System.Collections.Generic.List<ColumnWeaver.NET.Core.Columns.ColumnDefinition>
                {
                    // ── Column 0 — Fixed label ────────────────────────────
                    new ColumnWeaver.NET.Core.Columns.FixedTextColumn
                    {
                        Name      = "Label",
                        Order     = 0,
                        IsEnabled = true,
                        Text      = "ORDER"
                    },

                    // ── Column 1 — Order ID ───────────────────────────────
                    new ColumnWeaver.NET.Core.Columns.PropertyColumn
                    {
                        Name         = "Order ID",
                        Order        = 1,
                        IsEnabled    = true,
                        PropertyPath = "OrderId"
                    },

                    // ── Column 2 — Customer name (nested) ────────────────
                    new ColumnWeaver.NET.Core.Columns.PropertyColumn
                    {
                        Name         = "Customer",
                        Order        = 2,
                        IsEnabled    = true,
                        PropertyPath = "Customer.Name"
                    },

                    // ── Column 3 — Gross Total (UnitPrice × Quantity) ────
                    new ColumnWeaver.NET.Core.Columns.FormulaColumn
                    {
                        Name       = "Gross Total",
                        Order      = 3,
                        IsEnabled  = true,
                        Expression = "UnitPrice * Quantity",
                        Formatting = new ColumnWeaver.NET.Core.Models.FormattingOptions { FormatString = "F2" }
                    },

                    // ── Column 4 — Tax Amount: uses ${TaxRate} variable ───
                    new ColumnWeaver.NET.Core.Columns.FormulaColumn
                    {
                        Name       = "Tax Amount",
                        Order      = 4,
                        IsEnabled  = true,
                        Expression = "UnitPrice * Quantity * ${TaxRate}",
                        Formatting = new ColumnWeaver.NET.Core.Models.FormattingOptions { FormatString = "F2" }
                    },

                    // ── Column 5 — Net Total: subtracts ${Discount} ──────
                    new ColumnWeaver.NET.Core.Columns.FormulaColumn
                    {
                        Name       = "Net Total",
                        Order      = 5,
                        IsEnabled  = true,
                        Expression = "(UnitPrice * Quantity) - ${Discount}",
                        Formatting = new ColumnWeaver.NET.Core.Models.FormattingOptions { FormatString = "F2" }
                    },

                    // ── Column 6 — Status flag (ConditionalColumn) ───────
                    new ColumnWeaver.NET.Core.Columns.ConditionalColumn
                    {
                        Name                = "Status Flag",
                        Order               = 6,
                        IsEnabled           = true,
                        ConditionExpression = "IsActive",
                        TrueValue           = "ACTIVE",
                        FalseValue          = "INACTIVE"
                    },

                    // ── Column 7 — Delivery status ────────────────────────
                    new ColumnWeaver.NET.Core.Columns.PropertyColumn
                    {
                        Name         = "Delivery Status",
                        Order        = 7,
                        IsEnabled    = true,
                        PropertyPath = "Status"
                    },

                    // ── Column 8 — Order date with formatting ─────────────
                    new ColumnWeaver.NET.Core.Columns.PropertyColumn
                    {
                        Name         = "Order Date",
                        Order        = 8,
                        IsEnabled    = true,
                        PropertyPath = "OrderDate",
                        Formatting   = new ColumnWeaver.NET.Core.Models.FormattingOptions { FormatString = "yyyy-MM-dd" }
                    }
                }
            };
        }

        /// <summary>
        /// Returns the JSON string for the test-input panel matching
        /// <see cref="CreateWithVariables"/> / <see cref="CreateSampleOrder"/>.
        /// </summary>
        public static string SampleOrderWithVariablesJson =>
            "{\n" +
            "  \"OrderId\": 1001,\n" +
            "  \"Status\": \"Shipped\",\n" +
            "  \"IsActive\": true,\n" +
            "  \"UnitPrice\": 49.99,\n" +
            "  \"Quantity\": 3,\n" +
            "  \"OrderDate\": \"2026-04-15\",\n" +
            "  \"Customer\": {\n" +
            "    \"Name\": \"ACME Corp\",\n" +
            "    \"City\": \"New York\"\n" +
            "  }\n" +
            "}";
    }
}
