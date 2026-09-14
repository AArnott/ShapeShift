// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.MsgPack;

/// <summary>
/// The MessagePack extension type codes that ShapeShift reserves, reads, and writes.
/// </summary>
/// <remarks>
/// <para>
/// The MessagePack specification splits the signed 8-bit extension type space in two: codes
/// <c>0</c> through <c>127</c> are application specific, while negative codes are reserved for the
/// specification itself (<see cref="Timestamp"/> being the only one defined so far). ShapeShift
/// therefore places every encoding it invents in the application-specific half, and claims a
/// contiguous block (<see cref="ReservedInclusiveLowerBound"/> through
/// <see cref="ReservedInclusiveUpperBound"/>) so that future ShapeShift features never have to
/// negotiate with codes an application may already be using for its own extensions.
/// </para>
/// <para>
/// Codes outside that block are opaque to ShapeShift: they decode as binary values so that unknown
/// extension payloads survive an unknown-data round trip, and they are never produced by ShapeShift itself.
/// </para>
/// <para>
/// Payload shapes are fixed for each reserved code, and readers reject payloads whose length or type
/// code does not match what the reader expects. Reserved codes ShapeShift does not (yet) define are
/// rejected rather than silently treated as data.
/// </para>
/// </remarks>
public static class MsgPackExtensionCodes
{
	/// <summary>
	/// The MessagePack specification's timestamp extension, used for <see cref="DateTime"/> and
	/// <see cref="DateTimeOffset"/> instants.
	/// </summary>
	/// <remarks>
	/// Payloads are the standard 4-, 8-, or 12-byte timestamp encodings. This is the only negative
	/// (specification-reserved) code ShapeShift emits.
	/// </remarks>
	public const sbyte Timestamp = -1;

	/// <summary>
	/// The first extension type code in the block ShapeShift reserves for itself.
	/// </summary>
	public const sbyte ReservedInclusiveLowerBound = 100;

	/// <summary>
	/// The last extension type code in the block ShapeShift reserves for itself.
	/// </summary>
	public const sbyte ReservedInclusiveUpperBound = 109;

	/// <summary>
	/// A <see cref="decimal"/>, encoded as the four big-endian <see cref="int"/> words that
	/// <see cref="decimal.GetBits(decimal)"/> produces, in that order. Payloads are always 16 bytes.
	/// </summary>
	public const sbyte Decimal = 100;

	/// <summary>
	/// An <see cref="Int128"/>, encoded as a 16-byte big-endian two's complement integer.
	/// </summary>
	public const sbyte Int128 = 101;

	/// <summary>
	/// A <see cref="UInt128"/>, encoded as a 16-byte big-endian unsigned integer.
	/// </summary>
	public const sbyte UInt128 = 102;

	/// <summary>
	/// A <see cref="System.Numerics.BigInteger"/>, encoded as a variable length big-endian two's complement integer.
	/// </summary>
	public const sbyte BigInteger = 103;

	/// <summary>
	/// A <see cref="System.TimeSpan"/>, encoded as a big-endian signed 64-bit tick count. Payloads are always 8 bytes.
	/// </summary>
	public const sbyte TimeSpan = 104;

	/// <summary>
	/// A reference to an object that appeared earlier in the same payload, written when
	/// <see cref="ReferencePreservationMode"/> is not <see cref="ReferencePreservationMode.Off"/>.
	/// </summary>
	/// <remarks>
	/// The payload is a big-endian unsigned integer of 1, 2, or 4 bytes identifying the 0-based order in
	/// which the referenced object was first written. Readers reject any other payload length.
	/// </remarks>
	public const sbyte Reference = 105;

	/// <summary>
	/// Gets a value indicating whether a given extension type code falls within the block ShapeShift reserves.
	/// </summary>
	/// <param name="typeCode">The extension type code found in a payload.</param>
	/// <returns><see langword="true" /> when ShapeShift reserves <paramref name="typeCode"/>; otherwise <see langword="false" />.</returns>
	/// <remarks>
	/// <see cref="Timestamp"/> is defined by the MessagePack specification rather than reserved by ShapeShift,
	/// so this method returns <see langword="false" /> for it.
	/// </remarks>
	public static bool IsReservedByShapeShift(sbyte typeCode)
		=> typeCode is >= ReservedInclusiveLowerBound and <= ReservedInclusiveUpperBound;

	/// <summary>
	/// Gets a short human readable description of a reserved extension type code, for use in error messages.
	/// </summary>
	/// <param name="typeCode">The extension type code found in a payload.</param>
	/// <returns>A description, or <see langword="null" /> when ShapeShift assigns no meaning to <paramref name="typeCode"/>.</returns>
	internal static string? Describe(sbyte typeCode) => typeCode switch
	{
		Timestamp => "timestamp",
		Decimal => "decimal",
		Int128 => "Int128",
		UInt128 => "UInt128",
		BigInteger => "BigInteger",
		TimeSpan => "TimeSpan",
		Reference => "object reference",
		_ => IsReservedByShapeShift(typeCode) ? "an extension type code reserved by ShapeShift for future use" : null,
	};
}
