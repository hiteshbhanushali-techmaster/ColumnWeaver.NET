# ColumnWeaver.NET

**Client-specific data formatting without rewriting your application's transformation code.**

[![Target](https://img.shields.io/badge/target-.NET%20Framework%204.8%20%7C%20.NET%20Standard%202.0-blue.svg)](#requirements-building)
[![License: AGPL-3.0-or-later](https://img.shields.io/badge/License-AGPL--3.0--or--later-blue.svg)](#license)
[![NuGet](https://img.shields.io/nuget/v/ColumnWeaver.NET.svg)](https://www.nuget.org/packages/ColumnWeaver.NET/)

**Repository:** [GitHub](https://github.com/hiteshbhanushali-techmaster/ColumnWeaver.NET) · **NuGet:** [ColumnWeaver.NET](https://www.nuget.org/packages/ColumnWeaver.NET/) · **Portfolio page:** [ColumnWeaver.NET release overview](https://hiteshbhanushali-techmaster.github.io/ColumnWeaver.NET/) · **AI-readable project summary:** [llms.txt](https://github.com/hiteshbhanushali-techmaster/ColumnWeaver.NET/blob/main/docs/llms.txt) · [Full AI reference](https://github.com/hiteshbhanushali-techmaster/ColumnWeaver.NET/blob/main/docs/llms-full.txt) · [Source-verified feature and configurator guide](https://github.com/hiteshbhanushali-techmaster/ColumnWeaver.NET/blob/main/docs/features.md) · **Full logo:** [PNG](https://raw.githubusercontent.com/hiteshbhanushali-techmaster/ColumnWeaver.NET/main/Logo.png) · **Compact mark:** [PNG](https://raw.githubusercontent.com/hiteshbhanushali-techmaster/ColumnWeaver.NET/main/docs/assets/columnweaver-mark.png) · **Package icon:** [128×128 PNG](https://raw.githubusercontent.com/hiteshbhanushali-techmaster/ColumnWeaver.NET/main/docs/assets/columnweaver-icon.png)

## Overview

![ColumnWeaver.NET configurator demo: formula, variables, and live JSON parse preview](https://raw.githubusercontent.com/hiteshbhanushali-techmaster/ColumnWeaver.NET/main/docs/assets/configurator-demo.gif)

ColumnWeaver.NET exists to let each client or product define its own output format without forcing the host application to grow another hard-coded formatter. Integrate the library into a .NET Framework 4.8 or modern .NET (.NET 6/8/9+) project, define a reusable formatting configuration, and apply that configuration to source objects through the same transformation engine.

This is especially useful in trading and financial software, where multiple products, integrations, or clients may require different field selection, ordering, labels, calculations, conditions, separators, and value formatting. Instead of copying and rewriting transformation code for every project or customer, the application can reuse ColumnWeaver.NET and supply a different schema for each output contract.

Typical outputs include delimited records, export rows, logs, and product-specific text formats. The library provides the transformation mechanism; the integrating application remains responsible for the surrounding transport, protocol compliance, persistence, and delivery.

### One integration, multiple client formats

The application creates or loads the configuration appropriate for the destination and reuses the same library API:

```csharp
var clientAParser = ColumnWeaverClient.CreatePrecompiled(clientAFormat, typeof(TradeRecord));
var clientBParser = ColumnWeaverClient.CreatePrecompiled(clientBFormat, typeof(TradeRecord));

string clientARow = clientAParser.Parse(trade);
string clientBRow = clientBParser.Parse(trade);
```

The configurations can define different output columns, order, labels, formulas, conditions, separators, and formatting rules. The host application does not need a separate hard-coded formatter for each output layout.

### Client-owned format customization

The shared transformation engine exposes five column types: property, formula, conditional, custom function, and fixed text. Every column type shares settings for name/alias, order, enablement, separator override, null policy, formatting, and ordered replacement rules. A client schema can combine these settings per field and combine fields into one output row. See the [feature and configurator reference](https://github.com/hiteshbhanushali-techmaster/ColumnWeaver.NET/blob/main/docs/features.md) for the complete supported surface, examples, and UI/API distinctions.

The WinForms configurator is an authoring tool rather than a runtime dependency. It edits columns and variables, previews CLR sample objects, tests pasted JSON in a separate sandbox, and opens/saves JSON configurations. The library also exposes JSON and XML serializer APIs. Two API-level settings do not currently have dedicated controls in the configurator: XML file open/save, and preservation of separator slots for disabled columns.

### Current Implementation (V2.5)

- **Target Frameworks:** Multi-targets `.NET Framework 4.8` (`net48`) and `.NET Standard 2.0` (`netstandard2.0`, compatible with .NET 6/8/9+). The WinForms visual configurator runs on Windows/.NET Framework 4.8.
- **Compiled Execution:** `CompiledParserEngine` pre-compiles column pipelines into reusable execution plans. Simple property paths and expressions compile to LINQ expression tree delegates via `LambdaExpressionEngine`.
- **NCalc Fallback:** Complex mathematical, boolean, and custom expressions leverage NCalc 1.3.8 as a reliable runtime fallback without sacrificing configuration flexibility.
- **Cross-Column & Variable Binding:** Formulas and conditions can reference previous columns (`${ColumnName}`) and external variables (`${VariableName}`). Variables are passed as typed parameters into expressions, preserving strings with quotes and special characters, numbers, booleans, and nulls without string corruption.
- **Plan Caching:** Compiled plans are cached using a 64-bit FNV-1a fingerprint derived from configuration content and source type. This is a non-cryptographic cache key, not a collision-proof identity. Mutable configurations are checked for changes on the standard client path; use a frozen/precompiled client when the schema is stable and the hot path must avoid that scan.
- **WinForms Configurator Parity:** The WinForms configurator preview executes the same compiled engine (`CompiledParserEngine` + `LambdaExpressionEngine`) as the public `ColumnWeaverClient`. An isolated dynamic sandbox engine backed by `JObjectPropertyResolver` is provided for ad-hoc JSON testing without CLR classes.

---

## Quick Start

Install the package into a compatible project:

```powershell
dotnet add package ColumnWeaver.NET --version 1.0.0-beta
```

The library supports .NET Framework 4.8 and .NET Standard 2.0 (.NET 6/8/9+). Review the [NuGet package page](https://www.nuget.org/packages/ColumnWeaver.NET/1.0.0-beta) and [license](LICENSE) before integrating it.

```csharp
using ColumnWeaver.NET;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.ParserConfiguration;

// 1. Define your configuration
var config = new ParserConfiguration
{
    Name = "OrderExport",
    GlobalSeparator = "|",
    Variables =
    {
        ["TaxRate"] = 0.18,
        ["Prefix"]  = "ORD"
    },
    Columns =
    {
        new PropertyColumn { Name = "Id",       PropertyPath = "Id" },
        new PropertyColumn { Name = "Customer", PropertyPath = "Customer.Name" },
        new FormulaColumn  { Name = "Gross",    Expression   = "UnitPrice * Quantity" },
        new FormulaColumn  { Name = "Tax",      Expression   = "${Gross} * ${TaxRate}" },
        new ConditionalColumn
        {
            Name                = "Status",
            ConditionExpression = "IsActive == true",
            TrueValue           = "Active",
            FalseValue          = "Inactive"
        }
    }
};

// 2. Create the client (compiles the execution plan)
var client = ColumnWeaverClient.Create(config);

// 3. Parse an object
var sample = new
{
    Id        = 1001,
    Customer  = new { Name = "ACME Corp" },
    UnitPrice = 49.99,
    Quantity  = 3,
    IsActive  = true
};

string result = client.Parse(sample);
// Output: "1001|ACME Corp|149.97|26.9946|Active"
```

---

## Column Types

| Type | Purpose | Expression / Value Syntax |
|---|---|---|
| `PropertyColumn` | Extracts object property values via reflection / cached getter delegates | `PropertyPath = "Customer.Address.City"` |
| `FormulaColumn` | Computes arithmetic or string operations referencing columns or variables | `Expression = "${Gross} * ${TaxRate}"` |
| `ConditionalColumn` | Evaluates a boolean condition and selects true/false branch | `ConditionExpression = "${Total} > 100"`<br>`TrueValue = "Bulk"`<br>`FalseValue = "Standard"` |
| `CustomFunctionColumn` | Invokes custom functions registered with `ICustomFunctionRegistry` | `FunctionName = "MaskCard"`, `Parameters = { "${CardNum}" }` |
| `FixedTextColumn` | Emits a constant string token | `Text = "---"` |

---

## Variable Store

Configurations support an arbitrary dictionary of read-only variables passed to the pipeline:

```csharp
config.Variables["Discount"]     = 15.50;                          // Numeric
config.Variables["IsEligible"]   = true;                           // Boolean
config.Variables["TargetStatus"] = "O'Reilly & \"Sons\" / Active"; // Strings with quotes and symbols
config.Variables["NullVar"]      = null;                           // Null
```

Variables are case-insensitive and can be referenced in formulas and conditions using `${VariableName}`. The engine binds variables as typed parameters, preventing syntax breakage from string interpolation. Variable names colliding with existing column names or aliases are rejected at validation time.

---

## Client Execution Models & Recompilation Contract

ColumnWeaver.NET provides two operational client execution paths:

### 1. Dynamic Mutation Detection (`ColumnWeaverClient.Create`)
The standard client monitors configuration state on every `Parse` invocation using a zero-allocation, 64-bit FNV-1a fingerprint:
- **No fingerprint allocation:** The fingerprint itself allocates no managed objects, but it walks configuration content on each call, so its cost grows with schema size. An unchanged schema reuses the compiled plan rather than recompiling it.
- **Automatic Invalidation:** If columns, ordering, separators, formatting options, or variable values are mutated at runtime, the client automatically detects the change, invalidates the cached plan, and compiles a fresh execution plan on the next parse.

### 2. High-Throughput Precompiled Clients (`CreatePrecompiled` / `Freeze`)
For high-frequency record loops and latency-critical services:
```csharp
// Option A: Factory precompilation with target source type
var client = ColumnWeaverClient.CreatePrecompiled(config, typeof(BenchmarkOrder));

// Option B: Freeze an existing client
var client = ColumnWeaverClient.Create(config);
client.Freeze();
```
- **Bypasses Fingerprinting:** Reuses the compiled specialized row delegate directly, eliminating per-record configuration scanning and cache lookups. Steady-state parse latency drops to **~113 ns**.
- **Explicit Recompilation:** When frozen, callers who intentionally modify their configuration can call `client.Recompile()` to rebuild the plan, or call `config.Snapshot()` to decouple independent configuration instances.

### 3. Safe, Single-Pass Streaming (`ParseMany`)
`ParseMany` consumes `IEnumerable<object?>` using a single forward-only enumerator pass. It processes items on the fly without buffering the collection into memory or enumerating twice, guaranteeing safety for single-use or streaming inputs (such as database readers and network streams).

---

## WinForms Configurator & Architecture

The WinForms application (`ColumnWeaver.NET.UI.exe`) provides interactive editing and testing:

- **Live Preview (`CompiledParserEngine`):** Evaluates real CLR objects (`SampleOrder`) through `CompiledParserEngine` with `LambdaExpressionEngine`, guaranteeing output identical to `ColumnWeaverClient`.
- **JSON Sandbox Test (`ParserEngine`):** Evaluates pasted JSON text using `JObjectPropertyResolver` and the dynamic `ParserEngine`. Because JSON tokens are dynamic rather than CLR types, this mode operates in an isolated dynamic sandbox.

---

## Performance & Reproducible Benchmarks

ColumnWeaver.NET provides a reproducible BenchmarkDotNet suite with per-operation allocation tracking via `MemoryDiagnoser`.

### Benchmark Environment

- **Harness:** BenchmarkDotNet v0.13.12 with `MemoryDiagnoser`
- **OS:** Windows 11 (10.0.22000.2176 / 21H2 / SunValley) 64-bit
- **Processor:** Intel Core i7-10700F CPU @ 2.90GHz (1 CPU, 8 physical cores, 16 logical cores)
- **Runtime Target:** .NET Framework 4.8.1 (4.8.9277.0), X64 RyuJIT VectorSize=256
- **Build Configuration:** Release (`-c Release`)
- **Full BenchmarkDotNet Command:**
  ```bash
  dotnet run --project benchmarks/ColumnWeaver.NET.Benchmarks/ColumnWeaver.NET.Benchmarks.csproj -c Release -- --bdn
  ```
- **Generated Report:** [BenchmarkDotNet summary](https://github.com/hiteshbhanushali-techmaster/ColumnWeaver.NET/blob/main/BenchmarkDotNet.Artifacts/results/ColumnWeaver.NET.Benchmarks.ParserBenchmarks-report-github.md)

### Benchmark Results (BenchmarkDotNet v0.13.12)

All implementations use identical input objects and are asserted in setup to produce 100% equivalent string outputs prior to measurement.

| Feature Scenario | Implementation / Engine | Mean Latency | Error | StdDev | Gen0 / 1k Ops | Allocated / Op |
|:---|:---|---:|---:|---:|---:|---:|
| **1. Property Extraction**<br>*(Flat `Id` + nested `Customer.Name`, `City`)* | Hard-coded C# | 93.66 ns | 3.79 ns | 1.35 ns | 0.0395 | 249 B |
| | **Compiled Client** | **111.61 ns** | **3.42 ns** | **1.22 ns** | **0.0126** | **80 B** |
| | NCalc / Interpreted | 5,689.79 ns | 128.37 ns | 45.78 ns | 0.9460 | 5,978 B |
| **2. Formula Calculations**<br>*(Arithmetic `UnitPrice * Quantity` + "F2")* | Hard-coded C# | 224.52 ns | 8.56 ns | 3.05 ns | 0.0062 | 40 B |
| | **Compiled Client** | **286.64 ns** | **7.82 ns** | **2.79 ns** | **0.0124** | **80 B** |
| | NCalc / Interpreted | 20,095.66 ns | 953.71 ns | 340.10 ns | 1.1597 | 7,463 B |
| **3. Conditional Columns**<br>*(Full row: `Id` + boolean status branch)* | *Hard-coded C# (Micro)* | *0.77 ns* | *0.04 ns* | *0.01 ns* | - | - |
| | Hard-coded C# (Full Row) | 49.14 ns | 2.38 ns | 0.85 ns | 0.0134 | 84 B |
| | **Compiled Client (Full Row)** | **73.30 ns** | **2.76 ns** | **0.99 ns** | **0.0082** | **52 B** |
| | NCalc / Interpreted | 21,098.10 ns | 704.05 ns | 251.07 ns | 1.1902 | 7,581 B |
| **4. Variables (Numeric & String)**<br>*(Formula with `${TaxRate}` + Condition with `${Currency}`)* | Hard-coded C# | 244.14 ns | 9.53 ns | 2.47 ns | 0.0162 | 104 B |
| | **Compiled Client** | **304.85 ns** | **7.26 ns** | **2.59 ns** | **0.0162** | **104 B** |
| | NCalc / Interpreted | 45,489.07 ns | 1,887.41 ns | 490.15 ns | 2.3804 | 15,117 B |
| **5. Fixed Text / Labels**<br>*(Constant labels prepended & appended)* | Hard-coded C# | 48.07 ns | 2.21 ns | 0.79 ns | 0.0153 | 96 B |
| | **Compiled Client** | **80.93 ns** | **1.50 ns** | **0.53 ns** | **0.0088** | **56 B** |
| | NCalc / Interpreted | 3,603.08 ns | 193.74 ns | 69.09 ns | 0.7095 | 4,469 B |
| **6. Custom Functions**<br>*(Function registry delegate invocation `Upper`)* | Hard-coded C# | 71.07 ns | 2.77 ns | 0.99 ns | 0.0191 | 120 B |
| | **Compiled Client** | **181.58 ns** | **8.35 ns** | **2.98 ns** | **0.0241** | **152 B** |
| | NCalc / Interpreted | 7,735.21 ns | 359.17 ns | 128.08 ns | 1.1749 | 7,438 B |
| **7. Null Handling**<br>*(Full row: `Id` + fallback `Notes ?? "(none)"`)* | *Hard-coded C# (Micro)* | *0.77 ns* | *0.07 ns* | *0.02 ns* | - | - |
| | Hard-coded C# (Full Row) | 49.17 ns | 1.99 ns | 0.71 ns | 0.0137 | 86 B |
| | **Compiled Client (Full Row)** | **71.28 ns** | **0.99 ns** | **0.35 ns** | **0.0086** | **54 B** |
| | NCalc / Interpreted | 4,050.12 ns | 157.01 ns | 55.99 ns | 0.7706 | 4,868 B |
| **8. Formatting Options**<br>*(TrimBoth, UpperCase, PadRight, Prefix, Suffix)* | Hard-coded C# | 115.02 ns | 4.47 ns | 1.59 ns | 0.0331 | 209 B |
| | **Compiled Client** | **219.66 ns** | **3.70 ns** | **1.32 ns** | **0.0508** | **321 B** |
| | NCalc / Interpreted | 2,888.13 ns | 311.50 ns | 111.08 ns | 0.6599 | 4,164 B |
| **9. Replacement Rules**<br>*(Sequential string substitution rules)* | Hard-coded C# | 39.45 ns | 2.33 ns | 0.83 ns | 0.0102 | 64 B |
| | **Compiled Client** | **324.06 ns** | **16.89 ns** | **6.02 ns** | **0.0558** | **353 B** |
| | NCalc / Interpreted | 3,009.55 ns | 326.27 ns | 116.35 ns | 0.6828 | 4,301 B |
| **10. Disabled Column Slot**<br>*(`PreserveSeparatorForDisabledColumns = true`)* | Hard-coded C# | 50.20 ns | 1.82 ns | 0.65 ns | 0.0153 | 96 B |
| | **Compiled Client** | **75.90 ns** | **6.35 ns** | **2.27 ns** | **0.0088** | **56 B** |
| | NCalc / Interpreted | 4,649.95 ns | 640.26 ns | 228.32 ns | 0.7782 | 4,910 B |
| **11. Dependency-Heavy Formulas**<br>*(5 chained formulas + conditional tier)* | Hard-coded C# | 1,336.12 ns | 85.18 ns | 30.38 ns | 0.0839 | 530 B |
| | **Compiled Client** | **1,379.05 ns** | **211.58 ns** | **75.45 ns** | **0.0477** | **305 B** |
| | NCalc / Interpreted | 115,883.77 ns | 3,620.16 ns | 940.14 ns | 5.3711 | 34,046 B |
| **12. Batch 10k Items**<br>*(Representative schema across 10,000 items)* | Hard-coded C# | 8,677,470.83 ns | 608,810.71 ns | 217,107.76 ns | 734.3750 | 4,648,404 B |
| | **Compiled Client (`ParseMany`)** | **9,095,104.69 ns** | **596,430.94 ns** | **212,693.02 ns** | **312.5000** | **2,001,312 B** |
| | NCalc / Interpreted | 760,449,133.33 ns | 53,479,700.09 ns | 19,071,376.22 ns | 37000.0000 | 235,795,608 B |
| **13. Lifecycle: Cold Start vs Steady State** | Cold Start (Clone & Compile & Parse) | 587,690.04 ns | 29,419.52 ns | 10,491.28 ns | 7.8125 | 53,342 B |
| | **Steady State (Precompiled Parse)** | **113.46 ns** | **3.99 ns** | **1.42 ns** | **0.0126** | **80 B** |
| **14. Configuration Validation** | Representative Config Validation | 17,246.09 ns | 338.06 ns | 87.79 ns | 2.5940 | 16,432 B |

### Performance Analysis & Trade-offs

- **Where Hard-coded C# is Faster:** Hard-coded C# compiles directly to static machine instructions with ahead-of-time type resolution, avoiding dynamic string parsing and indirect dispatch. For simple micro-operations (e.g. single-field property extracts), native C# is naturally slightly faster.
- **Where ColumnWeaver Excels:** ColumnWeaver enables business analysts and software developers to define, edit, validate, and persist parsing and export schemas at runtime (via JSON/XML or the WinForms UI) without writing, compiling, testing, and redeploying application code.
- **Specialized Whole-Row Compilation (`SpecializedPipelineBuilder`):**
  - Statically compilable pipelines (Property, Formula, Conditional, FixedText, Variables) compile into a single specialized row delegate (`Func<object?, StringBuilder, string>`).
  - Eliminates per-node virtual dispatch, array clearing, value-type boxing, and per-column string allocations on the steady-state hot path.
  - Zero-allocation integer and long formatting is implemented via thread-static char buffers, bypassing .NET Framework 4.8's heap-allocating `StringBuilder.Append(int)` implementation.
  - Property extraction executes in **~111.6 ns** (down from 311.1 ns), with only **80 B** allocation (68% lower allocation than hard-coded C#'s 249 B).
  - Formula calculations execute in **~286.6 ns** (down from 369.8 ns; uncompiled NCalc takes ~20,096 ns — a **70x speedup**).
  - Conditionals evaluate full rows in **~73.3 ns** (allocating 52 B vs 84 B for hard-coded C#; uncompiled NCalc takes ~21,098 ns — a **287x speedup**).
  - Variables achieve **304.8 ns** (down from 641.6 ns; 2.1x speedup) with exact allocation parity to hard-coded C# (104 B).
  - Dependency-heavy chained formulas execute in **~1,379 ns** vs **~1,336 ns** (**1.032x latency ratio, only 3.2% overhead**) while allocating **42% less memory than hard-coded C#** (305 B vs 530 B).
- **Batch Processing Throughput & 4.8% Overhead:**
  - **Batch Latency:** `ParseMany` processes 10,000 items in **9.10 ms** compared to **8.68 ms** for equivalent hard-coded C#. This achieves a **1.048x latency ratio (4.8% overhead)**, handily beating the **≤13% overhead stretch target**.
  - **Batch Memory Efficiency:** `ParseMany` allocates only **2.00 MB** across 10,000 items compared to **4.65 MB** for equivalent hard-coded C# (**57% lower memory footprint**) because it streams output rows through a reused string builder rather than performing intermediate string concatenations.
  - Compared with interpreted NCalc batch processing (760.4 ms and 235.8 MB), ColumnWeaver compiled batching is **83.6x faster** and allocates **99.1% less memory**.
- **Throughput Realities:**
  - In steady-state single-record parsing (`Parse`), precompiled execution takes **~113.5 ns per record** (down from 315 ns) allocating only 80 bytes.
  - In batch parsing (`ParseMany`), throughput exceeds **1,099,000 records/sec per core** (~0.91 µs per record).
  - Cold-start execution plan compilation takes **~588 µs** once per schema.
  - Standard mutable clients compute an allocation-free FNV-1a fingerprint from configuration content when retrieving a plan. This work grows with configuration size; the precompiled/frozen path avoids the per-record configuration scan for stable schemas. FNV-1a is non-cryptographic and has a theoretical collision risk.
- **Limitations:** The suite benchmarks representative features and scenarios. Throughput in production will vary with complex object graphs, deep reflection hierarchies, and external garbage collection load.

---

## Roadmap & Status

| Feature | Status | Notes |
|---|---|---|
| Core column definitions & pipeline | **Completed** | Full support for 5 column types |
| Clean Architecture layers | **Completed** | Core, Application, Infrastructure, UI |
| Expression compilation (`LambdaExpressionEngine`) | **Completed** | LINQ expression trees for property paths |
| NCalc expression engine | **Completed (Fallback)** | Retained as robust runtime expression evaluator |
| Variable Store & safe escaping | **Completed** | Typed parameter binding for string, number, bool, null |
| Deterministic execution plan caching | **Completed** | Automatic cache invalidation on configuration mutation |
| WinForms configurator parity | **Completed** | Live preview aligned with `ColumnWeaverClient` |
| Reproducible benchmark suite | **Completed** | `ColumnWeaver.NET.Benchmarks` (BenchmarkDotNet & MemoryDiagnoser) |
| Fluent configuration builder (`ParserConfigurationBuilder`) | **Completed** | Fluent API for constructing configurations |
| .NET Standard 2.0 & modern .NET multi-targeting | **Completed** | Dual-targets `net48` and `netstandard2.0` (compatible with .NET 6/8/9+) |
| Web UI / Blazor configurator | **Planned (V3)** | Browser-based pipeline designer |
| REST API & AI-Agent integration | **Planned (V3)** | HTTP service for remote parsing pipelines |

---

## Requirements & Building

- **Target Frameworks:** Core library: .NET Framework 4.8 & .NET Standard 2.0 (compatible with .NET 6/8/9+). WinForms UI: .NET Framework 4.8 (Windows).
- **SDK:** .NET 8.0 SDK (version 8.0.100+) or Visual Studio 2022

```bash
# Build the core library and tests
dotnet build ColumnWeaver.NET.sln

# Run unit and regression tests
dotnet test ColumnWeaver.NET.sln

# Build the WinForms UI
dotnet build ColumnWeaver.NET.Win.UI.sln

# Run Quick Profiling mode (Stopwatch loop)
dotnet run --project benchmarks/ColumnWeaver.NET.Benchmarks/ColumnWeaver.NET.Benchmarks.csproj -c Release

# Run Full BenchmarkDotNet harness (with MemoryDiagnoser)
dotnet run --project benchmarks/ColumnWeaver.NET.Benchmarks/ColumnWeaver.NET.Benchmarks.csproj -c Release -- --bdn
```

---

## Project Credits

Concept and project direction: **Hitesh Bhanushali**. AI-assisted development tools: **Codex 6 Luna** and **Antigravity with Gemini 3.8**. The developer reviews and verifies each release; see the release-specific test, build, and benchmark results.

## License

ColumnWeaver.NET is licensed under the **GNU Affero General Public License v3 or any later version** (AGPL-3.0-or-later). See [LICENSE](https://github.com/hiteshbhanushali-techmaster/ColumnWeaver.NET/blob/main/LICENSE) for the SPDX identifier and link to the full license text.

Organizations that need to embed ColumnWeaver.NET in a proprietary or closed-source product may request a separate commercial license from the rights holder by emailing [Hitesh Bhanushali](mailto:hitesh.bhanushali.techmaster@gmail.com). Commercial scope, fees, and support terms are not yet published; until a separate agreement is signed, the AGPL applies. The commercial option does not take away rights already granted under the AGPL. See [COMMERCIAL-LICENSING.md](https://github.com/hiteshbhanushali-techmaster/ColumnWeaver.NET/blob/main/COMMERCIAL-LICENSING.md) for the current policy and important scope notes.

The software license applies to the library code; it does not impose a separate fee or permission requirement on ordinary output strings or records generated by using the library. The output may still be subject to rights based on its content. AGPL copyleft can affect distribution of applications incorporating the library, and the AGPL has extra source-offer requirements for modified versions used over a network. Organizations should review the full license for their architecture.
