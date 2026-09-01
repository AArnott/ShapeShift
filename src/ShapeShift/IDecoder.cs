// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace ShapeShift;

/// <summary>
/// Reads the ShapeShift data model from one particular wire format.
/// </summary>
/// <remarks>
/// <para>
/// A decoder is a pull parser. Two rules make up the whole contract the format-neutral converter
/// layer relies on:
/// </para>
/// <list type="number">
/// <item>
/// <see cref="NextTokenType"/> is a peek: it reports what comes next without consuming anything, and
/// may be called any number of times with the same answer.
/// </item>
/// <item>
/// Every <c>Read</c> method consumes exactly one token -- or, for containers, exactly one start or end
/// token -- and leaves the decoder positioned on the next one. A method that fails to produce a value
/// may leave the decoder unusable, but must fail by throwing <see cref="DecoderException"/>.
/// </item>
/// </list>
/// <para>
/// Implementations are conventionally <see langword="ref" /> structs and are always passed by
/// <see langword="ref" />, so that the position advanced by a nested read is seen by the caller.
/// </para>
/// <para>
/// A decoder reads attacker-controlled bytes. Every length or count it reads from the input must be
/// validated against the input actually available before it is used to slice, allocate, or loop.
/// </para>
/// </remarks>
public interface IDecoder
{
	/// <summary>
	/// Gets the type of the next token without consuming it.
	/// </summary>
	/// <value>
	/// The token that the next <c>Read</c> call would consume, or <see cref="TokenType.EndDocument"/>
	/// once the input has been fully consumed.
	/// </value>
	/// <exception cref="DecoderException">Thrown when the next bytes are not a recognizable token.</exception>
	public TokenType NextTokenType { get; }

	/// <summary>
	/// Consumes the next token if -- and only if -- it is a null.
	/// </summary>
	/// <returns>
	/// <see langword="true" /> when the next token was <see cref="TokenType.Null"/> and has now been
	/// consumed; <see langword="false" /> when it was anything else, in which case nothing was consumed
	/// and the decoder is left exactly where it was.
	/// </returns>
	/// <exception cref="DecoderException">Thrown when the next bytes are not a recognizable token.</exception>
	/// <remarks>
	/// <para>
	/// These are the conventional <c>Try</c> semantics: this is <see cref="ReadNull"/> without the throw.
	/// A <see langword="true" /> answer means the null is gone, so the caller must not follow it with
	/// <see cref="ReadNull"/>.
	/// </para>
	/// <para>
	/// A converter that needs to know whether a null is coming <em>without</em> consuming it -- because
	/// it intends to hand the token to another converter -- asks <see cref="NextTokenType"/> instead,
	/// which is the peek.
	/// </para>
	/// </remarks>
	public bool TryReadNull();

	/// <summary>
	/// Consumes the beginning of a map.
	/// </summary>
	/// <returns>
	/// The number of entries the map declares, or <see langword="null" /> when the format does not
	/// declare one. A count, once reported, must be correct.
	/// </returns>
	/// <exception cref="DecoderException">Thrown when the next token is not <see cref="TokenType.StartMap"/>.</exception>
	public int? ReadStartMap();

	/// <summary>
	/// Consumes the end of the innermost open map.
	/// </summary>
	/// <exception cref="DecoderException">Thrown when the map is not positioned at its end.</exception>
	/// <remarks>
	/// A format whose maps are length-prefixed has no end token on the wire and synthesizes one, so
	/// callers may always read the end token they saw reported by <see cref="NextTokenType"/>.
	/// </remarks>
	public void ReadEndMap();

	/// <summary>
	/// Consumes the beginning of a vector.
	/// </summary>
	/// <returns>
	/// The number of elements the vector declares, or <see langword="null" /> when the format does not
	/// declare one. A count, once reported, must be correct.
	/// </returns>
	/// <exception cref="DecoderException">Thrown when the next token is not <see cref="TokenType.StartVector"/>.</exception>
	public int? ReadStartVector();

	/// <summary>
	/// Consumes the end of the innermost open vector.
	/// </summary>
	/// <exception cref="DecoderException">Thrown when the vector is not positioned at its end.</exception>
	public void ReadEndVector();

	/// <summary>
	/// Consumes the name of the map entry whose value comes next.
	/// </summary>
	/// <returns>The property name. The span is valid only until the next read.</returns>
	/// <exception cref="DecoderException">Thrown when the decoder is not positioned at a map key.</exception>
	public ReadOnlySpan<char> ReadPropertyName();

	/// <summary>
	/// Consumes the next value in its entirety, however deeply nested, without converting it.
	/// </summary>
	/// <exception cref="DecoderException">Thrown when there is no value to skip, or the value is malformed.</exception>
	/// <remarks>
	/// Unknown-property retention, positional contracts, and <see cref="ShapeShiftPath"/> traversal all
	/// build on this, so it is worth implementing in terms of declared widths rather than by decoding
	/// each value. A skip walks attacker-controlled structure, so bound its nesting.
	/// </remarks>
	public void Skip();

	/// <summary>
	/// Consumes a null.
	/// </summary>
	/// <exception cref="DecoderException">Thrown when the next token is not <see cref="TokenType.Null"/>.</exception>
	/// <remarks>
	/// The default implementation defers to <see cref="TryReadNull"/>, which consumes the token, and
	/// throws when it reports <see langword="false" />. A decoder overrides it only to produce a more
	/// precise error message.
	/// </remarks>
	public void ReadNull()
	{
		if (!this.TryReadNull())
		{
			throw new DecoderException($"Expected a null token but instead got {this.NextTokenType}.");
		}
	}

	/// <summary>
	/// Consumes a Boolean.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not a Boolean.</exception>
	public bool ReadBoolean();

	/// <summary>
	/// Consumes a signed 64-bit integer.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not an integer this width can hold.</exception>
	public long ReadInt64();

	/// <summary>
	/// Consumes an unsigned 64-bit integer.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not an integer this width can hold.</exception>
	public ulong ReadUInt64();

	/// <summary>
	/// Consumes a signed 128-bit integer.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not an integer this width can hold.</exception>
	public Int128 ReadInt128();

	/// <summary>
	/// Consumes an unsigned 128-bit integer.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not an integer this width can hold.</exception>
	public UInt128 ReadUInt128();

	/// <summary>
	/// Consumes a half-precision floating-point number.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not a number.</exception>
	public Half ReadHalf();

	/// <summary>
	/// Consumes a single-precision floating-point number.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not a number.</exception>
	public float ReadSingle();

	/// <summary>
	/// Consumes a double-precision floating-point number.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not a number.</exception>
	public double ReadDouble();

	/// <summary>
	/// Consumes an exact decimal number.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not a number a decimal can hold.</exception>
	public decimal ReadDecimal();

	/// <summary>
	/// Consumes a date and time.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not one this format writes dates as.</exception>
	public DateTime ReadDateTime();

	/// <summary>
	/// Consumes a duration.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not one this format writes durations as.</exception>
	public TimeSpan ReadTimeSpan();

	/// <summary>
	/// Consumes an arbitrarily large integer.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not an integer.</exception>
	public BigInteger ReadBigInteger();

	/// <summary>
	/// Consumes a string.
	/// </summary>
	/// <returns>The value.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not a string.</exception>
	public string ReadString();

	/// <summary>
	/// Consumes text without necessarily allocating a <see cref="string"/>.
	/// </summary>
	/// <returns>The text. The span is valid only until the next read.</returns>
	/// <exception cref="DecoderException">Thrown when the next token is not a string.</exception>
	public ReadOnlySpan<char> ReadCharSpan();

	/// <summary>
	/// Reads a binary value.
	/// </summary>
	/// <returns>The decoded bytes.</returns>
	/// <exception cref="NotSupportedException">Thrown when the format has no binary representation.</exception>
	public byte[] ReadByteArray() => throw new NotSupportedException("This decoder does not support binary values.");

	/// <summary>
	/// Reads a number while preserving the representation available from the format.
	/// </summary>
	/// <returns>The dynamic number.</returns>
	/// <remarks>
	/// This is what <see cref="ShapeShiftValue"/> and unknown-property retention use, so a value that
	/// arrived as a wide or unsigned integer can be written back the way it came. The default
	/// implementation narrows everything to <see cref="decimal"/>; override it to keep the width the
	/// payload actually used.
	/// </remarks>
	public ShapeShiftNumber ReadDynamicNumber() => new ShapeShiftDecimal(this.ReadDecimal());
}
