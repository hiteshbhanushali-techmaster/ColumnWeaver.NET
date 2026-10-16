using System;
using ColumnWeaver.NET.Application.Engine;
using ColumnWeaver.NET.Application.Factory;
using ColumnWeaver.NET.Infrastructure.ExpressionEngine;
using ColumnWeaver.NET.Infrastructure.PropertyResolution;
using ColumnWeaver.NET.Infrastructure.Serialization;
using ColumnWeaver.NET.UI.Demo;
using ColumnWeaver.NET.UI.Forms;
using ColumnWeaver.NET.UI.Services;
using WinApp = System.Windows.Forms.Application;

namespace ColumnWeaver.NET.UI
{
    /// <summary>
    /// Application entry point and composition root.
    /// All dependency wiring is performed here so that Forms and Controls
    /// remain free of infrastructure concerns.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            WinApp.EnableVisualStyles();
            WinApp.SetCompatibleTextRenderingDefault(false);

            // ── Infrastructure layer ─────────────────────────────────────────
            var propertyResolver = new ReflectionPropertyResolver();
            var ncalcEngine      = new NCalcExpressionEngine(propertyResolver);
            var expressionEngine = new LambdaExpressionEngine(ncalcEngine);
            var jsonSerializer   = new JsonConfigurationSerializer();
            var xmlSerializer    = new XmlConfigurationSerializer();

            // ── Application layer ────────────────────────────────────────────
            var functionRegistry = new ColumnWeaver.NET.Infrastructure.DefaultFunctionRegistry();
            var evaluatorFactory = new ColumnEvaluatorFactory(propertyResolver, expressionEngine, functionRegistry);
            var parserEngine     = new ParserEngine(evaluatorFactory);
            var compiledEngine   = new CompiledParserEngine(propertyResolver, expressionEngine, functionRegistry);

            // ── Presentation service ─────────────────────────────────────────
            var parserService = new UIParserService(
                parserEngine, compiledEngine, expressionEngine, jsonSerializer, xmlSerializer, functionRegistry);

            // ── Demo scenario — Variable Store ───────────────────────────────
            // Load the "OrderSummary" config that demonstrates the Variable Store
            // feature (TaxRate, Discount, Currency variables driving formula columns).
            // The config Name = "OrderSummary" so the toolbar reflects it immediately.
            var demoConfig  = DemoConfigurationFactory.CreateWithVariables();
            var sampleOrder = DemoConfigurationFactory.CreateSampleOrder();
            parserService.LoadConfiguration(demoConfig);
            parserService.SetPreviewSource(sampleOrder);

            // ── Launch ───────────────────────────────────────────────────────
            WinApp.Run(new MainForm(parserService));
        }
    }
}
