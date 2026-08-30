// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares two values already known to belong to the same union case, by marshaling them to
/// that case's type.
/// </summary>
/// <typeparam name="TUnionCase">The union case type.</typeparam>
/// <typeparam name="TUnion">The declared union type.</typeparam>
/// <param name="caseComparer">The structural comparer for the case type.</param>
/// <param name="marshaler">Converts between the union type and the case type.</param>
internal sealed class UnionCaseComparer<TUnionCase, TUnion>(
	StructuralComparer<TUnionCase> caseComparer,
	IMarshaler<TUnionCase, TUnion> marshaler) : StructuralComparer<TUnion>
{
	/// <inheritdoc/>
	internal override bool EqualsCore(TUnion x, TUnion y, ref ComparisonState state)
		=> caseComparer.EqualsWithNullHandling(marshaler.Unmarshal(x), marshaler.Unmarshal(y), ref state);

	/// <inheritdoc/>
	internal override int GetHashCodeCore(TUnion value, ref HashState state)
		=> caseComparer.GetHashCodeWithNullHandling(marshaler.Unmarshal(value), ref state);
}
