// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace ShapeShift.MsgPack;

#pragma warning disable SA1201 // Nested implementation types are kept near state fields.

/// <summary>
/// Reads MessagePack tokens from contiguous memory or from a potentially segmented <see cref="ReadOnlySequence{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Segmented input is walked in place. This decoder never consolidates a <see cref="ReadOnlySequence{T}"/> into
/// one contiguous buffer, so skipping over (or seeking past) content that a caller does not want costs nothing but
/// pointer arithmetic, no matter how the input happens to be chopped into segments. Only a value that is actually
/// materialized (a string, a byte array, an extension payload) is copied, and even then only when that one value
/// straddles a segment boundary.
/// </para>
/// </remarks>
public ref struct MsgPackDecoder : IDecoder
{
	/// <summary>
	/// The whole input, when this decoder was created over contiguous memory.
	/// </summary>
	/// <remarks>
	/// Mutually exclusive with <see cref="reader"/>: exactly one of the two is in use, as recorded by <see cref="segmented"/>.
	/// </remarks>
	private readonly ReadOnlySpan<byte> span;

	/// <summary>
	/// A value indicating whether <see cref="reader"/> (rather than <see cref="span"/>) holds the input.
	/// </summary>
	private readonly bool segmented;

	private SequenceReader<byte> reader;
	private Frame[] frames;
	private int depth;
	private int offset;
	private string? stringBuffer;

	private enum ContainerKind
	{
		Map,
		Vector,
	}

	private struct Frame(ContainerKind kind, int remaining)
	{
		internal ContainerKind Kind = kind;
		internal int Remaining = remaining;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MsgPackDecoder"/> struct.
	/// </summary>
	/// <param name="messagePack">The encoded MessagePack value.</param>
	public MsgPackDecoder(ReadOnlySpan<byte> messagePack)
	{
		this.span = messagePack;
		this.frames = new Frame[8];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MsgPackDecoder"/> struct.
	/// </summary>
	/// <param name="messagePack">The encoded MessagePack value, which may be split across any number of segments.</param>
	/// <remarks>
	/// Multi-segment input is read in place; it is never copied into one contiguous buffer.
	/// </remarks>
	public MsgPackDecoder(in ReadOnlySequence<byte> messagePack)
	{
		this.frames = new Frame[8];
		if (messagePack.IsSingleSegment)
		{
			this.span = messagePack.FirstSpan;
		}
		else
		{
			this.segmented = true;
			this.reader = new SequenceReader<byte>(messagePack);
		}
	}

	/// <summary>
	/// Gets the number of unread bytes remaining in the input.
	/// </summary>
	public readonly long UnreadLength => this.segmented ? this.reader.Remaining : this.span.Length - this.offset;

	/// <summary>
	/// Gets the unread bytes that are contiguously available at the decoder's current position.
	/// </summary>
	/// <remarks>
	/// For a decoder created over contiguous memory this is all the unread input. For a decoder created over a
	/// segmented <see cref="ReadOnlySequence{T}"/> this is only the remainder of the current segment, which may be
	/// shorter than <see cref="UnreadLength"/>.
	/// </remarks>
	public readonly ReadOnlySpan<byte> UnreadSpan => this.segmented ? this.reader.UnreadSpan : this.span[this.offset..];

	/// <inheritdoc/>
	public readonly TokenType NextTokenType
	{
		get
		{
			if (this.depth > 0 && this.frames[this.depth - 1].Remaining == 0)
			{
				return this.frames[this.depth - 1].Kind == ContainerKind.Map ? TokenType.EndMap : TokenType.EndVector;
			}

			if (this.UnreadLength == 0)
			{
				return TokenType.EndDocument;
			}

			byte code = this.PeekByte();
			TokenType type = Classify(code, this.PeekExtensionType(code));
			if (this.depth > 0 && this.frames[this.depth - 1] is { Kind: ContainerKind.Map, Remaining: var remaining } && (remaining & 1) == 0)
			{
				if (type != TokenType.String)
				{
					throw new DecoderException("ShapeShift map keys must be strings.");
				}

				return TokenType.PropertyName;
			}

			return type;
		}
	}

	/// <inheritdoc/>
	public readonly bool TryReadNull() => this.NextTokenType == TokenType.Null;

	/// <inheritdoc/>
	public int? ReadStartMap()
	{
		int count = this.ReadContainerHeader(map: true);
		this.Push(ContainerKind.Map, count * 2);
		return count;
	}

	/// <inheritdoc/>
	public void ReadEndMap() => this.Pop(ContainerKind.Map);

	/// <inheritdoc/>
	public int? ReadStartVector()
	{
		int count = this.ReadContainerHeader(map: false);
		this.Push(ContainerKind.Vector, count);
		return count;
	}

	/// <inheritdoc/>
	public void ReadEndVector() => this.Pop(ContainerKind.Vector);

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadPropertyName() => this.ReadStringCore(expectPropertyName: true);

	/// <inheritdoc/>
	public void Skip()
	{
		switch (this.NextTokenType)
		{
			case TokenType.StartMap:
				this.ReadStartMap();
				while (this.NextTokenType != TokenType.EndMap)
				{
					_ = this.ReadPropertyName();
					this.Skip();
				}

				this.ReadEndMap();
				break;
			case TokenType.StartVector:
				this.ReadStartVector();
				while (this.NextTokenType != TokenType.EndVector)
				{
					this.Skip();
				}

				this.ReadEndVector();
				break;
			case TokenType.EndMap:
			case TokenType.EndVector:
			case TokenType.EndDocument:
				throw new DecoderException($"Cannot skip token {this.NextTokenType}.");
			default:
				this.SkipScalar();
				break;
		}
	}

	/// <inheritdoc/>
	public void ReadNull()
	{
		this.ExpectCode(0xc0, "null");
		this.Advance(1);
		this.CompleteValue();
	}

	/// <inheritdoc/>
	public bool ReadBoolean()
	{
		byte code = this.PeekByte();
		bool value = code switch
		{
			0xc2 => false,
			0xc3 => true,
			_ => throw this.UnexpectedCode(code, "a Boolean"),
		};
		this.Advance(1);
		this.CompleteValue();
		return value;
	}

	/// <inheritdoc/>
	public long ReadInt64()
	{
		(long signed, ulong unsigned, bool isUnsigned) = this.ReadInteger();
		return isUnsigned ? checked((long)unsigned) : signed;
	}

	/// <inheritdoc/>
	public ulong ReadUInt64()
	{
		(long signed, ulong unsigned, bool isUnsigned) = this.ReadInteger();
		return isUnsigned ? unsigned : checked((ulong)signed);
	}

	/// <inheritdoc/>
	public Int128 ReadInt128()
	{
		if (this.IsExtension(MsgPackExtensionCodes.Int128))
		{
			Span<byte> payload = stackalloc byte[16];
			this.ReadExtensionExactly(MsgPackExtensionCodes.Int128, payload);
			return BinaryPrimitives.ReadInt128BigEndian(payload);
		}

		return this.ReadInt64();
	}

	/// <inheritdoc/>
	public UInt128 ReadUInt128()
	{
		if (this.IsExtension(MsgPackExtensionCodes.UInt128))
		{
			Span<byte> payload = stackalloc byte[16];
			this.ReadExtensionExactly(MsgPackExtensionCodes.UInt128, payload);
			return BinaryPrimitives.ReadUInt128BigEndian(payload);
		}

		return this.ReadUInt64();
	}

	/// <inheritdoc/>
	public Half ReadHalf() => (Half)this.ReadDouble();

	/// <inheritdoc/>
	public float ReadSingle() => checked((float)this.ReadDouble());

	/// <inheritdoc/>
	public double ReadDouble()
	{
		byte code = this.PeekByte();
		if (code == 0xca)
		{
			Span<byte> scratch = stackalloc byte[4];
			float value = BinaryPrimitives.ReadSingleBigEndian(this.Peek(1, 4, scratch));
			this.Advance(5);
			this.CompleteValue();
			return value;
		}

		if (code == 0xcb)
		{
			Span<byte> scratch = stackalloc byte[8];
			double value = BinaryPrimitives.ReadDoubleBigEndian(this.Peek(1, 8, scratch));
			this.Advance(9);
			this.CompleteValue();
			return value;
		}

		(long signed, ulong unsigned, bool isUnsigned) = this.ReadInteger();
		return isUnsigned ? unsigned : signed;
	}

	/// <inheritdoc/>
	public decimal ReadDecimal()
	{
		if (this.IsExtension(MsgPackExtensionCodes.Decimal))
		{
			Span<byte> payload = stackalloc byte[16];
			this.ReadExtensionExactly(MsgPackExtensionCodes.Decimal, payload);
			Span<int> bits = stackalloc int[4];
			for (int i = 0; i < bits.Length; i++)
			{
				bits[i] = BinaryPrimitives.ReadInt32BigEndian(payload[(i * 4)..]);
			}

			try
			{
				return new decimal(bits);
			}
			catch (ArgumentException ex)
			{
				throw new DecoderException("The MessagePack decimal extension payload is not a valid decimal.", ex);
			}
		}

		return checked((decimal)this.ReadDouble());
	}

	/// <inheritdoc/>
	public DateTime ReadDateTime()
	{
		Span<byte> scratch = stackalloc byte[12];
		int payloadLength = this.ReadExtension(MsgPackExtensionCodes.Timestamp, scratch);
		ReadOnlySpan<byte> payload = scratch[..payloadLength];
		uint nanoseconds;
		long seconds;
		switch (payload.Length)
		{
			case 4:
				nanoseconds = 0;
				seconds = BinaryPrimitives.ReadUInt32BigEndian(payload);
				break;
			case 8:
				ulong packed = BinaryPrimitives.ReadUInt64BigEndian(payload);
				nanoseconds = (uint)(packed >> 34);
				seconds = (long)(packed & 0x3ffffffff);
				break;
			case 12:
				nanoseconds = BinaryPrimitives.ReadUInt32BigEndian(payload);
				seconds = BinaryPrimitives.ReadInt64BigEndian(payload[4..]);
				break;
			default:
				throw new DecoderException("Invalid MessagePack timestamp payload length.");
		}

		try
		{
			return DateTime.UnixEpoch.AddSeconds(seconds).AddTicks(nanoseconds / 100);
		}
		catch (ArgumentOutOfRangeException)
		{
			throw new DecoderException("MessagePack timestamp is outside the DateTime range.");
		}
	}

	/// <inheritdoc/>
	public TimeSpan ReadTimeSpan()
	{
		Span<byte> payload = stackalloc byte[8];
		this.ReadExtensionExactly(MsgPackExtensionCodes.TimeSpan, payload);
		return TimeSpan.FromTicks(BinaryPrimitives.ReadInt64BigEndian(payload));
	}

	/// <inheritdoc/>
	public BigInteger ReadBigInteger()
	{
		if (this.IsExtension(MsgPackExtensionCodes.BigInteger))
		{
			return new BigInteger(this.ReadExtension(MsgPackExtensionCodes.BigInteger), isUnsigned: false, isBigEndian: true);
		}

		(long signed, ulong unsigned, bool isUnsigned) = this.ReadInteger();
		return isUnsigned ? new BigInteger(unsigned) : new BigInteger(signed);
	}

	/// <inheritdoc/>
	public string ReadString() => this.ReadStringCore(expectPropertyName: false).ToString();

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadCharSpan() => this.ReadStringCore(expectPropertyName: false);

	/// <inheritdoc/>
	public byte[] ReadByteArray()
	{
		int length = this.ReadBinaryHeader();
		byte[] value = new byte[length];
		this.CopyTo(length, value);
		this.Advance(length);
		this.CompleteValue();
		return value;
	}

	/// <inheritdoc/>
	public ShapeShiftNumber ReadDynamicNumber()
	{
		if (this.IsExtension(MsgPackExtensionCodes.Decimal))
		{
			return new ShapeShiftDecimal(this.ReadDecimal());
		}

		if (this.IsExtension(MsgPackExtensionCodes.Int128))
		{
			Int128 value = this.ReadInt128();
			return value >= long.MinValue && value <= long.MaxValue
				? new ShapeShiftInteger((long)value)
				: new ShapeShiftBigInteger(BigInteger.Parse(value.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture));
		}

		if (this.IsExtension(MsgPackExtensionCodes.UInt128))
		{
			UInt128 value = this.ReadUInt128();
			return value <= ulong.MaxValue
				? new ShapeShiftUnsignedInteger((ulong)value)
				: new ShapeShiftBigInteger(BigInteger.Parse(value.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture));
		}

		if (this.IsExtension(MsgPackExtensionCodes.BigInteger))
		{
			return new ShapeShiftBigInteger(this.ReadBigInteger());
		}

		byte code = this.PeekByte();
		if (code is 0xca or 0xcb)
		{
			return new ShapeShiftFloat(this.ReadDouble());
		}

		(long signed, ulong unsigned, bool isUnsigned) = this.ReadInteger();
		return isUnsigned ? new ShapeShiftUnsignedInteger(unsigned) : new ShapeShiftInteger(signed);
	}

	/// <summary>
	/// Describes the extension value the decoder is positioned at, if any, without consuming it.
	/// </summary>
	/// <param name="header">Receives the extension's type code and payload length.</param>
	/// <returns><see langword="true" /> if the next value is an extension; <see langword="false" /> otherwise.</returns>
	/// <exception cref="DecoderException">Thrown when the input ends in the middle of the extension's header.</exception>
	/// <remarks>
	/// This is a low-level building block for custom converters that define their own extension encodings.
	/// See <see cref="MsgPackExtensionCodes"/> for the codes ShapeShift itself reserves.
	/// </remarks>
	public readonly bool TryPeekExtensionHeader(out MsgPackExtensionHeader header)
	{
		if (this.UnreadLength == 0 || !IsExtensionCode(this.PeekByte()))
		{
			header = default;
			return false;
		}

		byte code = this.PeekByte();
		(int headerLength, int payloadLength) = this.GetExtensionHeader(code);
		header = new MsgPackExtensionHeader(unchecked((sbyte)this.PeekByteAt(headerLength - 1)), payloadLength);
		return true;
	}

	/// <summary>
	/// Reads an extension value of an expected type code into a caller-supplied buffer.
	/// </summary>
	/// <param name="expectedTypeCode">The extension type code the caller requires.</param>
	/// <param name="destination">A buffer that receives the payload. It must be at least as long as the payload.</param>
	/// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
	/// <exception cref="DecoderException">
	/// Thrown when the next value is not an extension, carries a different type code, ends prematurely,
	/// or has a payload longer than <paramref name="destination"/>.
	/// </exception>
	public int ReadExtension(sbyte expectedTypeCode, scoped Span<byte> destination)
	{
		(int headerLength, int payloadLength, sbyte typeCode) = this.PeekExtensionHeaderCore(expectedTypeCode);
		if (payloadLength > destination.Length)
		{
			throw new DecoderException($"The MessagePack extension {typeCode} carries {payloadLength} bytes, which exceeds the {destination.Length} byte buffer provided to read it.");
		}

		this.Advance(headerLength);
		this.CopyTo(payloadLength, destination);
		this.Advance(payloadLength);
		this.CompleteValue();
		return payloadLength;
	}

	/// <summary>
	/// Reads an extension value of an expected type code.
	/// </summary>
	/// <param name="expectedTypeCode">The extension type code the caller requires.</param>
	/// <returns>The extension's payload.</returns>
	/// <exception cref="DecoderException">
	/// Thrown when the next value is not an extension, carries a different type code, or ends prematurely.
	/// </exception>
	public byte[] ReadExtension(sbyte expectedTypeCode)
	{
		(int headerLength, int payloadLength, _) = this.PeekExtensionHeaderCore(expectedTypeCode);
		this.Advance(headerLength);
		byte[] payload = new byte[payloadLength];
		this.CopyTo(payloadLength, payload);
		this.Advance(payloadLength);
		this.CompleteValue();
		return payload;
	}

	/// <summary>
	/// Verifies that one complete MessagePack value consumed the input.
	/// </summary>
	public readonly void EnsureEndOfDocument()
	{
		if (this.depth != 0 || this.UnreadLength != 0)
		{
			throw new DecoderException("Additional or incomplete MessagePack content remains.");
		}
	}

	private static TokenType Classify(byte code, sbyte? extensionType)
	{
		if (code <= 0x7f || code >= 0xe0 || code is >= 0xca and <= 0xcf || code is >= 0xd0 and <= 0xd3)
		{
			return TokenType.Number;
		}

		if (code is >= 0x80 and <= 0x8f || code is 0xde or 0xdf)
		{
			return TokenType.StartMap;
		}

		if (code is >= 0x90 and <= 0x9f || code is 0xdc or 0xdd)
		{
			return TokenType.StartVector;
		}

		if (code is >= 0xa0 and <= 0xbf || code is 0xd9 or 0xda or 0xdb)
		{
			return TokenType.String;
		}

		if (code is 0xc4 or 0xc5 or 0xc6)
		{
			return TokenType.Binary;
		}

		if (code == 0xc0)
		{
			return TokenType.Null;
		}

		if (code is 0xc2 or 0xc3)
		{
			return TokenType.Boolean;
		}

		if (IsExtensionCode(code))
		{
			// Extensions that carry a number are surfaced as numbers so that dynamic (untyped) reads preserve them.
			// Every other extension, reserved or not, is surfaced as opaque binary so that unknown payloads survive
			// a round trip. A reserved extension that is not valid where it appears is rejected by whichever typed
			// read is actually attempted, with a message that names the feature that produced it (see UnexpectedCode).
			return extensionType is MsgPackExtensionCodes.Decimal or MsgPackExtensionCodes.Int128 or MsgPackExtensionCodes.UInt128 or MsgPackExtensionCodes.BigInteger
				? TokenType.Number
				: TokenType.Binary;
		}

		throw new DecoderException($"Unsupported or reserved MessagePack code 0x{code:x2}.");
	}

	private static bool IsExtensionCode(byte code) => code is 0xc7 or 0xc8 or 0xc9 or >= 0xd4 and <= 0xd8;

	/// <summary>
	/// Gets the byte at the decoder's current position.
	/// </summary>
	/// <exception cref="DecoderException">Thrown when no bytes remain.</exception>
	private readonly byte PeekByte()
	{
		if (this.segmented)
		{
			if (this.reader.TryPeek(out byte value))
			{
				return value;
			}
		}
		else if (this.offset < this.span.Length)
		{
			return this.span[this.offset];
		}

		throw new DecoderException("Unexpected end of MessagePack input.");
	}

	/// <summary>
	/// Gets the byte at the decoder's current position, or <c>0xc1</c> (the one code MessagePack never assigns a
	/// meaning to) when the input is exhausted.
	/// </summary>
	private readonly byte PeekByteOrDefault() => this.UnreadLength == 0 ? (byte)0xc1 : this.PeekByte();

	private readonly byte PeekByteAt(int distance)
	{
		if (distance == 0)
		{
			return this.PeekByte();
		}

		Span<byte> scratch = stackalloc byte[1];
		return this.Peek(distance, 1, scratch)[0];
	}

	/// <summary>
	/// Gets a span over exactly <paramref name="length"/> bytes that begin <paramref name="distance"/> bytes past
	/// the decoder's current position, without advancing it.
	/// </summary>
	/// <param name="distance">The number of bytes to skip past the current position.</param>
	/// <param name="length">The number of bytes required.</param>
	/// <param name="scratch">
	/// A caller-owned buffer, at least <paramref name="length"/> bytes long, used only when the requested bytes
	/// straddle a segment boundary. The returned span may alias it, so it must outlive the returned span.
	/// </param>
	/// <returns>The requested bytes.</returns>
	/// <exception cref="DecoderException">Thrown when fewer than <c><paramref name="distance"/> + <paramref name="length"/></c> bytes remain.</exception>
	private readonly ReadOnlySpan<byte> Peek(int distance, int length, Span<byte> scratch)
	{
		if (this.UnreadLength < (long)distance + length)
		{
			throw new DecoderException("Unexpected end of MessagePack input.");
		}

		if (!this.segmented)
		{
			return this.span.Slice(this.offset + distance, length);
		}

		ReadOnlySpan<byte> unread = this.reader.UnreadSpan;
		if (unread.Length >= distance + length)
		{
			return unread.Slice(distance, length);
		}

		Debug.Assert(length <= scratch.Length, "The scratch buffer must be able to hold the requested bytes.");
		this.reader.Sequence.Slice(this.reader.Sequence.GetPosition(distance, this.reader.Position), length).CopyTo(scratch);
		return scratch[..length];
	}

	/// <summary>
	/// Copies <paramref name="length"/> bytes from the decoder's current position without advancing it.
	/// </summary>
	private readonly void CopyTo(int length, scoped Span<byte> destination)
	{
		if (this.UnreadLength < length)
		{
			throw new DecoderException("Unexpected end of MessagePack input.");
		}

		ReadOnlySpan<byte> contiguous = this.UnreadSpan;
		if (contiguous.Length >= length)
		{
			contiguous[..length].CopyTo(destination);
		}
		else
		{
			this.reader.Sequence.Slice(this.reader.Position, length).CopyTo(destination);
		}
	}

	private void Advance(long count)
	{
		if (this.UnreadLength < count)
		{
			throw new DecoderException("Unexpected end of MessagePack input.");
		}

		if (this.segmented)
		{
			this.reader.Advance(count);
		}
		else
		{
			this.offset += (int)count;
		}
	}

	private readonly DecoderException UnexpectedCode(byte code, string expectation)
	{
		if (IsExtensionCode(code) && this.PeekExtensionType(code) is sbyte typeCode && MsgPackExtensionCodes.Describe(typeCode) is string description)
		{
			return new DecoderException(
				$"Expected MessagePack {expectation} but found the ShapeShift extension {typeCode} ({description}). " +
				"This payload was produced by a feature or contract that does not match how it is being read.");
		}

		return new DecoderException($"Expected MessagePack {expectation} but found code 0x{code:x2}.");
	}

	private readonly void ExpectCode(byte expected, string description)
	{
		byte actual = this.PeekByte();
		if (actual != expected)
		{
			throw this.UnexpectedCode(actual, description);
		}
	}

	private int ReadContainerHeader(bool map)
	{
		byte code = this.PeekByte();
		long count;
		int headerLength;
		if (map ? code is >= 0x80 and <= 0x8f : code is >= 0x90 and <= 0x9f)
		{
			count = code & 0x0f;
			headerLength = 1;
		}
		else if (code == (map ? 0xde : 0xdc))
		{
			Span<byte> scratch = stackalloc byte[2];
			count = BinaryPrimitives.ReadUInt16BigEndian(this.Peek(1, 2, scratch));
			headerLength = 3;
		}
		else if (code == (map ? 0xdf : 0xdd))
		{
			Span<byte> scratch = stackalloc byte[4];
			count = BinaryPrimitives.ReadUInt32BigEndian(this.Peek(1, 4, scratch));
			headerLength = 5;
		}
		else
		{
			throw this.UnexpectedCode(code, map ? "a map header" : "an array header");
		}

		this.Advance(headerLength);

		// Every element occupies at least one byte (two for a map entry), so a declared element count that the
		// remaining input could not possibly satisfy is malformed. Rejecting it here keeps a hostile or corrupt
		// length header from being multiplied into an overflowing (or merely enormous) frame counter below.
		long minimumBytes = map ? count * 2 : count;
		if (minimumBytes > this.UnreadLength)
		{
			throw new DecoderException($"A MessagePack {(map ? "map" : "array")} header declares {count} {(map ? "entries" : "elements")}, which exceeds the {this.UnreadLength} bytes that remain.");
		}

		this.CompleteValue();
		return (int)count;
	}

	private (long Signed, ulong Unsigned, bool IsUnsigned) ReadInteger()
	{
		byte code = this.PeekByte();
		long signed = 0;
		ulong unsigned = 0;
		bool isUnsigned;
		int length;
		Span<byte> scratch = stackalloc byte[8];
		if (code <= 0x7f)
		{
			unsigned = code;
			isUnsigned = true;
			length = 1;
		}
		else if (code >= 0xe0)
		{
			signed = unchecked((sbyte)code);
			isUnsigned = false;
			length = 1;
		}
		else
		{
			switch (code)
			{
				case 0xcc:
					unsigned = this.PeekByteAt(1);
					isUnsigned = true;
					length = 2;
					break;
				case 0xcd:
					unsigned = BinaryPrimitives.ReadUInt16BigEndian(this.Peek(1, 2, scratch));
					isUnsigned = true;
					length = 3;
					break;
				case 0xce:
					unsigned = BinaryPrimitives.ReadUInt32BigEndian(this.Peek(1, 4, scratch));
					isUnsigned = true;
					length = 5;
					break;
				case 0xcf:
					unsigned = BinaryPrimitives.ReadUInt64BigEndian(this.Peek(1, 8, scratch));
					isUnsigned = true;
					length = 9;
					break;
				case 0xd0:
					signed = unchecked((sbyte)this.PeekByteAt(1));
					isUnsigned = false;
					length = 2;
					break;
				case 0xd1:
					signed = BinaryPrimitives.ReadInt16BigEndian(this.Peek(1, 2, scratch));
					isUnsigned = false;
					length = 3;
					break;
				case 0xd2:
					signed = BinaryPrimitives.ReadInt32BigEndian(this.Peek(1, 4, scratch));
					isUnsigned = false;
					length = 5;
					break;
				case 0xd3:
					signed = BinaryPrimitives.ReadInt64BigEndian(this.Peek(1, 8, scratch));
					isUnsigned = false;
					length = 9;
					break;
				default:
					throw this.UnexpectedCode(code, "an integer");
			}
		}

		this.Advance(length);
		this.CompleteValue();
		return (signed, unsigned, isUnsigned);
	}

	private ReadOnlySpan<char> ReadStringCore(bool expectPropertyName)
	{
		if (expectPropertyName != (this.NextTokenType == TokenType.PropertyName))
		{
			throw this.UnexpectedCode(this.PeekByteOrDefault(), expectPropertyName ? "a map property name" : "a string");
		}

		int length = this.ReadStringHeader();
		this.stringBuffer = this.ReadUtf8(length);
		this.CompleteValue();
		return this.stringBuffer;
	}

	/// <summary>
	/// Decodes and consumes <paramref name="length"/> UTF-8 bytes at the decoder's current position.
	/// </summary>
	/// <remarks>
	/// The bytes are copied only when the string straddles a segment boundary; a string wholly contained in one
	/// segment is decoded directly out of that segment.
	/// </remarks>
	private string ReadUtf8(int length)
	{
		if (this.UnreadLength < length)
		{
			throw new DecoderException("Unexpected end of MessagePack input.");
		}

		ReadOnlySpan<byte> contiguous = this.UnreadSpan;
		string value;
		if (contiguous.Length >= length)
		{
			value = Encoding.UTF8.GetString(contiguous[..length]);
		}
		else
		{
			byte[] rented = ArrayPool<byte>.Shared.Rent(length);
			try
			{
				this.CopyTo(length, rented);
				value = Encoding.UTF8.GetString(rented, 0, length);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(rented);
			}
		}

		this.Advance(length);
		return value;
	}

	private int ReadStringHeader()
	{
		byte code = this.PeekByte();
		if (code is >= 0xa0 and <= 0xbf)
		{
			this.Advance(1);
			return code & 0x1f;
		}

		long length;
		int headerLength;
		Span<byte> scratch = stackalloc byte[4];
		switch (code)
		{
			case 0xd9:
				length = this.PeekByteAt(1);
				headerLength = 2;
				break;
			case 0xda:
				length = BinaryPrimitives.ReadUInt16BigEndian(this.Peek(1, 2, scratch));
				headerLength = 3;
				break;
			case 0xdb:
				length = BinaryPrimitives.ReadUInt32BigEndian(this.Peek(1, 4, scratch));
				headerLength = 5;
				break;
			default:
				throw this.UnexpectedCode(code, "a string");
		}

		this.Advance(headerLength);
		return this.RequireAvailable(length, "string");
	}

	private int ReadBinaryHeader()
	{
		byte code = this.PeekByte();
		long length;
		int headerLength;
		Span<byte> scratch = stackalloc byte[4];
		switch (code)
		{
			case 0xc4:
				length = this.PeekByteAt(1);
				headerLength = 2;
				break;
			case 0xc5:
				length = BinaryPrimitives.ReadUInt16BigEndian(this.Peek(1, 2, scratch));
				headerLength = 3;
				break;
			case 0xc6:
				length = BinaryPrimitives.ReadUInt32BigEndian(this.Peek(1, 4, scratch));
				headerLength = 5;
				break;
			default:
				throw this.UnexpectedCode(code, "binary data");
		}

		this.Advance(headerLength);
		return this.RequireAvailable(length, "binary value");
	}

	/// <summary>
	/// Verifies that a declared payload length is actually present, so that a hostile or corrupt length header
	/// cannot cause an allocation far larger than the input that justified it.
	/// </summary>
	private readonly int RequireAvailable(long length, string description)
	{
		if (length > this.UnreadLength)
		{
			throw new DecoderException($"A MessagePack {description} declares {length} bytes, which exceeds the {this.UnreadLength} bytes that remain.");
		}

		return (int)length;
	}

	private readonly bool IsExtension(sbyte type)
	{
		byte code = this.PeekByte();
		return IsExtensionCode(code) && this.PeekExtensionType(code) == type;
	}

	private readonly sbyte? PeekExtensionType(byte code)
	{
		if (!IsExtensionCode(code))
		{
			return null;
		}

		(int headerLength, _) = this.GetExtensionHeader(code);
		return unchecked((sbyte)this.PeekByteAt(headerLength - 1));
	}

	/// <summary>
	/// Validates the extension the decoder is positioned at against an expected type code, without consuming anything.
	/// </summary>
	private readonly (int HeaderLength, int PayloadLength, sbyte TypeCode) PeekExtensionHeaderCore(sbyte expectedTypeCode)
	{
		byte code = this.PeekByte();
		if (!IsExtensionCode(code))
		{
			throw this.UnexpectedCode(code, $"extension {expectedTypeCode}");
		}

		(int headerLength, int payloadLength) = this.GetExtensionHeader(code);
		sbyte typeCode = unchecked((sbyte)this.PeekByteAt(headerLength - 1));
		if (typeCode != expectedTypeCode)
		{
			string described = MsgPackExtensionCodes.Describe(typeCode) is string description ? $" ({description})" : string.Empty;
			throw new DecoderException($"Expected MessagePack extension type {expectedTypeCode} but found {typeCode}{described}.");
		}

		return (headerLength, payloadLength, typeCode);
	}

	/// <summary>
	/// Reads an extension whose payload length must match <paramref name="destination"/> exactly.
	/// </summary>
	private void ReadExtensionExactly(sbyte expectedTypeCode, scoped Span<byte> destination)
	{
		int length = this.ReadExtension(expectedTypeCode, destination);
		if (length != destination.Length)
		{
			throw new DecoderException($"Expected an extension payload of {destination.Length} bytes but found {length}.");
		}
	}

	private readonly (int HeaderLength, int PayloadLength) GetExtensionHeader(byte code)
	{
		Span<byte> scratch = stackalloc byte[4];
		return code switch
		{
			0xd4 => (2, 1),
			0xd5 => (2, 2),
			0xd6 => (2, 4),
			0xd7 => (2, 8),
			0xd8 => (2, 16),
			0xc7 => (3, this.PeekByteAt(1)),
			0xc8 => (4, BinaryPrimitives.ReadUInt16BigEndian(this.Peek(1, 2, scratch))),
			0xc9 => (6, this.RequireAvailable(BinaryPrimitives.ReadUInt32BigEndian(this.Peek(1, 4, scratch)), "extension")),
			_ => throw this.UnexpectedCode(code, "an extension"),
		};
	}

	private void SkipScalar()
	{
		byte code = this.PeekByte();
		int length;
		if (code is >= 0xa0 and <= 0xbf || code is 0xd9 or 0xda or 0xdb)
		{
			length = this.ReadStringHeader();
			this.Advance(length);
		}
		else if (code is 0xc4 or 0xc5 or 0xc6)
		{
			length = this.ReadBinaryHeader();
			this.Advance(length);
		}
		else if (IsExtensionCode(code))
		{
			(int headerLength, int payloadLength) = this.GetExtensionHeader(code);
			this.Advance(headerLength);
			this.Advance(payloadLength);
		}
		else
		{
			length = code switch
			{
				<= 0x7f or >= 0xe0 or 0xc0 or 0xc2 or 0xc3 => 1,
				0xcc or 0xd0 => 2,
				0xcd or 0xd1 => 3,
				0xca or 0xce or 0xd2 => 5,
				0xcb or 0xcf or 0xd3 => 9,
				_ => throw new DecoderException($"Unsupported MessagePack scalar code 0x{code:x2}."),
			};
			this.Advance(length);
		}

		this.CompleteValue();
	}

	private void Push(ContainerKind kind, int remaining)
	{
		if (this.depth == this.frames.Length)
		{
			Array.Resize(ref this.frames, this.frames.Length * 2);
		}

		this.frames[this.depth++] = new(kind, remaining);
	}

	private void Pop(ContainerKind expected)
	{
		if (this.depth == 0 || this.frames[this.depth - 1] is not { Kind: var kind, Remaining: 0 } || kind != expected)
		{
			throw new DecoderException($"Expected the end of a MessagePack {expected.ToString().ToLowerInvariant()}.");
		}

		this.depth--;
	}

	private void CompleteValue()
	{
		if (this.depth == 0)
		{
			return;
		}

		ref Frame frame = ref this.frames[this.depth - 1];
		if (--frame.Remaining < 0)
		{
			throw new DecoderException("MessagePack container contains more values than its header declares.");
		}
	}
}
