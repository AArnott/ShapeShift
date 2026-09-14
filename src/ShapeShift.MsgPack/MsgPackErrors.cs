// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.MsgPack;

/// <summary>
/// Helpers for reporting failures from MessagePack-specific converters.
/// </summary>
internal static class MsgPackErrors
{
	/// <summary>
	/// Determines whether an exception should have a <see cref="ShapeShiftPath"/> breadcrumb attached to it.
	/// </summary>
	/// <param name="exception">The exception that is propagating.</param>
	/// <returns>
	/// <see langword="true" /> unless the exception represents cooperative cancellation or a condition from which
	/// the process cannot meaningfully continue.
	/// </returns>
	internal static bool IsAugmentable(Exception exception)
		=> exception is not (OperationCanceledException or OutOfMemoryException or StackOverflowException);
}
