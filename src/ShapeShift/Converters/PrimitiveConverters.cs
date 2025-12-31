// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

using Microsoft.NET.StringTools;

namespace ShapeShift.Converters;

internal class BooleanConverter<TEncoder, TDecoder> : ShapeShiftConverter<bool, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override bool Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => decoder.ReadBoolean();

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in bool value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write(value);
}

internal class HalfConverter<TEncoder, TDecoder> : ShapeShiftConverter<Half, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override Half Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => decoder.ReadHalf();

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in Half value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write(value);
}

internal class SingleConverter<TEncoder, TDecoder> : ShapeShiftConverter<float, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override float Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => decoder.ReadSingle();

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in float value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write(value);
}

internal class DoubleConverter<TEncoder, TDecoder> : ShapeShiftConverter<double, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override double Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => decoder.ReadDouble();

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in double value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write(value);
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
	public override void Write(ref TEncoder encoder, in char value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write([value]);
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
		encoder.Write(value);
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
		encoder.Write(value);
	}
}
