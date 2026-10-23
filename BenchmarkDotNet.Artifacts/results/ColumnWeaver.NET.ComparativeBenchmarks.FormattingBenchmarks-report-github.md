```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.22000.2176/21H2/SunValley)
Intel Core i7-10700F CPU 2.90GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 8.0.101
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-WMRRIW : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Runtime=.NET 8.0  IterationCount=10  WarmupCount=5

```
| Method                                 | Categories | Mean            | Error           | StdDev          | Ratio | RatioSD | Gen0      | Gen1    | Allocated  | Alloc Ratio |
|--------------------------------------- |----------- |----------------:|----------------:|----------------:|------:|--------:|----------:|--------:|-----------:|------------:|
| &#39;ColumnWeaver ParseMany 10k&#39;           | Batch 10k  |  3,376,841.4 ns |    39,027.40 ns |    23,224.58 ns |  1.00 |    0.00 |  175.7813 |       - |  1502234 B |        1.00 |
| &#39;StringTokenFormatter loop 10k&#39;        | Batch 10k  | 30,889,594.4 ns |   655,743.48 ns |   390,222.43 ns |  9.15 |    0.15 | 6750.0000 | 31.2500 | 56529053 B |       37.63 |
| &#39;TinyString loop 10k&#39;                  | Batch 10k  | 67,097,613.3 ns | 3,626,831.06 ns | 2,398,924.19 ns | 19.94 |    0.62 | 9666.6667 |       - | 82925973 B |       55.20 |
|                                        |            |                 |                 |                 |       |         |           |         |            |             |
| &#39;ColumnWeaver precompiled schema&#39;      | Single row |        244.7 ns |         0.71 ns |         0.42 ns |  1.00 |    0.00 |    0.0153 |       - |      128 B |        1.00 |
| &#39;StringTokenFormatter parsed template&#39; | Single row |      3,177.5 ns |       116.23 ns |        76.88 ns | 13.00 |    0.32 |    0.6676 |  0.0038 |     5608 B |       43.81 |
| &#39;TinyString fluent configuration&#39;      | Single row |      6,814.4 ns |       119.56 ns |        71.15 ns | 27.84 |    0.31 |    1.0071 |       - |     8632 B |       67.44 |
