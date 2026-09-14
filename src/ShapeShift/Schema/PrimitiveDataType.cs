// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Enumerates the scalar data types that ShapeShift knows how to encode natively.
/// </summary>
/// <remarks>
/// The precise encoding of each member is format specific.
/// Consult the documentation for each format for details.
/// </remarks>
public enum PrimitiveDataType
{
	/// <summary>
	/// A <see cref="bool"/>.
	/// </summary>
	Boolean,

	/// <summary>
	/// A <see cref="char"/>, encoded as a single-character string.
	/// </summary>
	Char,

	/// <summary>
	/// A <see cref="System.Text.Rune"/>, encoded as its unicode scalar value.
	/// </summary>
	Rune,

	/// <summary>
	/// A <see cref="string"/>.
	/// </summary>
	String,

	/// <summary>
	/// A sequence of bytes.
	/// </summary>
	Binary,

	/// <summary>
	/// An <see cref="sbyte"/>.
	/// </summary>
	SByte,

	/// <summary>
	/// A <see cref="byte"/>.
	/// </summary>
	Byte,

	/// <summary>
	/// An <see cref="short"/>.
	/// </summary>
	Int16,

	/// <summary>
	/// An <see cref="ushort"/>.
	/// </summary>
	UInt16,

	/// <summary>
	/// An <see cref="int"/>.
	/// </summary>
	Int32,

	/// <summary>
	/// An <see cref="uint"/>.
	/// </summary>
	UInt32,

	/// <summary>
	/// A <see cref="long"/>.
	/// </summary>
	Int64,

	/// <summary>
	/// A <see cref="ulong"/>.
	/// </summary>
	UInt64,

	/// <summary>
	/// An <see cref="Int128"/>.
	/// </summary>
	Int128,

	/// <summary>
	/// A <see cref="UInt128"/>.
	/// </summary>
	UInt128,

	/// <summary>
	/// A <see cref="System.Numerics.BigInteger"/>, which has no bounds.
	/// </summary>
	BigInteger,

	/// <summary>
	/// A <see cref="Half"/>.
	/// </summary>
	Half,

	/// <summary>
	/// A <see cref="float"/>.
	/// </summary>
	Single,

	/// <summary>
	/// A <see cref="double"/>.
	/// </summary>
	Double,

	/// <summary>
	/// A <see cref="decimal"/>.
	/// </summary>
	Decimal,

	/// <summary>
	/// A <see cref="System.DateTime"/>.
	/// </summary>
	DateTime,

	/// <summary>
	/// A <see cref="System.DateTimeOffset"/>, encoded as a two-element vector of the UTC time and the offset in minutes.
	/// </summary>
	DateTimeOffset,

	/// <summary>
	/// A <see cref="System.TimeSpan"/>.
	/// </summary>
	TimeSpan,
}
