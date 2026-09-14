// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // Closely related JSON primitive converters.
#pragma warning disable SA1649 // Closely related JSON primitive converters.

using System.Text.Json.Nodes;
using ShapeShift.Schema;

namespace ShapeShift.Json;

/// <summary>
/// Converts byte arrays as base64 JSON strings.
/// </summary>
public sealed class BinaryConverter : ShapeShiftConverter<byte[], JsonEncoder, JsonDecoder>
{
	/// <inheritdoc/>
	public override byte[]? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			return null;
		}

		byte[] value = decoder.ReadByteArray();
		ValidateLength(value.Length, context);
		return value;
	}

	/// <inheritdoc/>
	public override void Write(ref JsonEncoder encoder, in byte[]? value, SerializationContext<JsonEncoder, JsonDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
			return;
		}

		ValidateLength(value.Length, context);
		encoder.WriteValue(value);
	}

	/// <inheritdoc/>
	public override DataContract? GetContract(ContractContext<JsonEncoder, JsonDecoder> context)
		=> new PrimitiveContract(typeof(byte[]), PrimitiveDataType.Binary);

	private static void ValidateLength(int length, SerializationContext<JsonEncoder, JsonDecoder> context)
	{
		if (length > context.MaxBinaryLength)
		{
			throw new ShapeShiftSerializationException($"Binary length {length} exceeds the configured maximum of {context.MaxBinaryLength}.");
		}
	}
}

/// <summary>
/// Converts detached <see cref="JsonElement"/> values.
/// </summary>
public sealed class JsonElementConverter : ShapeShiftConverter<JsonElement, JsonEncoder, JsonDecoder>
{
	/// <inheritdoc/>
	public override JsonElement Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
		=> decoder.ReadJsonElement();

	/// <inheritdoc/>
	public override void Write(ref JsonEncoder encoder, in JsonElement value, SerializationContext<JsonEncoder, JsonDecoder> context)
		=> value.WriteTo(encoder.Writer);

	/// <inheritdoc/>
	public override DataContract? GetContract(ContractContext<JsonEncoder, JsonDecoder> context)
		=> new DynamicContract(typeof(JsonElement));
}

/// <summary>
/// Converts <see cref="JsonDocument"/> values.
/// </summary>
public sealed class JsonDocumentConverter : ShapeShiftConverter<JsonDocument, JsonEncoder, JsonDecoder>
{
	/// <inheritdoc/>
	public override JsonDocument? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
		=> JsonDocument.Parse(decoder.ReadJsonElement().GetRawText());

	/// <inheritdoc/>
	public override void Write(ref JsonEncoder encoder, in JsonDocument? value, SerializationContext<JsonEncoder, JsonDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
		}
		else
		{
			value.WriteTo(encoder.Writer);
		}
	}

	/// <inheritdoc/>
	public override DataContract? GetContract(ContractContext<JsonEncoder, JsonDecoder> context)
		=> new DynamicContract(typeof(JsonDocument));
}

/// <summary>
/// Converts <see cref="JsonNode"/> values.
/// </summary>
public sealed class JsonNodeConverter : ShapeShiftConverter<JsonNode, JsonEncoder, JsonDecoder>
{
	/// <inheritdoc/>
	public override JsonNode? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
		=> JsonNode.Parse(decoder.ReadJsonElement().GetRawText());

	/// <inheritdoc/>
	public override void Write(ref JsonEncoder encoder, in JsonNode? value, SerializationContext<JsonEncoder, JsonDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
		}
		else
		{
			value.WriteTo(encoder.Writer);
		}
	}

	/// <inheritdoc/>
	public override DataContract? GetContract(ContractContext<JsonEncoder, JsonDecoder> context)
		=> new DynamicContract(typeof(JsonNode));
}
