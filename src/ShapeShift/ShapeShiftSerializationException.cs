// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public class ShapeShiftSerializationException : Exception
{
	public ShapeShiftSerializationException(string message)
		: base(message)
	{
	}

	public ShapeShiftSerializationException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}
}
