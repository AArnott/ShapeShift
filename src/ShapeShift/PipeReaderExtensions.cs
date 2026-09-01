// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// Extension methods for <see cref="PipeReader"/> that drive incremental, boundary-scanned value decoding.
/// </summary>
/// <remarks>
/// These members are format-neutral: they know nothing about JSON or MessagePack syntax. Each format package
/// supplies an <see cref="IValueBoundaryScanner"/> implementation and a synchronous decode delegate; this class
/// supplies the shared loop that buffers input from a <see cref="PipeReader"/> a chunk at a time, invoking the
/// decode delegate exactly once, only after a complete top-level value has been confirmed present.
/// </remarks>
public static class PipeReaderExtensions
{
	/// <summary>
	/// Reads and decodes the next complete top-level value from a pipe, buffering only as much input as that one
	/// value requires.
	/// </summary>
	/// <typeparam name="T">The type of value to produce.</typeparam>
	/// <param name="reader">The pipe to read from.</param>
	/// <param name="scanner">
	/// A scanner that recognizes the boundary of one top-level value in the format being read. The same instance
	/// may be reused across repeated calls (e.g. to read a sequence of values from one pipe).
	/// </param>
	/// <param name="decode">
	/// Invoked synchronously with the exact bytes of one complete, buffered top-level value in order to produce
	/// <typeparamref name="T"/>. This runs before the underlying buffer segments are released back to the pipe,
	/// so it must not retain <see cref="ReadOnlySequence{T}"/> or any span/memory derived from it beyond its own return.
	/// </param>
	/// <param name="maxBufferedSize">
	/// The maximum number of bytes this method will retain, at any one time, while still waiting to resolve one
	/// top-level value. Bytes proven to precede the value entirely (such as insignificant whitespace separating
	/// NDJSON-style entries) are released as soon as the scanner accounts for them (see
	/// <see cref="IValueBoundaryScanner.TryScan"/>) and so do not count against this limit; once a value has
	/// begun, though, every byte of it must remain available for the eventual decode step, so this limit bounds
	/// the size of the value itself -- including one very large scalar token, or a value that never completes at
	/// all (e.g. one with a corrupt or hostile, effectively unbounded length header).
	/// </param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>
	/// A tuple whose <c>HasValue</c> is <see langword="true" /> and whose <c>Value</c> is the decoded value, if a
	/// complete top-level value was read; otherwise <c>HasValue</c> is <see langword="false" /> because the pipe
	/// reached its end before any further value began (a graceful end of a sequence of values).
	/// </returns>
	/// <exception cref="DecoderException">
	/// Thrown when the pipe ends in the middle of a value, or when <paramref name="scanner"/> detects malformed input.
	/// </exception>
	/// <exception cref="ShapeShiftSerializationException">
	/// Thrown when a single value would require buffering more than <paramref name="maxBufferedSize"/> bytes.
	/// </exception>
	public static async ValueTask<(bool HasValue, T? Value)> ReadValueAsync<T>(
		this PipeReader reader,
		IValueBoundaryScanner scanner,
		Func<ReadOnlySequence<byte>, T?> decode,
		long maxBufferedSize = long.MaxValue,
		CancellationToken cancellationToken = default)
	{
		Requires.NotNull(reader);
		Requires.NotNull(scanner);
		Requires.NotNull(decode);

		while (true)
		{
			ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
			if (result.IsCanceled)
			{
				throw new OperationCanceledException(cancellationToken);
			}

			ReadOnlySequence<byte> buffer = result.Buffer;
			if (scanner.TryScan(buffer, result.IsCompleted, out SequencePosition end, out SequencePosition examined))
			{
				ReadOnlySequence<byte> valueBytes = buffer.Slice(buffer.Start, end);
				T? value = decode(valueBytes);

				// Bytes up to (and including) the value we just decoded may be released; nothing further has been examined.
				reader.AdvanceTo(end, end);
				return (true, value);
			}

			// The scanner guarantees it will never need to re-inspect anything up through `examined`, so that
			// prefix can always be released regardless of what we decide below. What (if anything) remains
			// pending is exactly the portion the scanner still needs to resolve the value in progress.
			ReadOnlySequence<byte> pending = buffer.Slice(examined);

			if (result.IsCompleted)
			{
				if (pending.IsEmpty)
				{
					reader.AdvanceTo(buffer.End);
					return (false, default);
				}

				reader.AdvanceTo(examined, buffer.End);
				throw new DecoderException("The input ended in the middle of a value.");
			}

			if (pending.Length > maxBufferedSize)
			{
				reader.AdvanceTo(examined, buffer.End);
				throw new ShapeShiftSerializationException($"A single value required buffering more than {maxBufferedSize} bytes.");
			}

			// Release everything the scanner has already accounted for, but mark the rest of the buffer examined
			// so the pipe knows to wait for genuinely new data rather than immediately waking us again.
			reader.AdvanceTo(examined, buffer.End);
		}
	}
}
