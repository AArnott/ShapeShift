// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Schema;

namespace ShapeShift.Cbor;

/// <summary>
/// Converts byte arrays using the CBOR byte string family.
/// </summary>
public sealed class BinaryConverter : ShapeShiftConverter<byte[], CborEncoder, CborDecoder>
{
	/// <inheritdoc/>
	public override byte[]? Read(ref CborDecoder decoder, SerializationContext<CborEncoder, CborDecoder> context)
	{
		if (decoder.TryReadNull())
		{
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
	public override void Write(ref CborEncoder encoder, in byte[]? value, SerializationContext<CborEncoder, CborDecoder> context)
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
	public override DataContract? GetContract(ContractContext<CborEncoder, CborDecoder> context)
		=> new PrimitiveContract(typeof(byte[]), PrimitiveDataType.Binary);
}
