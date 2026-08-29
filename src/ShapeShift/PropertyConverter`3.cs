// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type

namespace ShapeShift;

internal delegate void WriteProperty<TDeclaringType, TEncoder, TDecoder>(ref TEncoder encoder, in TDeclaringType value, SerializationContext<TEncoder, TDecoder> context)
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct;

internal delegate void ReadProperty<TDeclaringType, TEncoder, TDecoder>(ref TDecoder decoder, ref TDeclaringType value, SerializationContext<TEncoder, TDecoder> context)
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct;

internal delegate bool ShouldWriteProperty<TDeclaringType>(in TDeclaringType value);

internal class PropertyConverter<TDeclaringType, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal required WriteProperty<TDeclaringType, TEncoder, TDecoder>? Write { get; init; }

	internal required ReadProperty<TDeclaringType, TEncoder, TDecoder>? Read { get; init; }

	internal ShouldWriteProperty<TDeclaringType>? ShouldWrite { get; init; }
}

internal sealed record ObjectPropertyWriter<TDeclaringType, TEncoder, TDecoder>(
	WriteProperty<TDeclaringType, TEncoder, TDecoder> Write,
	ShouldWriteProperty<TDeclaringType>? ShouldWrite)
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct;
