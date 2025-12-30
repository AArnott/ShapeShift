// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

internal struct PropertyConverter<TDeclaringType, TEncoder, TDecoder>
    where TEncoder : IEncoder, allows ref struct
    where TDecoder : IDecoder, allows ref struct
{
    internal required WriteProperty<TDeclaringType, TEncoder> Write { get; init; }

    internal required ReadProperty<TDeclaringType, TDecoder> Read { get; init; }
}

internal delegate void WriteProperty<TDeclaringType, TEncoder>(ref TEncoder encoder, in TDeclaringType value, SerializationContext context)
    where TEncoder : IEncoder, allows ref struct;

internal delegate void ReadProperty<TDeclaringType, TDecoder>(ref TDecoder decoder, ref TDeclaringType value, SerializationContext context)
    where TDecoder : IDecoder, allows ref struct;
