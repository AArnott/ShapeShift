// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Schema;

namespace Ubjson;

#region BinaryConverter
/// <summary>
/// Converts byte arrays through the UBJSON optimized <c>uint8</c> array form.
/// </summary>
/// <remarks>
/// <para>
/// A format that has a native binary representation supplies its own <see cref="byte"/> array
/// converter, because the shared converter layer has no way to know whether a format can carry bytes
/// natively or must fall back to writing an ordinary vector of numbers.
/// </para>
/// <para>
/// The converter is also where <see cref="SerializationContext{TEncoder, TDecoder}.MaxBinaryLength"/>
/// is enforced. Limits belong to whichever converter reads the length-bearing token, because the
/// decoder itself has no access to the serialization context.
/// </para>
/// </remarks>
public sealed class UbjsonBinaryConverter : ShapeShiftConverter<byte[], UbjsonEncoder, UbjsonDecoder>
{
    /// <inheritdoc/>
    public override byte[]? Read(ref UbjsonDecoder decoder, SerializationContext<UbjsonEncoder, UbjsonDecoder> context)
    {
        if (decoder.TryReadNull())
        {
            return null;
        }

        byte[] value = decoder.ReadByteArray();
        ValidateLength(value.Length, context);
        return value;
    }

    /// <inheritdoc/>
    public override void Write(ref UbjsonEncoder encoder, in byte[]? value, SerializationContext<UbjsonEncoder, UbjsonDecoder> context)
    {
        if (value is null)
        {
            encoder.WriteNull();
            return;
        }

        ValidateLength(value.Length, context);
        encoder.WriteValue(value);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Without this override, schema consumers would be told the value is undocumented rather than
    /// that it is binary, because the shared layer will not guess at a custom converter's output.
    /// </remarks>
    public override DataContract? GetContract(ContractContext<UbjsonEncoder, UbjsonDecoder> context)
        => new PrimitiveContract(typeof(byte[]), PrimitiveDataType.Binary);

    private static void ValidateLength(int length, SerializationContext<UbjsonEncoder, UbjsonDecoder> context)
    {
        if (length > context.MaxBinaryLength)
        {
            throw new ShapeShiftSerializationException($"Binary length {length} exceeds the configured maximum of {context.MaxBinaryLength}.");
        }
    }
}
#endregion
