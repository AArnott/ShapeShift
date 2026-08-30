// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Json;

/// <summary>
/// The serialization format that a JSON Schema projection describes.
/// </summary>
/// <remarks>
/// JSON Schema describes the JSON data model. When a profile other than <see cref="Json"/> is selected,
/// the projection still describes the logical structure using JSON Schema keywords, but adds
/// annotations that describe how the format actually encodes each value on the wire.
/// </remarks>
public enum JsonSchemaProfile
{
	/// <summary>
	/// Describes the JSON encoding produced by <see cref="JsonSerializer"/>.
	/// </summary>
	Json,

	/// <summary>
	/// Describes the MessagePack encoding, adding <c>x-msgpack-*</c> annotations
	/// wherever MessagePack uses a representation that JSON cannot express directly.
	/// </summary>
	MessagePack,
}
