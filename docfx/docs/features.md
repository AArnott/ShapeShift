# Features

## NativeAOT-ready contracts

ShapeShift uses PolyType source-generated type shapes for NativeAOT-ready object
mapping. Reflection-based converter type activation is available only through
the explicitly annotated `WithReflectionConverterTypes` opt-in. Converter
instances, factories, and generated shapes remain reflection-free.

## Shared format-neutral behavior

Every ShapeShift format shares support for:

- Objects with mutable or parameterized construction.
- Mutable and immutable collections.
- Rectangular arrays of any rank.
- String-keyed maps and non-string-keyed dictionary entries.
- Nullable and other optional values.
- Enum names and numeric values.
- PolyType surrogates and attributed unions.
- Custom converters and converter factories.
- Naming policies, callbacks, reference preservation, and string interning.
- Default-value omission.
- Strict duplicate, required-member, and non-nullable-member validation.
- Configurable depth, collection, string, and binary length limits.

## Dynamic values

`ShapeShiftValue` is a NativeAOT-safe, format-neutral value tree. Its concrete
types represent null, Boolean, signed and unsigned integers, arbitrary-precision
integers, floating-point and decimal numbers, strings, binary data, arrays, and
string-keyed maps.

Dynamic values do not load CLR types from payload metadata. This makes them
suitable for inspecting untyped input without introducing typeless
deserialization risks.

Formats may not be able to preserve every distinction. For example, JSON has no
native binary token, so a `ShapeShiftBinary` writes as base64 text but untyped
JSON text is read as `ShapeShiftString`.

See [JSON](json.md) for JSON APIs and representation details.

Rectangular arrays use a two-element envelope containing a dimensions vector
and a row-major flat values vector. For example, a `2 x 3` array is represented
as `[[2, 3], [v0, v1, v2, v3, v4, v5]]`. This preserves zero-length dimensions
that a naively nested representation would lose.
