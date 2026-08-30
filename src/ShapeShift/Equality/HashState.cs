// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Tracks the nodes visited while computing a structural hash code so that
/// shared references are only hashed once and cycles are detected.
/// </summary>
/// <remarks>
/// <para>
/// An acyclic object graph unfolds into a finite tree, so hashing it as a tree
/// produces the same hash for two graphs that differ only in how much they share,
/// which matches the value equivalence semantics implemented by
/// <see cref="ComparisonState"/>. Memoization keyed on reference identity keeps
/// that unfolding from becoming exponential.
/// </para>
/// <para>
/// A cyclic graph has no finite unfolding. When a cycle is detected the whole hash
/// computation reports <see cref="CycleDetected"/> and the caller substitutes a fixed
/// sentinel hash code. This cannot violate the equality/hash contract because a cyclic
/// graph is never structurally equal to an acyclic one, and all cyclic graphs share the
/// sentinel.
/// </para>
/// </remarks>
internal struct HashState
{
	/// <summary>
	/// The recursion depth beyond which node identities begin to be recorded.
	/// </summary>
	private const int TrackingDepthThreshold = 16;

	private Dictionary<object, int>? memoized;
	private HashSet<object>? path;
	private int depth;

	/// <summary>
	/// Gets a value indicating whether a reference cycle was observed during the hash computation.
	/// </summary>
	internal bool CycleDetected { get; private set; }

	/// <summary>
	/// Enters a reference typed node whose hash code is about to be computed.
	/// </summary>
	/// <param name="node">The node being hashed. Must not be <see langword="null"/>.</param>
	/// <param name="hash">Receives the previously computed hash code when this method returns <see langword="false"/>.</param>
	/// <returns>
	/// <see langword="true"/> if the caller should compute the node's hash code and then call <see cref="Exit"/>;
	/// <see langword="false"/> if <paramref name="hash"/> already holds the result.
	/// </returns>
	internal bool TryEnter(object node, out int hash)
	{
		if (++this.depth <= TrackingDepthThreshold)
		{
			hash = 0;
			return true;
		}

		this.memoized ??= new Dictionary<object, int>(IdentityComparer.Instance);
		if (this.memoized.TryGetValue(node, out hash))
		{
			this.depth--;
			return false;
		}

		this.path ??= new HashSet<object>(IdentityComparer.Instance);
		if (!this.path.Add(node))
		{
			this.CycleDetected = true;
			this.depth--;
			hash = 0;
			return false;
		}

		return true;
	}

	/// <summary>
	/// Leaves a node previously entered via <see cref="TryEnter"/>, recording its hash code.
	/// </summary>
	/// <param name="node">The node whose hash code was computed.</param>
	/// <param name="hash">The computed hash code.</param>
	internal void Exit(object node, int hash)
	{
		if (this.depth > TrackingDepthThreshold)
		{
			this.path!.Remove(node);
			this.memoized![node] = hash;
		}

		this.depth--;
	}

	/// <summary>
	/// An equality comparer that compares objects by reference identity.
	/// </summary>
	private sealed class IdentityComparer : IEqualityComparer<object>
	{
		/// <summary>
		/// The singleton instance.
		/// </summary>
		internal static readonly IdentityComparer Instance = new();

		private IdentityComparer()
		{
		}

		/// <inheritdoc/>
		public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

		/// <inheritdoc/>
		public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
	}
}
