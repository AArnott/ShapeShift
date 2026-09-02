// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;
using System.Text;

namespace ShapeShift.Protobuf;

/// <summary>
/// Decodes the protobuf-style binary representation created by <see cref="ProtobufEncoder"/>.
/// </summary>
/// <param name="buffer">The payload to decode.</param>
public ref struct ProtobufDecoder(byte[] buffer) : IDecoder
{
	private readonly byte[] buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
	private int position;
	private ContainerKind[] containerKinds = new ContainerKind[8];
	private int depth;
	private string? currentString;
	private byte[]? currentBinary;

	private enum ContainerKind
	{
		Map,
		Vector,
	}

	public TokenType NextTokenType
	{
		get
		{
			if (this.position >= this.buffer.Length)
			{
				return TokenType.EndDocument;
			}

			return this.buffer[this.position] switch
			{
				0x50 => TokenType.Null,
				0x40 => TokenType.Boolean,
				0x20 => TokenType.Number,
				0x21 => TokenType.Number,
				0x22 => TokenType.Number,
				0x30 => TokenType.String,
				0x31 => TokenType.Binary,
				0x60 => TokenType.PropertyName,
				0x70 => TokenType.StartMap,
				0x71 => TokenType.EndMap,
				0x80 => TokenType.StartVector,
				0x81 => TokenType.EndVector,
				_ => throw new DecoderException($"Unrecognized Protobuf tag 0x{this.buffer[this.position]:X2} at offset {this.position}."),
			};
		}
	}

	public bool TryReadNull()
	{
		if (this.NextTokenType != TokenType.Null)
		{
			return false;
		}

		this.position++;
		return true;
	}

	public void ReadNull()
	{
		if (!this.TryReadNull())
		{
			throw new DecoderException($"Expected a null token but instead got {this.NextTokenType}.");
		}
	}

	public int? ReadStartMap()
	{
		if (this.NextTokenType != TokenType.StartMap)
		{
			throw new DecoderException($"Expected a map start token but found {this.NextTokenType}.");
		}

		this.position++;
		int count = this.ReadCount();
		this.Push(ContainerKind.Map);
		return count;
	}

	public void ReadEndMap()
	{
		if (this.NextTokenType != TokenType.EndMap)
		{
			throw new DecoderException($"Expected an end-of-map token but found {this.NextTokenType}.");
		}

		this.position++;
		this.Pop();
	}

	public int? ReadStartVector()
	{
		if (this.NextTokenType != TokenType.StartVector)
		{
			throw new DecoderException($"Expected a vector start token but found {this.NextTokenType}.");
		}

		this.position++;
		int count = this.ReadCount();
		this.Push(ContainerKind.Vector);
		return count;
	}

	public void ReadEndVector()
	{
		if (this.NextTokenType != TokenType.EndVector)
		{
			throw new DecoderException($"Expected an end-of-vector token but found {this.NextTokenType}.");
		}

		this.position++;
		this.Pop();
	}

	public ReadOnlySpan<char> ReadPropertyName()
	{
		if (this.NextTokenType != TokenType.PropertyName)
		{
			throw new DecoderException($"Expected a property name token but found {this.NextTokenType}.");
		}

		this.position++;
		this.currentString = this.ReadUtf8StringFromCurrentPayload();
		return this.currentString.AsSpan();
	}

	public void Skip()
	{
		switch (this.NextTokenType)
		{
			case TokenType.Null:
				this.ReadNull();
				return;
			case TokenType.Boolean:
				this.ReadBoolean();
				return;
			case TokenType.Number:
				this.ReadDecimal();
				return;
			case TokenType.String:
				this.ReadString();
				return;
			case TokenType.Binary:
				this.ReadByteArray();
				return;
			case TokenType.PropertyName:
				this.ReadPropertyName();
				this.Skip();
				return;
			case TokenType.StartMap:
			{
				int count = this.ReadStartMap() ?? 0;
				for (int i = 0; i < count; i++)
				{
					this.ReadPropertyName();
					this.Skip();
				}

				this.ReadEndMap();
				return;
			}

			case TokenType.StartVector:
			{
				int count = this.ReadStartVector() ?? 0;
				for (int i = 0; i < count; i++)
				{
					this.Skip();
				}

				this.ReadEndVector();
				return;
			}

			case TokenType.EndMap:
				this.ReadEndMap();
				return;
			case TokenType.EndVector:
				this.ReadEndVector();
				return;
			case TokenType.EndDocument:
				throw new DecoderException("Cannot skip a value because the document is already exhausted.");
			default:
				throw new DecoderException($"Unsupported token type {this.NextTokenType} while skipping.");
		}
	}

	public bool ReadBoolean()
	{
		if (this.NextTokenType != TokenType.Boolean)
		{
			throw new DecoderException($"Expected a Boolean token but found {this.NextTokenType}.");
		}

		this.position++;
		bool value = this.buffer[this.position] != 0;
		this.position++;
		return value;
	}

	public long ReadInt64()
		=> long.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);

	public ulong ReadUInt64()
		=> ulong.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);

	public Int128 ReadInt128()
		=> Int128.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);

	public UInt128 ReadUInt128()
		=> UInt128.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);

	public Half ReadHalf()
		=> Half.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);

	public float ReadSingle()
		=> float.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);

	public double ReadDouble()
		=> double.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);

	public decimal ReadDecimal()
		=> decimal.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);

	public DateTime ReadDateTime()
	{
		string text = this.ReadString();
		return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
	}

	public TimeSpan ReadTimeSpan()
	{
		string text = this.ReadString();
		return TimeSpan.Parse(text, CultureInfo.InvariantCulture);
	}

	public BigInteger ReadBigInteger()
		=> BigInteger.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);

	public string ReadString()
	{
		if (this.NextTokenType != TokenType.String)
		{
			throw new DecoderException($"Expected a string token but found {this.NextTokenType}.");
		}

		this.position++;
		this.currentString = this.ReadUtf8StringFromCurrentPayload();
		return this.currentString;
	}

	public ReadOnlySpan<char> ReadCharSpan()
	{
		if (this.NextTokenType != TokenType.String)
		{
			throw new DecoderException($"Expected a string token but found {this.NextTokenType}.");
		}

		this.position++;
		this.currentString = this.ReadUtf8StringFromCurrentPayload();
		return this.currentString.AsSpan();
	}

	public byte[] ReadByteArray()
	{
		if (this.NextTokenType != TokenType.Binary)
		{
			throw new DecoderException($"Expected a binary token but found {this.NextTokenType}.");
		}

		this.position++;
		int length = this.ReadLength();
		this.currentBinary = new byte[length];
		Buffer.BlockCopy(this.buffer, this.position, this.currentBinary, 0, length);
		this.position += length;
		return this.currentBinary;
	}

	public ShapeShiftNumber ReadDynamicNumber() => new ShapeShiftDecimal(this.ReadDecimal());

	private static void ThrowUnsupportedNumericFormat(byte tag)
		=> throw new DecoderException($"Unsupported numeric tag 0x{tag:X2}.");

	private void Push(ContainerKind kind)
	{
		if (this.depth == this.containerKinds.Length)
		{
			Array.Resize(ref this.containerKinds, this.containerKinds.Length * 2);
		}

		this.containerKinds[this.depth++] = kind;
	}

	private void Pop()
	{
		if (this.depth == 0)
		{
			throw new InvalidOperationException("Attempted to pop a container when none are open.");
		}

		this.depth--;
	}

	private string ReadNumericString()
	{
		byte tag = this.buffer[this.position];
		this.position++;
		int length = this.ReadLength();
		if (this.position + length > this.buffer.Length)
		{
			throw new DecoderException("Numeric payload is truncated.");
		}

		string text = Encoding.UTF8.GetString(this.buffer, this.position, length);
		this.position += length;
		return text;
	}

	private string ReadUtf8StringFromCurrentPayload()
	{
		int length = this.ReadLength();
		if (this.position + length > this.buffer.Length)
		{
			throw new DecoderException("String payload is truncated.");
		}

		string text = Encoding.UTF8.GetString(this.buffer, this.position, length);
		this.position += length;
		return text;
	}

	private int ReadCount()
	{
		return checked((int)this.ReadVarint());
	}

	private int ReadLength()
	{
		return checked((int)this.ReadVarint());
	}

	private uint ReadVarint()
	{
		ulong value = 0;
		int shift = 0;
		while (true)
		{
			if (this.position >= this.buffer.Length)
			{
				throw new DecoderException("Varint is truncated.");
			}

			byte next = this.buffer[this.position++];
			value |= (ulong)(next & 0x7F) << shift;
			if ((next & 0x80) == 0)
			{
				return checked((uint)value);
			}

			shift += 7;
			if (shift >= 35)
			{
				throw new DecoderException("Varint is too large to decode.");
			}
		}
	}
}
