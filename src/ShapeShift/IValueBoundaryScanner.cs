// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// Locates the boundary of the next complete, self-delimited top-level value within a growing buffer,
/// without fully parsing or converting it.
/// </summary>
/// <remarks>
/// <para>
/// Implementations back the asynchronous <c>Stream</c>/<see cref="System.IO.Pipelines.PipeReader"/> based
/// deserialization APIs offered by each format. Because format decoders are typically <see langword="ref" /> structs
/// that cannot survive an <see langword="await" />, those APIs cannot incrementally resume a paused conversion.
/// Instead, they use an <see cref="IValueBoundaryScanner"/> to buffer input, a chunk at a time, only until a
/// complete top-level value is known to be present, and only then invoke the ordinary synchronous decoder
/// exactly once over that value's bytes.
/// </para>
/// <para>
/// A single instance is mutable and stateful across repeated calls to <see cref="TryScan"/>: each call may consume
/// only part of a value (returning <see langword="false" /> to request more input), and the instance remembers its
/// progress so that the next call can resume rather than re-scan from the beginning. Once a call returns
/// <see langword="true" />, the instance is ready to scan a subsequent top-level value (e.g. the next element of a
/// newline-delimited stream) starting from a fresh buffer whose start coincides with the position immediately after
/// the value that was just found.
/// </para>
/// <para>
/// Implementations must not throw for merely-incomplete input; they should return <see langword="false" /> so the
/// caller can supply more bytes (or fail with a clear error once <c>isFinalBlock</c> is <see langword="true" /> and
/// no more bytes will ever come). Genuinely malformed input (e.g. an unrecognized token) may be reported by throwing
/// <see cref="DecoderException"/>.
/// </para>
/// <para>
/// Implementations report, via the <c>examined</c> output of <see cref="TryScan"/>, exactly how much of the
/// buffer they guarantee they will never need to re-inspect -- even when a call returns <see langword="false" />.
/// This lets a caller (such as <see cref="PipeReaderExtensions.ReadValueAsync{T}"/>) release that prefix back to
/// its underlying <see cref="System.IO.Pipelines.PipeReader"/> immediately. Because the eventual decode step still
/// needs every byte of the value once it is fully recognized, an implementation may only report an
/// <c>examined</c> position past a value's first byte once that value is complete (<paramref name="end"/> is
/// known); before a value has begun, however, it is free to report progress through any bytes it can prove are
/// not part of one (for example, insignificant whitespace between JSON tokens), so pure separator bytes between
/// values need not be held onto merely because the next value has not yet arrived.
/// </para>
/// </remarks>
public interface IValueBoundaryScanner
{
	/// <summary>
	/// Attempts to locate the end of the next complete top-level value at the start of <paramref name="buffer"/>.
	/// </summary>
	/// <param name="buffer">
	/// All input buffered so far and not yet consumed. Callers are expected to advance their underlying reader
	/// past <paramref name="examined"/> after every call (whether it returns <see langword="true" /> or
	/// <see langword="false" />), so on the next call for the same value, <paramref name="buffer"/> begins exactly
	/// where the previous call's <paramref name="examined"/> left off.
	/// </param>
	/// <param name="isFinalBlock">
	/// <see langword="true" /> if no further input will ever be appended to <paramref name="buffer"/>
	/// (the source has reached its end).
	/// </param>
	/// <param name="end">
	/// Receives the position, within <paramref name="buffer"/>, immediately after the complete value,
	/// if this method returns <see langword="true" />; otherwise <see langword="default" />.
	/// </param>
	/// <param name="examined">
	/// Receives the position, within <paramref name="buffer"/>, up through which this instance guarantees it will
	/// never need to look again -- regardless of whether this call returns <see langword="true" /> or
	/// <see langword="false" />. When this method returns <see langword="true" />, this always equals
	/// <paramref name="end"/>. When it returns <see langword="false" />, this is <paramref name="buffer"/>'s start
	/// unless the implementation can prove the skipped-over bytes are not part of any value (for example,
	/// whitespace preceding the next JSON token) and therefore safe to discard even though the value itself has
	/// not yet been found. A caller that has reached <paramref name="isFinalBlock"/> and finds this method returns
	/// <see langword="false" /> with <paramref name="examined"/> equal to <paramref name="buffer"/>'s end may
	/// conclude that no further value begins here (a graceful end of a sequence of values), rather than that the
	/// input ended in the middle of one.
	/// </param>
	/// <returns>
	/// <see langword="true" /> if <paramref name="buffer"/> contains (starting at its start) one complete top-level
	/// value; <see langword="false" /> if more input is required before that can be determined.
	/// </returns>
	/// <exception cref="DecoderException">Thrown when the buffered input is definitely malformed.</exception>
	bool TryScan(in ReadOnlySequence<byte> buffer, bool isFinalBlock, out SequencePosition end, out SequencePosition examined);
}
