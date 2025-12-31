// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

internal delegate void WriteProperty<TDeclaringType, TEncoder, TDecoder>(ref TEncoder encoder, in TDeclaringType value, SerializationContext<TEncoder, TDecoder> context)
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct;

internal delegate void ReadProperty<TDeclaringType,	TEncoder, TDecoder>(ref TDecoder decoder, ref TDeclaringType value, SerializationContext<TEncoder, TDecoder> context)
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct;

internal struct PropertyConverter<TDeclaringType, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal required WriteProperty<TDeclaringType, TEncoder, TDecoder> Write { get; init; }

	internal required ReadProperty<TDeclaringType, TEncoder, TDecoder> Read { get; init; }
}
