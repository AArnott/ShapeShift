# Overview

ShapeShift is one serializer for every format: the same contracts, converters,
and policies, with a thin package for each encoding.

## Install

```bash
dotnet add package ShapeShift.Json
```

`ShapeShift.Json` is the usual starting point and brings in the ShapeShift
core. MessagePack, CBOR, YAML, Protobuf, TAML, and TOML are separate packages.

## Highlights

- **NativeAOT-ready contracts.** PolyType source-generates type shapes.
  Reflection-based converter activation stays off unless you opt in.
- **One configuration model.** Naming, default-value omission, strict
  validation, and security limits are immutable options on the serializer,
  and every format is configured the same way.
- **Reference preservation.** An object graph that shares instances stays a
  graph. Cycles can be rejected or reconstructed.
- **Targeted and streaming reads.** Seek to a path without deserializing the
  rest of the document, or enumerate values one at a time.
- **Dynamic values and unknown properties.** Inspect untyped input without
  loading CLR types from the payload, and round-trip properties the contract
  does not declare.
- **Thin format packages.** A new encoding supplies an encoder, a decoder, and
  a serializer. Objects, collections, policies, and limits stay in the shared
  core.

See [Features](docs/features.md) for the full set.

## Getting started

[Getting Started](docs/getting-started.md) lists every package and walks
through a first serializer.
