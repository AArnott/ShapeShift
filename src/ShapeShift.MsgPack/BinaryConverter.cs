// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Schema;

namespace ShapeShift.MsgPack;

/// <summary>
/// Converts byte arrays using the MessagePack binary family.
/// </summary>
public sealed class BinaryConverter : ShapeShiftConverter<byte[], MsgPackEncoder, MsgPackDecoder>
{
	/// <inheritdoc/>
	public override byte[]? Read(ref MsgPackDecoder decoder, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			decoder.ReadNull();
			return null;
		}

		byte[] value = decoder.ReadByteArray();
		if (value.Length > context.MaxBinaryLength)
		{
			throw new ShapeShiftSerializationException($"Binary length {value.Length} exceeds the configured maximum of {context.MaxBinaryLength}.");
		}

		return value;
	}

	/// <inheritdoc/>
	public override void Write(ref MsgPackEncoder encoder, in byte[]? value, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
			return;
		}

		if (value.Length > context.MaxBinaryLength)
		{
			throw new ShapeShiftSerializationException($"Binary length {value.Length} exceeds the configured maximum of {context.MaxBinaryLength}.");
		}

		encoder.WriteValue(value);
	}

	/// <inheritdoc/>
	public override DataContract? GetContract(ContractContext<MsgPackEncoder, MsgPackDecoder> context)
		=> new PrimitiveContract(typeof(byte[]), PrimitiveDataType.Binary);
}
