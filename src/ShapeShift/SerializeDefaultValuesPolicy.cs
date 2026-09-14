// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// Specifies which properties with default values are serialized.
/// </summary>
[Flags]
public enum SerializeDefaultValuesPolicy
{
	/// <summary>
	/// Omits all properties whose values equal their declared or CLR default values.
	/// </summary>
	Never = 0,

	/// <summary>
	/// Includes default values for properties required to reconstruct the object.
	/// </summary>
	Required = 0x1,

	/// <summary>
	/// Includes default values for value-typed properties.
	/// </summary>
	ValueTypes = 0x2,

	/// <summary>
	/// Includes default values for reference-typed properties.
	/// </summary>
	ReferenceTypes = 0x4,

	/// <summary>
	/// Includes every property regardless of its value.
	/// </summary>
	Always = Required | ValueTypes | ReferenceTypes,
}
