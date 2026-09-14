// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares a strongly typed member of a declaring type using the member type's structural comparer.
/// </summary>
/// <typeparam name="TDeclaringType">The type that declares the member.</typeparam>
/// <typeparam name="TPropertyType">The member's type.</typeparam>
/// <param name="getter">The member's getter.</param>
/// <param name="comparer">The structural comparer for the member's type.</param>
internal sealed class MemberComparer<TDeclaringType, TPropertyType>(
	Getter<TDeclaringType, TPropertyType> getter,
	StructuralComparer<TPropertyType> comparer) : MemberComparer<TDeclaringType>
{
	/// <inheritdoc/>
	internal override bool MembersEqual(in TDeclaringType x, in TDeclaringType y, ref ComparisonState state)
		=> comparer.EqualsWithNullHandling(
			getter(ref Unsafe.AsRef(in x)),
			getter(ref Unsafe.AsRef(in y)),
			ref state);

	/// <inheritdoc/>
	internal override int HashMember(in TDeclaringType value, ref HashState state)
		=> comparer.GetHashCodeWithNullHandling(getter(ref Unsafe.AsRef(in value)), ref state);
}
