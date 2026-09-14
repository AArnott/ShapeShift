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

internal delegate void ReadExtensionData<TDeclaringType, TEncoder, TDecoder>(
	ref TDecoder decoder,
	ref TDeclaringType value,
	string propertyName,
	SerializationContext<TEncoder, TDecoder> context)
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct;

internal class PropertyConverter<TDeclaringType, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal required WriteProperty<TDeclaringType, TEncoder, TDecoder>? Write { get; init; }

	internal required ReadProperty<TDeclaringType, TEncoder, TDecoder>? Read { get; init; }

	internal ShouldWriteProperty<TDeclaringType>? ShouldWrite { get; init; }
}

/// <summary>
/// Associates a property writer with its cached encoder-specific property name.
/// </summary>
/// <typeparam name="TDeclaringType">The type that declares the property.</typeparam>
/// <typeparam name="TEncoder">The encoder type.</typeparam>
/// <typeparam name="TDecoder">The decoder type.</typeparam>
/// <param name="Write">The delegate that writes the property value.</param>
/// <param name="ShouldWrite">The optional delegate that determines whether to write the property.</param>
/// <param name="PreparedName">The state prepared by the encoder for the serialized property name.</param>
internal sealed record ObjectPropertyWriter<TDeclaringType, TEncoder, TDecoder>(
	WriteProperty<TDeclaringType, TEncoder, TDecoder> Write,
	ShouldWriteProperty<TDeclaringType>? ShouldWrite,
	object? PreparedName)
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct;

/// <summary>
/// Associates a property reader with its position for duplicate-property detection.
/// </summary>
/// <typeparam name="TDeclaringType">The type that declares the property.</typeparam>
/// <typeparam name="TEncoder">The encoder type.</typeparam>
/// <typeparam name="TDecoder">The decoder type.</typeparam>
/// <param name="Read">The delegate that reads and assigns the property value.</param>
/// <param name="Index">The property's zero-based index in the contract.</param>
internal sealed record ObjectPropertyReader<TDeclaringType, TEncoder, TDecoder>(
	ReadProperty<TDeclaringType, TEncoder, TDecoder> Read,
	int Index)
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct;

internal sealed record ExtensionDataProperty<TDeclaringType, TEncoder, TDecoder>(
	Func<TDeclaringType, IReadOnlyDictionary<string, ShapeShiftValue>?> GetValues,
	ReadExtensionData<TDeclaringType, TEncoder, TDecoder> Read)
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct;
