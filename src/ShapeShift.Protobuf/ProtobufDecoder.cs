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

	/// <inheritdoc/>
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

	/// <inheritdoc/>
	public bool TryReadNull()
	{
		if (this.NextTokenType != TokenType.Null)
		{
			return false;
		}

		this.position++;
		return true;
	}

	/// <inheritdoc/>
	public void ReadNull()
	{
		if (!this.TryReadNull())
		{
			throw new DecoderException($"Expected a null token but instead got {this.NextTokenType}.");
		}
	}

	/// <inheritdoc/>
	public int? ReadStartMap()
	{
		if (this.NextTokenType != TokenType.StartMap)
		{
			throw new DecoderException($"Expected a map start token but found {this.NextTokenType}.");
		}

		this.position++;
		int? count = this.ReadCount();
		this.Push(ContainerKind.Map);
		return count;
	}

	/// <inheritdoc/>
	public void ReadEndMap()
	{
		if (this.NextTokenType != TokenType.EndMap)
		{
			throw new DecoderException($"Expected an end-of-map token but found {this.NextTokenType}.");
		}

		this.AssertCurrentContainer(ContainerKind.Map);
		this.position++;
		this.Pop();
	}

	/// <inheritdoc/>
	public int? ReadStartVector()
	{
		if (this.NextTokenType != TokenType.StartVector)
		{
			throw new DecoderException($"Expected a vector start token but found {this.NextTokenType}.");
		}

		this.position++;
		int? count = this.ReadCount();
		this.Push(ContainerKind.Vector);
		return count;
	}

	/// <inheritdoc/>
	public void ReadEndVector()
	{
		if (this.NextTokenType != TokenType.EndVector)
		{
			throw new DecoderException($"Expected an end-of-vector token but found {this.NextTokenType}.");
		}

		this.AssertCurrentContainer(ContainerKind.Vector);
		this.position++;
		this.Pop();
	}

	/// <inheritdoc/>
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

	/// <inheritdoc/>
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
					int? count = this.ReadStartMap();
					if (count is int mapCount)
					{
						for (int i = 0; i < mapCount; i++)
						{
							this.ReadPropertyName();
							this.Skip();
						}
					}
					else
					{
						while (this.NextTokenType != TokenType.EndMap)
						{
							this.ReadPropertyName();
							this.Skip();
						}
					}

					this.ReadEndMap();
					return;
				}

			case TokenType.StartVector:
				{
					int? count = this.ReadStartVector();
					if (count is int vectorCount)
					{
						for (int i = 0; i < vectorCount; i++)
						{
							this.Skip();
						}
					}
					else
					{
						while (this.NextTokenType != TokenType.EndVector)
						{
							this.Skip();
						}
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

	/// <inheritdoc/>
	public bool ReadBoolean()
	{
		if (this.NextTokenType != TokenType.Boolean)
		{
			throw new DecoderException($"Expected a Boolean token but found {this.NextTokenType}.");
		}

		this.position++;
		if (this.position >= this.buffer.Length)
		{
			throw new DecoderException("Boolean payload is truncated.");
		}

		bool value = this.buffer[this.position] != 0;
		this.position++;
		return value;
	}

	/// <inheritdoc/>
	public long ReadInt64()
	{
		try
		{
			return long.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException)
		{
			throw new DecoderException("The numeric payload is not a valid Int64.", ex);
		}
	}

	/// <inheritdoc/>
	public ulong ReadUInt64()
	{
		try
		{
			return ulong.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException)
		{
			throw new DecoderException("The numeric payload is not a valid UInt64.", ex);
		}
	}

	/// <inheritdoc/>
	public Int128 ReadInt128()
	{
		try
		{
			return Int128.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException)
		{
			throw new DecoderException("The numeric payload is not a valid Int128.", ex);
		}
	}

	/// <inheritdoc/>
	public UInt128 ReadUInt128()
	{
		try
		{
			return UInt128.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException)
		{
			throw new DecoderException("The numeric payload is not a valid UInt128.", ex);
		}
	}

	/// <inheritdoc/>
	public Half ReadHalf()
	{
		try
		{
			return Half.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException)
		{
			throw new DecoderException("The numeric payload is not a valid Half.", ex);
		}
	}

	/// <inheritdoc/>
	public float ReadSingle()
	{
		try
		{
			return float.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException)
		{
			throw new DecoderException("The numeric payload is not a valid Single.", ex);
		}
	}

	/// <inheritdoc/>
	public double ReadDouble()
	{
		try
		{
			return double.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException)
		{
			throw new DecoderException("The numeric payload is not a valid Double.", ex);
		}
	}

	/// <inheritdoc/>
	public decimal ReadDecimal()
	{
		try
		{
			return decimal.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException)
		{
			throw new DecoderException("The numeric payload is not a valid Decimal.", ex);
		}
	}

	/// <inheritdoc/>
	public DateTime ReadDateTime()
	{
		string text = this.ReadString();
		try
		{
			return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		}
		catch (FormatException ex)
		{
			throw new DecoderException($"\"{text}\" is not valid ISO 8601 date/time text.", ex);
		}
	}

	/// <inheritdoc/>
	public TimeSpan ReadTimeSpan()
	{
		string text = this.ReadString();
		try
		{
			return TimeSpan.Parse(text, CultureInfo.InvariantCulture);
		}
		catch (FormatException ex)
		{
			throw new DecoderException($"\"{text}\" is not valid duration text.", ex);
		}
	}

	/// <inheritdoc/>
	public BigInteger ReadBigInteger()
	{
		try
		{
			return BigInteger.Parse(this.ReadNumericString(), CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is FormatException or OverflowException)
		{
			throw new DecoderException("The numeric payload is not a valid BigInteger.", ex);
		}
	}

	/// <inheritdoc/>
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

	/// <inheritdoc/>
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

	/// <inheritdoc/>
	public byte[] ReadByteArray()
	{
		if (this.NextTokenType != TokenType.Binary)
		{
			throw new DecoderException($"Expected a binary token but found {this.NextTokenType}.");
		}

		this.position++;
		int length = this.ReadLength();
		if (this.position + length > this.buffer.Length)
		{
			throw new DecoderException("Binary payload is truncated.");
		}

		this.currentBinary = new byte[length];
		Buffer.BlockCopy(this.buffer, this.position, this.currentBinary, 0, length);
		this.position += length;
		return this.currentBinary;
	}

	/// <inheritdoc/>
	public ShapeShiftNumber ReadDynamicNumber() => new ShapeShiftDecimal(this.ReadDecimal());

	private static void ThrowUnsupportedNumericFormat(byte tag)
		=> throw new DecoderException($"Unsupported numeric tag 0x{tag:X2}.");

	private void AssertCurrentContainer(ContainerKind expected)
	{
		if (this.depth == 0)
		{
			throw new DecoderException($"Attempted to close a {expected} container when none is open.");
		}

		ContainerKind actual = this.containerKinds[this.depth - 1];
		if (actual != expected)
		{
			throw new DecoderException($"Expected to close a {expected} container, but found a {actual} container.");
		}
	}

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
			throw new DecoderException("Attempted to pop a container when none are open.");
		}

		this.depth--;
	}

	private string ReadNumericString()
	{
		if (this.position >= this.buffer.Length)
		{
			throw new DecoderException("Numeric payload is truncated.");
		}

		byte tag = this.buffer[this.position];
		if (tag is not 0x20 and not 0x21 and not 0x22)
		{
			ThrowUnsupportedNumericFormat(tag);
		}

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

	/// <summary>
	/// Reads a container count previously written by <see cref="ProtobufEncoder"/>, distinguishing
	/// a known count from an unknown one.
	/// </summary>
	/// <returns>The count, or <see langword="null"/> if the writer did not know the count when it wrote the container start.</returns>
	private int? ReadCount()
	{
		uint raw = this.ReadVarint();
		if (raw == 0)
		{
			return null;
		}

		try
		{
			return checked((int)(raw - 1));
		}
		catch (OverflowException ex)
		{
			throw new DecoderException("Container count is too large for this decoder.", ex);
		}
	}

	private int ReadLength()
	{
		try
		{
			return checked((int)this.ReadVarint());
		}
		catch (OverflowException ex)
		{
			throw new DecoderException("Value length is too large for this decoder.", ex);
		}
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
