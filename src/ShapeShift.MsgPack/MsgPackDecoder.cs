// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.MsgPack;

#pragma warning disable SA1201 // Nested implementation types are kept near state fields.

/// <summary>
/// Reads MessagePack tokens from contiguous memory.
/// </summary>
public ref struct MsgPackDecoder : IDecoder
{
	private readonly ReadOnlySpan<byte> messagePack;
	private readonly byte[]? ownedBuffer;
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
		this.messagePack = messagePack;
		this.frames = new Frame[8];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MsgPackDecoder"/> struct.
	/// </summary>
	/// <param name="messagePack">The encoded MessagePack value.</param>
	/// <remarks>Multi-segment input is consolidated into one buffer.</remarks>
	public MsgPackDecoder(in ReadOnlySequence<byte> messagePack)
	{
		this.frames = new Frame[8];
		if (messagePack.IsSingleSegment)
		{
			this.ownedBuffer = null;
			this.messagePack = messagePack.FirstSpan;
		}
		else
		{
			this.ownedBuffer = messagePack.ToArray();
			this.messagePack = this.ownedBuffer;
		}
	}

	/// <summary>
	/// Gets the unread encoded bytes.
	/// </summary>
	public readonly ReadOnlySpan<byte> Remaining => this.messagePack[this.offset..];

	/// <inheritdoc/>
	public readonly TokenType NextTokenType
	{
		get
		{
			if (this.depth > 0 && this.frames[this.depth - 1].Remaining == 0)
			{
				return this.frames[this.depth - 1].Kind == ContainerKind.Map ? TokenType.EndMap : TokenType.EndVector;
			}

			if (this.offset >= this.messagePack.Length)
			{
				return TokenType.EndDocument;
			}

			byte code = this.messagePack[this.offset];
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
		this.Push(ContainerKind.Map, checked(count * 2));
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
		this.offset++;
		this.CompleteValue();
	}

	/// <inheritdoc/>
	public bool ReadBoolean()
	{
		bool value = this.PeekByte() switch
		{
			0xc2 => false,
			0xc3 => true,
			_ => throw new DecoderException("Expected a MessagePack Boolean."),
		};
		this.offset++;
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
		if (this.IsExtension(MsgPackEncoder.Int128Extension))
		{
			ReadOnlySpan<byte> payload = this.ReadExtension(MsgPackEncoder.Int128Extension);
			RequireLength(payload, 16);
			return BinaryPrimitives.ReadInt128BigEndian(payload);
		}

		return this.ReadInt64();
	}

	/// <inheritdoc/>
	public UInt128 ReadUInt128()
	{
		if (this.IsExtension(MsgPackEncoder.UInt128Extension))
		{
			ReadOnlySpan<byte> payload = this.ReadExtension(MsgPackEncoder.UInt128Extension);
			RequireLength(payload, 16);
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
		double value;
		if (code == 0xca)
		{
			value = BinaryPrimitives.ReadSingleBigEndian(this.Slice(this.offset + 1, 4));
			this.offset += 5;
			this.CompleteValue();
			return value;
		}

		if (code == 0xcb)
		{
			value = BinaryPrimitives.ReadDoubleBigEndian(this.Slice(this.offset + 1, 8));
			this.offset += 9;
			this.CompleteValue();
			return value;
		}

		(long signed, ulong unsigned, bool isUnsigned) = this.ReadInteger();
		return isUnsigned ? unsigned : signed;
	}

	/// <inheritdoc/>
	public decimal ReadDecimal()
	{
		if (this.IsExtension(MsgPackEncoder.DecimalExtension))
		{
			ReadOnlySpan<byte> payload = this.ReadExtension(MsgPackEncoder.DecimalExtension);
			RequireLength(payload, 16);
			int[] bits = new int[4];
			for (int i = 0; i < bits.Length; i++)
			{
				bits[i] = BinaryPrimitives.ReadInt32BigEndian(payload[(i * 4)..]);
			}

			return new decimal(bits);
		}

		return checked((decimal)this.ReadDouble());
	}

	/// <inheritdoc/>
	public DateTime ReadDateTime()
	{
		ReadOnlySpan<byte> payload = this.ReadExtension(-1);
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
		ReadOnlySpan<byte> payload = this.ReadExtension(MsgPackEncoder.TimeSpanExtension);
		RequireLength(payload, 8);
		return TimeSpan.FromTicks(BinaryPrimitives.ReadInt64BigEndian(payload));
	}

	/// <inheritdoc/>
	public BigInteger ReadBigInteger()
	{
		if (this.IsExtension(MsgPackEncoder.BigIntegerExtension))
		{
			return new BigInteger(this.ReadExtension(MsgPackEncoder.BigIntegerExtension), isUnsigned: false, isBigEndian: true);
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
		byte[] value = this.Slice(this.offset, length).ToArray();
		this.offset += length;
		this.CompleteValue();
		return value;
	}

	/// <inheritdoc/>
	public ShapeShiftNumber ReadDynamicNumber()
	{
		if (this.IsExtension(MsgPackEncoder.DecimalExtension))
		{
			return new ShapeShiftDecimal(this.ReadDecimal());
		}

		if (this.IsExtension(MsgPackEncoder.Int128Extension))
		{
			Int128 value = this.ReadInt128();
			return value >= long.MinValue && value <= long.MaxValue
				? new ShapeShiftInteger((long)value)
				: new ShapeShiftBigInteger(BigInteger.Parse(value.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture));
		}

		if (this.IsExtension(MsgPackEncoder.UInt128Extension))
		{
			UInt128 value = this.ReadUInt128();
			return value <= ulong.MaxValue
				? new ShapeShiftUnsignedInteger((ulong)value)
				: new ShapeShiftBigInteger(BigInteger.Parse(value.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture));
		}

		if (this.IsExtension(MsgPackEncoder.BigIntegerExtension))
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
	/// Verifies that one complete MessagePack value consumed the input.
	/// </summary>
	public readonly void EnsureEndOfDocument()
	{
		if (this.depth != 0 || this.offset != this.messagePack.Length)
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
			return extensionType is MsgPackEncoder.DecimalExtension or MsgPackEncoder.Int128Extension or MsgPackEncoder.UInt128Extension or MsgPackEncoder.BigIntegerExtension
				? TokenType.Number
				: TokenType.Binary;
		}

		throw new DecoderException($"Unsupported or reserved MessagePack code 0x{code:x2}.");
	}

	private static bool IsExtensionCode(byte code) => code is 0xc7 or 0xc8 or 0xc9 or >= 0xd4 and <= 0xd8;

	private static void RequireLength(ReadOnlySpan<byte> payload, int expected)
	{
		if (payload.Length != expected)
		{
			throw new DecoderException($"Expected an extension payload of {expected} bytes but found {payload.Length}.");
		}
	}

	private byte PeekByte()
	{
		if (this.offset >= this.messagePack.Length)
		{
			throw new DecoderException("Unexpected end of MessagePack input.");
		}

		return this.messagePack[this.offset];
	}

	private readonly ReadOnlySpan<byte> Slice(int start, int length)
	{
		if ((uint)start > (uint)this.messagePack.Length || length < 0 || length > this.messagePack.Length - start)
		{
			throw new DecoderException("Unexpected end of MessagePack input.");
		}

		return this.messagePack.Slice(start, length);
	}

	private void ExpectCode(byte expected, string description)
	{
		byte actual = this.PeekByte();
		if (actual != expected)
		{
			throw new DecoderException($"Expected MessagePack {description} but found code 0x{actual:x2}.");
		}
	}

	private int ReadContainerHeader(bool map)
	{
		byte code = this.PeekByte();
		int count;
		int headerLength;
		if (map ? code is >= 0x80 and <= 0x8f : code is >= 0x90 and <= 0x9f)
		{
			count = code & 0x0f;
			headerLength = 1;
		}
		else if (code == (map ? 0xde : 0xdc))
		{
			count = BinaryPrimitives.ReadUInt16BigEndian(this.Slice(this.offset + 1, 2));
			headerLength = 3;
		}
		else if (code == (map ? 0xdf : 0xdd))
		{
			count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(this.Slice(this.offset + 1, 4)));
			headerLength = 5;
		}
		else
		{
			throw new DecoderException($"Expected a MessagePack {(map ? "map" : "array")} header.");
		}

		this.offset += headerLength;
		this.CompleteValue();
		return count;
	}

	private (long Signed, ulong Unsigned, bool IsUnsigned) ReadInteger()
	{
		byte code = this.PeekByte();
		long signed = 0;
		ulong unsigned = 0;
		bool isUnsigned;
		int length;
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
					unsigned = this.Slice(this.offset + 1, 1)[0];
					isUnsigned = true;
					length = 2;
					break;
				case 0xcd:
					unsigned = BinaryPrimitives.ReadUInt16BigEndian(this.Slice(this.offset + 1, 2));
					isUnsigned = true;
					length = 3;
					break;
				case 0xce:
					unsigned = BinaryPrimitives.ReadUInt32BigEndian(this.Slice(this.offset + 1, 4));
					isUnsigned = true;
					length = 5;
					break;
				case 0xcf:
					unsigned = BinaryPrimitives.ReadUInt64BigEndian(this.Slice(this.offset + 1, 8));
					isUnsigned = true;
					length = 9;
					break;
				case 0xd0:
					signed = unchecked((sbyte)this.Slice(this.offset + 1, 1)[0]);
					isUnsigned = false;
					length = 2;
					break;
				case 0xd1:
					signed = BinaryPrimitives.ReadInt16BigEndian(this.Slice(this.offset + 1, 2));
					isUnsigned = false;
					length = 3;
					break;
				case 0xd2:
					signed = BinaryPrimitives.ReadInt32BigEndian(this.Slice(this.offset + 1, 4));
					isUnsigned = false;
					length = 5;
					break;
				case 0xd3:
					signed = BinaryPrimitives.ReadInt64BigEndian(this.Slice(this.offset + 1, 8));
					isUnsigned = false;
					length = 9;
					break;
				default:
					throw new DecoderException("Expected a MessagePack integer.");
			}
		}

		this.offset += length;
		this.CompleteValue();
		return (signed, unsigned, isUnsigned);
	}

	private ReadOnlySpan<char> ReadStringCore(bool expectPropertyName)
	{
		if (expectPropertyName != (this.NextTokenType == TokenType.PropertyName))
		{
			throw new DecoderException(expectPropertyName ? "Expected a MessagePack map property name." : "Expected a MessagePack string.");
		}

		int length = this.ReadStringHeader();
		this.stringBuffer = Encoding.UTF8.GetString(this.Slice(this.offset, length));
		this.offset += length;
		this.CompleteValue();
		return this.stringBuffer;
	}

	private int ReadStringHeader()
	{
		byte code = this.PeekByte();
		if (code is >= 0xa0 and <= 0xbf)
		{
			this.offset++;
			return code & 0x1f;
		}

		int length;
		int headerLength;
		switch (code)
		{
			case 0xd9:
				length = this.Slice(this.offset + 1, 1)[0];
				headerLength = 2;
				break;
			case 0xda:
				length = BinaryPrimitives.ReadUInt16BigEndian(this.Slice(this.offset + 1, 2));
				headerLength = 3;
				break;
			case 0xdb:
				length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(this.Slice(this.offset + 1, 4)));
				headerLength = 5;
				break;
			default:
				throw new DecoderException("Expected a MessagePack string.");
		}

		this.offset += headerLength;
		return length;
	}

	private int ReadBinaryHeader()
	{
		byte code = this.PeekByte();
		int length;
		int headerLength;
		switch (code)
		{
			case 0xc4:
				length = this.Slice(this.offset + 1, 1)[0];
				headerLength = 2;
				break;
			case 0xc5:
				length = BinaryPrimitives.ReadUInt16BigEndian(this.Slice(this.offset + 1, 2));
				headerLength = 3;
				break;
			case 0xc6:
				length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(this.Slice(this.offset + 1, 4)));
				headerLength = 5;
				break;
			default:
				throw new DecoderException("Expected MessagePack binary data.");
		}

		this.offset += headerLength;
		return length;
	}

	private bool IsExtension(sbyte type)
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
		return unchecked((sbyte)this.Slice(this.offset + headerLength - 1, 1)[0]);
	}

	private ReadOnlySpan<byte> ReadExtension(sbyte expectedType)
	{
		byte code = this.PeekByte();
		if (!IsExtensionCode(code))
		{
			throw new DecoderException("Expected a MessagePack extension.");
		}

		(int headerLength, int payloadLength) = this.GetExtensionHeader(code);
		sbyte type = unchecked((sbyte)this.Slice(this.offset + headerLength - 1, 1)[0]);
		if (type != expectedType)
		{
			throw new DecoderException($"Expected MessagePack extension type {expectedType} but found {type}.");
		}

		ReadOnlySpan<byte> payload = this.Slice(this.offset + headerLength, payloadLength);
		this.offset += headerLength + payloadLength;
		this.CompleteValue();
		return payload;
	}

	private readonly (int HeaderLength, int PayloadLength) GetExtensionHeader(byte code)
	{
		return code switch
		{
			0xd4 => (2, 1),
			0xd5 => (2, 2),
			0xd6 => (2, 4),
			0xd7 => (2, 8),
			0xd8 => (2, 16),
			0xc7 => (3, this.Slice(this.offset + 1, 1)[0]),
			0xc8 => (4, BinaryPrimitives.ReadUInt16BigEndian(this.Slice(this.offset + 1, 2))),
			0xc9 => (6, checked((int)BinaryPrimitives.ReadUInt32BigEndian(this.Slice(this.offset + 1, 4)))),
			_ => throw new DecoderException("Expected a MessagePack extension."),
		};
	}

	private void SkipScalar()
	{
		byte code = this.PeekByte();
		int length;
		if (code is >= 0xa0 and <= 0xbf || code is 0xd9 or 0xda or 0xdb)
		{
			length = this.ReadStringHeader();
			this.offset += length;
		}
		else if (code is 0xc4 or 0xc5 or 0xc6)
		{
			length = this.ReadBinaryHeader();
			this.offset += length;
		}
		else if (IsExtensionCode(code))
		{
			(int headerLength, int payloadLength) = this.GetExtensionHeader(code);
			this.offset += headerLength + payloadLength;
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
			_ = this.Slice(this.offset, length);
			this.offset += length;
		}

		_ = this.Slice(0, this.offset);
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
