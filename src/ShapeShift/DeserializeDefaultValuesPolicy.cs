// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// Specifies how missing and null values are handled during deserialization.
/// </summary>
[Flags]
public enum DeserializeDefaultValuesPolicy
{
	/// <summary>
	/// Rejects missing required values and null values assigned to non-nullable members.
	/// </summary>
	Default = 0,

	/// <summary>
	/// Allows null values to be assigned to non-nullable members.
	/// </summary>
	AllowNullValuesForNonNullableProperties = 0x1,

	/// <summary>
	/// Allows required values to be absent, leaving their default values in place.
	/// </summary>
	AllowMissingValuesForRequiredProperties = 0x2 | AllowNullValuesForNonNullableProperties,
}
