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

        var expected = RenderExpected(_sample);
        AssertEqual("ColumnWeaver", expected, _columnWeaver.Parse(_sample));
        AssertEqual("StringTokenFormatter", expected, _renderTokenTemplate(_sample));
        AssertEqual("TinyString", expected, RenderTinyString(_sample));

        var expectedBatchLength = _batch10K.Sum(row => RenderExpected(row).Length);
        AssertEqual("ColumnWeaver batch", expectedBatchLength, RenderColumnWeaverBatch());
        AssertEqual("StringTokenFormatter batch", expectedBatchLength, RenderTokenBatch());
        AssertEqual("TinyString batch", expectedBatchLength, RenderTinyBatch());
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

    [Benchmark(Baseline = true, Description = "ColumnWeaver ParseMany 10k")]
    [BenchmarkCategory("Batch 10k")]
    public int ColumnWeaver_Batch10K() => RenderColumnWeaverBatch();

    [Benchmark(Description = "StringTokenFormatter loop 10k")]
    [BenchmarkCategory("Batch 10k")]
    public int StringTokenFormatter_Batch10K() => RenderTokenBatch();

    [Benchmark(Description = "TinyString loop 10k")]
    [BenchmarkCategory("Batch 10k")]
    public int TinyString_Batch10K() => RenderTinyBatch();

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

    private static TradeRecord CreateRecord(int index) => new()
    {
        Symbol = "SYM" + (index % 100).ToString("D2", CultureInfo.InvariantCulture),
        Quantity = 1 + (index % 1_000),
        UnitPrice = 10.125 + (index % 250) * 0.37,
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

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Contains("--verify", StringComparer.OrdinalIgnoreCase))
        {
            new FormattingBenchmarks().Setup();
            Console.WriteLine("All three libraries produced identical single-row and 10,000-row outputs.");
            return;
        }

        BenchmarkRunner.Run<FormattingBenchmarks>();
    }
}
