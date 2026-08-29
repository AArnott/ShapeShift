// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Json;

/// <summary>
/// Serializes PolyType-described object graphs as JSON.
/// </summary>
public sealed record JsonSerializer : ShapeShiftSerializer<JsonEncoder, JsonDecoder>
{
	/// <summary>
	/// Gets a value indicating whether JSON output is indented.
	/// </summary>
	public bool Indented { get; init; }

	/// <summary>
	/// Gets a value indicating whether trailing commas are accepted while reading.
	/// </summary>
	public bool AllowTrailingCommas { get; init; }

	/// <summary>
	/// Gets the handling applied to JSON comments while reading.
	/// </summary>
	public JsonCommentHandling CommentHandling { get; init; }

	/// <summary>
	/// Serializes a value to UTF-8 JSON.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The UTF-8 JSON document.</returns>
	public byte[] SerializeToUtf8Bytes<T>(in T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeToUtf8Bytes<T, T>(value, cancellationToken);

	/// <summary>
	/// Serializes a value to UTF-8 JSON using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The UTF-8 JSON document.</returns>
	public byte[] SerializeToUtf8Bytes<T, TProvider>(in T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArrayBufferWriter<byte> buffer = new();
		this.Serialize<T, TProvider>(buffer, value, cancellationToken);
		return buffer.WrittenSpan.ToArray();
	}

	/// <summary>
	/// Serializes a value as JSON text.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The JSON document.</returns>
	public string Serialize<T>(in T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => Encoding.UTF8.GetString(this.SerializeToUtf8Bytes(value, cancellationToken));

	/// <summary>
	/// Serializes a value as JSON text using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The JSON document.</returns>
	public string Serialize<T, TProvider>(in T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => Encoding.UTF8.GetString(this.SerializeToUtf8Bytes<T, TProvider>(value, cancellationToken));

	/// <summary>
	/// Serializes a value into a caller-provided buffer.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="destination">The destination buffer.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public void Serialize<T, TProvider>(IBufferWriter<byte> destination, in T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(destination);
		using Utf8JsonWriter writer = new(destination, new JsonWriterOptions { Indented = this.Indented });
		JsonEncoder encoder = new(writer);
		this.Serialize(ref encoder, value, TProvider.GetTypeShape(), cancellationToken);
		writer.Flush();
	}

	/// <summary>
	/// Deserializes a value from UTF-8 JSON.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="json">The UTF-8 JSON document.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T>(ReadOnlySpan<byte> json, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Deserialize<T, T>(json, cancellationToken);

	/// <summary>
	/// Deserializes a value from JSON text.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T>(string json, CancellationToken cancellationToken = default)
		where T : IShapeable<T>
		=> this.Deserialize<T, T>(json, cancellationToken);

	/// <summary>
	/// Deserializes a value from JSON text using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T, TProvider>(string json, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(json);
		return this.Deserialize<T, TProvider>(Encoding.UTF8.GetBytes(json), cancellationToken);
	}

	/// <summary>
	/// Deserializes a value from UTF-8 JSON using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="json">The UTF-8 JSON document.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T, TProvider>(ReadOnlySpan<byte> json, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		JsonDecoder decoder = new(json, new JsonReaderOptions { AllowTrailingCommas = this.AllowTrailingCommas, CommentHandling = this.CommentHandling });
		T? value = this.Deserialize(ref decoder, TProvider.GetTypeShape(), cancellationToken);
		decoder.EnsureEndOfDocument();
		return value;
	}

	/// <summary>
	/// Asynchronously serializes a value to a stream.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="stream">The destination stream.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	public async ValueTask SerializeAsync<T>(Stream stream, T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(stream);
		byte[] json = this.SerializeToUtf8Bytes(value, cancellationToken);
		await stream.WriteAsync(json, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Asynchronously deserializes a value from a stream.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="stream">The source stream.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public async ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		where T : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(stream);
		using MemoryStream buffer = new();
		await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
		return this.Deserialize<T>(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)), cancellationToken);
	}
}
