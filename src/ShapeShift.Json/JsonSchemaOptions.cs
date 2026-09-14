// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Schema;

namespace ShapeShift.Json;

/// <summary>
/// Options that influence how a <see cref="DataContract"/> is projected onto JSON Schema.
/// </summary>
public record JsonSchemaOptions
{
	/// <summary>
	/// Gets the options used when the caller does not supply any.
	/// </summary>
	public static JsonSchemaOptions Default { get; } = new();

	/// <summary>
	/// Gets the serialization format that the generated schema describes.
	/// </summary>
	/// <value>The default is <see cref="JsonSchemaProfile.Json"/>.</value>
	public JsonSchemaProfile Profile { get; init; } = JsonSchemaProfile.Json;

	/// <summary>
	/// Gets a value indicating whether the root schema carries a <c>$schema</c> keyword
	/// that identifies the JSON Schema dialect.
	/// </summary>
	/// <value>The default is <see langword="true" />.</value>
	public bool IncludeSchemaKeyword { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether floating point schemas also accept
	/// the <c>"NaN"</c>, <c>"Infinity"</c> and <c>"-Infinity"</c> string literals.
	/// </summary>
	/// <value>The default is <see langword="false" />.</value>
	/// <remarks>
	/// Set this to match <see cref="JsonSerializer.AllowNamedFloatingPointValues"/>.
	/// <see cref="JsonSerializer.GetJsonSchema{T}(JsonSchemaOptions?)"/> does so automatically
	/// when the caller does not specify options.
	/// </remarks>
	public bool AllowNamedFloatingPointValues { get; init; }

	/// <summary>
	/// Gets a value indicating whether schemas include <c>$comment</c> keywords
	/// that explain representation choices that JSON Schema cannot express.
	/// </summary>
	/// <value>The default is <see langword="true" />.</value>
	public bool IncludeComments { get; init; } = true;

	/// <summary>
	/// Gets the limits that the serializer enforces, which are projected onto
	/// <c>maxItems</c>, <c>maxLength</c> and similar keywords.
	/// </summary>
	/// <value>The default is <see langword="null" />, which omits all limit keywords.</value>
	/// <remarks>
	/// Limits are opt-in because they describe a particular deserializer's configuration
	/// rather than the data contract itself.
	/// </remarks>
	public JsonSchemaLimits? Limits { get; init; }
}
