// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

namespace ShapeShift.Json;

/// <summary>
/// Serializes PolyType-described object graphs as JSON.
/// </summary>
public sealed record JsonSerializer : ShapeShiftSerializer<JsonEncoder, JsonDecoder>
{
	/// <summary>
	/// The default maximum number of bytes buffered while searching for one complete top-level value via the
	/// incremental <see cref="Stream"/>/<see cref="PipeReader"/> based deserialization APIs on this type.
	/// </summary>
	private const long DefaultMaxBufferedValueSize = 64 * 1024 * 1024;

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
	/// Creates a JSON Schema document that describes the JSON this serializer reads and writes for a type.
	/// </summary>
	/// <param name="typeShape">The shape of the type to describe.</param>
	/// <param name="options">
	/// Options that influence the projection.
	/// When <see langword="null" />, defaults are used except that
	/// <see cref="JsonSchemaOptions.AllowNamedFloatingPointValues"/> is taken from this serializer.
	/// </param>
	/// <returns>A mutable JSON Schema document conforming to the <see cref="JsonSchema.Dialect"/> dialect.</returns>
	/// <exception cref="NotSupportedException">
	/// Thrown when <see cref="ShapeShiftSerializer{TEncoder, TDecoder}.PreserveReferences"/> is enabled.
	/// </exception>
	public JsonObject GetJsonSchema(ITypeShape typeShape, JsonSchemaOptions? options = null)
		=> JsonSchema.Create(this.GetContract(typeShape), options ?? new JsonSchemaOptions { AllowNamedFloatingPointValues = this.AllowNamedFloatingPointValues });

	/// <inheritdoc cref="GetJsonSchema(ITypeShape, JsonSchemaOptions?)"/>
	/// <typeparam name="T">The type to describe.</typeparam>
	public JsonObject GetJsonSchema<T>(JsonSchemaOptions? options = null)
		where T : IShapeable<T> => this.GetJsonSchema(T.GetTypeShape(), options);

	/// <inheritdoc cref="GetJsonSchema(ITypeShape, JsonSchemaOptions?)"/>
	/// <typeparam name="T">The type to describe.</typeparam>
	/// <typeparam name="TProvider">The witness class that provides the shape for <typeparamref name="T"/>.</typeparam>
	public JsonObject GetJsonSchema<T, TProvider>(JsonSchemaOptions? options = null)
		where TProvider : IShapeable<T> => this.GetJsonSchema(TProvider.GetTypeShape(), options);

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
		using PooledByteBufferWriter buffer = new();
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
		using Utf8JsonWriter writer = new(destination, new JsonWriterOptions
		{
			Indented = this.Indented,
			Encoder = JsonEncoder.Rfc8259StringEncoder,
		});
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
	/// Deserializes a value from a potentially segmented UTF-8 JSON sequence.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="json">The UTF-8 JSON document.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T>(in ReadOnlySequence<byte> json, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Deserialize<T, T>(json, cancellationToken);

	/// <summary>
	/// Deserializes a value from a potentially segmented UTF-8 JSON sequence using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="json">The UTF-8 JSON document.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <remarks>Multi-segment input is consolidated into one buffer before decoding.</remarks>
	public T? Deserialize<T, TProvider>(in ReadOnlySequence<byte> json, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		if (json.IsSingleSegment)
		{
			return this.Deserialize<T, TProvider>(json.FirstSpan, cancellationToken);
		}

		return this.Deserialize<T, TProvider>(json.ToArray(), cancellationToken);
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
	/// Asynchronously serializes a value to a <see cref="PipeWriter"/>.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="writer">The destination writer. This method flushes it but does not complete it.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	/// <remarks>The value is converted synchronously (as with any other synchronous <c>Serialize</c> overload); only the flush to <paramref name="writer"/> is asynchronous.</remarks>
	public ValueTask SerializeAsync<T>(PipeWriter writer, T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeAsync<T, T>(writer, value, cancellationToken);

	/// <summary>
	/// Asynchronously serializes a value to a <see cref="PipeWriter"/> using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="writer">The destination writer. This method flushes it but does not complete it.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	/// <remarks>The value is converted synchronously (as with any other synchronous <c>Serialize</c> overload); only the flush to <paramref name="writer"/> is asynchronous.</remarks>
	public async ValueTask SerializeAsync<T, TProvider>(PipeWriter writer, T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(writer);
		this.Serialize<T, TProvider>(writer, value, cancellationToken);
		await writer.FlushAndThrowIfCanceledAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Asynchronously serializes a value to a stream.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="stream">The destination stream. It is not closed or disposed by this method.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	public ValueTask SerializeAsync<T>(Stream stream, T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeAsync<T, T>(stream, value, cancellationToken);

	/// <summary>
	/// Asynchronously serializes a value to a stream using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="stream">The destination stream. It is not closed or disposed by this method.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	public async ValueTask SerializeAsync<T, TProvider>(Stream stream, T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(stream);
		PipeWriter writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
		try
		{
			await this.SerializeAsync<T, TProvider>(writer, value, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await writer.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously deserializes one value from a <see cref="PipeReader"/>, buffering only as much input as that
	/// value requires.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for the value, bounding memory use against a value
	/// that never completes (e.g. one truncated by a misbehaving sender).
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="DecoderException">Thrown if the reader has no more values, or ends in the middle of one.</exception>
	public ValueTask<T?> DeserializeAsync<T>(PipeReader reader, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAsync<T, T>(reader, maxBufferedSize, cancellationToken);

	/// <summary>
	/// Asynchronously deserializes one value from a <see cref="PipeReader"/> using a specified shape provider,
	/// buffering only as much input as that value requires.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for the value, bounding memory use against a value
	/// that never completes (e.g. one truncated by a misbehaving sender).
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="DecoderException">Thrown if the reader has no more values, or ends in the middle of one.</exception>
	public async ValueTask<T?> DeserializeAsync<T, TProvider>(PipeReader reader, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(reader);
		JsonValueBoundaryScanner scanner = new(new JsonReaderOptions { AllowTrailingCommas = this.AllowTrailingCommas, CommentHandling = this.CommentHandling });
		(bool hasValue, T? value) = await reader.ReadValueAsync(
			scanner,
			valueBytes => this.Deserialize<T, TProvider>(valueBytes, cancellationToken),
			maxBufferedSize,
			cancellationToken).ConfigureAwait(false);
		return hasValue ? value : throw new DecoderException("The input did not contain any value to deserialize.");
	}

	/// <summary>
	/// Asynchronously deserializes one value from a stream, buffering only as much input as that value requires.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="stream">The source stream. It is not closed or disposed by this method.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for the value, bounding memory use against a value
	/// that never completes (e.g. one truncated by a misbehaving sender).
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="DecoderException">Thrown if the stream has no more values, or ends in the middle of one.</exception>
	public ValueTask<T?> DeserializeAsync<T>(Stream stream, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAsync<T, T>(stream, maxBufferedSize, cancellationToken);

	/// <summary>
	/// Asynchronously deserializes one value from a stream using a specified shape provider, buffering only as
	/// much input as that value requires.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="stream">The source stream. It is not closed or disposed by this method.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for the value, bounding memory use against a value
	/// that never completes (e.g. one truncated by a misbehaving sender).
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="DecoderException">Thrown if the stream has no more values, or ends in the middle of one.</exception>
	public async ValueTask<T?> DeserializeAsync<T, TProvider>(Stream stream, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(stream);
		PipeReader reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
		try
		{
			return await this.DeserializeAsync<T, TProvider>(reader, maxBufferedSize, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously deserializes a sequence of whole top-level values sharing one <see cref="PipeReader"/>,
	/// such as newline-delimited JSON (NDJSON), buffering only as much input as each value requires.
	/// </summary>
	/// <typeparam name="T">The type of each top-level value.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for any single value, bounding memory use against a
	/// value that never completes (e.g. one truncated by a misbehaving sender).
	/// </param>
	/// <param name="cancellationToken">A cancellation token that applies throughout the enumeration.</param>
	/// <returns>An async sequence of the values read, ending gracefully when the reader reaches its end.</returns>
	public IAsyncEnumerable<T?> DeserializeAllAsync<T>(PipeReader reader, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAllAsync<T, T>(reader, maxBufferedSize, cancellationToken);

	/// <summary>
	/// Asynchronously deserializes a sequence of whole top-level values sharing one <see cref="PipeReader"/> using a
	/// specified shape provider, such as newline-delimited JSON (NDJSON), buffering only as much input as each value requires.
	/// </summary>
	/// <typeparam name="T">The type of each top-level value.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for any single value, bounding memory use against a
	/// value that never completes (e.g. one truncated by a misbehaving sender).
	/// </param>
	/// <param name="cancellationToken">A cancellation token that applies throughout the enumeration.</param>
	/// <returns>An async sequence of the values read, ending gracefully when the reader reaches its end.</returns>
	public async IAsyncEnumerable<T?> DeserializeAllAsync<T, TProvider>(PipeReader reader, long maxBufferedSize = DefaultMaxBufferedValueSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(reader);
		JsonValueBoundaryScanner scanner = new(new JsonReaderOptions { AllowTrailingCommas = this.AllowTrailingCommas, CommentHandling = this.CommentHandling });
		while (true)
		{
			(bool hasValue, T? value) = await reader.ReadValueAsync(
				scanner,
				valueBytes => this.Deserialize<T, TProvider>(valueBytes, cancellationToken),
				maxBufferedSize,
				cancellationToken).ConfigureAwait(false);
			if (!hasValue)
			{
				yield break;
			}

			yield return value;
		}
	}
}
