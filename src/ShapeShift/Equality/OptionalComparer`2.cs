// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares optional values (such as <see cref="Nullable{T}"/> or F# options) by comparing
/// their presence and, when present, their wrapped element.
/// </summary>
/// <typeparam name="TOptional">The optional wrapper type.</typeparam>
/// <typeparam name="TElement">The wrapped element type.</typeparam>
/// <param name="deconstructor">Extracts the element from the wrapper.</param>
/// <param name="elementComparer">The structural comparer for the wrapped element.</param>
internal sealed class OptionalComparer<TOptional, TElement>(
	OptionDeconstructor<TOptional, TElement> deconstructor,
	StructuralComparer<TElement> elementComparer) : StructuralComparer<TOptional>
{
	private const int NoneHash = 0x4E4F4E45;
	private const int SomeSeed = 0x534F4D45;

	/// <inheritdoc/>
	internal override bool EqualsCore(TOptional x, TOptional y, ref ComparisonState state)
	{
		bool xHasValue = deconstructor(x, out TElement? xElement);
		bool yHasValue = deconstructor(y, out TElement? yElement);
		if (xHasValue != yHasValue)
		{
			return false;
		}

		return !xHasValue || elementComparer.EqualsWithNullHandling(xElement, yElement, ref state);
	}

	/// <inheritdoc/>
	internal override int GetHashCodeCore(TOptional value, ref HashState state)
		=> deconstructor(value, out TElement? element)
			? HashCombiner.Finalize(HashCombiner.Combine(SomeSeed, elementComparer.GetHashCodeWithNullHandling(element, ref state)))
			: NoneHash;
}
