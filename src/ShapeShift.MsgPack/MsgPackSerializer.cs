// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.MsgPack;

/// <summary>
/// Serializes PolyType-described object graphs as MessagePack.
/// </summary>
public sealed partial record MsgPackSerializer : ShapeShiftSerializer<MsgPackEncoder, MsgPackDecoder>, IReferencePreservingSerializer<MsgPackEncoder, MsgPackDecoder>
{
	/// <summary>
	/// The default maximum number of bytes buffered while searching for one complete top-level value via the
	/// incremental <see cref="Stream"/>/<see cref="PipeReader"/> based deserialization APIs on this type.
	/// </summary>
	private const long DefaultMaxBufferedValueSize = 64 * 1024 * 1024;

	/// <summary>
	/// The factory that supplies positional (array) converters, which is always present and always consulted last.
	/// </summary>
	private static readonly MsgPackArrayContractFactory ArrayContractFactory = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="MsgPackSerializer"/> class.
	/// </summary>
	public MsgPackSerializer()
	{
		this.Converters = [new BinaryConverter()];
		this.ConverterFactories = [];
	}

	/// <inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}.ConverterFactories"/>
	/// <remarks>
	/// The built-in factory that implements <see cref="MsgPackArrayContractAttribute"/> is always retained, and
	/// always consulted after the factories assigned here, so assigning this property can extend but never disable
	/// positional contracts.
	/// </remarks>
	public new ImmutableArray<IShapeShiftConverterFactory<MsgPackEncoder, MsgPackDecoder>> ConverterFactories
	{
		get => base.ConverterFactories;
		init => base.ConverterFactories = [.. value.IsDefault ? [] : value.Where(f => f is not MsgPackArrayContractFactory), ArrayContractFactory];
	}

	/// <summary>
	/// Writes a reference to an object that has already been written in this payload.
	/// </summary>
	/// <param name="writer">The encoder.</param>
	/// <param name="referenceId">The 0-based order in which the referenced object was first written.</param>
	/// <param name="context">The serialization context.</param>
	/// <remarks>
	/// The reference is written as the MessagePack extension <see cref="MsgPackExtensionCodes.Reference"/>,
	/// whose payload is the smallest big-endian unsigned integer (1, 2, or 4 bytes) that can carry
	/// <paramref name="referenceId"/>, for a total of 3 to 6 bytes.
	/// </remarks>
	void IReferencePreservingSerializer<MsgPackEncoder, MsgPackDecoder>.WriteObjectReference(ref MsgPackEncoder writer, int referenceId, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
	{
		if (referenceId < 0)
		{
			throw new ShapeShiftSerializationException("Reference identifiers cannot be negative.");
		}

		Span<byte> payload = stackalloc byte[4];
		if (referenceId <= byte.MaxValue)
		{
			payload[0] = (byte)referenceId;
			writer.WriteExtension(MsgPackExtensionCodes.Reference, payload[..1]);
		}
		else if (referenceId <= ushort.MaxValue)
		{
			BinaryPrimitives.WriteUInt16BigEndian(payload, (ushort)referenceId);
			writer.WriteExtension(MsgPackExtensionCodes.Reference, payload[..2]);
		}
		else
		{
			BinaryPrimitives.WriteUInt32BigEndian(payload, (uint)referenceId);
			writer.WriteExtension(MsgPackExtensionCodes.Reference, payload);
		}
	}

	/// <summary>
	/// Reads a reference to an object that appeared earlier in this payload, if the decoder is positioned at one.
	/// </summary>
	/// <param name="reader">The decoder.</param>
	/// <param name="referenceId">Receives the referenced object's identifier when this method returns <see langword="true" />.</param>
	/// <param name="context">The serialization context.</param>
	/// <returns><see langword="true" /> if a reference was read; <see langword="false" /> if the decoder is positioned at an ordinary value.</returns>
	/// <exception cref="DecoderException">Thrown when a reference extension carries a payload of an unsupported length.</exception>
	bool IReferencePreservingSerializer<MsgPackEncoder, MsgPackDecoder>.TryReadObjectReference(ref MsgPackDecoder reader, out int referenceId, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
	{
		if (!reader.TryPeekExtensionHeader(out MsgPackExtensionHeader header) || header.TypeCode != MsgPackExtensionCodes.Reference)
		{
			referenceId = 0;
			return false;
		}

		Span<byte> payload = stackalloc byte[4];
		int length = reader.ReadExtension(MsgPackExtensionCodes.Reference, payload);
		referenceId = length switch
		{
			1 => payload[0],
			2 => BinaryPrimitives.ReadUInt16BigEndian(payload),
			4 => checked((int)BinaryPrimitives.ReadUInt32BigEndian(payload)),
			_ => throw new DecoderException($"A MessagePack object reference must carry 1, 2, or 4 bytes, but this one carries {length}."),
		};
		return true;
	}

	/// <summary>
	/// Serializes a value to MessagePack.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The encoded bytes.</returns>
	public byte[] Serialize<T>(in T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Serialize<T, T>(value, cancellationToken);

	/// <summary>
	/// Serializes a value using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The encoded bytes.</returns>
	public byte[] Serialize<T, TProvider>(in T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		using PooledByteBufferWriter output = new();
		this.Serialize<T, TProvider>(output, value, cancellationToken);
		return output.WrittenSpan.ToArray();
	}

	/// <summary>
	/// Serializes a value into a caller-owned buffer.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="output">The destination.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public void Serialize<T, TProvider>(IBufferWriter<byte> output, in T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(output);
		MsgPackEncoder encoder = new(output);
		this.Serialize(ref encoder, value, TProvider.GetTypeShape(), cancellationToken);
	}

	/// <summary>
	/// Deserializes a value from MessagePack.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="messagePack">The encoded bytes.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
	public T? Deserialize<T>(ReadOnlySpan<byte> messagePack, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Deserialize<T, T>(messagePack, cancellationToken);

	/// <summary>
	/// Deserializes a value from a potentially segmented MessagePack sequence.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="messagePack">The encoded bytes.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
	public T? Deserialize<T>(in ReadOnlySequence<byte> messagePack, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Deserialize<T, T>(messagePack, cancellationToken);

	/// <summary>
	/// Deserializes a value using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="messagePack">The encoded bytes.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
	public T? Deserialize<T, TProvider>(ReadOnlySpan<byte> messagePack, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		MsgPackDecoder decoder = new(messagePack);
		T? value = this.Deserialize(ref decoder, TProvider.GetTypeShape(), cancellationToken);
		decoder.EnsureEndOfDocument();
		return value;
	}

	/// <summary>
	/// Deserializes a value from a potentially segmented MessagePack sequence using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="messagePack">The encoded bytes.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
	public T? Deserialize<T, TProvider>(in ReadOnlySequence<byte> messagePack, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		MsgPackDecoder decoder = new(messagePack);
		T? value = this.Deserialize(ref decoder, TProvider.GetTypeShape(), cancellationToken);
		decoder.EnsureEndOfDocument();
		return value;
	}

	/// <summary>
	/// Attempts to deserialize the value found at a given <see cref="ShapeShiftPath"/> within a MessagePack buffer,
	/// skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="messagePack">The encoded bytes.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="value">Receives the deserialized value if this method returns <see langword="true" />; otherwise <see langword="default" />.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns><see langword="true" /> if <paramref name="path"/> was found; <see langword="false" /> otherwise.</returns>
	public bool TryDeserializeFragment<T>(ReadOnlySpan<byte> messagePack, ShapeShiftPath path, out T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.TryDeserializeFragment<T, T>(messagePack, path, out value, cancellationToken);

	/// <summary>
	/// Attempts to deserialize the value found at a given <see cref="ShapeShiftPath"/> within a MessagePack buffer
	/// using a specified shape provider, skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="messagePack">The encoded bytes.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="value">Receives the deserialized value if this method returns <see langword="true" />; otherwise <see langword="default" />.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns><see langword="true" /> if <paramref name="path"/> was found; <see langword="false" /> otherwise.</returns>
	public bool TryDeserializeFragment<T, TProvider>(ReadOnlySpan<byte> messagePack, ShapeShiftPath path, out T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		MsgPackDecoder decoder = new(messagePack);
		return this.TryDeserializeFragment(ref decoder, path, TProvider.GetTypeShape(), out value, cancellationToken);
	}

	/// <summary>
	/// Attempts to deserialize the value found at a given <see cref="ShapeShiftPath"/> within a potentially segmented
	/// MessagePack sequence, skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="messagePack">The encoded bytes.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="value">Receives the deserialized value if this method returns <see langword="true" />; otherwise <see langword="default" />.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns><see langword="true" /> if <paramref name="path"/> was found; <see langword="false" /> otherwise.</returns>
	public bool TryDeserializeFragment<T>(in ReadOnlySequence<byte> messagePack, ShapeShiftPath path, out T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.TryDeserializeFragment<T, T>(messagePack, path, out value, cancellationToken);

	/// <summary>
	/// Attempts to deserialize the value found at a given <see cref="ShapeShiftPath"/> within a potentially segmented
	/// MessagePack sequence using a specified shape provider, skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="messagePack">The encoded bytes.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="value">Receives the deserialized value if this method returns <see langword="true" />; otherwise <see langword="default" />.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns><see langword="true" /> if <paramref name="path"/> was found; <see langword="false" /> otherwise.</returns>
	public bool TryDeserializeFragment<T, TProvider>(in ReadOnlySequence<byte> messagePack, ShapeShiftPath path, out T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		MsgPackDecoder decoder = new(messagePack);
		return this.TryDeserializeFragment(ref decoder, path, TProvider.GetTypeShape(), out value, cancellationToken);
	}

	/// <summary>
	/// Deserializes the value found at a given <see cref="ShapeShiftPath"/> within a MessagePack buffer,
	/// skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="messagePack">The encoded bytes.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="ShapeShiftSerializationException">Thrown when <paramref name="path"/> could not be found.</exception>
	public T? DeserializeFragment<T>(ReadOnlySpan<byte> messagePack, ShapeShiftPath path, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeFragment<T, T>(messagePack, path, cancellationToken);

	/// <summary>
	/// Deserializes the value found at a given <see cref="ShapeShiftPath"/> within a MessagePack buffer
	/// using a specified shape provider, skipping over everything else without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="messagePack">The encoded bytes.</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="ShapeShiftSerializationException">Thrown when <paramref name="path"/> could not be found.</exception>
	public T? DeserializeFragment<T, TProvider>(ReadOnlySpan<byte> messagePack, ShapeShiftPath path, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		MsgPackDecoder decoder = new(messagePack);
		return this.DeserializeFragment(ref decoder, path, TProvider.GetTypeShape(), cancellationToken);
	}

	/// <summary>
	/// Creates a reader that incrementally enumerates the elements of a MessagePack array,
	/// whether that array is the root of the document or is reached by first seeking into an enclosing document.
	/// </summary>
	/// <typeparam name="T">The type of each element in the array.</typeparam>
	/// <param name="cancellationToken">A cancellation token that applies throughout the lifetime of the reader.</param>
	/// <returns>The reader. Callers should dispose of it (or use a <see langword="using" /> statement) when done.</returns>
	public ShapeShiftSequenceReader<T, MsgPackEncoder, MsgPackDecoder> CreateSequenceReader<T>(CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.CreateSequenceReader<T, T>(cancellationToken);

	/// <summary>
	/// Creates a reader that incrementally enumerates the elements of a MessagePack array using a specified shape provider,
	/// whether that array is the root of the document or is reached by first seeking into an enclosing document.
	/// </summary>
	/// <typeparam name="T">The type of each element in the array.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="cancellationToken">A cancellation token that applies throughout the lifetime of the reader.</param>
	/// <returns>The reader. Callers should dispose of it (or use a <see langword="using" /> statement) when done.</returns>
	public ShapeShiftSequenceReader<T, MsgPackEncoder, MsgPackDecoder> CreateSequenceReader<T, TProvider>(CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.CreateSequenceReader(TProvider.GetTypeShape(), cancellationToken);

	/// <summary>
	/// Creates a reader that incrementally enumerates a sequence of whole top-level MessagePack values sharing one buffer.
	/// </summary>
	/// <typeparam name="T">The type of each top-level value.</typeparam>
	/// <param name="cancellationToken">A cancellation token that applies throughout the lifetime of the reader.</param>
	/// <returns>The reader. Callers should dispose of it (or use a <see langword="using" /> statement) when done.</returns>
	public ShapeShiftDocumentReader<T, MsgPackEncoder, MsgPackDecoder> CreateDocumentReader<T>(CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.CreateDocumentReader<T, T>(cancellationToken);

	/// <summary>
	/// Creates a reader that incrementally enumerates a sequence of whole top-level MessagePack values sharing one buffer
	/// using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The type of each top-level value.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="cancellationToken">A cancellation token that applies throughout the lifetime of the reader.</param>
	/// <returns>The reader. Callers should dispose of it (or use a <see langword="using" /> statement) when done.</returns>
	public ShapeShiftDocumentReader<T, MsgPackEncoder, MsgPackDecoder> CreateDocumentReader<T, TProvider>(CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.CreateDocumentReader(TProvider.GetTypeShape(), cancellationToken);

	/// <summary>
	/// Asynchronously writes one MessagePack value to a <see cref="PipeWriter"/>.
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
	/// Asynchronously writes one MessagePack value to a <see cref="PipeWriter"/> using a specified shape provider.
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
	/// Asynchronously writes one MessagePack value to a stream.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="stream">The destination stream. It is not closed or disposed by this method.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	public ValueTask SerializeAsync<T>(Stream stream, T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeAsync<T, T>(stream, value, cancellationToken);

	/// <summary>
	/// Asynchronously writes one MessagePack value to a stream using a specified shape provider.
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
	/// Asynchronously reads one MessagePack value from a <see cref="PipeReader"/>, buffering only as much input as
	/// that value requires.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for the value, bounding memory use against a value
	/// that never completes (e.g. one truncated by a misbehaving sender, or a hostile/corrupt length header).
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
	/// <exception cref="DecoderException">Thrown if the reader has no more values, or ends in the middle of one.</exception>
	public ValueTask<T?> DeserializeAsync<T>(PipeReader reader, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAsync<T, T>(reader, maxBufferedSize, cancellationToken);

	/// <summary>
	/// Asynchronously reads one MessagePack value from a <see cref="PipeReader"/> using a specified shape provider,
	/// buffering only as much input as that value requires.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for the value, bounding memory use against a value
	/// that never completes (e.g. one truncated by a misbehaving sender, or a hostile/corrupt length header).
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
	/// <exception cref="DecoderException">Thrown if the reader has no more values, or ends in the middle of one.</exception>
	public async ValueTask<T?> DeserializeAsync<T, TProvider>(PipeReader reader, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(reader);
		MsgPackValueBoundaryScanner scanner = new();
		(bool hasValue, T? value) = await reader.ReadValueAsync(
			scanner,
			valueBytes => this.Deserialize<T, TProvider>(valueBytes, cancellationToken),
			maxBufferedSize,
			cancellationToken).ConfigureAwait(false);
		return hasValue ? value : throw new DecoderException("The input did not contain any value to deserialize.");
	}

	/// <summary>
	/// Asynchronously reads one MessagePack value from a stream, buffering only as much input as that value requires.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="stream">The source stream. It is not closed or disposed by this method.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for the value, bounding memory use against a value
	/// that never completes (e.g. one truncated by a misbehaving sender, or a hostile/corrupt length header).
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
	/// <exception cref="DecoderException">Thrown if the stream has no more values, or ends in the middle of one.</exception>
	public ValueTask<T?> DeserializeAsync<T>(Stream stream, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAsync<T, T>(stream, maxBufferedSize, cancellationToken);

	/// <summary>
	/// Asynchronously reads one MessagePack value from a stream using a specified shape provider, buffering only
	/// as much input as that value requires.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="stream">The source stream. It is not closed or disposed by this method.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for the value, bounding memory use against a value
	/// that never completes (e.g. one truncated by a misbehaving sender, or a hostile/corrupt length header).
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
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
	/// Asynchronously reads a sequence of whole top-level MessagePack values sharing one <see cref="PipeReader"/>,
	/// buffering only as much input as each value requires.
	/// </summary>
	/// <typeparam name="T">The type of each top-level value.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for any single value, bounding memory use against a
	/// value that never completes (e.g. one truncated by a misbehaving sender, or a hostile/corrupt length header).
	/// </param>
	/// <param name="cancellationToken">A cancellation token that applies throughout the enumeration.</param>
	/// <returns>An async sequence of the values read, ending gracefully when the reader reaches its end.</returns>
	public IAsyncEnumerable<T?> DeserializeAllAsync<T>(PipeReader reader, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAllAsync<T, T>(reader, maxBufferedSize, cancellationToken);

	/// <summary>
	/// Asynchronously reads a sequence of whole top-level MessagePack values sharing one <see cref="PipeReader"/>
	/// using a specified shape provider, buffering only as much input as each value requires.
	/// </summary>
	/// <typeparam name="T">The type of each top-level value.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for any single value, bounding memory use against a
	/// value that never completes (e.g. one truncated by a misbehaving sender, or a hostile/corrupt length header).
	/// </param>
	/// <param name="cancellationToken">A cancellation token that applies throughout the enumeration.</param>
	/// <returns>An async sequence of the values read, ending gracefully when the reader reaches its end.</returns>
	public async IAsyncEnumerable<T?> DeserializeAllAsync<T, TProvider>(PipeReader reader, long maxBufferedSize = DefaultMaxBufferedValueSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(reader);
		MsgPackValueBoundaryScanner scanner = new();
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
