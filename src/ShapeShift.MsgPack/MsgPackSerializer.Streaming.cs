// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace ShapeShift.MsgPack;

/// <content>
/// Endless top-level streaming, length-prefixed framing, and targeted path deserialization over asynchronous
/// sources. None of these members buffer more than the one value (or frame) they are currently resolving, and
/// none of them consolidate a segmented buffer: the synchronous <see cref="MsgPackDecoder"/> reads the pipe's
/// own segments in place.
/// </content>
public sealed partial record MsgPackSerializer
{
	/// <summary>
	/// Asynchronously writes a sequence of MessagePack values, one after another, to a <see cref="PipeWriter"/>.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="writer">The destination writer. This method flushes it but does not complete it.</param>
	/// <param name="values">The values to write. The sequence may be endless; this method writes until it ends or the operation is canceled.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	public ValueTask SerializeAllAsync<T>(PipeWriter writer, IAsyncEnumerable<T?> values, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeAllAsync<T, T>(writer, values, cancellationToken);

	/// <summary>
	/// Asynchronously writes a sequence of MessagePack values, one after another, to a <see cref="PipeWriter"/>
	/// using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="writer">The destination writer. This method flushes it but does not complete it.</param>
	/// <param name="values">The values to write. The sequence may be endless; this method writes until it ends or the operation is canceled.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	/// <remarks>
	/// Each value is converted synchronously and then flushed, so a slow consumer applies backpressure to the
	/// producer between values rather than accumulating an unbounded buffer.
	/// </remarks>
	public async ValueTask SerializeAllAsync<T, TProvider>(PipeWriter writer, IAsyncEnumerable<T?> values, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(values);
		await foreach (T? value in values.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			this.Serialize<T, TProvider>(writer, value, cancellationToken);
			await writer.FlushAndThrowIfCanceledAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously writes a sequence of MessagePack values, one after another, to a <see cref="PipeWriter"/>.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="writer">The destination writer. This method flushes it but does not complete it.</param>
	/// <param name="values">The values to write.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	public ValueTask SerializeAllAsync<T>(PipeWriter writer, IEnumerable<T?> values, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeAllAsync<T, T>(writer, values, cancellationToken);

	/// <summary>
	/// Asynchronously writes a sequence of MessagePack values, one after another, to a <see cref="PipeWriter"/>
	/// using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="writer">The destination writer. This method flushes it but does not complete it.</param>
	/// <param name="values">The values to write.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	/// <remarks>
	/// Each value is converted synchronously and then flushed, so a slow consumer applies backpressure to the
	/// producer between values rather than accumulating an unbounded buffer.
	/// </remarks>
	public async ValueTask SerializeAllAsync<T, TProvider>(PipeWriter writer, IEnumerable<T?> values, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(values);
		foreach (T? value in values)
		{
			cancellationToken.ThrowIfCancellationRequested();
			this.Serialize<T, TProvider>(writer, value, cancellationToken);
			await writer.FlushAndThrowIfCanceledAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously writes a sequence of MessagePack values, one after another, to a stream.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="stream">The destination stream. It is not closed or disposed by this method.</param>
	/// <param name="values">The values to write. The sequence may be endless; this method writes until it ends or the operation is canceled.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	public ValueTask SerializeAllAsync<T>(Stream stream, IAsyncEnumerable<T?> values, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeAllAsync<T, T>(stream, values, cancellationToken);

	/// <summary>
	/// Asynchronously writes a sequence of MessagePack values, one after another, to a stream using a specified
	/// shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="stream">The destination stream. It is not closed or disposed by this method.</param>
	/// <param name="values">The values to write. The sequence may be endless; this method writes until it ends or the operation is canceled.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	public async ValueTask SerializeAllAsync<T, TProvider>(Stream stream, IAsyncEnumerable<T?> values, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(stream);
		PipeWriter writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
		try
		{
			await this.SerializeAllAsync<T, TProvider>(writer, values, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await writer.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously reads a sequence of whole top-level MessagePack values from a stream, buffering only as much
	/// input as each value requires.
	/// </summary>
	/// <typeparam name="T">The type of each top-level value.</typeparam>
	/// <param name="stream">The source stream. It is not closed or disposed by this method.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for any single value, bounding memory use against a
	/// value that never completes (e.g. one truncated by a misbehaving sender, or a hostile/corrupt length header).
	/// </param>
	/// <param name="cancellationToken">A cancellation token that applies throughout the enumeration.</param>
	/// <returns>An async sequence of the values read, ending gracefully when the stream reaches its end.</returns>
	public IAsyncEnumerable<T?> DeserializeAllAsync<T>(Stream stream, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAllAsync<T, T>(stream, maxBufferedSize, cancellationToken);

	/// <summary>
	/// Asynchronously reads a sequence of whole top-level MessagePack values from a stream using a specified shape
	/// provider, buffering only as much input as each value requires.
	/// </summary>
	/// <typeparam name="T">The type of each top-level value.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="stream">The source stream. It is not closed or disposed by this method.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for any single value, bounding memory use against a
	/// value that never completes (e.g. one truncated by a misbehaving sender, or a hostile/corrupt length header).
	/// </param>
	/// <param name="cancellationToken">A cancellation token that applies throughout the enumeration.</param>
	/// <returns>An async sequence of the values read, ending gracefully when the stream reaches its end.</returns>
	public async IAsyncEnumerable<T?> DeserializeAllAsync<T, TProvider>(Stream stream, long maxBufferedSize = DefaultMaxBufferedValueSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(stream);
		PipeReader reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
		try
		{
			await foreach (T? value in this.DeserializeAllAsync<T, TProvider>(reader, maxBufferedSize, cancellationToken).ConfigureAwait(false))
			{
				yield return value;
			}
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously writes one MessagePack value to a <see cref="PipeWriter"/> inside a length-prefixed frame.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="writer">The destination writer. This method flushes it but does not complete it.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	/// <remarks>See <see cref="MsgPackFraming"/> for the frame format and when framing is worth its four bytes.</remarks>
	public ValueTask SerializeFrameAsync<T>(PipeWriter writer, T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeFrameAsync<T, T>(writer, value, cancellationToken);

	/// <summary>
	/// Asynchronously writes one MessagePack value to a <see cref="PipeWriter"/> inside a length-prefixed frame
	/// using a specified shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="writer">The destination writer. This method flushes it but does not complete it.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	/// <remarks>See <see cref="MsgPackFraming"/> for the frame format and when framing is worth its four bytes.</remarks>
	public async ValueTask SerializeFrameAsync<T, TProvider>(PipeWriter writer, T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(writer);

		// The length prefix cannot be written until the value's length is known, so the value is converted into a
		// scratch buffer first. This is the one place framing costs more than the four bytes it adds.
		ArrayBufferWriter<byte> frame = new();
		this.Serialize<T, TProvider>(frame, value, cancellationToken);
		WriteFrame(writer, frame.WrittenSpan);
		await writer.FlushAndThrowIfCanceledAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Asynchronously writes one MessagePack value to a stream inside a length-prefixed frame.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="stream">The destination stream. It is not closed or disposed by this method.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	/// <remarks>See <see cref="MsgPackFraming"/> for the frame format and when framing is worth its four bytes.</remarks>
	public ValueTask SerializeFrameAsync<T>(Stream stream, T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeFrameAsync<T, T>(stream, value, cancellationToken);

	/// <summary>
	/// Asynchronously writes one MessagePack value to a stream inside a length-prefixed frame using a specified
	/// shape provider.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="stream">The destination stream. It is not closed or disposed by this method.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that represents the operation.</returns>
	/// <remarks>See <see cref="MsgPackFraming"/> for the frame format and when framing is worth its four bytes.</remarks>
	public async ValueTask SerializeFrameAsync<T, TProvider>(Stream stream, T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(stream);
		PipeWriter writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
		try
		{
			await this.SerializeFrameAsync<T, TProvider>(writer, value, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await writer.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously reads one length-prefixed frame from a <see cref="PipeReader"/> and deserializes the single
	/// MessagePack value it contains.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="maxFrameLength">The largest frame this call will accept, checked against the length prefix before any of the frame is buffered.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
	/// <exception cref="DecoderException">Thrown when the reader has no more frames, ends inside one, or a frame does not contain exactly one MessagePack value.</exception>
	/// <exception cref="ShapeShiftSerializationException">Thrown when a frame declares a length greater than <paramref name="maxFrameLength"/>.</exception>
	public ValueTask<T?> DeserializeFrameAsync<T>(PipeReader reader, long maxFrameLength = MsgPackFraming.DefaultMaxFrameLength, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeFrameAsync<T, T>(reader, maxFrameLength, cancellationToken);

	/// <summary>
	/// Asynchronously reads one length-prefixed frame from a <see cref="PipeReader"/> using a specified shape
	/// provider, and deserializes the single MessagePack value it contains.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="maxFrameLength">The largest frame this call will accept, checked against the length prefix before any of the frame is buffered.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
	/// <exception cref="DecoderException">Thrown when the reader has no more frames, ends inside one, or a frame does not contain exactly one MessagePack value.</exception>
	/// <exception cref="ShapeShiftSerializationException">Thrown when a frame declares a length greater than <paramref name="maxFrameLength"/>.</exception>
	public async ValueTask<T?> DeserializeFrameAsync<T, TProvider>(PipeReader reader, long maxFrameLength = MsgPackFraming.DefaultMaxFrameLength, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		(bool hasFrame, T? value) = await this.ReadFrameAsync<T, TProvider>(reader, maxFrameLength, cancellationToken).ConfigureAwait(false);
		return hasFrame ? value : throw new DecoderException("The input did not contain a MessagePack frame.");
	}

	/// <summary>
	/// Asynchronously reads one length-prefixed frame from a stream and deserializes the single MessagePack value
	/// it contains.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="stream">The source stream. It is not closed or disposed by this method.</param>
	/// <param name="maxFrameLength">The largest frame this call will accept, checked against the length prefix before any of the frame is buffered.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
	/// <exception cref="DecoderException">Thrown when the stream has no more frames, ends inside one, or a frame does not contain exactly one MessagePack value.</exception>
	/// <exception cref="ShapeShiftSerializationException">Thrown when a frame declares a length greater than <paramref name="maxFrameLength"/>.</exception>
	public ValueTask<T?> DeserializeFrameAsync<T>(Stream stream, long maxFrameLength = MsgPackFraming.DefaultMaxFrameLength, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeFrameAsync<T, T>(stream, maxFrameLength, cancellationToken);

	/// <summary>
	/// Asynchronously reads one length-prefixed frame from a stream using a specified shape provider, and
	/// deserializes the single MessagePack value it contains.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="stream">The source stream. It is not closed or disposed by this method.</param>
	/// <param name="maxFrameLength">The largest frame this call will accept, checked against the length prefix before any of the frame is buffered.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The decoded value.</returns>
	/// <exception cref="DecoderException">Thrown when the stream has no more frames, ends inside one, or a frame does not contain exactly one MessagePack value.</exception>
	/// <exception cref="ShapeShiftSerializationException">Thrown when a frame declares a length greater than <paramref name="maxFrameLength"/>.</exception>
	public async ValueTask<T?> DeserializeFrameAsync<T, TProvider>(Stream stream, long maxFrameLength = MsgPackFraming.DefaultMaxFrameLength, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(stream);
		PipeReader reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
		try
		{
			return await this.DeserializeFrameAsync<T, TProvider>(reader, maxFrameLength, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously reads an endless sequence of length-prefixed frames from a <see cref="PipeReader"/>.
	/// </summary>
	/// <typeparam name="T">The type of each framed value.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="maxFrameLength">The largest frame this call will accept, checked against each length prefix before any of that frame is buffered.</param>
	/// <param name="cancellationToken">A cancellation token that applies throughout the enumeration.</param>
	/// <returns>An async sequence of the framed values, ending gracefully when the reader reaches its end between frames.</returns>
	/// <exception cref="DecoderException">Thrown when the reader ends inside a frame, or a frame does not contain exactly one MessagePack value.</exception>
	/// <exception cref="ShapeShiftSerializationException">Thrown when a frame declares a length greater than <paramref name="maxFrameLength"/>.</exception>
	public IAsyncEnumerable<T?> DeserializeAllFramesAsync<T>(PipeReader reader, long maxFrameLength = MsgPackFraming.DefaultMaxFrameLength, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAllFramesAsync<T, T>(reader, maxFrameLength, cancellationToken);

	/// <summary>
	/// Asynchronously reads an endless sequence of length-prefixed frames from a <see cref="PipeReader"/> using a
	/// specified shape provider.
	/// </summary>
	/// <typeparam name="T">The type of each framed value.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="maxFrameLength">The largest frame this call will accept, checked against each length prefix before any of that frame is buffered.</param>
	/// <param name="cancellationToken">A cancellation token that applies throughout the enumeration.</param>
	/// <returns>An async sequence of the framed values, ending gracefully when the reader reaches its end between frames.</returns>
	/// <exception cref="DecoderException">Thrown when the reader ends inside a frame, or a frame does not contain exactly one MessagePack value.</exception>
	/// <exception cref="ShapeShiftSerializationException">Thrown when a frame declares a length greater than <paramref name="maxFrameLength"/>.</exception>
	public async IAsyncEnumerable<T?> DeserializeAllFramesAsync<T, TProvider>(PipeReader reader, long maxFrameLength = MsgPackFraming.DefaultMaxFrameLength, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		while (true)
		{
			(bool hasFrame, T? value) = await this.ReadFrameAsync<T, TProvider>(reader, maxFrameLength, cancellationToken).ConfigureAwait(false);
			if (!hasFrame)
			{
				yield break;
			}

			yield return value;
		}
	}

	/// <summary>
	/// Asynchronously reads an endless sequence of length-prefixed frames from a stream.
	/// </summary>
	/// <typeparam name="T">The type of each framed value.</typeparam>
	/// <param name="stream">The source stream. It is not closed or disposed by this method.</param>
	/// <param name="maxFrameLength">The largest frame this call will accept, checked against each length prefix before any of that frame is buffered.</param>
	/// <param name="cancellationToken">A cancellation token that applies throughout the enumeration.</param>
	/// <returns>An async sequence of the framed values, ending gracefully when the stream reaches its end between frames.</returns>
	/// <exception cref="DecoderException">Thrown when the stream ends inside a frame, or a frame does not contain exactly one MessagePack value.</exception>
	/// <exception cref="ShapeShiftSerializationException">Thrown when a frame declares a length greater than <paramref name="maxFrameLength"/>.</exception>
	public IAsyncEnumerable<T?> DeserializeAllFramesAsync<T>(Stream stream, long maxFrameLength = MsgPackFraming.DefaultMaxFrameLength, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAllFramesAsync<T, T>(stream, maxFrameLength, cancellationToken);

	/// <summary>
	/// Asynchronously reads an endless sequence of length-prefixed frames from a stream using a specified shape
	/// provider.
	/// </summary>
	/// <typeparam name="T">The type of each framed value.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="stream">The source stream. It is not closed or disposed by this method.</param>
	/// <param name="maxFrameLength">The largest frame this call will accept, checked against each length prefix before any of that frame is buffered.</param>
	/// <param name="cancellationToken">A cancellation token that applies throughout the enumeration.</param>
	/// <returns>An async sequence of the framed values, ending gracefully when the stream reaches its end between frames.</returns>
	/// <exception cref="DecoderException">Thrown when the stream ends inside a frame, or a frame does not contain exactly one MessagePack value.</exception>
	/// <exception cref="ShapeShiftSerializationException">Thrown when a frame declares a length greater than <paramref name="maxFrameLength"/>.</exception>
	public async IAsyncEnumerable<T?> DeserializeAllFramesAsync<T, TProvider>(Stream stream, long maxFrameLength = MsgPackFraming.DefaultMaxFrameLength, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(stream);
		PipeReader reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
		try
		{
			await foreach (T? value in this.DeserializeAllFramesAsync<T, TProvider>(reader, maxFrameLength, cancellationToken).ConfigureAwait(false))
			{
				yield return value;
			}
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously reads one top-level MessagePack value from a <see cref="PipeReader"/> and deserializes only
	/// the fragment found at a given <see cref="ShapeShiftPath"/> within it.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the fragment as.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="path">The location, within the next top-level value, of the value to deserialize.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for the enclosing top-level value's end, bounding
	/// memory use against a value that never completes.
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>
	/// A tuple whose <c>Found</c> is <see langword="true" /> and whose <c>Value</c> is the deserialized fragment,
	/// or whose <c>Found</c> is <see langword="false" /> because the path is not present in the value that was read.
	/// </returns>
	/// <exception cref="DecoderException">Thrown when the reader has no more values, or ends in the middle of one.</exception>
	/// <remarks>
	/// The enclosing value's bytes are buffered (a value's extent cannot be known before its framing has been
	/// walked), but they are never copied into one contiguous buffer: the path is walked directly over the pipe's
	/// own segments, and everything that is not on the path is stepped over rather than parsed.
	/// </remarks>
	public ValueTask<(bool Found, T? Value)> TryDeserializeFragmentAsync<T>(PipeReader reader, ShapeShiftPath path, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.TryDeserializeFragmentAsync<T, T>(reader, path, maxBufferedSize, cancellationToken);

	/// <summary>
	/// Asynchronously reads one top-level MessagePack value from a <see cref="PipeReader"/> using a specified shape
	/// provider, and deserializes only the fragment found at a given <see cref="ShapeShiftPath"/> within it.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the fragment as.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="reader">The source reader. This method does not complete it; the caller retains ownership.</param>
	/// <param name="path">The location, within the next top-level value, of the value to deserialize.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for the enclosing top-level value's end, bounding
	/// memory use against a value that never completes.
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>
	/// A tuple whose <c>Found</c> is <see langword="true" /> and whose <c>Value</c> is the deserialized fragment,
	/// or whose <c>Found</c> is <see langword="false" /> because the path is not present in the value that was read.
	/// </returns>
	/// <exception cref="DecoderException">Thrown when the reader has no more values, or ends in the middle of one.</exception>
	/// <remarks>
	/// The enclosing value's bytes are buffered (a value's extent cannot be known before its framing has been
	/// walked), but they are never copied into one contiguous buffer: the path is walked directly over the pipe's
	/// own segments, and everything that is not on the path is stepped over rather than parsed.
	/// </remarks>
	public async ValueTask<(bool Found, T? Value)> TryDeserializeFragmentAsync<T, TProvider>(PipeReader reader, ShapeShiftPath path, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(reader);
		MsgPackValueBoundaryScanner scanner = new();
		(bool hasValue, (bool Found, T? Value) fragment) = await reader.ReadValueAsync(
			scanner,
			valueBytes => this.SeekAndDeserialize<T, TProvider>(valueBytes, path, cancellationToken),
			maxBufferedSize,
			cancellationToken).ConfigureAwait(false);
		return hasValue ? fragment : throw new DecoderException("The input did not contain any value to deserialize.");
	}

	/// <summary>
	/// Asynchronously reads one top-level MessagePack value from a stream and deserializes only the fragment found
	/// at a given <see cref="ShapeShiftPath"/> within it.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the fragment as.</typeparam>
	/// <param name="stream">The source stream. It is not closed or disposed by this method.</param>
	/// <param name="path">The location, within the next top-level value, of the value to deserialize.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for the enclosing top-level value's end, bounding
	/// memory use against a value that never completes.
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>
	/// A tuple whose <c>Found</c> is <see langword="true" /> and whose <c>Value</c> is the deserialized fragment,
	/// or whose <c>Found</c> is <see langword="false" /> because the path is not present in the value that was read.
	/// </returns>
	/// <exception cref="DecoderException">Thrown when the stream has no more values, or ends in the middle of one.</exception>
	public ValueTask<(bool Found, T? Value)> TryDeserializeFragmentAsync<T>(Stream stream, ShapeShiftPath path, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.TryDeserializeFragmentAsync<T, T>(stream, path, maxBufferedSize, cancellationToken);

	/// <summary>
	/// Asynchronously reads one top-level MessagePack value from a stream using a specified shape provider, and
	/// deserializes only the fragment found at a given <see cref="ShapeShiftPath"/> within it.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the fragment as.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="stream">The source stream. It is not closed or disposed by this method.</param>
	/// <param name="path">The location, within the next top-level value, of the value to deserialize.</param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes to buffer while searching for the enclosing top-level value's end, bounding
	/// memory use against a value that never completes.
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>
	/// A tuple whose <c>Found</c> is <see langword="true" /> and whose <c>Value</c> is the deserialized fragment,
	/// or whose <c>Found</c> is <see langword="false" /> because the path is not present in the value that was read.
	/// </returns>
	/// <exception cref="DecoderException">Thrown when the stream has no more values, or ends in the middle of one.</exception>
	public async ValueTask<(bool Found, T? Value)> TryDeserializeFragmentAsync<T, TProvider>(Stream stream, ShapeShiftPath path, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(stream);
		PipeReader reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
		try
		{
			return await this.TryDeserializeFragmentAsync<T, TProvider>(reader, path, maxBufferedSize, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Writes a length-prefixed frame around already-encoded MessagePack bytes.
	/// </summary>
	/// <param name="writer">The destination writer.</param>
	/// <param name="messagePack">Exactly one complete, well-formed MessagePack value.</param>
	private static void WriteFrame(PipeWriter writer, ReadOnlySpan<byte> messagePack)
	{
		Span<byte> destination = writer.GetSpan(MsgPackFraming.LengthPrefixByteCount + messagePack.Length);
		BinaryPrimitives.WriteUInt32BigEndian(destination, checked((uint)messagePack.Length));
		messagePack.CopyTo(destination[MsgPackFraming.LengthPrefixByteCount..]);
		writer.Advance(MsgPackFraming.LengthPrefixByteCount + messagePack.Length);
	}

	/// <summary>
	/// Reads a frame's big-endian length prefix from the front of a buffer.
	/// </summary>
	/// <param name="buffer">A buffer holding at least <see cref="MsgPackFraming.LengthPrefixByteCount"/> bytes.</param>
	/// <returns>The declared length of the frame that follows.</returns>
	private static long ReadFrameLength(in ReadOnlySequence<byte> buffer)
	{
		SequenceReader<byte> reader = new(buffer);
		return reader.TryReadBigEndian(out int length) ? unchecked((uint)length) : throw new DecoderException("A MessagePack frame is missing its length prefix.");
	}

	/// <summary>
	/// Seeks to a path within one complete top-level value and deserializes whatever is found there.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the fragment as.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="valueBytes">Exactly one complete MessagePack value, possibly spread across segments.</param>
	/// <param name="path">The location, within that value, of the fragment to deserialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>Whether the path was found, and the fragment if it was.</returns>
	private (bool Found, T? Value) SeekAndDeserialize<T, TProvider>(in ReadOnlySequence<byte> valueBytes, ShapeShiftPath path, CancellationToken cancellationToken)
		where TProvider : IShapeable<T>
	{
		MsgPackDecoder decoder = new(valueBytes);
		bool found = this.TryDeserializeFragment(ref decoder, path, TProvider.GetTypeShape(), out T? value, cancellationToken);
		return (found, value);
	}

	/// <summary>
	/// Reads the next length-prefixed frame, if the reader has one.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The type shape provider.</typeparam>
	/// <param name="reader">The source reader.</param>
	/// <param name="maxFrameLength">The largest frame this call will accept.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>
	/// A tuple whose <c>HasFrame</c> is <see langword="false" /> when the reader reached its end cleanly between
	/// frames.
	/// </returns>
	private async ValueTask<(bool HasFrame, T? Value)> ReadFrameAsync<T, TProvider>(PipeReader reader, long maxFrameLength, CancellationToken cancellationToken)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(reader);
		while (true)
		{
			ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
			if (result.IsCanceled)
			{
				throw new OperationCanceledException(cancellationToken);
			}

			ReadOnlySequence<byte> buffer = result.Buffer;
			if (buffer.Length >= MsgPackFraming.LengthPrefixByteCount)
			{
				long frameLength = ReadFrameLength(buffer);
				if (frameLength > maxFrameLength)
				{
					// Reject before buffering: the prefix alone is enough to know this frame is unacceptable.
					reader.AdvanceTo(buffer.Start, buffer.End);
					throw new ShapeShiftSerializationException($"A MessagePack frame declares {frameLength} bytes, which exceeds the maximum of {maxFrameLength}.");
				}

				if (buffer.Length >= MsgPackFraming.LengthPrefixByteCount + frameLength)
				{
					ReadOnlySequence<byte> frame = buffer.Slice(MsgPackFraming.LengthPrefixByteCount, frameLength);
					T? value = this.Deserialize<T, TProvider>(frame, cancellationToken);
					SequencePosition end = buffer.GetPosition(MsgPackFraming.LengthPrefixByteCount + frameLength);
					reader.AdvanceTo(end, end);
					return (true, value);
				}
			}

			if (result.IsCompleted)
			{
				if (buffer.IsEmpty)
				{
					reader.AdvanceTo(buffer.End);
					return (false, default);
				}

				reader.AdvanceTo(buffer.Start, buffer.End);
				throw new DecoderException("The input ended in the middle of a MessagePack frame.");
			}

			reader.AdvanceTo(buffer.Start, buffer.End);
		}
	}
}
