// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Formats.Cbor;
using PolyType;

namespace ShapeShift.Cbor;

/// <summary>
/// Serializes PolyType-described object graphs as CBOR.
/// </summary>
public sealed record CborSerializer : ShapeShiftSerializer<CborEncoder, CborDecoder>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CborSerializer"/> class.
	/// </summary>
	public CborSerializer()
	{
		this.Converters = [new BinaryConverter()];
	}

	/// <summary>
	/// Serializes a value as CBOR.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The CBOR document.</returns>
	public byte[] Serialize<T>(in T? value)
		where T : IShapeable<T> => this.Serialize<T, T>(value);

	/// <summary>
	/// Serializes a value as CBOR using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The CBOR document.</returns>
	public byte[] Serialize<T, TProvider>(in T? value)
		where TProvider : IShapeable<T>
	{
		CborWriter writer = new();
		CborEncoder encoder = new(writer);
		this.Serialize(ref encoder, value, TProvider.GetTypeShape());
		return writer.Encode();
	}

	/// <summary>
	/// Deserializes a value from CBOR.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="cbor">The CBOR document.</param>
	/// <returns>The decoded value.</returns>
	public T? Deserialize<T>(ReadOnlyMemory<byte> cbor)
		where T : IShapeable<T> => this.Deserialize<T, T>(cbor);

	/// <summary>
	/// Deserializes a value from CBOR using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="cbor">The CBOR document.</param>
	/// <returns>The decoded value.</returns>
	public T? Deserialize<T, TProvider>(ReadOnlyMemory<byte> cbor)
		where TProvider : IShapeable<T>
	{
		CborDecoder decoder = new(cbor);
		T? value = this.Deserialize(ref decoder, TProvider.GetTypeShape());
		decoder.EnsureEndOfDocument();
		return value;
	}
}
