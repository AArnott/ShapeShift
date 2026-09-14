// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// The exception a decoder throws when its input is malformed.
/// </summary>
/// <remarks>
/// <para>
/// This is how a format reports "these bytes are not valid", as distinct from
/// <see cref="ShapeShiftSerializationException"/>, which reports that a structurally valid document
/// does not match the shape being deserialized and carries the <see cref="ShapeShiftPath"/> to the
/// value that failed.
/// </para>
/// <para>
/// A decoder reads attacker-controlled bytes, so every rejection must arrive here. An
/// <see cref="IndexOutOfRangeException"/>, <see cref="ArgumentOutOfRangeException"/>,
/// <see cref="NullReferenceException"/>, or an unbounded allocation escaping a decoder means a
/// missing length or bounds check rather than a merely inconvenient exception type.
/// </para>
/// </remarks>
public class DecoderException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DecoderException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the malformed input.</param>
	public DecoderException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DecoderException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the malformed input.</param>
	/// <param name="innerException">The exception that revealed the input was malformed.</param>
	public DecoderException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}
}
