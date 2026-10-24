# Comparative object-to-string benchmarks

This BenchmarkDotNet project compares ColumnWeaver.NET with reusable template or formatter APIs using the same five-field trade record. It includes libraries recommended in earlier discovery answers: Scriban, SmartFormat, Fluid, and Handlebars.Net, plus StringTokenFormatter and TinyString.

## Workload and method

Each implementation produces the same delimited record:

```text
symbol|quantity|unit-price (F2)|side (B/S)|notional (F2)
```

ColumnWeaver uses a precompiled schema with property, conditional, and formula columns. Scriban uses a parsed template with a condition and arithmetic formula. SmartFormat uses a parsed named-placeholder template. Handlebars.Net uses a compiled template and a custom numeric formatting helper. Fluid uses a parsed Liquid template and a custom numeric filter. StringTokenFormatter reuses a parsed template. TinyString applies its fluent `Stringify` configuration at the call site.

The source record exposes derived `Side` and `Notional` properties for the simple formatting APIs. The ColumnWeaver schema and Scriban template calculate these fields in their respective transformation definitions. Setup, parsing, and compilation are excluded from timed methods; output creation and formatting allocations are included. Fluid gets a new `TemplateContext` for each render, as its contexts are not shared across concurrent renders. `--verify` checks exact single-row strings and total output lengths across 10,000 varied rows before timing.

This is a deliberately small repeated-render workload, not a universal ranking. Different input models, template complexity, output escaping, startup costs, concurrency, and application workloads can change the results. The seven APIs are configured to the closest equivalent output; they do not all offer the same transformation language or protocol semantics.

## Results from the latest run

BenchmarkDotNet 0.13.12; Windows 11; .NET 8.0.1 x64 RyuJIT; 10 measurement iterations after 5 warmups. BenchmarkDotNet could not identify the processor on this machine, and it could not change the Windows power plan. Repeat the run on a controlled host before using small differences for decisions.

| Implementation | One record | Relative time | Allocated | 10,000 records | Relative time | Allocated |
|---|---:|---:|---:|---:|---:|---:|
| ColumnWeaver.NET | 250 ns | 1.00x | 128 B | 3.348 ms | 1.00x | 1.50 MB |
| Handlebars.Net 2.4.3 | 706 ns | 2.82x | 200 B | 8.182 ms | 2.44x | 2.45 MB |
| Fluid.Core 2.40.0 | 890 ns | 3.56x | 688 B | 8.993 ms | 2.69x | 7.18 MB |
| Scriban 7.5.3 | 2.327 µs | 9.31x | 520 B | 24.360 ms | 7.28x | 5.65 MB |
| StringTokenFormatter 9.0.1 | 2.997 µs | 11.99x | 5.61 KB | 32.876 ms | 9.82x | 56.53 MB |
| SmartFormat 3.6.1 | 3.316 µs | 13.26x | 1.06 KB | 34.662 ms | 10.35x | 11.02 MB |
| TinyString 1.1.0 | 6.488 µs | 25.96x | 8.06 KB | 67.841 ms | 20.28x | 82.93 MB |

The closest template competitors in this workload are Handlebars.Net and Fluid. Their render times are roughly 2.8–3.6 times ColumnWeaver for one row and 2.4–2.7 times for the batch. This result does not make them worse choices for free-form text: their template syntax, escaping, and user-editable layout are capabilities this structured-record benchmark does not measure. SmartFormat is aimed at named formatting, localization, and pluralization, rather than compiled multi-column transformation pipelines.

## What each library is for

| Library | Strong fit | Why it can be a better choice | Tradeoff for this use case |
|---|---|---|---|
| **ColumnWeaver.NET** | Ordered structured text records from one object model | Schema keeps selection, ordering, conditions, formulas, custom functions, separators, and fixed-width options together; precompiled reuse and batch path | Smaller ecosystem and newer project; AGPL-3.0-or-later may not suit proprietary embedding without a separate agreement |
| **Scriban** | Free-form templates, reports, emails, generated files | Rich scripting/template language, Liquid compatibility mode, extensibility, runtime controls | More general and more expressive than a flat record needs; template code must be safely constrained and validated |
| **SmartFormat** | Named placeholders, lists, pluralization, localization | Small syntax close to `string.Format`; broad format extensions and easy custom extension points | Not a schema-oriented engine for computed columns, dependent fields, or fixed-width records |
| **Handlebars.Net** | Logic-light views and templates | Compiles templates to IL; compile once and cache for reuse; fast rendering in this harness | Templates express output layout, but are not a first-class column schema or transformation pipeline |
| **Fluid** | Liquid templates edited by users | Liquid is deliberately constrained for user-authored templates; per-template member access is configurable | Template-oriented rather than a dedicated tabular transformation schema |
| **CsvHelper** | CSV reading/writing | CSV class maps, type conversion, quoting/escaping, and CSV-specific behavior | Use it for CSV protocol correctness; ColumnWeaver currently does not implement CSV escaping |

### How the leading alternatives are implemented

- **Handlebars.Net** compiles templates into .NET IL and recommends caching the compiled template. Its implementation and compile-once usage are a good match for repeatedly rendered views. In this benchmark it was the fastest external template engine and closest in allocations. [Project documentation](https://github.com/Handlebars-Net/Handlebars.Net) · [2.4.3 release performance notes](https://github.com/Handlebars-Net/Handlebars.Net/releases/tag/2.4.3).
- **Scriban** uses a lexer/parser and AST, then evaluates within a configurable `TemplateContext`. It exposes strong runtime access controls and supports asynchronous rendering and scripting features. This is well engineered for flexible templates, but its broad expressiveness is unnecessary for many record schemas. [Project source and feature overview](https://github.com/scriban/scriban) · [language reference](https://github.com/scriban/scriban/blob/master/site/docs/language.md).
- **SmartFormat** parses format strings into a reusable format representation and resolves named selectors through sources and formatter extensions. Its compact, extensible design is a strength for application messages and localization. [Project source and feature overview](https://github.com/axuno/SmartFormat) · [parser API](https://github.com/axuno/SmartFormat/blob/main/src/SmartFormat/Core/Parsing/Parser.cs).
- **Fluid** implements Liquid and limits which model members templates can access. It was created for scenarios where content editors author templates; this safety and authoring experience can matter more than raw render speed. [Project source](https://github.com/sebastienros/fluid).

These are source-structure observations, not a full security audit or code-quality score. Each project has a different primary goal, so “better code” depends on whether the application needs a scripting language, localization formatter, safe user template, schema compiler, or CSV protocol implementation.

## What makes libraries popular

In the six prompt/answer pairs in the supplied keyword-discovery log, library names appeared in this many answers (each answer counted at most once):

| Rank by appearance | Library | Answers mentioning it |
|---:|---|---:|
| 1 | Scriban | 4 of 6 |
| 2 (tie) | CsvHelper | 3 of 6 |
| 2 (tie) | Fluid | 3 of 6 |
| 4 | Handlebars.Net | 2 of 6 |
| 5 (tie) | Cottle | 1 of 6 |
| 5 (tie) | NLightTemplate | 1 of 6 |
| 5 (tie) | ObjectSemantics.NET | 1 of 6 |

This is mention frequency, not a model preference score: CsvHelper appears in part because several prompts included CSV exports, while it is not the closest choice for a general configurable text pipeline. SmartFormat was not named in these six logged answers, although it appeared in a later answer. Scriban is the clear first place in this sample. Fluid and CsvHelper tie for second by count; for the original general template/formatting use case, Fluid is the more comparable template engine, while CsvHelper is the right tool when CSV compliance is the requirement.

Scriban, SmartFormat, Handlebars.Net, and Fluid also solve broader and more familiar problems than a purpose-built column transformer. They have established user bases, substantial examples, integration packages, recognizable template syntax, and in several cases permissive MIT/BSD licensing. Official NuGet pages show long-running package histories and many dependent packages; those ecosystem signals help discoverability but are not unique-user counts. NuGet download totals can include CI restores and transitive dependencies. Popularity is evidence of discoverability and ecosystem maturity, not proof that one API fits every transformation task.

## Fluent API recommendation

ColumnWeaver.NET already includes `ParserConfigurationBuilder` for code-authored schemas. A second or redesigned fluent API is not required to compete on capability. Keep JSON/XML configuration as the data-driven path and the builder as the C# authoring path. Improve the builder only where onboarding shows friction: stronger compile-time typing, clear validation diagnostics, an end-to-end quick start, and a few complete recipes. Avoid adding fluent calls that only duplicate existing configuration properties.

## License and adoption tradeoff

ColumnWeaver.NET is licensed under AGPL-3.0-or-later and its commercial-license scope and pricing are not published. Scriban is BSD-2-Clause; SmartFormat, Handlebars.Net, and Fluid are MIT-licensed according to their project/package metadata. This licensing gap is a concrete adoption disadvantage for companies embedding the library in closed-source products. It is separate from engineering quality and does not mean the AGPL is unsuitable for all users. Before changing licensing, confirm all rights holders and decide whether to retain AGPL, publish complete dual-license terms, or choose another license. This benchmark does not change the project license or provide legal advice.

## Reproduce

```powershell
dotnet run --project benchmarks/ColumnWeaver.NET.ComparativeBenchmarks/ColumnWeaver.NET.ComparativeBenchmarks.csproj -c Release -- --verify
dotnet run --project benchmarks/ColumnWeaver.NET.ComparativeBenchmarks/ColumnWeaver.NET.ComparativeBenchmarks.csproj -c Release -- --filter '*'
```

Pinned packages are in [`ColumnWeaver.NET.ComparativeBenchmarks.csproj`](ColumnWeaver.NET.ComparativeBenchmarks.csproj). The generated [Markdown report](../../BenchmarkDotNet.Artifacts/results/ColumnWeaver.NET.ComparativeBenchmarks.FormattingBenchmarks-report-github.md), [CSV](../../BenchmarkDotNet.Artifacts/results/ColumnWeaver.NET.ComparativeBenchmarks.FormattingBenchmarks-report.csv), and [HTML report](../../BenchmarkDotNet.Artifacts/results/ColumnWeaver.NET.ComparativeBenchmarks.FormattingBenchmarks-report.html) include the complete run data.
