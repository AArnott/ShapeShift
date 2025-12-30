// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

public interface IConverter<T, TEncoder, TDecoder>
    where TEncoder : IEncoder, allows ref struct
    where TDecoder : IDecoder, allows ref struct
{
    void Write(ref TEncoder encoder, in T? value, SerializationContext context);

    T? Read(ref TDecoder decoder, SerializationContext context);
}
