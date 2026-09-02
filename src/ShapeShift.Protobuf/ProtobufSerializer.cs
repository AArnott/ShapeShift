// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using PolyType;

namespace ShapeShift.Protobuf;

/// <summary>
/// Serializes PolyType-described object graphs to the protobuf-style binary encoding used by this package.
/// </summary>
public record ProtobufSerializer : ShapeShiftSerializer<ProtobufEncoder, ProtobufDecoder>
{
	/// <summary>
	/// Serializes a value as a protobuf-style binary payload.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The binary payload.</returns>
	public byte[] Serialize<T>(in T? value)
		where T : IShapeable<T> => this.Serialize<T, T>(value);

	/// <summary>
	/// Serializes a value as a protobuf-style binary payload using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The shape provider.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The binary payload.</returns>
	public byte[] Serialize<T, TProvider>(in T? value)
		where TProvider : IShapeable<T>
	{
		using MemoryStream stream = new();
		ProtobufEncoder encoder = new(stream);
		this.Serialize(ref encoder, value, TProvider.GetTypeShape());
		return stream.ToArray();
	}

	/// <summary>
	/// Deserializes a value from a protobuf-style binary payload.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="payload">The encoded bytes.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T>(ReadOnlySpan<byte> payload)
		where T : IShapeable<T> => this.Deserialize<T, T>(payload);

	/// <summary>
	/// Deserializes a value from a protobuf-style binary payload using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="payload">The encoded bytes.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T, TProvider>(ReadOnlySpan<byte> payload)
		where TProvider : IShapeable<T>
	{
		byte[] bytes = payload.ToArray();
		ProtobufDecoder decoder = new(bytes);
		return this.Deserialize(ref decoder, TProvider.GetTypeShape());
	}
}
