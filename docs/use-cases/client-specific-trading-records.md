# Client-specific trading record formats in .NET

## The problem

A trading or brokerage application can receive the same execution or order record and still need to emit a different string for each downstream client or product. One destination may need an account reference and notional value; another may require a venue label, a different field order, and a side code. If each variation becomes another branch or copied formatter, format changes require edits and releases to application code.

ColumnWeaver.NET moves those field-layout differences into separate `ParserConfiguration` schemas. The application continues to own the source model, client selection, schema lifecycle, validation, and delivery. The library applies the chosen schema to an object and returns a string.

This page is an illustrative integration pattern. It does not represent a real customer deployment and does not claim compliance with FIX, exchange, broker, drop-copy, or other message specifications. Validate output against the receiver's contract before using it in production.

## Install

```shell
dotnet add package ColumnWeaver.NET --version 1.0.0-beta
```

The library targets .NET Framework 4.8 and .NET Standard 2.0 (usable by modern .NET applications). The WinForms configurator is an optional Windows authoring tool; it is not required by the runtime integration.

## One record, two client schemas

The source record remains independent of destination formatting:

```csharp
public sealed class TradeExecution
{
    public string ClientReference { get; set; }
    public string Account { get; set; }
    public string Symbol { get; set; }
    public bool IsBuy { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
```

Define one schema per output contract. For example, a compact execution row can include a fixed record label and a calculated notional:

```csharp
using ColumnWeaver.NET;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.ParserConfiguration;

var compactClientFormat = new ParserConfiguration
{
    Name = "CompactExecution",
    GlobalSeparator = "|",
    Columns =
    {
        new FixedTextColumn { Name = "RecordType", Text = "EXEC" },
        new PropertyColumn { Name = "Reference", PropertyPath = "ClientReference" },
        new PropertyColumn { Name = "Symbol", PropertyPath = "Symbol" },
        new ConditionalColumn
        {
            Name = "SideCode",
            ConditionExpression = "IsBuy == true",
            TrueValue = "B",
            FalseValue = "S"
        },
        new PropertyColumn { Name = "Quantity", PropertyPath = "Quantity" },
        new PropertyColumn { Name = "Price", PropertyPath = "Price" },
        new FormulaColumn { Name = "Notional", Expression = "Price * Quantity" }
    }
};

var operationsFormat = new ParserConfiguration
{
    Name = "OperationsExecution",
    GlobalSeparator = "|",
    Columns =
    {
        new FixedTextColumn { Name = "RecordType", Text = "TRADE" },
        new PropertyColumn { Name = "Account", PropertyPath = "Account" },
        new PropertyColumn { Name = "Reference", PropertyPath = "ClientReference" },
        new PropertyColumn { Name = "Symbol", PropertyPath = "Symbol" },
        new PropertyColumn { Name = "Quantity", PropertyPath = "Quantity" },
        new PropertyColumn { Name = "Price", PropertyPath = "Price" }
    }
};
```

The same object can be parsed through either schema. Create the clients during application setup and reuse them for records of that type:

```csharp
var compactParser = ColumnWeaverClient.CreatePrecompiled(
    compactClientFormat, typeof(TradeExecution));
var operationsParser = ColumnWeaverClient.CreatePrecompiled(
    operationsFormat, typeof(TradeExecution));

var execution = new TradeExecution
{
    ClientReference = "C-1042",
    Account = "AC-77",
    Symbol = "ABC",
    IsBuy = true,
    Quantity = 100,
    Price = 25.50m
};

string compactRow = compactParser.Parse(execution);
string operationsRow = operationsParser.Parse(execution);
```

The first configuration emits a record label, client reference, symbol, side code, quantity, price, and calculated notional. The second emits a different label, account, reference, symbol, quantity, and price. Column order and content are configuration choices; the source object and application conversion code are shared.

## Choose the schema from application context

The host application knows the destination client and selects its parser. Keep that lookup outside any per-column rule; initialize the parser map when loading the corresponding schemas:

```csharp
var parsersByClient = new Dictionary<string, ColumnWeaverClient>
{
    ["compact"] = compactParser,
    ["operations"] = operationsParser
};

string FormatFor(string clientKey, TradeExecution execution)
{
    return parsersByClient[clientKey].Parse(execution);
}
```

In a real service, handle unknown client keys explicitly, load and validate configuration through the application's configuration process, and associate a schema version with the destination contract. If the configuration changes, create or recompile the parser according to the chosen client lifecycle; frozen/precompiled clients are intended for stable schemas.

## Extend a schema without adding a new formatter

The shared column model supports property extraction, formulas, conditions, custom functions, and fixed text. Columns also support settings such as ordering, enablement, null behavior, formatting, prefixes/suffixes, padding, and ordered replacement rules. A client schema can combine these settings where appropriate. See the [feature and configurator guide](../features.md) for the source-verified option list and current UI limits.

Examples of changes that can be represented as schema changes include:

- Reordering or omitting a field for one destination.
- Adding a fixed record label or a status value selected by a condition.
- Calculating a value from source properties or earlier columns.
- Applying a field-specific null fallback, format string, padding, or replacement rule.
- Registering a consumer-owned custom function for domain-specific formatting.

The library produces the string row. The host application remains responsible for transport, protocol-specific quoting/escaping, message framing, persistence, retries, and delivery. A configured separator alone does not make a result a standards-compliant CSV, FIX message, or broker drop-copy record.

## Performance and production validation

Use the checked-in [BenchmarkDotNet report](../../BenchmarkDotNet.Artifacts/results/ColumnWeaver.NET.Benchmarks.ParserBenchmarks-report-github.md) as workload-specific evidence. It compares the measured schemas with hard-coded C# and an interpreted NCalc path; results are not a latency guarantee for a production feed. For stable schemas, the precompiled client avoids the standard mutable-configuration fingerprint check. Measure the complete application path with representative records and validate tail latency, allocations, configuration reload behavior, and destination contract compatibility before production rollout.

## Search phrases this guide answers

How to format trading records differently for each client in .NET; reusable client-specific object-to-string formatting; avoid separate hard-coded formatters for each broker or product; configurable .NET execution export columns; calculate and conditionally format trade fields; apply multiple customer output schemas to the same C# object; client-specific financial-market data exports.

## Project links

- [GitHub repository](https://github.com/hiteshbhanushali-techmaster/ColumnWeaver.NET)
- [ColumnWeaver.NET on NuGet](https://www.nuget.org/packages/ColumnWeaver.NET/1.0.0-beta)
- [Project overview and configurator demo](https://hiteshbhanushali-techmaster.github.io/ColumnWeaver.NET/)
- [Complete feature reference](../features.md)
