// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace ShapeShift.Equality;

/// <summary>
/// Produces canonical byte encodings for well-known leaf types so that
/// collision resistant hashing can hash the full content of a value rather than
/// a lossy 32-bit hash code of it.
/// </summary>
internal static class LeafEncoders
{
	/// <summary>
	/// The largest number of bytes any encoder returned by <see cref="TryGet{T}"/> will write.
	/// </summary>
	internal const int MaxEncodedLength = 16;

	private const ushort CanonicalHalfNaN = 0xFE00;

	private const uint CanonicalSingleNaN = 0xFFC00000;

	private const ulong CanonicalDoubleNaN = 0xFFF8000000000000;

	/// <summary>
	/// Gets a canonical byte encoder for a leaf type, if one is known.
	/// </summary>
	/// <typeparam name="T">The leaf type.</typeparam>
	/// <returns>An encoder, or <see langword="null"/> when the type has no known canonical encoding.</returns>
	internal static LeafEncoder<T>? TryGet<T>()
	{
		if (typeof(T).IsEnum)
		{
			return Unsafe.SizeOf<T>() switch
			{
				1 => static (v, d) => Write(Unsafe.BitCast<T, byte>(v), d),
				2 => static (v, d) => Write(Unsafe.BitCast<T, ushort>(v), d),
				4 => static (v, d) => Write(Unsafe.BitCast<T, uint>(v), d),
				8 => static (v, d) => Write(Unsafe.BitCast<T, ulong>(v), d),
				_ => null,
			};
		}

		if (typeof(T) == typeof(bool))
		{
			return static (v, d) => Write((byte)(Unsafe.BitCast<T, bool>(v) ? 1 : 0), d);
		}

		if (typeof(T) == typeof(byte))
		{
			return static (v, d) => Write(Unsafe.BitCast<T, byte>(v), d);
		}

		if (typeof(T) == typeof(sbyte))
		{
			return static (v, d) => Write(unchecked((byte)Unsafe.BitCast<T, sbyte>(v)), d);
		}

		if (typeof(T) == typeof(short))
		{
			return static (v, d) => Write(unchecked((ushort)Unsafe.BitCast<T, short>(v)), d);
		}

		if (typeof(T) == typeof(ushort))
		{
			return static (v, d) => Write(Unsafe.BitCast<T, ushort>(v), d);
		}

		if (typeof(T) == typeof(char))
		{
			return static (v, d) => Write(Unsafe.BitCast<T, char>(v), d);
		}

		if (typeof(T) == typeof(int))
		{
			return static (v, d) => Write(unchecked((uint)Unsafe.BitCast<T, int>(v)), d);
		}

		if (typeof(T) == typeof(uint))
		{
			return static (v, d) => Write(Unsafe.BitCast<T, uint>(v), d);
		}

		if (typeof(T) == typeof(Rune))
		{
			return static (v, d) => Write(unchecked((uint)Unsafe.BitCast<T, Rune>(v).Value), d);
		}

		if (typeof(T) == typeof(long))
		{
			return static (v, d) => Write(unchecked((ulong)Unsafe.BitCast<T, long>(v)), d);
		}

		if (typeof(T) == typeof(ulong))
		{
			return static (v, d) => Write(Unsafe.BitCast<T, ulong>(v), d);
		}

		if (typeof(T) == typeof(Int128))
		{
			return static (v, d) => Write(unchecked((UInt128)Unsafe.BitCast<T, Int128>(v)), d);
		}

		if (typeof(T) == typeof(UInt128))
		{
			return static (v, d) => Write(Unsafe.BitCast<T, UInt128>(v), d);
		}

		if (typeof(T) == typeof(Half))
		{
			return static (v, d) =>
			{
				Half value = Unsafe.BitCast<T, Half>(v);
				return Write(Half.IsNaN(value) ? CanonicalHalfNaN : BitConverter.HalfToUInt16Bits(value == Half.Zero ? Half.Zero : value), d);
			};
		}

		if (typeof(T) == typeof(float))
		{
			return static (v, d) =>
			{
				float value = Unsafe.BitCast<T, float>(v);
				return Write(float.IsNaN(value) ? CanonicalSingleNaN : BitConverter.SingleToUInt32Bits(value == 0f ? 0f : value), d);
			};
		}

		if (typeof(T) == typeof(double))
		{
			return static (v, d) =>
			{
				double value = Unsafe.BitCast<T, double>(v);
				return Write(double.IsNaN(value) ? CanonicalDoubleNaN : BitConverter.DoubleToUInt64Bits(value == 0d ? 0d : value), d);
			};
		}

		if (typeof(T) == typeof(Guid))
		{
			return static (v, d) =>
			{
				Unsafe.BitCast<T, Guid>(v).TryWriteBytes(d);
				return 16;
			};
		}

		if (typeof(T) == typeof(DateTime))
		{
			// DateTime.Equals compares ticks only, ignoring DateTimeKind.
			return static (v, d) => Write(unchecked((ulong)Unsafe.BitCast<T, DateTime>(v).Ticks), d);
		}

		if (typeof(T) == typeof(DateTimeOffset))
		{
			// DateTimeOffset.Equals compares the UTC instant only, ignoring the offset.
			return static (v, d) => Write(unchecked((ulong)Unsafe.BitCast<T, DateTimeOffset>(v).UtcTicks), d);
		}

		if (typeof(T) == typeof(TimeSpan))
		{
			return static (v, d) => Write(unchecked((ulong)Unsafe.BitCast<T, TimeSpan>(v).Ticks), d);
		}

		if (typeof(T) == typeof(DateOnly))
		{
			return static (v, d) => Write(unchecked((uint)Unsafe.BitCast<T, DateOnly>(v).DayNumber), d);
		}

		if (typeof(T) == typeof(TimeOnly))
		{
			return static (v, d) => Write(unchecked((ulong)Unsafe.BitCast<T, TimeOnly>(v).Ticks), d);
		}

		return null;
	}

	private static int Write(byte value, Span<byte> destination)
	{
		destination[0] = value;
		return 1;
	}

	private static int Write(ushort value, Span<byte> destination)
	{
		BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
		return 2;
	}

	private static int Write(char value, Span<byte> destination) => Write((ushort)value, destination);

	private static int Write(uint value, Span<byte> destination)
	{
		BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
		return 4;
	}

	private static int Write(ulong value, Span<byte> destination)
	{
		BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
		return 8;
	}

	private static int Write(UInt128 value, Span<byte> destination)
	{
		BinaryPrimitives.WriteUInt64LittleEndian(destination, unchecked((ulong)value));
		BinaryPrimitives.WriteUInt64LittleEndian(destination[8..], unchecked((ulong)(value >> 64)));
		return 16;
	}
}
