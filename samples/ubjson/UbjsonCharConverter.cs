// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Schema;

namespace Ubjson;

#region NativeCharConverter
/// <summary>
/// Converts <see cref="char"/> through UBJSON's native <c>C</c> type.
/// </summary>
/// <remarks>
/// <para>
/// This is the answer to "my format has a native representation for a primitive that
/// <see cref="IEncoder"/> and <see cref="IDecoder"/> do not expose". The three pieces are:
/// </para>
/// <list type="number">
/// <item>a format-specific encoder method (<see cref="UbjsonEncoder.WriteChar"/>);</item>
/// <item>a format-specific decoder method (<see cref="UbjsonDecoder.TryReadChar"/>);</item>
/// <item>
/// this converter, which is typed over the <em>concrete</em> encoder and decoder and is therefore
/// free to call both. It is registered in <see cref="UbjsonSerializer"/>'s constructor, where it
/// takes precedence over the shared layer's <see cref="char"/> converter.
/// </item>
/// </list>
/// <para>
/// Nothing about this requires a change to the shared token vocabulary, and nothing about it costs
/// another format anything. A format that had no <c>C</c> simply would not write this class.
/// </para>
/// <para>
/// The converter is not generic, so no runtime type is constructed on the serialization path and
/// nothing here impedes trimming or NativeAOT. Because it names the concrete
/// <see langword="ref" /> struct encoder and decoder, it also sidesteps the limitation that a
/// <see langword="ref" /> struct may not inherit a default interface method -- see
/// <see href="../../docfx/docs/format-authoring.md">the format authoring guide</see>.
/// </para>
/// </remarks>
public sealed class UbjsonCharConverter : ShapeShiftConverter<char, UbjsonEncoder, UbjsonDecoder>
{
    /// <inheritdoc/>
    /// <remarks>
    /// The fallback matters as much as the fast path: a payload whose character arrived as an ordinary
    /// one-character string must still deserialize, because other UBJSON writers -- including earlier
    /// versions of this one -- produce exactly that.
    /// </remarks>
    public override char Read(ref UbjsonDecoder decoder, SerializationContext<UbjsonEncoder, UbjsonDecoder> context)
    {
        if (decoder.TryReadChar(out char value))
        {
            return value;
        }

        return decoder.ReadCharSpan() is [char c]
            ? c
            : throw new ShapeShiftSerializationException("Expected a single character.");
    }

    /// <inheritdoc/>
    /// <remarks>
    /// UBJSON's <c>C</c> is one ASCII byte, so anything above U+007F falls back to the shared string
    /// representation rather than being silently mangled.
    /// </remarks>
    public override void Write(ref UbjsonEncoder encoder, in char value, SerializationContext<UbjsonEncoder, UbjsonDecoder> context)
    {
        if (value <= 0x7F)
        {
            encoder.WriteChar(value);
        }
        else
        {
            encoder.WriteValue([value]);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A format-specific representation still owes the schema projection an honest description. The
    /// wire type is narrower than <see cref="PrimitiveDataType.String"/>, and
    /// <see cref="PrimitiveDataType.Char"/> says so.
    /// </remarks>
    public override DataContract? GetContract(ContractContext<UbjsonEncoder, UbjsonDecoder> context)
        => new PrimitiveContract(typeof(char), PrimitiveDataType.Char);
}
#endregion
