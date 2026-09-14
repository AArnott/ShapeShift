// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// The exception thrown by <see cref="ConformanceTestCase.Run"/> when the case does not apply to the format.
/// </summary>
/// <remarks>
/// Consumers that want a case to be reported as skipped rather than failed either filter on
/// <see cref="ConformanceTestCase.IsSkipped"/> before running, or catch this exception and translate it
/// into their test framework's skip mechanism.
/// </remarks>
public class ConformanceSkippedException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConformanceSkippedException"/> class.
	/// </summary>
	public ConformanceSkippedException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConformanceSkippedException"/> class.
	/// </summary>
	/// <param name="message">The reason the case does not apply.</param>
	public ConformanceSkippedException(string? message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConformanceSkippedException"/> class.
	/// </summary>
	/// <param name="message">The reason the case does not apply.</param>
	/// <param name="innerException">The exception that led to the skip.</param>
	public ConformanceSkippedException(string? message, Exception? innerException)
		: base(message, innerException)
	{
	}
}
