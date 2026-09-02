// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Toml;

/// <summary>
/// A ShapeShift-compatible TOML serializer.
/// </summary>
public record TomlSerializer : ShapeShiftSerializer<TomlEncoder, TomlDecoder>
{
	/// <summary>
	/// Serializes a value to a TOML string.
	/// </summary>
	/// <typeparam name="T">The type of the value to serialize.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The TOML string representation of the value.</returns>
	public string Serialize<T>(in T? value)
		where T : IShapeable<T> => this.Serialize<T, T>(value);

	/// <summary>
	/// Serializes a value to a TOML string using the specified type shape.
	/// </summary>
	/// <typeparam name="T">The type of the value to serialize.</typeparam>
	/// <typeparam name="TProvider">The type that provides the PolyType shape.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The TOML string representation of the value.</returns>
	public string Serialize<T, TProvider>(in T? value)
		where TProvider : IShapeable<T>
	{
		StringWriter stringWriter = new();
		TomlEncoder encoder = new(stringWriter);
		this.Serialize(ref encoder, value, TProvider.GetTypeShape());
		return stringWriter.ToString();
	}

	/// <summary>
	/// Deserializes a TOML string to a value.
	/// </summary>
	/// <typeparam name="T">The type of the value to deserialize to.</typeparam>
	/// <param name="toml">The TOML string to deserialize.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T>(string toml)
		where T : IShapeable<T> => this.Deserialize<T, T>(toml);

	/// <summary>
	/// Deserializes a TOML string to a value using the specified type shape.
	/// </summary>
	/// <typeparam name="T">The type of the value to deserialize to.</typeparam>
	/// <typeparam name="TProvider">The type that provides the PolyType shape.</typeparam>
	/// <param name="toml">The TOML string to deserialize.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T, TProvider>(string toml)
		where TProvider : IShapeable<T>
	{
		StringReader stringReader = new(toml);
		TomlDecoder decoder = new(stringReader);
		return this.Deserialize(ref decoder, TProvider.GetTypeShape());
	}
}
