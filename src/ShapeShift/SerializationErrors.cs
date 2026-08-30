// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// Helpers that attach actionable <see cref="ShapeShiftPath"/> breadcrumbs to failures
/// that occur while converting a nested value.
/// </summary>
/// <remarks>
/// The helpers here never swallow an exception. They either augment the exception that is already
/// propagating (preserving its stack trace via a <see langword="throw" /> rethrow at the call site)
/// or wrap it in a <see cref="ShapeShiftSerializationException"/> that keeps the original as its
/// <see cref="Exception.InnerException"/>.
/// </remarks>
internal static class SerializationErrors
{
	/// <summary>
	/// Determines whether an exception should have a path breadcrumb attached to it.
	/// </summary>
	/// <param name="exception">The exception that is propagating.</param>
	/// <returns>
	/// <see langword="true" /> unless the exception represents cooperative cancellation or a condition
	/// from which the process cannot meaningfully continue.
	/// </returns>
	internal static bool IsAugmentable(Exception exception)
		=> exception is not (OperationCanceledException or OutOfMemoryException or StackOverflowException);

	/// <summary>
	/// Wraps an exception that is not already a <see cref="ShapeShiftSerializationException"/> so that it
	/// carries a path breadcrumb, and throws the wrapper.
	/// </summary>
	/// <param name="innerException">The original exception, which is preserved as the inner exception.</param>
	/// <param name="element">The step from the enclosing container to the value that failed.</param>
	/// <param name="declaringType">The type whose value was being converted.</param>
	/// <param name="serializing"><see langword="true" /> when writing; <see langword="false" /> when reading.</param>
	/// <returns>This method never returns normally.</returns>
	/// <exception cref="ShapeShiftSerializationException">Always thrown.</exception>
	[DoesNotReturn]
	internal static Exception Wrap(Exception innerException, ShapeShiftPathElement element, Type declaringType, bool serializing)
	{
		ShapeShiftSerializationException wrapper = new(
			$"An error occurred while {(serializing ? "serializing" : "deserializing")} {declaringType.FullName}.",
			innerException);
		wrapper.AddEnclosingPathElement(element);
		throw wrapper;
	}

	/// <summary>
	/// Wraps an exception that is not already a <see cref="ShapeShiftSerializationException"/> so that it carries
	/// a two-step path breadcrumb, and throws the wrapper.
	/// </summary>
	/// <param name="innerException">The original exception, which is preserved as the inner exception.</param>
	/// <param name="outerElement">The step from the enclosing container to the entry that failed.</param>
	/// <param name="innerElement">The step from that entry to the value that failed.</param>
	/// <param name="declaringType">The type whose value was being converted.</param>
	/// <param name="serializing"><see langword="true" /> when writing; <see langword="false" /> when reading.</param>
	/// <returns>This method never returns normally.</returns>
	/// <exception cref="ShapeShiftSerializationException">Always thrown.</exception>
	[DoesNotReturn]
	internal static Exception WrapEntry(Exception innerException, ShapeShiftPathElement outerElement, ShapeShiftPathElement innerElement, Type declaringType, bool serializing)
	{
		ShapeShiftSerializationException wrapper = new(
			$"An error occurred while {(serializing ? "serializing" : "deserializing")} {declaringType.FullName}.",
			innerException);
		wrapper.AddEnclosingPathElement(innerElement);
		wrapper.AddEnclosingPathElement(outerElement);
		throw wrapper;
	}
}
