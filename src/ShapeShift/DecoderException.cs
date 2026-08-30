// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

public class DecoderException : Exception
{
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
