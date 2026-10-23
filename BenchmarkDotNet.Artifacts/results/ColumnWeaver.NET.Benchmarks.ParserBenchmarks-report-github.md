```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.22000.2176/21H2/SunValley)
Intel Core i7-10700F CPU 2.90GHz, 1 CPU, 16 logical and 8 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9277.0), X64 RyuJIT VectorSize=256
  Job-BTNILH : .NET Framework 4.8.1 (4.8.9277.0), X64 RyuJIT VectorSize=256

Runtime=.NET Framework 4.8  IterationCount=6  WarmupCount=3  

```
| Method                                              | Mean                | Error              | StdDev            | Gen0       | Gen1   | Allocated   |
|---------------------------------------------------- |--------------------:|-------------------:|------------------:|-----------:|-------:|------------:|
| &#39;Property Extraction (Hard-coded C#)&#39;               |          95.6793 ns |          8.4901 ns |         2.2048 ns |     0.0395 |      - |       249 B |
| &#39;Property Extraction (Compiled Client)&#39;             |         110.4082 ns |          2.4737 ns |         0.6424 ns |     0.0126 |      - |        80 B |
| &#39;Property Extraction (NCalc / Interpreted)&#39;         |       5,816.6339 ns |        173.4032 ns |        61.8372 ns |     0.9460 |      - |      5978 B |
| &#39;Formula Calculations (Hard-coded C#)&#39;              |         219.5014 ns |          2.2648 ns |         0.5882 ns |     0.0062 |      - |        40 B |
| &#39;Formula Calculations (Compiled Client)&#39;            |         281.4626 ns |          5.0742 ns |         1.8095 ns |     0.0124 |      - |        80 B |
| &#39;Formula Calculations (NCalc / Interpreted)&#39;        |       7,616.8316 ns |        121.4439 ns |        43.3081 ns |     1.3733 | 0.0076 |      8665 B |
| &#39;Conditional Branch (Hard-coded C# Microbenchmark)&#39; |           0.7040 ns |          0.0241 ns |         0.0063 ns |          - |      - |           - |
| &#39;Conditional Columns (Hard-coded C#)&#39;               |          47.5620 ns |          0.7065 ns |         0.2519 ns |     0.0134 |      - |        84 B |
| &#39;Conditional Columns (Compiled Client)&#39;             |          71.6786 ns |          1.2136 ns |         0.3152 ns |     0.0082 |      - |        52 B |
| &#39;Conditional Columns (NCalc / Interpreted)&#39;         |       8,252.3453 ns |        273.9369 ns |        71.1405 ns |     1.3733 |      - |      8702 B |
| &#39;Variables (Hard-coded C#)&#39;                         |         238.1856 ns |         13.7531 ns |         4.9045 ns |     0.0162 |      - |       104 B |
| &#39;Variables (Compiled Client)&#39;                       |         301.3888 ns |          8.8860 ns |         3.1688 ns |     0.0162 |      - |       104 B |
| &#39;Variables (NCalc / Interpreted)&#39;                   |      19,749.9023 ns |        339.2100 ns |        88.0918 ns |     2.7466 |      - |     17403 B |
| &#39;Fixed Text / Labels (Hard-coded C#)&#39;               |          47.5942 ns |          0.6174 ns |         0.2202 ns |     0.0153 |      - |        96 B |
| &#39;Fixed Text / Labels (Compiled Client)&#39;             |          80.0650 ns |          0.7511 ns |         0.1951 ns |     0.0088 |      - |        56 B |
| &#39;Fixed Text / Labels (NCalc / Interpreted)&#39;         |       3,640.7117 ns |        142.9669 ns |        37.1281 ns |     0.7095 |      - |      4469 B |
| &#39;Custom Functions (Hard-coded C#)&#39;                  |          72.1640 ns |          1.1055 ns |         0.2871 ns |     0.0191 |      - |       120 B |
| &#39;Custom Functions (Compiled Client)&#39;                |         189.5685 ns |         15.2691 ns |         5.4451 ns |     0.0241 |      - |       152 B |
| &#39;Custom Functions (NCalc / Interpreted)&#39;            |       8,183.7247 ns |        303.5978 ns |        78.8434 ns |     1.1749 |      - |      7438 B |
| &#39;Null Coalescing (Hard-coded C# Microbenchmark)&#39;    |           0.7187 ns |          0.1113 ns |         0.0397 ns |          - |      - |           - |
| &#39;Null Handling (Hard-coded C#)&#39;                     |          47.8840 ns |          0.3862 ns |         0.1003 ns |     0.0137 |      - |        86 B |
| &#39;Null Handling (Compiled Client)&#39;                   |          70.0242 ns |          2.5162 ns |         0.8973 ns |     0.0086 |      - |        54 B |
| &#39;Null Handling (NCalc / Interpreted)&#39;               |       4,000.4898 ns |        150.7961 ns |        53.7753 ns |     0.7706 |      - |      4868 B |
| &#39;Formatting Options (Hard-coded C#)&#39;                |         114.2190 ns |          3.1245 ns |         0.8114 ns |     0.0331 |      - |       209 B |
| &#39;Formatting Options (Compiled Client)&#39;              |         208.2752 ns |          5.2062 ns |         1.8566 ns |     0.0508 |      - |       321 B |
| &#39;Formatting Options (NCalc / Interpreted)&#39;          |       2,616.3397 ns |         46.1708 ns |        11.9904 ns |     0.6599 |      - |      4164 B |
| &#39;Replacement Rules (Hard-coded C#)&#39;                 |          37.9152 ns |          1.5174 ns |         0.5411 ns |     0.0102 |      - |        64 B |
| &#39;Replacement Rules (Compiled Client)&#39;               |         316.1772 ns |         18.9397 ns |         6.7541 ns |     0.0558 |      - |       353 B |
| &#39;Replacement Rules (NCalc / Interpreted)&#39;           |       2,781.7122 ns |        113.0807 ns |        29.3667 ns |     0.6828 |      - |      4301 B |
| &#39;Disabled Columns (Hard-coded C#)&#39;                  |          47.2768 ns |          2.1778 ns |         0.7766 ns |     0.0153 |      - |        96 B |
| &#39;Disabled Columns (Compiled Client)&#39;                |          73.9230 ns |          2.2705 ns |         0.8097 ns |     0.0088 |      - |        56 B |
| &#39;Disabled Columns (NCalc / Interpreted)&#39;            |       4,047.7718 ns |        215.6925 ns |        76.9180 ns |     0.7782 |      - |      4910 B |
| &#39;Dependency-Heavy (Hard-coded C#)&#39;                  |       1,260.4980 ns |         32.3616 ns |        11.5405 ns |     0.0839 |      - |       530 B |
| &#39;Dependency-Heavy (Compiled Client)&#39;                |       1,275.4453 ns |          4.3092 ns |         1.5367 ns |     0.0477 |      - |       305 B |
| &#39;Dependency-Heavy (NCalc / Interpreted)&#39;            |      42,913.1582 ns |        581.3686 ns |       207.3216 ns |     6.4697 |      - |     40816 B |
| &#39;Batch 10k Items (Hard-coded C#)&#39;                   |   8,131,809.3750 ns |    321,481.2762 ns |   114,643.3199 ns |   734.3750 |      - |   4648404 B |
| &#39;Batch 10k Items (Compiled Client)&#39;                 |   8,491,350.0000 ns |    485,184.2213 ns |   173,021.3671 ns |   312.5000 |      - |   2001312 B |
| &#39;Batch 10k Items (NCalc / Interpreted)&#39;             | 321,509,900.0000 ns | 11,451,800.5479 ns | 4,083,822.3915 ns | 42500.0000 |      - | 269831116 B |
| &#39;Cold Start (Clone &amp; Compile &amp; Parse)&#39;              |     563,945.6868 ns |     10,377.6162 ns |     3,700.7579 ns |     7.8125 | 3.9063 |     53363 B |
| &#39;Steady State (Precompiled Parse)&#39;                  |         107.4457 ns |          2.6309 ns |         0.9382 ns |     0.0126 |      - |        80 B |
| &#39;Configuration Validation (Representative)&#39;         |      16,763.2940 ns |        901.1844 ns |       321.3710 ns |     2.5940 |      - |     16432 B |
