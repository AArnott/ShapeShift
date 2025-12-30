// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PolyType;

namespace ShapeShift;

public abstract class SerializerBase<TEncoder, TDecoder>
    where TEncoder : IEncoder, allows ref struct
    where TDecoder : IDecoder, allows ref struct
{
    private readonly ShapeVisitor<TEncoder, TDecoder> visitor = new();

    public void Serialize<T>(ref TEncoder encoder, in T? value, ITypeShape<T> typeShape)
    {
        SerializationContext context = default;
        this.GetConverter<T>(typeShape).Write(ref encoder, value, context);
    }

    public T? Deserialize<T>(ref TDecoder decoder, ITypeShape<T> typeShape)
    {
        SerializationContext context = default;
        return this.GetConverter<T>(typeShape).Read(ref decoder, context);
    }

    private IConverter<T, TEncoder, TDecoder> GetConverter<T>(ITypeShape<T> typeShape) => (IConverter<T, TEncoder, TDecoder>)typeShape.Accept(this.visitor)!;
}
