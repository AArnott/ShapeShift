// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Tracks the pairs of objects that are already under comparison so that
/// cyclic and shared object graphs can be compared in finite time.
/// </summary>
/// <remarks>
/// <para>
/// This implements the standard co-inductive (bisimulation) algorithm:
/// a pair of nodes that is already being compared is <em>assumed</em> to be equal.
/// If every other comparison succeeds, the assumption set is a valid bisimulation
/// and the two graphs are structurally equivalent. If any comparison fails, the
/// entire comparison fails and the assumption set is discarded, so an incorrect
/// assumption can never be observed.
/// </para>
/// <para>
/// Because a failed comparison aborts the whole operation, retaining pairs after
/// they have been fully compared is sound, and doubles as memoization that keeps
/// heavily shared (DAG shaped) graphs from being compared exponentially often.
/// </para>
/// <para>
/// The tracking set is allocated lazily once recursion exceeds a fixed number of
/// frames. Cycles necessarily exceed any fixed
/// depth, so termination is preserved while shallow graphs (the common case) avoid
/// all allocations.
/// </para>
/// </remarks>
internal struct ComparisonState
{
	/// <summary>
	/// The recursion depth beyond which reference pairs begin to be recorded.
	/// </summary>
	private const int TrackingDepthThreshold = 16;

	private HashSet<ReferencePair>? assumedEqual;
	private int depth;

	/// <summary>
	/// Enters a pair of reference typed nodes, recording the pair as assumed equal.
	/// </summary>
	/// <param name="x">The first node. Must not be <see langword="null"/>.</param>
	/// <param name="y">The second node. Must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> if the pair is already under comparison and may be assumed equal;
	/// <see langword="false"/> if the caller should compare the pair's contents.
	/// </returns>
	/// <remarks>
	/// The caller must invoke <see cref="Exit"/> exactly once for each call to this method,
	/// regardless of the value returned.
	/// </remarks>
	internal bool EnterOrAssumeEqual(object x, object y)
	{
		if (++this.depth <= TrackingDepthThreshold)
		{
			return false;
		}

		this.assumedEqual ??= new HashSet<ReferencePair>();
		return !this.assumedEqual.Add(new ReferencePair(x, y));
	}

	/// <summary>
	/// Leaves a node pair previously entered via <see cref="EnterOrAssumeEqual"/>.
	/// </summary>
	internal void Exit() => this.depth--;

	/// <summary>
	/// An ordered pair of object references compared by reference identity.
	/// </summary>
	/// <param name="X">The first object.</param>
	/// <param name="Y">The second object.</param>
	private readonly record struct ReferencePair(object X, object Y)
	{
		/// <summary>
		/// Determines whether this pair references the same two objects as another pair.
		/// </summary>
		/// <param name="other">The other pair.</param>
		/// <returns><see langword="true"/> if both elements are reference equal.</returns>
		public bool Equals(ReferencePair other) => ReferenceEquals(this.X, other.X) && ReferenceEquals(this.Y, other.Y);

		/// <summary>
		/// Gets a hash code derived from the identity of both objects.
		/// </summary>
		/// <returns>The hash code.</returns>
		public override int GetHashCode()
			=> unchecked((RuntimeHelpers.GetHashCode(this.X) * 397) ^ RuntimeHelpers.GetHashCode(this.Y));
	}
}
