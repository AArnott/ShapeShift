// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// The exception thrown when a conformance expectation is not met.
/// </summary>
/// <remarks>
/// The kit throws this instead of a test framework's assertion type so that the package
/// carries no test framework dependency. Every test framework reports an unexpected exception
/// as a failure, so the message reaches the user either way.
/// </remarks>
public class ConformanceAssertionException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConformanceAssertionException"/> class.
	/// </summary>
	public ConformanceAssertionException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConformanceAssertionException"/> class.
	/// </summary>
	/// <param name="message">A description of the expectation that was not met.</param>
	public ConformanceAssertionException(string? message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConformanceAssertionException"/> class.
	/// </summary>
	/// <param name="message">A description of the expectation that was not met.</param>
	/// <param name="innerException">The exception that revealed the failure.</param>
	public ConformanceAssertionException(string? message, Exception? innerException)
		: base(message, innerException)
	{
	}
}
