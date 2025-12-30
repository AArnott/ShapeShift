// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Converters;

internal class StringConverter<TEncoder, TDecoder> : IConverter<string, TEncoder, TDecoder>
    where TEncoder : IEncoder, allows ref struct
    where TDecoder : IDecoder, allows ref struct
{
    /// <inheritdoc/>
    public string? Read(ref TDecoder decoder, SerializationContext context)
    {
        if (decoder.TryReadNull())
        {
            decoder.ReadNull();
            return null;
        }

        return decoder.ReadString();
    }

    /// <inheritdoc/>
    public void Write(ref TEncoder encoder, in string? value, SerializationContext context)
    {
        encoder.Write(value);
    }
}

internal class Int32Converter<TEncoder, TDecoder> : IConverter<int, TEncoder, TDecoder>
    where TEncoder : IEncoder, allows ref struct
    where TDecoder : IDecoder, allows ref struct
{
    /// <inheritdoc/>
    public int Read(ref TDecoder decoder, SerializationContext context) => decoder.ReadInt32();

    /// <inheritdoc/>
    public void Write(ref TEncoder encoder, in int value, SerializationContext context) => encoder.Write(value);
}
