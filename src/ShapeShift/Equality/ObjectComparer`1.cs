// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Compares objects by comparing each of their readable members.
/// </summary>
/// <typeparam name="T">The object type.</typeparam>
/// <param name="members">The comparers for each readable member, in declaration order.</param>
internal sealed class ObjectComparer<T>(MemberComparer<T>[] members) : StructuralComparer<T>
{
	private const int Seed = 0x4F424A00;

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

		bool result = true;
		foreach (MemberComparer<T> member in members)
		{
			if (!member.MembersEqual(x, y, ref state))
			{
				result = false;
				break;
			}
		}

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

		int hash = Seed;
		foreach (MemberComparer<T> member in members)
		{
			hash = HashCombiner.Combine(hash, member.HashMember(value, ref state));
		}

		hash = HashCombiner.Finalize(hash);
		if (IsReferenceType)
		{
			state.Exit(value!, hash);
		}

		return hash;
	}
}
