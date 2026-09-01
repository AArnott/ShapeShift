// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

namespace ShapeShift;

/// <summary>
/// An <see cref="IBufferWriter{T}"/> that rents temporary storage and produces an exact-sized array.
/// </summary>
internal sealed class PooledByteBufferWriter : IBufferWriter<byte>, IDisposable
{
	private byte[] buffer = Array.Empty<byte>();
	private int writtenCount;

	/// <summary>
	/// Gets the written portion of the buffer.
	/// </summary>
	public ReadOnlySpan<byte> WrittenSpan => this.buffer.AsSpan(0, this.writtenCount);

	/// <inheritdoc/>
	public void Advance(int count)
	{
		if ((uint)count > (uint)(this.buffer.Length - this.writtenCount))
		{
			throw new ArgumentOutOfRangeException(nameof(count));
		}

		this.writtenCount += count;
	}

	/// <inheritdoc/>
	public Memory<byte> GetMemory(int sizeHint = 0)
	{
		this.EnsureCapacity(sizeHint);
		return this.buffer.AsMemory(this.writtenCount);
	}

	/// <inheritdoc/>
	public Span<byte> GetSpan(int sizeHint = 0)
	{
		this.EnsureCapacity(sizeHint);
		return this.buffer.AsSpan(this.writtenCount);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		byte[] buffer = this.buffer;
		this.buffer = Array.Empty<byte>();
		this.writtenCount = 0;
		if (buffer.Length > 0)
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	private void EnsureCapacity(int sizeHint)
	{
		if (sizeHint < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(sizeHint));
		}

		if (sizeHint == 0)
		{
			sizeHint = 1;
		}

		if (sizeHint <= this.buffer.Length - this.writtenCount)
		{
			return;
		}

		int requiredCapacity = checked(this.writtenCount + sizeHint);
		byte[] newBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(requiredCapacity, this.buffer.Length * 2));
		this.WrittenSpan.CopyTo(newBuffer);
		byte[] oldBuffer = this.buffer;
		this.buffer = newBuffer;
		if (oldBuffer.Length > 0)
		{
			ArrayPool<byte>.Shared.Return(oldBuffer);
		}
	}
}
