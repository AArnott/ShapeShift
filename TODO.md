# ShapeShift feature punch list

This document compares the format-neutral features of ShapeShift with
Nerdbank.MessagePack and records the work required to make ShapeShift a strong
foundation for JSON, MessagePack, YAML, TAML, and third-party formats.

The comparison is intentionally about capabilities that belong in ShapeShift.
MessagePack-specific details such as extension type codes and array-keyed
contracts belong in `ShapeShift.MsgPack`, while host integrations such as
SignalR and ASP.NET Core formatters should remain separate optional packages.

## Already present

ShapeShift already has several important Nerdbank.MessagePack features:

- PolyType source-generated contracts, including modern C# constructors and
  `init` properties.
- Format-independent custom converter instances, converter types, converter
  factories, member-level converter attributes, and converter state.
- Property naming policies and per-member names supplied by PolyType.
- Serialization callbacks.
- Configurable maximum depth and cancellation checks.
- Optional string interning.
- Optional reference preservation and cycle handling.
- Immutable serializer configuration and converter caches with no mutable
  process-wide configuration.

These features need more tests and documentation as the work below fills in
the missing type-shape paths and exposes all intended configuration publicly.

## Applicable Nerdbank.MessagePack features missing from ShapeShift

The following items are implementation TODOs. They are ordered so that later
work can build on earlier abstractions.

- [ ] **Complete PolyType shape coverage.** Add converters for dictionaries,
  nullable values, enums, surrogates, unions/polymorphism, arrays (including
  multidimensional arrays where a format can represent them), and the other
  collection construction strategies exposed by PolyType. The existing
  visitor handles primitives, objects, and basic enumerables only. Keep these
  converters format-neutral; a format may supply a more specialized converter
  when its wire model requires one.

- [ ] **Public, immutable serializer configuration.** Expose custom converter
  instances, converter types, converter factories, reference preservation,
  default-value policy, required-member policy, security limits, and comparer
  selection through an elegant immutable API. Runtime activation of converter
  `Type` objects uses reflection and therefore must remain an explicit opt-in;
  converter instances, factories, and source-generated type shapes remain the
  NativeAOT-safe default.

- [ ] **Secure and strict deserialization defaults.** Reject duplicate object
  properties, enforce required constructor parameters/properties and
  non-nullable members, bound collection and string/binary lengths, and retain
  the existing depth limit. Limits must be configurable for trusted or unusual
  inputs. Hash-collision-resistant comparers are useful for hostile dictionary
  keys, but should be supplied through a comparer provider because not every
  key type has the same equality semantics.

- [ ] **Default-value omission.** Optionally omit properties with default
  values while preserving required values and keeping the default wire shape
  stable. This is directly applicable to map/object formats. Positional
  encodings cannot generally omit an interior value without an explicit
  presence scheme, so format-specific positional converters may decline this
  option.

- [ ] **Format-neutral dynamic value model and unknown-data retention.** Add a
  NativeAOT-safe ShapeShift value tree for null, boolean, number, string,
  binary, sequence, and map values. It should support untyped serialization
  and deserialization and allow an extension-data member to capture unknown
  properties for forward-compatible round trips. Arbitrary CLR type loading
  must not be part of this feature; reflection-based `object` conversion, if
  ever offered, must be a separate explicit opt-in.

- [ ] **Targeted and streaming deserialization primitives.** Let a decoder skip
  to a strongly typed property/index path and deserialize just that fragment,
  and support incremental enumeration of top-level values or a sequence inside
  an envelope. The reusable path model belongs in ShapeShift, while each
  decoder is responsible for efficient skipping. Text formats may need to
  buffer individual scalar tokens; they should not have to buffer the entire
  document.

- [ ] **Async I/O without sync-over-async.** Define format-package APIs that
  serialize to and deserialize from `Stream`, `PipeReader`/`PipeWriter`, and
  `IBufferWriter<byte>` where appropriate. The synchronous ref-struct
  encoder/decoder interfaces cannot cross `await`, so async adapters should
  incrementally fill/drain buffers around synchronous conversion rather than
  pretending a synchronous `TextReader.ReadToEnd` implementation is async.

- [ ] **Schema and contract inspection.** Export a format-neutral contract
  description from PolyType shapes, with JSON Schema projection in
  `ShapeShift.Json`. MessagePack can expose the same contract as JSON Schema
  with MessagePack-specific annotations. Custom converters need an optional
  schema hook; converters without one should produce an explicit
  "undocumented" contract rather than an incorrect schema.

- [ ] **Structural equality and hashing.** Provide deep
  `IEqualityComparer<T>` generation from PolyType shapes, including collection
  contents and cycles. This is independent of serialization formats and useful
  to ShapeShift callers. Collision-resistant hashing should be a distinct
  opt-in API with documented comparer caveats.

- [ ] **Diagnostics and analyzers.** Add actionable exception paths (property
  and index breadcrumbs) and analyzers for converter construction, missing
  generated shapes, ambiguous wire names, unsafe reflection activation, and
  unsupported contracts. Runtime behavior must remain correct without the
  analyzers; analyzers improve authoring feedback only.

## `ShapeShift.Json`

- [ ] Add a `ShapeShift.Json` package with `JsonEncoder`, `JsonDecoder`, and
  `JsonSerializer` built on `System.Text.Json` primitives. Support UTF-8 spans,
  streams, `IBufferWriter<byte>`, comments/trailing-comma policy, named floating
  point values, and configurable indentation. Do not delegate object mapping
  to `System.Text.Json.JsonSerializer`; ShapeShift converters and PolyType
  contracts must remain authoritative.
- [ ] Integrate the dynamic value model with `JsonElement`, `JsonDocument`, and
  `JsonNode` through optional converters. These BCL types are NativeAOT-safe;
  reflection-based `System.Text.Json` contract discovery is not used.
- [ ] Project ShapeShift contracts to JSON Schema and document JSON-specific
  representation choices for dates, times, binary data, large integers, and
  non-string dictionary keys.

## `ShapeShift.MsgPack`

- [ ] Add a `ShapeShift.MsgPack` package with span/sequence-based
  `MsgPackEncoder`, `MsgPackDecoder`, and `MsgPackSerializer`. Implement the
  MessagePack primitives directly or depend only on a NativeAOT-safe primitive
  package; do not route object mapping through another serializer.
- [ ] Preserve numeric widths where practical, support binary values and
  timestamp extensions, and expose low-level reader/writer access for custom
  converters. Decimal, `Int128`, `UInt128`, `BigInteger`, and `TimeSpan` need
  documented interoperable encodings because MessagePack has no universal
  native representation for them.
- [ ] Support map contracts by default and an explicit positional/array
  contract mode for compact payloads. Positional mode requires stable integer
  keys and has stricter versioning caveats than map mode.
- [ ] Reserve and document extension codes used by ShapeShift reference
  preservation and other optional features. Readers must reject malformed or
  conflicting extension payloads.
- [ ] Add endless top-level streaming, framed stream helpers, and targeted
  path deserialization over `ReadOnlySequence<byte>`/`PipeReader` without
  requiring a contiguous copy.

## Ecosystem and extensibility

- [ ] Publish a format-authoring guide based on `ShapeShift.Taml`, including
  token semantics, decoder state invariants, error reporting, security limits,
  optimized scalar/binary hooks, async adapter guidance, NativeAOT rules, and
  a conformance test kit that third-party format packages can reuse.
- [ ] Add focused samples for core customization, JSON, MessagePack, streaming,
  schema generation, unknown-data retention, and third-party format creation;
  link them from docfx topics.
- [ ] Consider separate ASP.NET Core MVC and SignalR integration packages after
  the JSON and MessagePack stream APIs stabilize. These integrations are
  applicable, but keeping framework dependencies out of the core format
  packages preserves trimming, NativeAOT, and deployment flexibility.

## Features intentionally not copied directly

- **Typeless CLR deserialization is not a default feature.** Loading arbitrary
  runtime types from untrusted data is unsafe and incompatible with robust
  trimming. A future compatibility package may opt into reflection with
  explicit registration and annotations, but the core remains NativeAOT-safe
  when that opt-in is never called.
- **Compression is transport composition, not serialization.** LZ4, Brotli,
  and similar compression should wrap streams/pipelines rather than alter the
  ShapeShift object model.
- **Unity support is a compatibility target, not a converter feature.** It can
  be evaluated once target frameworks and dependencies support the relevant
  Unity runtime.
- **Mutable global defaults will not be added.** Serializer instances remain
  immutable and safe for concurrent reuse.
