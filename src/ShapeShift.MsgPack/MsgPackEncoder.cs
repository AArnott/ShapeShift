// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.MsgPack;

/// <summary>
/// Writes MessagePack tokens to an <see cref="IBufferWriter{T}"/>.
/// </summary>
/// <param name="output">The destination buffer.</param>
public ref struct MsgPackEncoder(IBufferWriter<byte> output) : IEncoder
{
	/// <summary>
	/// Gets the destination buffer.
	/// </summary>
	public IBufferWriter<byte> Output => output;

	/// <inheritdoc/>
	public static object PreparePropertyName(string name) => Encoding.UTF8.GetBytes(name);

	/// <inheritdoc/>
	public void WriteStartMap(int? propertyCount) => this.WriteMapHeader(RequireCount(propertyCount));

	/// <inheritdoc/>
	public void WriteEndMap()
	{
	}

	/// <inheritdoc/>
	public void WriteStartVector(int? itemCount) => this.WriteArrayHeader(RequireCount(itemCount));

	/// <inheritdoc/>
	public void WriteEndVector()
	{
	}

	/// <inheritdoc/>
	public void WritePropertyName(scoped ReadOnlySpan<char> name) => this.WriteString(name);

	/// <inheritdoc/>
	public void WritePropertyName(scoped ReadOnlySpan<char> name, object? preparedName)
	{
		ArgumentNullException.ThrowIfNull(preparedName);
		byte[] utf8Name = (byte[])preparedName;
		this.WriteStringHeader(utf8Name.Length);
		this.WriteBytes(utf8Name);
	}

	/// <inheritdoc/>
	public void WriteNull() => this.WriteByte(0xc0);

	/// <inheritdoc/>
	public void WriteValue(bool value) => this.WriteByte(value ? (byte)0xc3 : (byte)0xc2);

	/// <inheritdoc/>
	public void WriteValue(long value)
	{
		if (value >= 0)
		{
			this.WriteValue((ulong)value);
		}
		else if (value >= -32)
		{
			this.WriteByte(unchecked((byte)value));
		}
		else if (value >= sbyte.MinValue)
		{
			this.WriteByteAndBigEndian(0xd0, (sbyte)value);
		}
		else if (value >= short.MinValue)
		{
			this.WriteByteAndBigEndian(0xd1, (short)value);
		}
		else if (value >= int.MinValue)
		{
			this.WriteByteAndBigEndian(0xd2, (int)value);
		}
		else
		{
			this.WriteByteAndBigEndian(0xd3, value);
		}
	}

	/// <inheritdoc/>
	public void WriteValue(ulong value)
	{
		if (value <= 0x7f)
		{
			this.WriteByte((byte)value);
		}
		else if (value <= byte.MaxValue)
		{
			this.WriteByteAndBigEndian(0xcc, (byte)value);
		}
		else if (value <= ushort.MaxValue)
		{
			this.WriteByteAndBigEndian(0xcd, (ushort)value);
		}
		else if (value <= uint.MaxValue)
		{
			this.WriteByteAndBigEndian(0xce, (uint)value);
		}
		else
		{
			this.WriteByteAndBigEndian(0xcf, value);
		}
	}

	/// <inheritdoc/>
	public void WriteValue(Int128 value)
	{
		Span<byte> payload = stackalloc byte[16];
		BinaryPrimitives.WriteInt128BigEndian(payload, value);
		this.WriteExtension(MsgPackExtensionCodes.Int128, payload);
	}

	/// <inheritdoc/>
	public void WriteValue(UInt128 value)
	{
		Span<byte> payload = stackalloc byte[16];
		BinaryPrimitives.WriteUInt128BigEndian(payload, value);
		this.WriteExtension(MsgPackExtensionCodes.UInt128, payload);
	}

	/// <inheritdoc/>
	public void WriteValue(Half value) => this.WriteValue((float)value);

	/// <inheritdoc/>
	public void WriteValue(float value)
	{
		Span<byte> destination = output.GetSpan(5);
		destination[0] = 0xca;
		BinaryPrimitives.WriteSingleBigEndian(destination[1..], value);
		output.Advance(5);
	}

	/// <inheritdoc/>
	public void WriteValue(double value)
	{
		Span<byte> destination = output.GetSpan(9);
		destination[0] = 0xcb;
		BinaryPrimitives.WriteDoubleBigEndian(destination[1..], value);
		output.Advance(9);
	}

	/// <inheritdoc/>
	public void WriteValue(decimal value)
	{
		Span<byte> payload = stackalloc byte[16];
		int[] bits = decimal.GetBits(value);
		for (int i = 0; i < bits.Length; i++)
		{
			BinaryPrimitives.WriteInt32BigEndian(payload[(i * 4)..], bits[i]);
		}

		this.WriteExtension(MsgPackExtensionCodes.Decimal, payload);
	}

	/// <inheritdoc/>
	public void WriteValue(DateTime value)
	{
		DateTime utc = value.ToUniversalTime();
		long ticksSinceEpoch = utc.Ticks - DateTime.UnixEpoch.Ticks;
		long seconds = Math.DivRem(ticksSinceEpoch, TimeSpan.TicksPerSecond, out long remainingTicks);
		if (remainingTicks < 0)
		{
			seconds--;
			remainingTicks += TimeSpan.TicksPerSecond;
		}

		uint nanoseconds = checked((uint)(remainingTicks * 100));
		Span<byte> payload = stackalloc byte[12];
		BinaryPrimitives.WriteUInt32BigEndian(payload, nanoseconds);
		BinaryPrimitives.WriteInt64BigEndian(payload[4..], seconds);
		this.WriteExtension(MsgPackExtensionCodes.Timestamp, payload);
	}

	/// <inheritdoc/>
	public void WriteValue(TimeSpan value)
	{
		Span<byte> payload = stackalloc byte[8];
		BinaryPrimitives.WriteInt64BigEndian(payload, value.Ticks);
		this.WriteExtension(MsgPackExtensionCodes.TimeSpan, payload);
	}

	/// <inheritdoc/>
	public void WriteValue(BigInteger value)
		=> this.WriteExtension(MsgPackExtensionCodes.BigInteger, value.ToByteArray(isUnsigned: false, isBigEndian: true));

	/// <inheritdoc/>
	public void WriteValue(string value)
	{
		ArgumentNullException.ThrowIfNull(value);
		this.WriteString(value);
	}

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<char> value) => this.WriteString(value);

	/// <inheritdoc/>
	public void WriteValue(scoped ReadOnlySpan<byte> value)
	{
		this.WriteBinaryHeader(value.Length);
		this.WriteBytes(value);
	}

	/// <summary>
	/// Writes a MessagePack array header, after which exactly <paramref name="count"/> values must be written.
	/// </summary>
	/// <param name="count">The number of elements the array will contain.</param>
	/// <remarks>
	/// This is a low-level building block for custom converters that need to emit a MessagePack structure the
	/// format-neutral <see cref="IEncoder"/> members do not describe.
	/// </remarks>
	public void WriteArrayHeader(int count)
	{
		if (count <= 15)
		{
			this.WriteByte((byte)(0x90 | RequireNonNegative(count)));
		}
		else if (count <= ushort.MaxValue)
		{
			this.WriteByteAndBigEndian(0xdc, (ushort)count);
		}
		else
		{
			this.WriteByteAndBigEndian(0xdd, checked((uint)count));
		}
	}

	/// <summary>
	/// Writes a MessagePack map header, after which exactly <paramref name="count"/> key/value pairs must be written.
	/// </summary>
	/// <param name="count">The number of entries the map will contain.</param>
	/// <remarks>
	/// <inheritdoc cref="WriteArrayHeader(int)" path="/remarks"/>
	/// </remarks>
	public void WriteMapHeader(int count)
	{
		if (count <= 15)
		{
			this.WriteByte((byte)(0x80 | RequireNonNegative(count)));
		}
		else if (count <= ushort.MaxValue)
		{
			this.WriteByteAndBigEndian(0xde, (ushort)count);
		}
		else
		{
			this.WriteByteAndBigEndian(0xdf, checked((uint)count));
		}
	}

	/// <summary>
	/// Writes a MessagePack extension value, choosing the most compact of the fixext, ext8, ext16, and ext32 forms.
	/// </summary>
	/// <param name="typeCode">
	/// The extension type code. Codes 0-127 are application specific; negative codes are reserved by the
	/// MessagePack specification. See <see cref="MsgPackExtensionCodes"/> for the codes ShapeShift itself reserves,
	/// which custom converters should avoid.
	/// </param>
	/// <param name="payload">The extension's payload.</param>
	public void WriteExtension(sbyte typeCode, scoped ReadOnlySpan<byte> payload)
	{
		switch (payload.Length)
		{
			case 1:
				this.WriteByte(0xd4);
				break;
			case 2:
				this.WriteByte(0xd5);
				break;
			case 4:
				this.WriteByte(0xd6);
				break;
			case 8:
				this.WriteByte(0xd7);
				break;
			case 16:
				this.WriteByte(0xd8);
				break;
			default:
				if (payload.Length <= byte.MaxValue)
				{
					this.WriteByteAndBigEndian(0xc7, (byte)payload.Length);
				}
				else if (payload.Length <= ushort.MaxValue)
				{
					this.WriteByteAndBigEndian(0xc8, (ushort)payload.Length);
				}
				else
				{
					this.WriteByteAndBigEndian(0xc9, checked((uint)payload.Length));
				}

				break;
		}

		this.WriteByte(unchecked((byte)typeCode));
		this.WriteBytes(payload);
	}

	/// <summary>
	/// Copies already-encoded MessagePack bytes to the output verbatim.
	/// </summary>
	/// <param name="messagePack">One or more complete, well-formed MessagePack values.</param>
	/// <remarks>
	/// The caller is responsible for the bytes being valid MessagePack and for their count matching whatever
	/// container header preceded them; this method does not validate them.
	/// </remarks>
	public void WriteRaw(scoped ReadOnlySpan<byte> messagePack) => this.WriteBytes(messagePack);

	private static int RequireCount(int? count)
		=> count is >= 0 ? count.Value : throw new ShapeShiftSerializationException("MessagePack containers require a known non-negative length.");

	private static int RequireNonNegative(int count)
		=> count >= 0 ? count : throw new ShapeShiftSerializationException("MessagePack containers require a known non-negative length.");

	private void WriteString(scoped ReadOnlySpan<char> value)
	{
		int byteCount = Encoding.UTF8.GetByteCount(value);
		this.WriteStringHeader(byteCount);
		Span<byte> destination = output.GetSpan(byteCount);
		int bytesWritten = Encoding.UTF8.GetBytes(value, destination);
		output.Advance(bytesWritten);
	}

	private void WriteStringHeader(int count)
	{
		if (count <= 31)
		{
			this.WriteByte((byte)(0xa0 | count));
		}
		else if (count <= byte.MaxValue)
		{
			this.WriteByteAndBigEndian(0xd9, (byte)count);
		}
		else if (count <= ushort.MaxValue)
		{
			this.WriteByteAndBigEndian(0xda, (ushort)count);
		}
		else
		{
			this.WriteByteAndBigEndian(0xdb, checked((uint)count));
		}
	}

	private void WriteBinaryHeader(int count)
	{
		if (count <= byte.MaxValue)
		{
			this.WriteByteAndBigEndian(0xc4, (byte)count);
		}
		else if (count <= ushort.MaxValue)
		{
			this.WriteByteAndBigEndian(0xc5, (ushort)count);
		}
		else
		{
			this.WriteByteAndBigEndian(0xc6, checked((uint)count));
		}
	}

	private void WriteByte(byte value)
	{
		Span<byte> destination = output.GetSpan(1);
		destination[0] = value;
		output.Advance(1);
	}

	private void WriteBytes(scoped ReadOnlySpan<byte> value)
	{
		value.CopyTo(output.GetSpan(value.Length));
		output.Advance(value.Length);
	}

	private void WriteByteAndBigEndian<T>(byte code, T value)
		where T : unmanaged, IBinaryInteger<T>
	{
		int width = value.GetByteCount();
		Span<byte> destination = output.GetSpan(width + 1);
		destination[0] = code;
		if (!value.TryWriteBigEndian(destination[1..], out int written) || written != width)
		{
			throw new InvalidOperationException("Failed to encode an integer.");
		}

		output.Advance(width + 1);
	}
}
