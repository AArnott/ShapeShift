// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.MsgPack;

/// <summary>
/// Recognizes the boundary of one complete, top-level MessagePack value by walking its self-delimiting
/// headers, without buffering more than that one value requires and without fully decoding it.
/// </summary>
/// <remarks>
/// <para>
/// MessagePack has no BCL equivalent of <see cref="System.Text.Json.Utf8JsonReader"/>'s incremental-parsing
/// support, so this type implements a small, dedicated, resumable scan. It intentionally does not reuse
/// <see cref="MsgPackDecoder"/> for this purpose: that type is a <see langword="ref"/> struct whose <c>Skip()</c>
/// recursion uses the CLR call stack to track nested container state, which cannot be paused and resumed
/// across an <see langword="await"/> boundary, and re-running <c>Deserialize()</c> from the start on every
/// buffer growth would re-invoke user converter callbacks and reference-equality tracking for the same
/// already-scanned prefix multiple times.
/// </para>
/// <para>
/// Instead, this scanner walks the same byte-code grammar <see cref="MsgPackDecoder"/> understands, but only
/// far enough to count bytes: it tracks how many sibling values remain in each open container (as a stack of
/// counts) and how many payload bytes remain to be skipped for the value currently in progress, persisting both
/// across calls so a value that arrives one small chunk at a time is recognized without re-scanning bytes
/// already accounted for.
/// </para>
/// <para>
/// Container and string/binary/extension lengths are tracked as <see cref="long"/> rather than <see cref="int"/>
/// specifically so that a hostile or corrupt 32-bit length header (up to <see cref="uint.MaxValue"/>, doubled for
/// map entry counts) cannot overflow the counters used here. This scanner never allocates memory proportional to
/// a claimed length -- it only decrements a counter as real bytes arrive -- but (unlike its JSON counterpart) it
/// cannot release any of those bytes back to the caller before the whole value is recognized: MessagePack has no
/// concept of insignificant bytes between values, so every byte examined here is unconditionally part of the
/// value that the caller's decode step still needs in full afterward. The guard against unbounded buffering of a
/// hostile value is therefore entirely the caller-supplied maximum buffered size (see
/// <see cref="PipeReaderExtensions.ReadValueAsync{T}"/>), not this type.
/// </para>
/// </remarks>
public sealed class MsgPackValueBoundaryScanner : IValueBoundaryScanner
{
	private long[] containerRemaining = new long[8];
	private int depth;
	private long pendingSkip;

	/// <summary>
	/// The number of bytes, counted from the start of the buffer most recently passed to <see cref="TryScan"/>,
	/// that have already been walked by a <see cref="SequenceReader{T}"/> in a previous call for the value
	/// currently in progress.
	/// </summary>
	/// <remarks>
	/// Because <see cref="TryScan"/> never releases any of the value's own bytes back to the caller before it is
	/// fully recognized (see the remarks on this scanner), the buffer is passed in unreleased and growing-at-the-tail
	/// across repeated calls for the same value. This field lets the scan resume from where it left off (via
	/// <see cref="ReadOnlySequence{T}.Slice(long)"/>) rather than re-walking, and re-counting against
	/// <see cref="containerRemaining"/>/<see cref="pendingSkip"/>, bytes already accounted for.
	/// </remarks>
	private long consumed;

	/// <inheritdoc/>
	public bool TryScan(in ReadOnlySequence<byte> buffer, bool isFinalBlock, out SequencePosition end, out SequencePosition examined)
	{
		ReadOnlySequence<byte> remainder = this.consumed == 0 ? buffer : buffer.Slice(this.consumed);
		SequenceReader<byte> reader = new(remainder);

		while (true)
		{
			if (this.pendingSkip > 0)
			{
				long advance = Math.Min(this.pendingSkip, reader.Remaining);
				reader.Advance(advance);
				this.pendingSkip -= advance;
				if (this.pendingSkip > 0)
				{
					break;
				}

				if (!this.CompleteOne())
				{
					goto Done;
				}

				continue;
			}

			if (!reader.TryPeek(out byte code))
			{
				break;
			}

			SequenceReader<byte> probe = reader;
			if (!TryReadHeader(ref probe, code, out long payload, out long containerCount, out bool isContainer))
			{
				break;
			}

			reader = probe;

			if (isContainer)
			{
				if (containerCount == 0)
				{
					if (!this.CompleteOne())
					{
						goto Done;
					}
				}
				else
				{
					this.Push(containerCount);
				}
			}
			else if (payload > 0)
			{
				this.pendingSkip = payload;
				long advance = Math.Min(this.pendingSkip, reader.Remaining);
				reader.Advance(advance);
				this.pendingSkip -= advance;
				if (this.pendingSkip > 0)
				{
					break;
				}

				if (!this.CompleteOne())
				{
					goto Done;
				}
			}
			else
			{
				if (!this.CompleteOne())
				{
					goto Done;
				}
			}
		}

		// Unlike JSON, MessagePack has no insignificant bytes between values: the very first byte of the buffer
		// either begins the value's header or hasn't been examined yet, so once any progress has been made here
		// (a header decoded, a container partially walked, payload bytes skipped) all of those bytes are part of
		// the value the eventual decode() call still needs in full. Nothing may be released before the value is
		// completely recognized, but we still remember how far the reader itself progressed so the next call
		// doesn't re-walk (and double-count against containerRemaining/pendingSkip) bytes already accounted for.
		this.consumed += reader.Consumed;
		end = default;
		examined = buffer.Start;
		return false;

Done:
		end = buffer.GetPosition(this.consumed + reader.Consumed);
		examined = end;
		this.depth = 0;
		this.pendingSkip = 0;
		this.consumed = 0;
		return true;
	}

	/// <summary>
	/// Attempts to read one complete MessagePack header (and, for fixed-width scalars with no separate payload
	/// step, the entire value) from <paramref name="reader"/>, without committing any change unless the whole
	/// header is available.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if a complete header was read (and <paramref name="reader"/> advanced past it);
	/// <see langword="false"/> if more data is needed, in which case <paramref name="reader"/> is unspecified and
	/// must be discarded by the caller (the caller retains its own earlier copy).
	/// </returns>
	private static bool TryReadHeader(ref SequenceReader<byte> reader, byte code, out long payloadLength, out long containerCount, out bool isContainer)
	{
		payloadLength = 0;
		containerCount = 0;
		isContainer = false;

		// Positive fixint / negative fixint: the code byte itself is the entire value.
		if (code <= 0x7f || code >= 0xe0)
		{
			return TryAdvance(ref reader, 1);
		}

		// fixmap / fixarray: 1-byte header; count is encoded in the low nibble.
		if (code is >= 0x80 and <= 0x8f)
		{
			if (!TryAdvance(ref reader, 1))
			{
				return false;
			}

			isContainer = true;
			containerCount = (code & 0x0f) * 2L;
			return true;
		}

		if (code is >= 0x90 and <= 0x9f)
		{
			if (!TryAdvance(ref reader, 1))
			{
				return false;
			}

			isContainer = true;
			containerCount = code & 0x0f;
			return true;
		}

		// fixstr: 1-byte header; length in the low 5 bits.
		if (code is >= 0xa0 and <= 0xbf)
		{
			if (!TryAdvance(ref reader, 1))
			{
				return false;
			}

			payloadLength = code & 0x1f;
			return true;
		}

		switch (code)
		{
			case 0xc0: // nil
			case 0xc2: // false
			case 0xc3: // true
				return TryAdvance(ref reader, 1);
			case 0xc4: // bin8
				return TryReadLengthPrefixedHeader(ref reader, lengthByteCount: 1, out payloadLength);
			case 0xc5: // bin16
				return TryReadLengthPrefixedHeader(ref reader, lengthByteCount: 2, out payloadLength);
			case 0xc6: // bin32
				return TryReadLengthPrefixedHeader(ref reader, lengthByteCount: 4, out payloadLength);
			case 0xc7: // ext8
				return TryReadExtensionHeader(ref reader, lengthByteCount: 1, out payloadLength);
			case 0xc8: // ext16
				return TryReadExtensionHeader(ref reader, lengthByteCount: 2, out payloadLength);
			case 0xc9: // ext32
				return TryReadExtensionHeader(ref reader, lengthByteCount: 4, out payloadLength);
			case 0xca: // float32
				payloadLength = 4;
				return TryAdvance(ref reader, 1);
			case 0xcb: // float64
				payloadLength = 8;
				return TryAdvance(ref reader, 1);
			case 0xcc: // uint8
				payloadLength = 1;
				return TryAdvance(ref reader, 1);
			case 0xcd: // uint16
				payloadLength = 2;
				return TryAdvance(ref reader, 1);
			case 0xce: // uint32
				payloadLength = 4;
				return TryAdvance(ref reader, 1);
			case 0xcf: // uint64
				payloadLength = 8;
				return TryAdvance(ref reader, 1);
			case 0xd0: // int8
				payloadLength = 1;
				return TryAdvance(ref reader, 1);
			case 0xd1: // int16
				payloadLength = 2;
				return TryAdvance(ref reader, 1);
			case 0xd2: // int32
				payloadLength = 4;
				return TryAdvance(ref reader, 1);
			case 0xd3: // int64
				payloadLength = 8;
				return TryAdvance(ref reader, 1);
			case 0xd4: // fixext1
				payloadLength = 1;
				return TryAdvancePastFixExtTypeByte(ref reader);
			case 0xd5: // fixext2
				payloadLength = 2;
				return TryAdvancePastFixExtTypeByte(ref reader);
			case 0xd6: // fixext4
				payloadLength = 4;
				return TryAdvancePastFixExtTypeByte(ref reader);
			case 0xd7: // fixext8
				payloadLength = 8;
				return TryAdvancePastFixExtTypeByte(ref reader);
			case 0xd8: // fixext16
				payloadLength = 16;
				return TryAdvancePastFixExtTypeByte(ref reader);
			case 0xd9: // str8
				return TryReadLengthPrefixedHeader(ref reader, lengthByteCount: 1, out payloadLength);
			case 0xda: // str16
				return TryReadLengthPrefixedHeader(ref reader, lengthByteCount: 2, out payloadLength);
			case 0xdb: // str32
				return TryReadLengthPrefixedHeader(ref reader, lengthByteCount: 4, out payloadLength);
			case 0xdc: // array16
				if (!TryReadLengthPrefixedHeader(ref reader, lengthByteCount: 2, out containerCount))
				{
					return false;
				}

				isContainer = true;
				return true;
			case 0xdd: // array32
				if (!TryReadLengthPrefixedHeader(ref reader, lengthByteCount: 4, out containerCount))
				{
					return false;
				}

				isContainer = true;
				return true;
			case 0xde: // map16
				if (!TryReadLengthPrefixedHeader(ref reader, lengthByteCount: 2, out containerCount))
				{
					return false;
				}

				isContainer = true;
				containerCount *= 2;
				return true;
			case 0xdf: // map32
				if (!TryReadLengthPrefixedHeader(ref reader, lengthByteCount: 4, out containerCount))
				{
					return false;
				}

				isContainer = true;
				containerCount *= 2;
				return true;
			default:
				throw new DecoderException($"Unsupported or reserved MessagePack code 0x{code:x2}.");
		}
	}

	/// <summary>
	/// Reads a 1-byte code followed by a 1, 2, or 4-byte big-endian length, e.g. bin8/16/32, str8/16/32, array16/32.
	/// </summary>
	private static bool TryReadLengthPrefixedHeader(ref SequenceReader<byte> reader, int lengthByteCount, out long length)
	{
		length = 0;
		SequenceReader<byte> probe = reader;
		if (!TryAdvance(ref probe, 1))
		{
			return false;
		}

		if (!TryReadUnsignedBigEndian(ref probe, lengthByteCount, out length))
		{
			return false;
		}

		reader = probe;
		return true;
	}

	/// <summary>
	/// Reads ext8/16/32: a 1-byte code, a 1/2/4-byte big-endian payload length, and a 1-byte extension type.
	/// </summary>
	private static bool TryReadExtensionHeader(ref SequenceReader<byte> reader, int lengthByteCount, out long payloadLength)
	{
		payloadLength = 0;
		SequenceReader<byte> probe = reader;
		if (!TryAdvance(ref probe, 1))
		{
			return false;
		}

		if (!TryReadUnsignedBigEndian(ref probe, lengthByteCount, out payloadLength))
		{
			return false;
		}

		if (!TryAdvance(ref probe, 1))
		{
			// The 1-byte extension type that follows the length.
			return false;
		}

		reader = probe;
		return true;
	}

	/// <summary>
	/// Reads fixext1/2/4/8/16: a 1-byte code followed immediately by a 1-byte extension type (fixed payload length
	/// is supplied by the caller based on the code).
	/// </summary>
	private static bool TryAdvancePastFixExtTypeByte(ref SequenceReader<byte> reader)
	{
		SequenceReader<byte> probe = reader;
		if (!TryAdvance(ref probe, 1) || !TryAdvance(ref probe, 1))
		{
			return false;
		}

		reader = probe;
		return true;
	}

	/// <summary>
	/// Advances <paramref name="reader"/> by exactly <paramref name="count"/> bytes if that many remain,
	/// otherwise leaves it unchanged and returns <see langword="false"/>.
	/// </summary>
	private static bool TryAdvance(ref SequenceReader<byte> reader, long count)
	{
		if (reader.Remaining < count)
		{
			return false;
		}

		reader.Advance(count);
		return true;
	}

	private static bool TryReadUnsignedBigEndian(ref SequenceReader<byte> reader, int byteCount, out long value)
	{
		switch (byteCount)
		{
			case 1:
				if (!reader.TryRead(out byte b))
				{
					value = 0;
					return false;
				}

				value = b;
				return true;
			case 2:
				if (!reader.TryReadBigEndian(out short s))
				{
					value = 0;
					return false;
				}

				value = unchecked((ushort)s);
				return true;
			case 4:
				if (!reader.TryReadBigEndian(out int i))
				{
					value = 0;
					return false;
				}

				value = unchecked((uint)i);
				return true;
			default:
				throw new ArgumentOutOfRangeException(nameof(byteCount));
		}
	}

	/// <summary>
	/// Records that one value (whether a leaf scalar or an entire container just fully skipped) has been
	/// completed, cascading up through any parent containers that are consequently also now complete.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> if scanning should continue (an enclosing container is still open);
	/// <see langword="false"/> if the top-level value is now fully consumed.
	/// </returns>
	private bool CompleteOne()
	{
		while (this.depth > 0)
		{
			if (--this.containerRemaining[this.depth - 1] > 0)
			{
				return true;
			}

			this.depth--;
		}

		return false;
	}

	private void Push(long remaining)
	{
		if (this.depth == this.containerRemaining.Length)
		{
			Array.Resize(ref this.containerRemaining, this.containerRemaining.Length * 2);
		}

		this.containerRemaining[this.depth++] = remaining;
	}
}
