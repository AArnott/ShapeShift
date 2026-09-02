# ShapeShift.Toml

## Features

* PolyType-based serialization means a shared set of attributes for your data models regardless of the use case.
* TOML 1.0-compliant parsing and serialization, including standard tables, arrays of tables, dotted keys, inline tables, comments, multiline strings, and all date/time forms.
* Native support for strings, signed 64-bit integers, binary64 floats, booleans, and date-times.
* Cargo-style documents with `[package]`, `[dependencies]`, and `[[bin]]` sections.
* Trim-safe and NativeAOT-compatible parsing through Tomlyn 0.19's validated syntax tree.
* Conformance-tested against the ShapeShift conformance suite and focused TOML 1.0 valid/invalid cases.

TOML documents always have a table at their root. TOML has no null, binary, or duration scalar; null object properties are omitted, while unsupported values in arrays or at the root are rejected.
