// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Tests;

/// <summary>
/// A read-only <see cref="Stream"/> wrapper that deliberately truncates every read to at most
/// a small number of bytes, regardless of how large a buffer the caller supplies. This exercises
/// callers (such as <see cref="System.IO.Pipelines.PipeReader"/> wrappers) that must tolerate
/// short reads instead of assuming a single call fills the destination buffer.
/// </summary>
public sealed class ChunkedReadStream : Stream
{
	private readonly byte[] data;
	private readonly int maxBytesPerRead;
	private int position;

	/// <summary>
	/// Initializes a new instance of the <see cref="ChunkedReadStream"/> class.
	/// </summary>
	/// <param name="data">The complete content the stream will yield, a few bytes at a time.</param>
	/// <param name="maxBytesPerRead">The maximum number of bytes returned by any single read call.</param>
	public ChunkedReadStream(byte[] data, int maxBytesPerRead = 1)
	{
		this.data = data;
		this.maxBytesPerRead = maxBytesPerRead;
	}

	/// <inheritdoc/>
	public override bool CanRead => true;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <inheritdoc/>
	public override bool CanWrite => false;

	/// <inheritdoc/>
	public override long Length => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Position
	{
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan(offset, count));

	/// <inheritdoc/>
	public override int Read(Span<byte> buffer)
	{
		int remaining = this.data.Length - this.position;
		if (remaining == 0)
		{
			return 0;
		}

		int toCopy = Math.Min(Math.Min(remaining, this.maxBytesPerRead), buffer.Length);
		this.data.AsSpan(this.position, toCopy).CopyTo(buffer);
		this.position += toCopy;
		return toCopy;
	}

	/// <inheritdoc/>
	public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Yield to guarantee this genuinely behaves asynchronously in tests that race cancellation.
		await Task.Yield();
		cancellationToken.ThrowIfCancellationRequested();
		return this.Read(buffer.Span);
	}

	/// <inheritdoc/>
	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		=> this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

	/// <inheritdoc/>
	public override void Flush() => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
