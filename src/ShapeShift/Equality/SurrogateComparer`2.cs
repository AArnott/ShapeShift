// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares values through their PolyType surrogate.
/// </summary>
/// <typeparam name="T">The represented type.</typeparam>
/// <typeparam name="TSurrogate">The surrogate type.</typeparam>
/// <param name="marshaler">Converts between the represented type and its surrogate.</param>
/// <param name="surrogateComparer">The structural comparer for the surrogate type.</param>
/// <remarks>
/// State that the surrogate does not carry has no effect on equality or hash codes.
/// Cycle detection is performed on the original values, because marshaling typically
/// produces a fresh surrogate instance on every call.
/// </remarks>
internal sealed class SurrogateComparer<T, TSurrogate>(
	IMarshaler<T, TSurrogate> marshaler,
	StructuralComparer<TSurrogate> surrogateComparer) : StructuralComparer<T>
{
	/// <inheritdoc/>
	internal override bool EqualsCore(T x, T y, ref ComparisonState state)
	{
		if (IsReferenceType)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}

			if (state.EnterOrAssumeEqual(x!, y!))
			{
				state.Exit();
				return true;
			}
		}

		bool result = surrogateComparer.EqualsWithNullHandling(marshaler.Marshal(x), marshaler.Marshal(y), ref state);

		if (IsReferenceType)
		{
			state.Exit();
		}

		return result;
	}

	/// <inheritdoc/>
	internal override int GetHashCodeCore(T value, ref HashState state)
	{
		if (IsReferenceType && !state.TryEnter(value!, out int memoized))
		{
			return memoized;
		}

		int hash = surrogateComparer.GetHashCodeWithNullHandling(marshaler.Marshal(value), ref state);
		if (IsReferenceType)
		{
			state.Exit(value!, hash);
		}

		return hash;
	}
}
