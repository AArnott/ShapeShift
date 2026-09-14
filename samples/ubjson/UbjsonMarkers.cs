// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Ubjson;

/// <summary>
/// The UBJSON Draft 12 type markers this sample reads and writes.
/// </summary>
/// <remarks>
/// Every UBJSON value begins with a one-byte ASCII marker that names its type, and every
/// multi-byte numeric payload is big-endian. Keeping the markers in one place makes the
/// encoder and the decoder provably agree on the wire format.
/// </remarks>
internal static class UbjsonMarkers
{
    /// <summary>The null value, which carries no payload.</summary>
    internal const byte Null = (byte)'Z';

    /// <summary>A no-op filler byte that a reader must accept and ignore where a value is expected.</summary>
    internal const byte NoOp = (byte)'N';

    /// <summary>Boolean true, which carries no payload.</summary>
    internal const byte True = (byte)'T';

    /// <summary>Boolean false, which carries no payload.</summary>
    internal const byte False = (byte)'F';

    /// <summary>A signed 8-bit integer.</summary>
    internal const byte Int8 = (byte)'i';

    /// <summary>An unsigned 8-bit integer.</summary>
    internal const byte UInt8 = (byte)'U';

    /// <summary>A signed 16-bit big-endian integer.</summary>
    internal const byte Int16 = (byte)'I';

    /// <summary>A signed 32-bit big-endian integer.</summary>
    internal const byte Int32 = (byte)'l';

    /// <summary>A signed 64-bit big-endian integer.</summary>
    internal const byte Int64 = (byte)'L';

    /// <summary>An IEEE 754 binary32 value.</summary>
    internal const byte Float32 = (byte)'d';

    /// <summary>An IEEE 754 binary64 value.</summary>
    internal const byte Float64 = (byte)'D';

    /// <summary>An arbitrary-precision number carried as its decimal text.</summary>
    internal const byte HighPrecision = (byte)'H';

    /// <summary>A single ASCII character.</summary>
    internal const byte Char = (byte)'C';

    /// <summary>A length-prefixed UTF-8 string.</summary>
    internal const byte String = (byte)'S';

    /// <summary>The start of an array.</summary>
    internal const byte ArrayStart = (byte)'[';

    /// <summary>The end of an array that declared no element count.</summary>
    internal const byte ArrayEnd = (byte)']';

    /// <summary>The start of an object.</summary>
    internal const byte ObjectStart = (byte)'{';

    /// <summary>The end of an object that declared no entry count.</summary>
    internal const byte ObjectEnd = (byte)'}';

    /// <summary>Introduces the single element type shared by every member of an optimized container.</summary>
    internal const byte ContainerType = (byte)'$';

    /// <summary>Introduces the element count of an optimized container.</summary>
    internal const byte ContainerCount = (byte)'#';

    /// <summary>
    /// Gets a value indicating whether a marker names a scalar type that may be used as an
    /// optimized container's shared element type.
    /// </summary>
    /// <param name="marker">The marker to classify.</param>
    /// <returns><see langword="true" /> when the marker names a scalar.</returns>
    /// <remarks>
    /// Container markers are deliberately excluded. UBJSON permits a container of containers to
    /// declare <c>$[</c>, but the nested containers then carry their own optional headers, which
    /// this sample rejects rather than half-supports.
    /// </remarks>
    internal static bool IsScalarMarker(byte marker) => marker
        is Null or True or False or Int8 or UInt8 or Int16 or Int32 or Int64
        or Float32 or Float64 or HighPrecision or Char or String;

    /// <summary>
    /// Gets the human-readable name of a marker, for error messages.
    /// </summary>
    /// <param name="marker">The marker to describe.</param>
    /// <returns>A short description.</returns>
    internal static string Describe(byte marker)
        => marker is >= 0x20 and < 0x7F
            ? $"'{(char)marker}' (0x{marker:X2})"
            : $"0x{marker:X2}";
}
