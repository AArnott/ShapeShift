// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Formats.Cbor;
using System.Numerics;

namespace ShapeShift.Cbor;

/// <summary>
/// Reads ShapeShift tokens from a CBOR document.
/// </summary>
public ref struct CborDecoder : IDecoder
{
	private CborReader reader;
	private Frame[] frames;
	private int depth;
	private string? stringBuffer;

	/// <summary>
	/// Initializes a new instance of the <see cref="CborDecoder"/> struct.
	/// </summary>
	/// <param name="cbor">The encoded CBOR document.</param>
	public CborDecoder(ReadOnlyMemory<byte> cbor)
	{
		this.reader = new(cbor, CborConformanceMode.Lax, allowMultipleRootLevelValues: true);
		this.frames = new Frame[8];
	}

	/// <summary>
	/// Gets the number of unread bytes remaining in the document.
	/// </summary>
	public int BytesRemaining => this.reader.BytesRemaining;

	/// <summary>
	/// Gets the underlying CBOR reader for advanced custom converters.
	/// </summary>
	public CborReader Reader => this.reader;

	/// <inheritdoc/>
	public TokenType NextTokenType
	{
		get
		{
			CborReaderState state;
			try
			{
				state = this.reader.PeekState();
			}
			catch (CborContentException ex)
			{
				throw this.MalformedInput(ex);
			}
			catch (InvalidOperationException ex)
			{
				throw this.TokenMismatch(ex);
			}

			if (this.depth > 0 && this.frames[this.depth - 1].IsMap && this.frames[this.depth - 1].ExpectingProperty)
			{
				if (state == CborReaderState.EndMap)
				{
					return TokenType.EndMap;
				}

				if (state != CborReaderState.TextString)
				{
					throw new DecoderException("ShapeShift CBOR map keys must be text strings.");
				}

				return TokenType.PropertyName;
			}

			return state switch
			{
				CborReaderState.StartMap => TokenType.StartMap,
				CborReaderState.EndMap => TokenType.EndMap,
				CborReaderState.StartArray => TokenType.StartVector,
				CborReaderState.EndArray => TokenType.EndVector,
				CborReaderState.Null => TokenType.Null,
				CborReaderState.Boolean => TokenType.Boolean,
				CborReaderState.TextString or CborReaderState.StartIndefiniteLengthTextString => TokenType.String,
				CborReaderState.ByteString or CborReaderState.StartIndefiniteLengthByteString => TokenType.Binary,
				CborReaderState.UnsignedInteger or CborReaderState.NegativeInteger or CborReaderState.HalfPrecisionFloat or CborReaderState.SinglePrecisionFloat or CborReaderState.DoublePrecisionFloat or CborReaderState.Tag => TokenType.Number,
				CborReaderState.Finished => TokenType.EndDocument,
				_ => throw new DecoderException($"Unsupported CBOR reader state {state}."),
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

		this.ReadNull();
		return true;
	}

	/// <inheritdoc/>
	public int? ReadStartMap()
	{
		this.CompleteValue();
		int? count;
		try
		{
			count = this.reader.ReadStartMap();
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}

		this.Push(isMap: true);
		return count;
	}

	/// <inheritdoc/>
	public void ReadEndMap()
	{
		try
		{
			this.reader.ReadEndMap();
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}

		this.Pop(isMap: true);
		this.CompleteValue();
	}

	/// <inheritdoc/>
	public int? ReadStartVector()
	{
		this.CompleteValue();
		int? count;
		try
		{
			count = this.reader.ReadStartArray();
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}

		this.Push(isMap: false);
		return count;
	}

	/// <inheritdoc/>
	public void ReadEndVector()
	{
		try
		{
			this.reader.ReadEndArray();
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}

		this.Pop(isMap: false);
		this.CompleteValue();
	}

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadPropertyName()
	{
		if (this.depth == 0 || !this.frames[this.depth - 1].IsMap || !this.frames[this.depth - 1].ExpectingProperty)
		{
			throw new DecoderException("A CBOR property name must appear directly inside a map.");
		}

		try
		{
			this.stringBuffer = this.reader.ReadTextString();
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}

		this.frames[this.depth - 1].ExpectingProperty = false;
		return this.stringBuffer.AsSpan();
	}

	/// <inheritdoc/>
	public void Skip()
	{
		if (this.NextTokenType == TokenType.PropertyName)
		{
			_ = this.ReadPropertyName();
		}

		try
		{
			this.reader.SkipValue(disableConformanceModeChecks: false);
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}

		this.CompleteValue();
	}

	/// <inheritdoc/>
	public void ReadNull()
	{
		try
		{
			this.reader.ReadNull();
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}

		this.CompleteValue();
	}

	/// <inheritdoc/>
	public bool ReadBoolean()
	{
		try
		{
			bool value = this.reader.ReadBoolean();
			this.CompleteValue();
			return value;
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
	}

	/// <inheritdoc/>
	public long ReadInt64()
	{
		try
		{
			long value = this.reader.ReadInt64();
			this.CompleteValue();
			return value;
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
		catch (OverflowException ex)
		{
			throw this.NumberTooLarge(ex);
		}
	}

	/// <inheritdoc/>
	public ulong ReadUInt64()
	{
		try
		{
			ulong value = this.reader.ReadUInt64();
			this.CompleteValue();
			return value;
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
		catch (OverflowException ex)
		{
			throw this.NumberTooLarge(ex);
		}
	}

	/// <inheritdoc/>
	public Int128 ReadInt128()
	{
		try
		{
			Int128 value = checked((Int128)this.reader.ReadBigInteger());
			this.CompleteValue();
			return value;
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
		catch (OverflowException ex)
		{
			throw this.NumberTooLarge(ex);
		}
	}

	/// <inheritdoc/>
	public UInt128 ReadUInt128()
	{
		try
		{
			UInt128 value = checked((UInt128)this.reader.ReadBigInteger());
			this.CompleteValue();
			return value;
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
		catch (OverflowException ex)
		{
			throw this.NumberTooLarge(ex);
		}
	}

	/// <inheritdoc/>
	public Half ReadHalf()
	{
		try
		{
			Half value = this.reader.ReadHalf();
			this.CompleteValue();
			return value;
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
	}

	/// <inheritdoc/>
	public float ReadSingle()
	{
		try
		{
			float value = this.reader.ReadSingle();
			this.CompleteValue();
			return value;
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
	}

	/// <inheritdoc/>
	public double ReadDouble()
	{
		try
		{
			double value = this.reader.ReadDouble();
			this.CompleteValue();
			return value;
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
	}

	/// <inheritdoc/>
	public decimal ReadDecimal()
	{
		try
		{
			decimal value = this.reader.ReadDecimal();
			this.CompleteValue();
			return value;
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
		catch (OverflowException ex)
		{
			throw this.NumberTooLarge(ex);
		}
	}

	/// <inheritdoc/>
	public DateTime ReadDateTime()
	{
		string text;
		try
		{
			if (this.reader.ReadTag() != CborTag.DateTimeString)
			{
				throw new DecoderException("The CBOR tag is not a date/time string.");
			}

			text = this.reader.ReadTextString();
			this.CompleteValue();
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}

		if (!DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime value))
		{
			throw new DecoderException($"\"{text}\" is not a valid RFC 3339 date/time string.");
		}

		return value;
	}

	/// <inheritdoc/>
	public TimeSpan ReadTimeSpan() => new(this.ReadInt64());

	/// <inheritdoc/>
	public BigInteger ReadBigInteger()
	{
		try
		{
			BigInteger value = this.reader.ReadBigInteger();
			this.CompleteValue();
			return value;
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
	}

	/// <inheritdoc/>
	public string ReadString()
	{
		try
		{
			string value = this.reader.ReadTextString();
			this.CompleteValue();
			return value;
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
	}

	/// <inheritdoc/>
	public ReadOnlySpan<char> ReadCharSpan()
	{
		this.stringBuffer = this.ReadString();
		return this.stringBuffer.AsSpan();
	}

	/// <inheritdoc/>
	public byte[] ReadByteArray()
	{
		try
		{
			byte[] value = this.reader.ReadByteString();
			this.CompleteValue();
			return value;
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
	}

	/// <inheritdoc/>
	public ShapeShiftNumber ReadDynamicNumber()
	{
		return this.GetNextCborReaderState() switch
		{
			CborReaderState.UnsignedInteger => new ShapeShiftUnsignedInteger(this.ReadUInt64()),
			CborReaderState.NegativeInteger => new ShapeShiftInteger(this.ReadInt64()),
			CborReaderState.HalfPrecisionFloat => new ShapeShiftFloat((double)this.ReadHalf()),
			CborReaderState.SinglePrecisionFloat => new ShapeShiftFloat(this.ReadSingle()),
			CborReaderState.DoublePrecisionFloat => new ShapeShiftFloat(this.ReadDouble()),
			CborReaderState.Tag => this.ReadTaggedDynamicNumber(),
			_ => new ShapeShiftDecimal(this.ReadDecimal()),
		};
	}

	/// <summary>
	/// Ensures no trailing CBOR data remains.
	/// </summary>
	/// <exception cref="DecoderException">Thrown when trailing CBOR data remains.</exception>
	public void EnsureEndOfDocument()
	{
		if (this.NextTokenType != TokenType.EndDocument)
		{
			throw new DecoderException("The CBOR input contains trailing data.");
		}
	}

	private CborReaderState GetNextCborReaderState()
	{
		try
		{
			return this.reader.PeekState();
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}
	}

	private ShapeShiftNumber ReadTaggedDynamicNumber()
	{
		CborTag tag;
		try
		{
			tag = this.reader.PeekTag();
		}
		catch (CborContentException ex)
		{
			throw this.MalformedInput(ex);
		}
		catch (InvalidOperationException ex)
		{
			throw this.TokenMismatch(ex);
		}

		return tag switch
		{
			CborTag.UnsignedBigNum or CborTag.NegativeBigNum => new ShapeShiftBigInteger(this.ReadBigInteger()),
			CborTag.DecimalFraction => new ShapeShiftDecimal(this.ReadDecimal()),
			_ => throw new DecoderException($"CBOR tag {tag} is not a number ShapeShift can represent dynamically."),
		};
	}

	private DecoderException MalformedInput(CborContentException exception) => new("The CBOR input is malformed.", exception);

	private DecoderException TokenMismatch(InvalidOperationException exception) => new("The CBOR token does not match the requested value.", exception);

	private DecoderException NumberTooLarge(OverflowException exception) => new("The CBOR number does not fit the requested value.", exception);

	private void CompleteValue()
	{
		if (this.depth > 0 && this.frames[this.depth - 1].IsMap && !this.frames[this.depth - 1].ExpectingProperty)
		{
			this.frames[this.depth - 1].ExpectingProperty = true;
		}
	}

	private void Push(bool isMap)
	{
		if (this.depth == this.frames.Length)
		{
			Array.Resize(ref this.frames, this.frames.Length * 2);
		}

		this.frames[this.depth++] = new Frame(isMap);
	}

	private void Pop(bool isMap)
	{
		if (this.depth == 0 || this.frames[this.depth - 1].IsMap != isMap)
		{
			throw new DecoderException("The CBOR container end does not match its start.");
		}

		this.depth--;
	}

	private struct Frame(bool isMap)
	{
		internal bool IsMap = isMap;
		internal bool ExpectingProperty = isMap;
	}
}
