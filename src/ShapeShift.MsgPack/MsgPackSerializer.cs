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
