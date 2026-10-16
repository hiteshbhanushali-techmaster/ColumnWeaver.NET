```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.22000.2176/21H2/SunValley)
Intel Core i7-10700F CPU 2.90GHz, 1 CPU, 16 logical and 8 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9277.0), X64 RyuJIT VectorSize=256
  Job-BTNILH : .NET Framework 4.8.1 (4.8.9277.0), X64 RyuJIT VectorSize=256

Runtime=.NET Framework 4.8  IterationCount=6  WarmupCount=3  

```
| Method                                              | Mean                | Error              | StdDev             | Gen0       | Gen1   | Allocated   |
|---------------------------------------------------- |--------------------:|-------------------:|-------------------:|-----------:|-------:|------------:|
| &#39;Property Extraction (Hard-coded C#)&#39;               |          93.6565 ns |          3.7879 ns |          1.3508 ns |     0.0395 |      - |       249 B |
| &#39;Property Extraction (Compiled Client)&#39;             |         111.6139 ns |          3.4154 ns |          1.2180 ns |     0.0126 |      - |        80 B |
| &#39;Property Extraction (NCalc / Interpreted)&#39;         |       5,689.7938 ns |        128.3661 ns |         45.7766 ns |     0.9460 |      - |      5978 B |
| &#39;Formula Calculations (Hard-coded C#)&#39;              |         224.5192 ns |          8.5627 ns |          3.0535 ns |     0.0062 |      - |        40 B |
| &#39;Formula Calculations (Compiled Client)&#39;            |         286.6414 ns |          7.8182 ns |          2.7881 ns |     0.0124 |      - |        80 B |
| &#39;Formula Calculations (NCalc / Interpreted)&#39;        |      20,095.6558 ns |        953.7130 ns |        340.1033 ns |     1.1597 |      - |      7463 B |
| &#39;Conditional Branch (Hard-coded C# Microbenchmark)&#39; |           0.7662 ns |          0.0387 ns |          0.0138 ns |          - |      - |           - |
| &#39;Conditional Columns (Hard-coded C#)&#39;               |          49.1405 ns |          2.3845 ns |          0.8504 ns |     0.0134 |      - |        84 B |
| &#39;Conditional Columns (Compiled Client)&#39;             |          73.3046 ns |          2.7642 ns |          0.9857 ns |     0.0082 |      - |        52 B |
| &#39;Conditional Columns (NCalc / Interpreted)&#39;         |      21,098.0967 ns |        704.0523 ns |        251.0718 ns |     1.1902 |      - |      7581 B |
| &#39;Variables (Hard-coded C#)&#39;                         |         244.1436 ns |          9.5250 ns |          2.4736 ns |     0.0162 |      - |       104 B |
| &#39;Variables (Compiled Client)&#39;                       |         304.8455 ns |          7.2553 ns |          2.5873 ns |     0.0162 |      - |       104 B |
| &#39;Variables (NCalc / Interpreted)&#39;                   |      45,489.0698 ns |      1,887.4057 ns |        490.1533 ns |     2.3804 |      - |     15117 B |
| &#39;Fixed Text / Labels (Hard-coded C#)&#39;               |          48.0717 ns |          2.2134 ns |          0.7893 ns |     0.0153 |      - |        96 B |
| &#39;Fixed Text / Labels (Compiled Client)&#39;             |          80.9302 ns |          1.4976 ns |          0.5341 ns |     0.0088 |      - |        56 B |
| &#39;Fixed Text / Labels (NCalc / Interpreted)&#39;         |       3,603.0844 ns |        193.7419 ns |         69.0902 ns |     0.7095 |      - |      4469 B |
| &#39;Custom Functions (Hard-coded C#)&#39;                  |          71.0735 ns |          2.7660 ns |          0.9864 ns |     0.0191 |      - |       120 B |
| &#39;Custom Functions (Compiled Client)&#39;                |         181.5830 ns |          8.3453 ns |          2.9760 ns |     0.0241 |      - |       152 B |
| &#39;Custom Functions (NCalc / Interpreted)&#39;            |       7,735.2089 ns |        359.1658 ns |        128.0820 ns |     1.1749 |      - |      7438 B |
| &#39;Null Coalescing (Hard-coded C# Microbenchmark)&#39;    |           0.7741 ns |          0.0742 ns |          0.0193 ns |          - |      - |           - |
| &#39;Null Handling (Hard-coded C#)&#39;                     |          49.1746 ns |          1.9942 ns |          0.7111 ns |     0.0137 |      - |        86 B |
| &#39;Null Handling (Compiled Client)&#39;                   |          71.2821 ns |          0.9912 ns |          0.3535 ns |     0.0086 |      - |        54 B |
| &#39;Null Handling (NCalc / Interpreted)&#39;               |       4,050.1225 ns |        157.0068 ns |         55.9902 ns |     0.7706 |      - |      4868 B |
| &#39;Formatting Options (Hard-coded C#)&#39;                |         115.0167 ns |          4.4709 ns |          1.5944 ns |     0.0331 |      - |       209 B |
| &#39;Formatting Options (Compiled Client)&#39;              |         219.6595 ns |          3.7016 ns |          1.3200 ns |     0.0508 |      - |       321 B |
| &#39;Formatting Options (NCalc / Interpreted)&#39;          |       2,888.1348 ns |        311.4960 ns |        111.0825 ns |     0.6599 |      - |      4164 B |
| &#39;Replacement Rules (Hard-coded C#)&#39;                 |          39.4512 ns |          2.3269 ns |          0.8298 ns |     0.0102 |      - |        64 B |
| &#39;Replacement Rules (Compiled Client)&#39;               |         324.0612 ns |         16.8864 ns |          6.0219 ns |     0.0558 |      - |       353 B |
| &#39;Replacement Rules (NCalc / Interpreted)&#39;           |       3,009.5460 ns |        326.2714 ns |        116.3515 ns |     0.6828 |      - |      4301 B |
| &#39;Disabled Columns (Hard-coded C#)&#39;                  |          50.2039 ns |          1.8153 ns |          0.6474 ns |     0.0153 |      - |        96 B |
| &#39;Disabled Columns (Compiled Client)&#39;                |          75.9030 ns |          6.3535 ns |          2.2657 ns |     0.0088 |      - |        56 B |
| &#39;Disabled Columns (NCalc / Interpreted)&#39;            |       4,649.9490 ns |        640.2568 ns |        228.3218 ns |     0.7782 |      - |      4910 B |
| &#39;Dependency-Heavy (Hard-coded C#)&#39;                  |       1,336.1204 ns |         85.1793 ns |         30.3758 ns |     0.0839 |      - |       530 B |
| &#39;Dependency-Heavy (Compiled Client)&#39;                |       1,379.0480 ns |        211.5787 ns |         75.4510 ns |     0.0477 |      - |       305 B |
| &#39;Dependency-Heavy (NCalc / Interpreted)&#39;            |     115,883.7744 ns |      3,620.1627 ns |        940.1448 ns |     5.3711 |      - |     34046 B |
| &#39;Batch 10k Items (Hard-coded C#)&#39;                   |   8,677,470.8333 ns |    608,810.7057 ns |    217,107.7624 ns |   734.3750 |      - |   4648404 B |
| &#39;Batch 10k Items (Compiled Client)&#39;                 |   9,095,104.6875 ns |    596,430.9354 ns |    212,693.0170 ns |   312.5000 |      - |   2001312 B |
| &#39;Batch 10k Items (NCalc / Interpreted)&#39;             | 760,449,133.3333 ns | 53,479,700.0863 ns | 19,071,376.2249 ns | 37000.0000 |      - | 235795608 B |
| &#39;Cold Start (Clone &amp; Compile &amp; Parse)&#39;              |     587,690.0391 ns |     29,419.5192 ns |     10,491.2840 ns |     7.8125 | 3.9063 |     53342 B |
| &#39;Steady State (Precompiled Parse)&#39;                  |         113.4632 ns |          3.9882 ns |          1.4222 ns |     0.0126 |      - |        80 B |
| &#39;Configuration Validation (Representative)&#39;         |      17,246.0876 ns |        338.0550 ns |         87.7918 ns |     2.5940 |      - |     16432 B |
