// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

using System.Numerics;
using Microsoft.NET.StringTools;

namespace ShapeShift.Converters;

internal class BooleanConverter<TEncoder, TDecoder> : ShapeShiftConverter<bool, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override bool Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => decoder.ReadBoolean();

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in bool value, SerializationContext<TEncoder, TDecoder> context) => encoder.WriteValue(value);
}

internal class HalfConverter<TEncoder, TDecoder> : ShapeShiftConverter<Half, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override Half Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => decoder.ReadHalf();

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in Half value, SerializationContext<TEncoder, TDecoder> context) => encoder.WriteValue(value);
}

internal class SingleConverter<TEncoder, TDecoder> : ShapeShiftConverter<float, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override float Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => decoder.ReadSingle();

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in float value, SerializationContext<TEncoder, TDecoder> context) => encoder.WriteValue(value);
}

internal class DoubleConverter<TEncoder, TDecoder> : ShapeShiftConverter<double, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override double Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => decoder.ReadDouble();

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in double value, SerializationContext<TEncoder, TDecoder> context) => encoder.WriteValue(value);
}

internal class DecimalConverter<TEncoder, TDecoder> : ShapeShiftConverter<decimal, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override decimal Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => decoder.ReadDecimal();

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in decimal value, SerializationContext<TEncoder, TDecoder> context) => encoder.WriteValue(value);
}

internal class DateTimeConverter<TEncoder, TDecoder> : ShapeShiftConverter<DateTime, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override DateTime Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => decoder.ReadDateTime();

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in DateTime value, SerializationContext<TEncoder, TDecoder> context) => encoder.WriteValue(value);
}

internal class DateTimeOffsetConverter<TEncoder, TDecoder> : ShapeShiftConverter<DateTimeOffset, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override DateTimeOffset Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		int? count = decoder.ReadStartVector();
		if (count is not null and not 2)
		{
			throw new ShapeShiftSerializationException($"Expected a vector of length 2 to deserialize {nameof(DateTimeOffset)}.");
		}

		DateTime utcDateTime = decoder.ReadDateTime();
		short offsetMinutes = decoder.ReadInt16();

		// We construct the offset very carefully so that it knows it's being initialized with UTC time
		// *and* that we want the time expressed in the offset specified.
		// Passing the offset to the DateTimeOffset constructor would cause it to misinterpret the UTC time
		// as if it had an offset.
		return new DateTimeOffset(utcDateTime).ToOffset(TimeSpan.FromMinutes(offsetMinutes));
	}

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in DateTimeOffset value, SerializationContext<TEncoder, TDecoder> context)
	{
		encoder.WriteStartVector(2);
		encoder.WriteValue(value.UtcDateTime);
		encoder.WriteValue(checked((short)value.Offset.TotalMinutes));
		encoder.WriteEndVector();
	}
}

internal class TimeSpanConverter<TEncoder, TDecoder> : ShapeShiftConverter<TimeSpan, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override TimeSpan Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => decoder.ReadTimeSpan();

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in TimeSpan value, SerializationContext<TEncoder, TDecoder> context) => encoder.WriteValue(value);
}

internal class BigIntegerConverter<TEncoder, TDecoder> : ShapeShiftConverter<BigInteger, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override BigInteger Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => decoder.ReadBigInteger();

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in BigInteger value, SerializationContext<TEncoder, TDecoder> context) => encoder.WriteValue(value);
}

internal class CharConverter<TEncoder, TDecoder> : ShapeShiftConverter<char, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override char Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		return decoder.ReadCharSpan() is [char c] ? c : throw new ShapeShiftSerializationException("Expected a single character.");
	}

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in char value, SerializationContext<TEncoder, TDecoder> context) => encoder.WriteValue([value]);
}

internal class StringConverter<TEncoder, TDecoder> : ShapeShiftConverter<string, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override string? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			decoder.ReadNull();
			return null;
		}

		return decoder.ReadString();
	}

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in string? value, SerializationContext<TEncoder, TDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
		}
		else
		{
			encoder.WriteValue(value);
		}
	}
}

internal class InterningStringConverter<TEncoder, TDecoder> : ShapeShiftConverter<string, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override string? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			decoder.ReadNull();
			return null;
		}

		ReadOnlySpan<char> span = decoder.ReadCharSpan();
		return Strings.WeakIntern(span);
	}

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in string? value, SerializationContext<TEncoder, TDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
		}
		else
		{
			encoder.WriteValue(value);
		}
	}
}
