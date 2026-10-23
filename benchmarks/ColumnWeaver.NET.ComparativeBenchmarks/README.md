# Comparative object-to-string benchmarks

This BenchmarkDotNet project compares ColumnWeaver.NET, StringTokenFormatter, and TinyString on .NET 8 using one equivalent object-to-string workload.

## Workload

Each implementation renders the same five-field delimited record:

```text
symbol|quantity|unit-price (F2)|side (B/S)|notional (F2)
```

ColumnWeaver uses a precompiled schema with property, conditional, and formula columns. StringTokenFormatter uses a parsed template reused across calls. TinyString uses its fluent `Stringify` configuration at the call site. The source object exposes `Side` and `Notional` getters for the template and fluent APIs; ColumnWeaver computes those fields from `IsBuy`, `UnitPrice`, and `Quantity` inside its configured condition/formula columns. `--verify` checks exact string equality and the aggregate output length over 10,000 varied records before any timing begins.

Schema/template setup is outside the timed methods. Rendering, formatting, and output-string allocations are measured. The batch methods enumerate all 10,000 results and consume their lengths; they do not retain result arrays. TinyString's documented fluent call-site configuration is part of each measured operation.

## Reproduce

```powershell
dotnet run --project benchmarks/ColumnWeaver.NET.ComparativeBenchmarks/ColumnWeaver.NET.ComparativeBenchmarks.csproj -c Release -- --verify
dotnet run --project benchmarks/ColumnWeaver.NET.ComparativeBenchmarks/ColumnWeaver.NET.ComparativeBenchmarks.csproj -c Release -- --filter '*'
```

The run used BenchmarkDotNet 0.13.12 on Windows 11, Intel Core i7-10700F, and .NET 8.0.1 x64 RyuJIT. Both NuGet alternatives were pinned in the project: StringTokenFormatter 9.0.1 and TinyString 1.1.0.

## Results from this run

| Workload | ColumnWeaver | StringTokenFormatter | TinyString |
|---|---:|---:|---:|
| One record | 244.7 ns, 128 B | 3,177.5 ns, 5,608 B (13.00x time) | 6,814.4 ns, 8,632 B (27.84x time) |
| 10,000 records | 3.377 ms, 1.50 MB | 30.890 ms, 56.53 MB (9.15x time) | 67.098 ms, 82.93 MB (19.94x time) |

These figures describe this record shape, runtime, machine, and usage pattern. They are not a claim that ColumnWeaver is faster for every string-formatting task or production workload. The complete generated report is in [`BenchmarkDotNet.Artifacts/results`](../../BenchmarkDotNet.Artifacts/results/ColumnWeaver.NET.ComparativeBenchmarks.FormattingBenchmarks-report-github.md); the implementation and pinned package references are in this directory.
