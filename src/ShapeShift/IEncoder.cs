// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace ShapeShift;

/// <summary>
/// Writes the ShapeShift data model in one particular wire format.
/// </summary>
/// <remarks>
/// <para>
/// An encoder is the "how" half of a format package: the shared converter layer decides what tokens
/// to write, and the encoder decides how each one appears on the wire. It holds no converter,
/// contract, or policy knowledge.
/// </para>
/// <para>
/// Implementations are conventionally <see langword="ref" /> structs so that they may hold spans and
/// cannot escape to the heap or cross an <see langword="await" />. They are always passed by
/// <see langword="ref" /> so that a caller sees the mutations each write makes.
/// </para>
/// <para>
/// Every write must be balanced: each <see cref="WriteStartMap"/> is matched by a
/// <see cref="WriteEndMap"/>, each <see cref="WriteStartVector"/> by a <see cref="WriteEndVector"/>,
/// and inside a map every <see cref="WritePropertyName"/> is followed by exactly one value.
/// </para>
/// </remarks>
public interface IEncoder
{
	/// <summary>
	/// Writes the beginning of a map.
	/// </summary>
	/// <param name="propertyCount">
	/// The number of entries that will follow, when the caller knows it; otherwise <see langword="null" />.
	/// A length-prefixed format uses it; a format that writes an explicit end token may ignore it, but
	/// must then also ignore it consistently so its decoder never reports a count it did not write.
	/// </param>
	void WriteStartMap(int? propertyCount);

	/// <summary>
	/// Writes the end of the innermost open map.
	/// </summary>
	void WriteEndMap();

	/// <summary>
	/// Writes the beginning of a vector.
	/// </summary>
	/// <param name="itemCount">
	/// The number of elements that will follow, when the caller knows it; otherwise <see langword="null" />.
	/// </param>
	void WriteStartVector(int? itemCount);

	/// <summary>
	/// Writes the end of the innermost open vector.
	/// </summary>
	void WriteEndVector();

	/// <summary>
	/// Writes the name of the map entry whose value comes next.
	/// </summary>
	/// <param name="name">The property name.</param>
	/// <remarks>
	/// Valid only directly inside a map. Many formats encode a key differently from a string value,
	/// so this is a distinct operation rather than a call to <see cref="WriteValue(ReadOnlySpan{char})"/>.
	/// </remarks>
	void WritePropertyName(scoped ReadOnlySpan<char> name);

	/// <summary>
	/// Writes an explicit null.
	/// </summary>
	void WriteNull();

	/// <summary>
	/// Writes a Boolean.
	/// </summary>
	/// <param name="value">The value to write.</param>
	void WriteValue(bool value);

	/// <summary>
	/// Writes a signed 64-bit integer.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <remarks>
	/// A format is free to choose the narrowest representation that holds the value, provided its
	/// decoder widens it back losslessly.
	/// </remarks>
	void WriteValue(long value);

	/// <summary>
	/// Writes an unsigned 64-bit integer.
	/// </summary>
	/// <param name="value">The value to write.</param>
	void WriteValue(ulong value);

	/// <summary>
	/// Writes a signed 128-bit integer.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <remarks>
	/// Few formats have a native 128-bit type. Choose a documented, interoperable encoding (such as
	/// decimal text) rather than one that narrows the value.
	/// </remarks>
	void WriteValue(Int128 value);

	/// <summary>
	/// Writes an unsigned 128-bit integer.
	/// </summary>
	/// <param name="value">The value to write.</param>
	void WriteValue(UInt128 value);

	/// <summary>
	/// Writes a half-precision floating-point number.
	/// </summary>
	/// <param name="value">The value to write.</param>
	void WriteValue(Half value);

	/// <summary>
	/// Writes a single-precision floating-point number.
	/// </summary>
	/// <param name="value">The value to write.</param>
	void WriteValue(float value);

	/// <summary>
	/// Writes a double-precision floating-point number.
	/// </summary>
	/// <param name="value">The value to write.</param>
	void WriteValue(double value);

	/// <summary>
	/// Writes an exact decimal number.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <remarks>
	/// Binary floating point cannot hold a <see cref="decimal"/> exactly. A format without a native
	/// decimal type should carry its text so that both the digits and the scale survive.
	/// </remarks>
	void WriteValue(decimal value);

	/// <summary>
	/// Writes a date and time.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <remarks>
	/// The representation must preserve whatever the format's decoder promises to read back, including
	/// the <see cref="DateTimeKind"/> if the round trip is to be faithful.
	/// </remarks>
	void WriteValue(DateTime value);

	/// <summary>
	/// Writes a duration.
	/// </summary>
	/// <param name="value">The value to write.</param>
	void WriteValue(TimeSpan value);

	/// <summary>
	/// Writes an arbitrarily large integer.
	/// </summary>
	/// <param name="value">The value to write.</param>
	void WriteValue(BigInteger value);

	/// <summary>
	/// Writes a string.
	/// </summary>
	/// <param name="value">The value to write.</param>
	void WriteValue(string value);

	/// <summary>
	/// Writes text without requiring the caller to materialize a <see cref="string"/>.
	/// </summary>
	/// <param name="value">The text to write.</param>
	/// <remarks>
	/// This is the allocation-free path converters prefer; implement it directly rather than by
	/// forwarding to <see cref="WriteValue(string)"/>.
	/// </remarks>
	void WriteValue(scoped ReadOnlySpan<char> value);

	/// <summary>
	/// Writes a binary value.
	/// </summary>
	/// <param name="value">The bytes to write.</param>
	/// <exception cref="NotSupportedException">Thrown when the format has no binary representation.</exception>
	void WriteValue(scoped ReadOnlySpan<byte> value) => throw new NotSupportedException("This encoder does not support binary values.");
}
