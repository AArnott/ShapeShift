// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Json;

/// <summary>
/// The deserializer limits that a JSON Schema projection should express.
/// </summary>
/// <param name="MaxCollectionLength">The maximum number of elements permitted in an array or object.</param>
/// <param name="MaxStringLength">The maximum number of characters permitted in a string.</param>
/// <param name="MaxBinaryLength">The maximum number of bytes permitted in a binary value.</param>
public record struct JsonSchemaLimits(int MaxCollectionLength, int MaxStringLength, int MaxBinaryLength)
{
	/// <summary>
	/// Creates limits that match a serialization context.
	/// </summary>
	/// <param name="context">The context whose limits should be described.</param>
	/// <returns>The limits.</returns>
	public static JsonSchemaLimits FromContext(SerializationContext<JsonEncoder, JsonDecoder> context)
		=> new(context.MaxCollectionLength, context.MaxStringLength, context.MaxBinaryLength);
}
