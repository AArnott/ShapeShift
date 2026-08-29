// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.MsgPack;

/// <summary>
/// Serializes PolyType-described object graphs as MessagePack.
/// </summary>
public sealed record MsgPackSerializer : ShapeShiftSerializer<MsgPackEncoder, MsgPackDecoder>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MsgPackSerializer"/> class.
	/// </summary>
	public MsgPackSerializer()
	{
		this.Converters = [new BinaryConverter()];
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
		ArrayBufferWriter<byte> output = new();
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
	/// Asynchronously writes one MessagePack value to a stream.
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
		await stream.WriteAsync(this.Serialize(value, cancellationToken), cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Asynchronously reads one MessagePack value from a stream.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="stream">The source stream.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
	public async ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		where T : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(stream);
		using MemoryStream buffer = new();
		await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
		return this.Deserialize<T>(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)), cancellationToken);
	}
}
