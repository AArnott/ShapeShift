// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

using System.Diagnostics.CodeAnalysis;

namespace ShapeShift.Converters;

internal static class BuiltInConverters
{
    internal static bool TryGetBuiltInConverter<T, TEncoder, TDecoder>([NotNullWhen(true)] out IConverter<T, TEncoder, TDecoder>? converter)
        where TEncoder : IEncoder, allows ref struct
        where TDecoder : IDecoder, allows ref struct
    {
        if (typeof(T) == typeof(string))
        {
            converter = (IConverter<T, TEncoder, TDecoder>)new StringConverter<TEncoder, TDecoder>();
        }
        else if (typeof(T) == typeof(int))
        {
            converter = (IConverter<T, TEncoder, TDecoder>)new Int32Converter<TEncoder, TDecoder>();
        }
        else
        {
            converter = null;
            return false;
        }

        return true;
    }
}

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
