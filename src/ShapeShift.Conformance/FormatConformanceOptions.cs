// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// Declares which parts of the ShapeShift data model a format can represent.
/// </summary>
/// <remarks>
/// <para>
/// Every property defaults to the strictest, fully self-describing behavior, which is what a new
/// binary format should aim for. A format that cannot meet one of these expectations relaxes the
/// corresponding property, and the conformance suite reports the affected cases as
/// <see cref="ConformanceOutcome.Skipped"/> with the reason instead of failing them.
/// </para>
/// <para>
/// Relaxing a property is a documented limitation of the format, not a way to hide a bug: the
/// remaining cases still hold the format to the invariants that ShapeShift's converters rely on.
/// </para>
/// </remarks>
public sealed record FormatConformanceOptions
{
	/// <summary>
	/// Gets a value indicating whether the format has a binary representation, so that
	/// <see cref="IEncoder.WriteValue(ReadOnlySpan{byte})"/> and <see cref="IDecoder.ReadByteArray"/> work.
	/// </summary>
	public bool SupportsBinary { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether a scalar may be the entire document, rather than only appearing
	/// inside a map or vector.
	/// </summary>
	public bool SupportsRootScalars { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether a map with no entries round-trips.
	/// </summary>
	public bool SupportsEmptyMaps { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether a vector with no elements round-trips.
	/// </summary>
	public bool SupportsEmptyVectors { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether a vector may appear directly inside another vector.
	/// </summary>
	public bool SupportsNestedVectors { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether an empty string round-trips as a string rather than as
	/// <see langword="null" /> or as a missing value.
	/// </summary>
	public bool SupportsEmptyStrings { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether a string whose text looks like some other scalar
	/// (for example <c>"42"</c>, <c>"true"</c>, or <c>"null"</c>) still reads back as that string.
	/// </summary>
	public bool PreservesAmbiguousStrings { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether <see cref="ulong"/> values above <see cref="long.MaxValue"/> round-trip.
	/// </summary>
	public bool SupportsUnsignedIntegers { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether <see cref="Int128"/> and <see cref="UInt128"/> round-trip at their extremes.
	/// </summary>
	public bool SupportsInt128 { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether <see cref="BigInteger"/> values wider than 128 bits round-trip.
	/// </summary>
	public bool SupportsBigInteger { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether <see cref="decimal"/> round-trips without loss.
	/// </summary>
	public bool SupportsDecimal { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether <see cref="Half"/> round-trips.
	/// </summary>
	public bool SupportsHalf { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether <see cref="double.NaN"/> and the infinities round-trip.
	/// </summary>
	public bool SupportsNonFiniteFloats { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether <see cref="System.DateTime"/> round-trips.
	/// </summary>
	public bool SupportsDateTime { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether <see cref="System.TimeSpan"/> round-trips.
	/// </summary>
	public bool SupportsTimeSpan { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether <see cref="IDecoder.ReadStartMap"/> and
	/// <see cref="IDecoder.ReadStartVector"/> return the exact element count rather than <see langword="null" />.
	/// </summary>
	/// <remarks>
	/// Length-prefixed formats set this; streaming text formats leave it <see langword="false" />.
	/// Either answer conforms, but a format that returns a count must return the correct one.
	/// </remarks>
	public bool ReportsContainerCounts { get; init; }

	/// <summary>
	/// Gets a value indicating whether <see cref="IDecoder.Skip"/> is implemented.
	/// </summary>
	public bool SupportsSkip { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether the decoder supports <see cref="ShapeShiftPath"/> traversal,
	/// which builds on <see cref="IDecoder.Skip"/>.
	/// </summary>
	public bool SupportsPathSeek { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether <see cref="IDecoder.NextTokenType"/> reports
	/// <see cref="TokenType.EndDocument"/> once the top-level value has been consumed.
	/// </summary>
	public bool ReportsEndDocument { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether the format is self-delimiting enough that truncating a payload
	/// is detectable, so a truncated payload fails with <see cref="DecoderException"/> or
	/// <see cref="ShapeShiftSerializationException"/> instead of succeeding or throwing something unrelated.
	/// </summary>
	public bool DetectsTruncatedInput { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether reading a value as the wrong type throws
	/// <see cref="DecoderException"/> rather than coercing it.
	/// </summary>
	public bool RejectsTypeMismatches { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether the format supports
	/// <see cref="ShapeShiftSerializer{TEncoder, TDecoder}.PreserveReferences"/>.
	/// </summary>
	public bool SupportsReferencePreservation { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether <see cref="ShapeShiftValue"/> trees round-trip.
	/// </summary>
	public bool SupportsDynamicValues { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether strings containing astral-plane characters (surrogate pairs) round-trip.
	/// </summary>
	public bool SupportsSurrogatePairs { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether strings containing control characters, including newlines and tabs, round-trip.
	/// </summary>
	public bool SupportsControlCharactersInStrings { get; init; } = true;

	/// <summary>
	/// Gets the deepest nesting the format supports, which bounds the depth the suite tests.
	/// </summary>
	public int MaxTestedNestingDepth { get; init; } = 24;

	/// <summary>
	/// Gets the shared instance describing a fully self-describing format that supports everything.
	/// </summary>
	public static FormatConformanceOptions Default { get; } = new();
}
