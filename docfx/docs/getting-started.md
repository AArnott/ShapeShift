# Getting Started

## Installation

Consume this ShapeShift via its NuGet Package.
Click on the badge to find its latest version and the instructions for consuming it that best apply to your project.

[![ShapeShift NuGet package](https://img.shields.io/nuget/v/ShapeShift.svg?label=ShapeShift)](https://www.nuget.org/packages/ShapeShift)<br />
[![ShapeShift.Json NuGet package](https://img.shields.io/nuget/v/ShapeShift.Json.svg?label=ShapeShift.Json)](https://www.nuget.org/packages/ShapeShift.Json)<br />
[![ShapeShift.MsgPack NuGet package](https://img.shields.io/nuget/v/ShapeShift.MsgPack.svg?label=ShapeShift.MsgPack)](https://www.nuget.org/packages/ShapeShift.MsgPack)<br />
![ShapeShift.Cbor NuGet package](https://img.shields.io/nuget/v/ShapeShift.Cbor.svg?label=ShapeShift.Cbor)<br />
[![ShapeShift.Yaml NuGet package](https://img.shields.io/nuget/v/ShapeShift.Yaml.svg?label=ShapeShift.Yaml)](https://www.nuget.org/packages/ShapeShift.Yaml)<br />
[![ShapeShift.Taml NuGet package](https://img.shields.io/nuget/v/ShapeShift.Taml.svg?label=ShapeShift.Taml)](https://www.nuget.org/packages/ShapeShift.Taml)<br />

## Usage

Pick a format package, annotate the types you serialize with PolyType's
`GenerateShapeAttribute`, and create a serializer:

[!code-csharp[JsonSerialization](../../samples/cs/JsonSerialization.cs#JsonSerialization)]

From there:

- [Features](features.md) surveys what every format shares.
- [Customizing the core](customization.md) covers immutable serializer
  configuration, the naming, default-value, strictness, and security policies,
  and custom converters and converter factories.
- [JSON](json.md), [MessagePack](msgpack.md), and [CBOR](cbor.md) document the
  format-specific APIs and wire representations.
- [Authoring a format package](format-authoring.md) explains how to add a
  format of your own.
