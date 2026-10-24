```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.22000.2176/21H2/SunValley)
Unknown processor
.NET SDK 8.0.101
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  Job-KWPIDX : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Runtime=.NET 8.0  IterationCount=10  WarmupCount=5

```
| Method                                 | Categories | Mean            | Error           | StdDev          | Ratio | RatioSD | Gen0      | Gen1     | Allocated  | Alloc Ratio |
|--------------------------------------- |----------- |----------------:|----------------:|----------------:|------:|--------:|----------:|---------:|-----------:|------------:|
| &#39;ColumnWeaver ParseMany 10k&#39;           | Batch 10k  |  3,347,760.4 ns |    95,546.88 ns |    56,858.42 ns |  1.00 |    0.00 |  175.7813 |        - |  1501514 B |        1.00 |
| &#39;StringTokenFormatter loop 10k&#39;        | Batch 10k  | 32,876,371.5 ns | 1,326,601.92 ns |   789,439.52 ns |  9.82 |    0.28 | 6750.0000 |        - | 56528662 B |       37.65 |
| &#39;TinyString loop 10k&#39;                  | Batch 10k  | 67,841,450.0 ns | 3,038,824.28 ns | 2,009,994.11 ns | 20.28 |    0.54 | 9666.6667 |        - | 82925573 B |       55.23 |
| &#39;Scriban template loop 10k&#39;            | Batch 10k  | 24,359,964.1 ns |   726,060.76 ns |   480,244.24 ns |  7.28 |    0.17 |  656.2500 |        - |  5645452 B |        3.76 |
| &#39;Handlebars.Net template loop 10k&#39;     | Batch 10k  |  8,181,628.7 ns |   829,869.75 ns |   548,907.45 ns |  2.42 |    0.18 |  289.0625 |        - |  2445440 B |        1.63 |
| &#39;Fluid template loop 10k&#39;              | Batch 10k  |  8,993,255.5 ns |   479,977.17 ns |   317,475.18 ns |  2.68 |    0.10 |  843.7500 |        - |  7180166 B |        4.78 |
| &#39;SmartFormat template loop 10k&#39;        | Batch 10k  | 34,662,199.3 ns | 5,216,597.90 ns | 3,104,313.73 ns | 10.35 |    0.87 | 1312.5000 | 375.0000 | 11019493 B |        7.34 |
|                                        |            |                 |                 |                 |       |         |           |          |            |             |
| &#39;ColumnWeaver precompiled schema&#39;      | Single row |        250.0 ns |         5.85 ns |         3.48 ns |  1.00 |    0.00 |    0.0153 |        - |      128 B |        1.00 |
| &#39;StringTokenFormatter parsed template&#39; | Single row |      2,997.4 ns |       131.88 ns |        87.23 ns | 11.97 |    0.34 |    0.6676 |   0.0038 |     5608 B |       43.81 |
| &#39;TinyString fluent configuration&#39;      | Single row |      6,487.8 ns |        52.61 ns |        34.80 ns | 25.96 |    0.41 |    0.9842 |   0.0076 |     8248 B |       64.44 |
| &#39;Scriban parsed template&#39;              | Single row |      2,327.4 ns |        33.86 ns |        20.15 ns |  9.31 |    0.17 |    0.0610 |        - |      520 B |        4.06 |
| &#39;Handlebars.Net compiled template&#39;     | Single row |        705.7 ns |        18.85 ns |        12.47 ns |  2.82 |    0.07 |    0.0238 |        - |      200 B |        1.56 |
| &#39;Fluid parsed template&#39;                | Single row |        889.8 ns |       103.58 ns |        68.51 ns |  3.51 |    0.20 |    0.0820 |        - |      688 B |        5.38 |
| &#39;SmartFormat parsed template&#39;          | Single row |      3,315.7 ns |       201.26 ns |       133.12 ns | 13.26 |    0.55 |    0.1297 |   0.0381 |     1088 B |        8.50 |
