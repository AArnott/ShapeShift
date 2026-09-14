// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// Identifies a family of values whose <see cref="TokenType"/> a format may map differently
/// than the default, self-describing mapping.
/// </summary>
/// <remarks>
/// Self-describing binary formats generally use the default mapping. Plain-text formats that do not
/// tag their scalars (for example an indentation-based format that writes <see langword="true" /> as bare text)
/// may legitimately surface booleans, dates, or durations as <see cref="TokenType.String"/>.
/// A format declares those deviations by overriding
/// <see cref="FormatConformanceAdapter{TEncoder, TDecoder}.GetExpectedTokenType(ConformanceValueKind)"/>.
/// </remarks>
public enum ConformanceValueKind
{
	/// <summary>The <see langword="null" /> value.</summary>
	Null,

	/// <summary>A boolean value.</summary>
	Boolean,

	/// <summary>An integral number.</summary>
	Integer,

	/// <summary>A real number.</summary>
	Float,

	/// <summary>A textual value.</summary>
	String,

	/// <summary>An opaque sequence of bytes.</summary>
	Binary,

	/// <summary>A <see cref="DateTime"/> value.</summary>
	DateTime,

	/// <summary>A <see cref="TimeSpan"/> value.</summary>
	TimeSpan,

	/// <summary>A map (an object with named members).</summary>
	Map,

	/// <summary>A vector (an ordered sequence of values).</summary>
	Vector,
}
