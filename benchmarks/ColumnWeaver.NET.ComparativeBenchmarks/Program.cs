using System.Globalization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using ColumnWeaver.NET;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Enums;
using ColumnWeaver.NET.Core.Models;
using ColumnWeaver.NET.Core.ParserConfiguration;
using Fluid;
using Fluid.Values;
using HandlebarsDotNet;
using Scriban;
using Scriban.Runtime;
using SmartFormat;
using SmartFormat.Core.Parsing;
using StringTokenFormatter;
using TinyString;

namespace ColumnWeaver.NET.ComparativeBenchmarks;

public sealed class TradeRecord
{
    public string Symbol { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public double UnitPrice { get; init; }
    public bool IsBuy { get; init; }
    public string Side => IsBuy ? "B" : "S";
    public double Notional => UnitPrice * Quantity;
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 5, iterationCount: 10)]
public class FormattingBenchmarks
{
    private const string Template = "{Symbol}|{Quantity}|{UnitPrice:F2}|{Side}|{Notional:F2}";
    private TradeRecord _sample = null!;
    private TradeRecord[] _batch10K = null!;
    private ColumnWeaverClient _columnWeaver = null!;
    private InterpolatedStringResolver _tokenResolver = null!;
    private Func<TradeRecord, string> _renderTokenTemplate = null!;
    private Scriban.Template _scribanTemplate = null!;
    private Scriban.TemplateContext _scribanContext = null!;
    private ScriptObject _scribanGlobals = null!;
    private Func<TradeRecord, string> _renderHandlebarsTemplate = null!;
    private IFluidTemplate _fluidTemplate = null!;
    private TemplateOptions _fluidOptions = null!;
    private SmartFormatter _smartFormatter = null!;
    private Format _smartParsedTemplate = null!;

    [GlobalSetup]
    public void Setup()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        _sample = CreateRecord(0);
        _batch10K = Enumerable.Range(0, 10_000).Select(CreateRecord).ToArray();

        var config = new ParserConfiguration
        {
            Name = "ComparableTradeRow",
            GlobalSeparator = "|",
            Columns = new List<ColumnDefinition>
            {
                new PropertyColumn { Name = "Symbol", PropertyPath = "Symbol", Order = 1 },
                new PropertyColumn { Name = "Quantity", PropertyPath = "Quantity", Order = 2 },
                new PropertyColumn
                {
                    Name = "UnitPrice",
                    PropertyPath = "UnitPrice",
                    Order = 3,
                    Formatting = new FormattingOptions { FormatString = "F2" }
                },
                new ConditionalColumn
                {
                    Name = "Side",
                    Order = 4,
                    ConditionExpression = "IsBuy == true",
                    TrueValue = "B",
                    FalseValue = "S"
                },
                new FormulaColumn
                {
                    Name = "Notional",
                    Order = 5,
                    Expression = "UnitPrice * Quantity",
                    Formatting = new FormattingOptions { FormatString = "F2" }
                }
            }
        };
        _columnWeaver = ColumnWeaverClient.CreatePrecompiled(config, typeof(TradeRecord));

        _tokenResolver = new InterpolatedStringResolver(StringTokenFormatterSettings.Default with
        {
            FormatProvider = CultureInfo.InvariantCulture
        });
        var parsedTemplate = _tokenResolver.Interpolate(Template);
        _renderTokenTemplate = row => _tokenResolver.FromObject(parsedTemplate, row);

        _scribanGlobals = new ScriptObject();
        _scribanGlobals.Import(typeof(ScribanFormattingFunctions));
        _scribanGlobals.SetValue("trade", _sample, readOnly: false);
        _scribanContext = new Scriban.TemplateContext();
        _scribanContext.PushGlobal(_scribanGlobals);
        _scribanTemplate = Scriban.Template.Parse(
            "{{ trade.symbol }}|{{ trade.quantity }}|{{ format trade.unit_price \"F2\" }}|{{ if trade.is_buy }}B{{ else }}S{{ end }}|{{ format (trade.unit_price * trade.quantity) \"F2\" }}");
        if (_scribanTemplate.HasErrors)
            throw new InvalidOperationException("Scriban benchmark template failed to parse.");

        var handlebars = Handlebars.Create();
        handlebars.RegisterHelper("format", (output, _, arguments) =>
        {
            var value = Convert.ToDouble(arguments[0], CultureInfo.InvariantCulture);
            var format = Convert.ToString(arguments[1], CultureInfo.InvariantCulture) ?? "F2";
            output.WriteSafeString(value.ToString(format, CultureInfo.InvariantCulture));
        });
        var handlebarsTemplate = handlebars.Compile(
            "{{Symbol}}|{{Quantity}}|{{format UnitPrice \"F2\"}}|{{#if IsBuy}}B{{else}}S{{/if}}|{{format Notional \"F2\"}}");
        _renderHandlebarsTemplate = row => handlebarsTemplate(row);

        var fluidParser = new FluidParser();
        _fluidOptions = new TemplateOptions();
        _fluidOptions.MemberAccessStrategy.Register<TradeRecord>();
        _fluidOptions.Filters.AddFilter("format", (input, arguments, _) =>
        {
            var format = arguments.At(0).ToStringValue();
            var number = input.ToNumberValue();
            return new StringValue(number.ToString(format, CultureInfo.InvariantCulture));
        });
        const string fluidSource = "{{ Symbol }}|{{ Quantity }}|{{ UnitPrice | format: 'F2' }}|{% if IsBuy %}B{% else %}S{% endif %}|{{ Notional | format: 'F2' }}";
        if (!fluidParser.TryParse(fluidSource, out _fluidTemplate, out var fluidError))
            throw new InvalidOperationException($"Fluid benchmark template failed to parse: {fluidError}");

        _smartFormatter = Smart.Default;
        _smartParsedTemplate = _smartFormatter.Parser.ParseFormat(
            "{Symbol}|{Quantity}|{UnitPrice:F2}|{Side}|{Notional:F2}");

        var expected = RenderExpected(_sample);
        AssertEqual("ColumnWeaver", expected, _columnWeaver.Parse(_sample));
        AssertEqual("StringTokenFormatter", expected, _renderTokenTemplate(_sample));
        AssertEqual("TinyString", expected, RenderTinyString(_sample));
        AssertEqual("Scriban", expected, RenderScriban(_sample));
        AssertEqual("Handlebars.Net", expected, _renderHandlebarsTemplate(_sample));
        AssertEqual("Fluid", expected, RenderFluid(_sample));
        AssertEqual("SmartFormat", expected, RenderSmartFormat(_sample));

        var expectedBatchLength = _batch10K.Sum(row => RenderExpected(row).Length);
        AssertEqual("ColumnWeaver batch", expectedBatchLength, RenderColumnWeaverBatch());
        AssertEqual("StringTokenFormatter batch", expectedBatchLength, RenderTokenBatch());
        AssertEqual("TinyString batch", expectedBatchLength, RenderTinyBatch());
        AssertEqual("Scriban batch", expectedBatchLength, RenderScribanBatch());
        AssertEqual("Handlebars.Net batch", expectedBatchLength, RenderHandlebarsBatch());
        AssertEqual("Fluid batch", expectedBatchLength, RenderFluidBatch());
        AssertEqual("SmartFormat batch", expectedBatchLength, RenderSmartFormatBatch());
    }

    [Benchmark(Baseline = true, Description = "ColumnWeaver precompiled schema")]
    [BenchmarkCategory("Single row")]
    public string ColumnWeaver_Single() => _columnWeaver.Parse(_sample);

    [Benchmark(Description = "StringTokenFormatter parsed template")]
    [BenchmarkCategory("Single row")]
    public string StringTokenFormatter_Single() => _renderTokenTemplate(_sample);

    [Benchmark(Description = "TinyString fluent configuration")]
    [BenchmarkCategory("Single row")]
    public string TinyString_Single() => RenderTinyString(_sample);

    [Benchmark(Description = "Scriban parsed template")]
    [BenchmarkCategory("Single row")]
    public string Scriban_Single() => RenderScriban(_sample);

    [Benchmark(Description = "Handlebars.Net compiled template")]
    [BenchmarkCategory("Single row")]
    public string Handlebars_Single() => _renderHandlebarsTemplate(_sample);

    [Benchmark(Description = "Fluid parsed template")]
    [BenchmarkCategory("Single row")]
    public string Fluid_Single() => RenderFluid(_sample);

    [Benchmark(Description = "SmartFormat parsed template")]
    [BenchmarkCategory("Single row")]
    public string SmartFormat_Single() => RenderSmartFormat(_sample);

    [Benchmark(Baseline = true, Description = "ColumnWeaver ParseMany 10k")]
    [BenchmarkCategory("Batch 10k")]
    public int ColumnWeaver_Batch10K() => RenderColumnWeaverBatch();

    [Benchmark(Description = "StringTokenFormatter loop 10k")]
    [BenchmarkCategory("Batch 10k")]
    public int StringTokenFormatter_Batch10K() => RenderTokenBatch();

    [Benchmark(Description = "TinyString loop 10k")]
    [BenchmarkCategory("Batch 10k")]
    public int TinyString_Batch10K() => RenderTinyBatch();

    [Benchmark(Description = "Scriban template loop 10k")]
    [BenchmarkCategory("Batch 10k")]
    public int Scriban_Batch10K() => RenderScribanBatch();

    [Benchmark(Description = "Handlebars.Net template loop 10k")]
    [BenchmarkCategory("Batch 10k")]
    public int Handlebars_Batch10K() => RenderHandlebarsBatch();

    [Benchmark(Description = "Fluid template loop 10k")]
    [BenchmarkCategory("Batch 10k")]
    public int Fluid_Batch10K() => RenderFluidBatch();

    [Benchmark(Description = "SmartFormat template loop 10k")]
    [BenchmarkCategory("Batch 10k")]
    public int SmartFormat_Batch10K() => RenderSmartFormatBatch();

    private static string RenderTinyString(TradeRecord row) => row.Stringify(options => options
        .NoLabel()
        .Separator("|")
        .Only(x => x.Symbol, x => x.Quantity, x => x.UnitPrice, x => x.Side, x => x.Notional)
        .For(x => x.Symbol).NoKey()
        .For(x => x.Quantity).NoKey()
        .For(x => x.UnitPrice).NoKey().NumberFormat("F2")
        .For(x => x.Side).NoKey()
        .For(x => x.Notional).NoKey().NumberFormat("F2")) ?? string.Empty;

    private int RenderColumnWeaverBatch()
    {
        var length = 0;
        foreach (var value in _columnWeaver.ParseMany(_batch10K))
            length += value.Length;
        return length;
    }

    private int RenderTokenBatch()
    {
        var length = 0;
        foreach (var row in _batch10K)
            length += _renderTokenTemplate(row).Length;
        return length;
    }

    private int RenderTinyBatch()
    {
        var length = 0;
        foreach (var row in _batch10K)
            length += RenderTinyString(row).Length;
        return length;
    }

    private string RenderFluid(TradeRecord row) => _fluidTemplate.Render(new Fluid.TemplateContext(row, _fluidOptions));

    private string RenderSmartFormat(TradeRecord row) =>
        _smartFormatter.Format(CultureInfo.InvariantCulture, _smartParsedTemplate, new object[] { row });

    private string RenderScriban(TradeRecord row)
    {
        _scribanGlobals.SetValue("trade", row, readOnly: false);
        return _scribanTemplate.Render(_scribanContext);
    }

    private int RenderScribanBatch()
    {
        var length = 0;
        foreach (var row in _batch10K)
            length += RenderScriban(row).Length;
        return length;
    }

    private int RenderHandlebarsBatch()
    {
        var length = 0;
        foreach (var row in _batch10K)
            length += _renderHandlebarsTemplate(row).Length;
        return length;
    }

    private int RenderFluidBatch()
    {
        var length = 0;
        foreach (var row in _batch10K)
            length += RenderFluid(row).Length;
        return length;
    }

    private int RenderSmartFormatBatch()
    {
        var length = 0;
        foreach (var row in _batch10K)
            length += RenderSmartFormat(row).Length;
        return length;
    }

    private static TradeRecord CreateRecord(int index) => new()
    {
        Symbol = "SYM" + (index % 100).ToString("D2", CultureInfo.InvariantCulture),
        // Keep prices at two-decimal precision and quantities even so midpoint
        // rounding differences between template engines do not contaminate the comparison.
        Quantity = 2 * (1 + (index % 500)),
        UnitPrice = 10.13 + (index % 250) * 0.37,
        IsBuy = index % 2 == 0
    };

    private static string RenderExpected(TradeRecord row) => string.Concat(
        row.Symbol, "|",
        row.Quantity.ToString(CultureInfo.InvariantCulture), "|",
        row.UnitPrice.ToString("F2", CultureInfo.InvariantCulture), "|",
        row.Side, "|",
        row.Notional.ToString("F2", CultureInfo.InvariantCulture));

    private static void AssertEqual(string name, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException($"{name} output differs. Expected '{expected}', got '{actual}'.");
    }

    private static void AssertEqual(string name, int expected, int actual)
    {
        if (expected != actual)
            throw new InvalidOperationException($"{name} output length differs. Expected {expected}, got {actual}.");
    }
}

public static class ScribanFormattingFunctions
{
    public static string Format(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
}

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Contains("--verify", StringComparer.OrdinalIgnoreCase))
        {
            new FormattingBenchmarks().Setup();
            Console.WriteLine("All seven implementations produced identical single-row and 10,000-row outputs.");
            return;
        }

        BenchmarkRunner.Run<FormattingBenchmarks>();
    }
}
