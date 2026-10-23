# ColumnWeaver.NET configuration and feature reference

## Product purpose

ColumnWeaver.NET lets a host application reuse one object-to-string transformation engine while each client, product, or integration supplies its own output schema. It is intended to replace copied and rewritten format-specific conversion code, including in trading and financial-market applications that emit client-specific records. It produces strings; the host application owns transport, protocol compliance, persistence, and delivery.

## Configuration surface verified in source

| Area | Available choices |
|---|---|
| Column kinds | Property, Formula, Conditional, Custom Function, Fixed Text |
| Property extraction | Flat and dotted nested paths; resolver-dependent dictionary/index access |
| Output layout | Explicit column order; auto-sort or insertion order; global separator; per-column separator override; enabled/disabled columns |
| Column identity | Name and optional alias |
| Null policy per column | Empty string, configured default, skip, throw with optional message, literal `NULL` |
| Value format | .NET format string; no casing, upper, lower, title, sentence; trim start/end/both; max length; prefix/suffix; left/right/center padding with configurable width and character |
| Text replacements | Ordered literal or regular-expression substitutions; case-sensitive or insensitive |
| Expressions | Formula and condition expressions; references to earlier columns and typed configuration variables; exact expression support depends on the selected expression engine and compiled/fallback path |
| Custom logic | Named functions from a consumer-provided function registry, with ordered configured parameters |
| Configuration variables | Case-insensitive keys; object values including strings, numeric, boolean and null values |
| Configuration files | JSON and XML serialization/deserialization; WinForms configurator currently provides JSON open/save |
| Runtime | Standard client with mutation detection; frozen/precompiled client for stable schemas; `ParseMany` for forward-only enumerables |
| Validation and authoring | Configuration validation; WinForms column editors, variables editor, test JSON input, compiled CLR-object live preview, undo/redo, copy/paste, reorder, and JSON save/open |

## Combinations and semantics

Common per-column properties are inherited by all five column types. Therefore a property, formula, conditional, custom-function, or fixed-text result can be enabled or disabled, ordered, aliased, given a separator override, assigned a null policy, formatted, and passed through ordered replacement rules. These settings compose in the pipeline; they do not imply that every cross-product has a dedicated benchmark or regression test.

Useful schema patterns include:

- Delimited client export: select/reorder properties, set a global delimiter, and override a separator for exceptional fields.
- Fixed-width record: set a format string as appropriate, then trim, cap length, and pad each field with a chosen character and alignment.
- Financial output: calculate amounts, reference earlier calculated columns and typed rate/currency variables, use culture-aware numeric/date formats, and branch on conditions.
- Client-specific labels/status codes: combine fixed-text columns, conditional branches, aliases, and replacement rules.
- Optional fields: choose an explicit null strategy and, when using the API, preserve separator slots for disabled columns if required.
- Consumer-specific rules: register custom functions for domain logic that is not expressed by built-in formulas and conditions.

## UI scope and known authoring gap

The WinForms configurator provides common per-column property, null-handling, formatting, and replacement tabs; type-specific editors; variables; preview/testing; and JSON open/save. JSON and XML are both supported by the library serializer APIs, but XML open/save is not exposed by the current UI toolbar. `ParserConfiguration.PreserveSeparatorForDisabledColumns` is supported by the core model/API but has no current configurator control. Set these through code or serialized configuration. `AutoSortByOrder` is also a configuration/API setting; the UI reorders columns directly.

Formula syntax is expression-engine-specific. Do not assume every NCalc function or expression compiles to the specialized fast path. Custom-function availability depends on the application's registered functions. JSON sandbox preview uses dynamic JSON input, while the live preview uses a CLR sample object and the compiled engine.

## Package discovery terms

Search phrases: ColumnWeaver.NET NuGet, client-specific formatting, configurable object-to-string transformation, customer-specific data export, .NET Framework 4.8 delimited export, .NET Standard 2.0 string formatter, fixed-width record formatter, financial/trading data transformation, runtime-configurable output schema, JSON/XML transformation configuration, conditional column mapping, formula-based export, streaming object-to-string batch.

The published package is [`ColumnWeaver.NET` version `1.0.1-beta`](https://www.nuget.org/packages/ColumnWeaver.NET/1.0.1-beta). Install it with `dotnet add package ColumnWeaver.NET --version 1.0.1-beta`. The library multi-targets `net48` and `netstandard2.0` (compatible with modern .NET 6/8/9+), while the visual configurator targets `net48` on Windows.

## License and commercial option

The community license is GNU AGPL-3.0-or-later. Organizations seeking to embed the library in proprietary software may request a separate commercial license by emailing [Hitesh Bhanushali](mailto:hitesh.bhanushali.techmaster@gmail.com); commercial scope, pricing, and support are not published. The AGPL remains available and its rights cannot be withdrawn from recipients. The library license does not impose a separate fee on ordinary generated output, though output may have other rights based on its contents. See `../COMMERCIAL-LICENSING.md`.
