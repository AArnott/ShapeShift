// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Enumerates the kinds of <see cref="DataContract"/> that ShapeShift can describe.
/// </summary>
/// <remarks>
/// Each member corresponds to exactly one concrete <see cref="DataContract"/> subclass,
/// allowing consumers to switch over <see cref="DataContract.Kind"/> instead of type testing.
/// </remarks>
public enum DataContractKind
{
	/// <summary>
	/// A scalar value described by <see cref="PrimitiveContract"/>.
	/// </summary>
	Primitive,

	/// <summary>
	/// A map of named properties described by <see cref="ObjectContract"/>.
	/// </summary>
	Object,

	/// <summary>
	/// A variable-length vector of homogeneous elements described by <see cref="SequenceContract"/>.
	/// </summary>
	Sequence,

	/// <summary>
	/// A multidimensional array described by <see cref="RectangularArrayContract"/>.
	/// </summary>
	RectangularArray,

	/// <summary>
	/// An association of keys to values described by <see cref="MapContract"/>.
	/// </summary>
	Map,

	/// <summary>
	/// An enumeration described by <see cref="EnumContract"/>.
	/// </summary>
	Enum,

	/// <summary>
	/// A value that may be absent, described by <see cref="OptionalContract"/>.
	/// </summary>
	Optional,

	/// <summary>
	/// A discriminated union described by <see cref="UnionContract"/>.
	/// </summary>
	Union,

	/// <summary>
	/// A type serialized through a surrogate, described by <see cref="SurrogateContract"/>.
	/// </summary>
	Surrogate,

	/// <summary>
	/// A value whose structure is only known at runtime, described by <see cref="DynamicContract"/>.
	/// </summary>
	Dynamic,

	/// <summary>
	/// A value whose representation is deliberately not described, per <see cref="UndocumentedContract"/>.
	/// </summary>
	Undocumented,
}
