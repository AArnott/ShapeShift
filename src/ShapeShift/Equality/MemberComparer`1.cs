// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares a single member (property or field) of a declaring type.
/// </summary>
/// <typeparam name="TDeclaringType">The type that declares the member.</typeparam>
internal abstract class MemberComparer<TDeclaringType>
{
	/// <summary>
	/// Compares the member's value across two instances.
	/// </summary>
	/// <param name="x">The first instance.</param>
	/// <param name="y">The second instance.</param>
	/// <param name="state">The traversal state.</param>
	/// <returns><see langword="true"/> if the member values are structurally equal.</returns>
	internal abstract bool MembersEqual(in TDeclaringType x, in TDeclaringType y, ref ComparisonState state);

	/// <summary>
	/// Computes the structural hash code of the member's value.
	/// </summary>
	/// <param name="value">The instance declaring the member.</param>
	/// <param name="state">The traversal state.</param>
	/// <returns>The hash code of the member's value.</returns>
	internal abstract int HashMember(in TDeclaringType value, ref HashState state);
}
