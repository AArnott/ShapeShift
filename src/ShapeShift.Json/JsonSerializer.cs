// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Json;

/// <summary>
/// Serializes PolyType-described object graphs as JSON.
/// </summary>
public sealed record JsonSerializer : ShapeShiftSerializer<JsonEncoder, JsonDecoder>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonSerializer"/> class.
	/// </summary>
	public JsonSerializer()
	{
		this.Converters = [new BinaryConverter(), new JsonElementConverter(), new JsonDocumentConverter(), new JsonNodeConverter()];
	}

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
	/// Gets a value indicating whether NaN and infinity are written and accepted as named JSON strings.
	/// </summary>
	public bool AllowNamedFloatingPointValues { get; init; }

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
		JsonEncoder encoder = new(writer, this.AllowNamedFloatingPointValues);
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
		JsonDecoder decoder = new(json, new JsonReaderOptions { AllowTrailingCommas = this.AllowTrailingCommas, CommentHandling = this.CommentHandling }, this.AllowNamedFloatingPointValues);
		T? value = this.Deserialize(ref decoder, TProvider.GetTypeShape(), cancellationToken);
		decoder.EnsureEndOfDocument();
		return value;
	}

	/// <summary>
	/// Attempts to deserialize the value found at a given <see cref="ShapeShiftPath"/> within a UTF-8 JSON document,
	/// skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="json">The UTF-8 JSON document.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="value">Receives the deserialized value if this method returns <see langword="true" />; otherwise <see langword="default" />.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns><see langword="true" /> if <paramref name="path"/> was found; <see langword="false" /> otherwise.</returns>
	public bool TryDeserializeFragment<T>(ReadOnlySpan<byte> json, ShapeShiftPath path, out T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.TryDeserializeFragment<T, T>(json, path, out value, cancellationToken);

	/// <summary>
	/// Attempts to deserialize the value found at a given <see cref="ShapeShiftPath"/> within a UTF-8 JSON document
	/// using a specified shape provider, skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="json">The UTF-8 JSON document.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="value">Receives the deserialized value if this method returns <see langword="true" />; otherwise <see langword="default" />.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns><see langword="true" /> if <paramref name="path"/> was found; <see langword="false" /> otherwise.</returns>
	public bool TryDeserializeFragment<T, TProvider>(ReadOnlySpan<byte> json, ShapeShiftPath path, out T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		JsonDecoder decoder = new(json, new JsonReaderOptions { AllowTrailingCommas = this.AllowTrailingCommas, CommentHandling = this.CommentHandling }, this.AllowNamedFloatingPointValues);
		return this.TryDeserializeFragment(ref decoder, path, TProvider.GetTypeShape(), out value, cancellationToken);
	}

	/// <summary>
	/// Deserializes the value found at a given <see cref="ShapeShiftPath"/> within a UTF-8 JSON document,
	/// skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="json">The UTF-8 JSON document.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="ShapeShiftSerializationException">Thrown when <paramref name="path"/> could not be found.</exception>
	public T? DeserializeFragment<T>(ReadOnlySpan<byte> json, ShapeShiftPath path, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeFragment<T, T>(json, path, cancellationToken);

	/// <summary>
	/// Deserializes the value found at a given <see cref="ShapeShiftPath"/> within a UTF-8 JSON document
	/// using a specified shape provider, skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="json">The UTF-8 JSON document.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="ShapeShiftSerializationException">Thrown when <paramref name="path"/> could not be found.</exception>
	public T? DeserializeFragment<T, TProvider>(ReadOnlySpan<byte> json, ShapeShiftPath path, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		JsonDecoder decoder = new(json, new JsonReaderOptions { AllowTrailingCommas = this.AllowTrailingCommas, CommentHandling = this.CommentHandling }, this.AllowNamedFloatingPointValues);
		return this.DeserializeFragment(ref decoder, path, TProvider.GetTypeShape(), cancellationToken);
	}

	/// <summary>
	/// Attempts to deserialize the value found at a given <see cref="ShapeShiftPath"/> within JSON text,
	/// skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="value">Receives the deserialized value if this method returns <see langword="true" />; otherwise <see langword="default" />.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns><see langword="true" /> if <paramref name="path"/> was found; <see langword="false" /> otherwise.</returns>
	public bool TryDeserializeFragment<T>(string json, ShapeShiftPath path, out T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.TryDeserializeFragment<T, T>(json, path, out value, cancellationToken);

	/// <summary>
	/// Attempts to deserialize the value found at a given <see cref="ShapeShiftPath"/> within JSON text
	/// using a specified shape provider, skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="value">Receives the deserialized value if this method returns <see langword="true" />; otherwise <see langword="default" />.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns><see langword="true" /> if <paramref name="path"/> was found; <see langword="false" /> otherwise.</returns>
	public bool TryDeserializeFragment<T, TProvider>(string json, ShapeShiftPath path, out T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(json);
		return this.TryDeserializeFragment<T, TProvider>(Encoding.UTF8.GetBytes(json), path, out value, cancellationToken);
	}

	/// <summary>
	/// Deserializes the value found at a given <see cref="ShapeShiftPath"/> within JSON text,
	/// skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="ShapeShiftSerializationException">Thrown when <paramref name="path"/> could not be found.</exception>
	public T? DeserializeFragment<T>(string json, ShapeShiftPath path, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeFragment<T, T>(json, path, cancellationToken);

	/// <summary>
	/// Deserializes the value found at a given <see cref="ShapeShiftPath"/> within JSON text
	/// using a specified shape provider, skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="ShapeShiftSerializationException">Thrown when <paramref name="path"/> could not be found.</exception>
	public T? DeserializeFragment<T, TProvider>(string json, ShapeShiftPath path, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(json);
		return this.DeserializeFragment<T, TProvider>(Encoding.UTF8.GetBytes(json), path, cancellationToken);
	}

	/// <summary>
	/// Creates a reader that incrementally enumerates the elements of a JSON array,
	/// whether that array is the root of the document or is reached by first seeking into an enclosing document
	/// (e.g. with the <c>TrySeek</c> decoder extension member).
	/// </summary>
	/// <typeparam name="T">The type of each element in the array.</typeparam>
	/// <param name="cancellationToken">A cancellation token that applies throughout the lifetime of the reader.</param>
	/// <returns>The reader. Callers should dispose of it (or use a <see langword="using" /> statement) when done.</returns>
	public ShapeShiftSequenceReader<T, JsonEncoder, JsonDecoder> CreateSequenceReader<T>(CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.CreateSequenceReader<T, T>(cancellationToken);

	/// <summary>
	/// Creates a reader that incrementally enumerates the elements of a JSON array using a specified shape provider,
	/// whether that array is the root of the document or is reached by first seeking into an enclosing document.
	/// </summary>
	/// <typeparam name="T">The type of each element in the array.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="cancellationToken">A cancellation token that applies throughout the lifetime of the reader.</param>
	/// <returns>The reader. Callers should dispose of it (or use a <see langword="using" /> statement) when done.</returns>
	public ShapeShiftSequenceReader<T, JsonEncoder, JsonDecoder> CreateSequenceReader<T, TProvider>(CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.CreateSequenceReader(TProvider.GetTypeShape(), cancellationToken);

	/// <summary>
	/// Creates a reader that incrementally enumerates a sequence of whole top-level JSON values sharing one buffer,
	/// such as newline-delimited JSON (NDJSON).
	/// </summary>
	/// <typeparam name="T">The type of each top-level value.</typeparam>
	/// <param name="cancellationToken">A cancellation token that applies throughout the lifetime of the reader.</param>
	/// <returns>The reader. Callers should dispose of it (or use a <see langword="using" /> statement) when done.</returns>
	public ShapeShiftDocumentReader<T, JsonEncoder, JsonDecoder> CreateDocumentReader<T>(CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.CreateDocumentReader<T, T>(cancellationToken);

	/// <summary>
	/// Creates a reader that incrementally enumerates a sequence of whole top-level JSON values sharing one buffer
	/// using a specified shape provider, such as newline-delimited JSON (NDJSON).
	/// </summary>
	/// <typeparam name="T">The type of each top-level value.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="cancellationToken">A cancellation token that applies throughout the lifetime of the reader.</param>
	/// <returns>The reader. Callers should dispose of it (or use a <see langword="using" /> statement) when done.</returns>
	public ShapeShiftDocumentReader<T, JsonEncoder, JsonDecoder> CreateDocumentReader<T, TProvider>(CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.CreateDocumentReader(TProvider.GetTypeShape(), cancellationToken);

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
