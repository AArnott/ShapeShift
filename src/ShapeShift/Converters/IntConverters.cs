// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/* THIS (.cs) FILE IS GENERATED. DO NOT CHANGE IT.
 * CHANGE THE .tt FILE INSTEAD. */

#pragma warning disable SA1121 // Simplify type syntax
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single class

namespace ShapeShift.Converters;

/// <summary>Serializes the primitive integer type <see cref="SByte"/>.</summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
internal class SByteConverter<TEncoder, TDecoder> : ShapeShiftConverter<SByte, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override SByte Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => checked((SByte)decoder.ReadInt64());

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in SByte value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write((long)value);
}

/// <summary>Serializes the primitive integer type <see cref="Int16"/>.</summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
internal class Int16Converter<TEncoder, TDecoder> : ShapeShiftConverter<Int16, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override Int16 Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => checked((Int16)decoder.ReadInt64());

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in Int16 value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write((long)value);
}

/// <summary>Serializes the primitive integer type <see cref="Int32"/>.</summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
internal class Int32Converter<TEncoder, TDecoder> : ShapeShiftConverter<Int32, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override Int32 Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => checked((Int32)decoder.ReadInt64());

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in Int32 value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write((long)value);
}

/// <summary>Serializes the primitive integer type <see cref="Int64"/>.</summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
internal class Int64Converter<TEncoder, TDecoder> : ShapeShiftConverter<Int64, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override Int64 Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => checked((Int64)decoder.ReadInt64());

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in Int64 value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write((long)value);
}

/// <summary>Serializes the primitive integer type <see cref="Byte"/>.</summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
internal class ByteConverter<TEncoder, TDecoder> : ShapeShiftConverter<Byte, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override Byte Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => checked((Byte)decoder.ReadUInt64());

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in Byte value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write((ulong)value);
}

/// <summary>Serializes the primitive integer type <see cref="UInt16"/>.</summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
internal class UInt16Converter<TEncoder, TDecoder> : ShapeShiftConverter<UInt16, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override UInt16 Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => checked((UInt16)decoder.ReadUInt64());

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in UInt16 value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write((ulong)value);
}

/// <summary>Serializes the primitive integer type <see cref="UInt32"/>.</summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
internal class UInt32Converter<TEncoder, TDecoder> : ShapeShiftConverter<UInt32, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override UInt32 Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => checked((UInt32)decoder.ReadUInt64());

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in UInt32 value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write((ulong)value);
}

/// <summary>Serializes the primitive integer type <see cref="UInt64"/>.</summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
internal class UInt64Converter<TEncoder, TDecoder> : ShapeShiftConverter<UInt64, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override UInt64 Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context) => checked((UInt64)decoder.ReadUInt64());

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in UInt64 value, SerializationContext<TEncoder, TDecoder> context) => encoder.Write((ulong)value);
}
