// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // The conformance models are small and belong together.

namespace ShapeShift.Conformance;

/// <summary>
/// A small object with mixed member types, used by the converter and limit suites.
/// </summary>
/// <param name="Name">A string member.</param>
/// <param name="Age">An integral member.</param>
/// <param name="Scores">A vector member.</param>
[GenerateShape]
public partial record ConformancePerson(string Name, int Age, List<int> Scores)
{
	/// <inheritdoc/>
	public virtual bool Equals(ConformancePerson? other)
		=> other is not null
			&& this.Name == other.Name
			&& this.Age == other.Age
			&& this.Scores.SequenceEqual(other.Scores);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode hash = default;
		hash.Add(this.Name);
		hash.Add(this.Age);
		foreach (int score in this.Scores)
		{
			hash.Add(score);
		}

		return hash.ToHashCode();
	}
}

/// <summary>
/// A self-referencing node, used to build documents of a controlled depth.
/// </summary>
[GenerateShape]
public partial record ConformanceNode
{
	/// <summary>
	/// Gets or sets the nested node, or <see langword="null" /> at the deepest level.
	/// </summary>
	public ConformanceNode? Child { get; set; }
}

/// <summary>
/// A node that can be referenced more than once, used by the reference-preservation cases.
/// </summary>
[GenerateShape]
public partial class ConformanceSharedNode
{
	/// <summary>
	/// Gets or sets the node's label.
	/// </summary>
	public string? Label { get; set; }
}

/// <summary>
/// A pair of members that may point at the same instance.
/// </summary>
[GenerateShape]
public partial class ConformanceSharedPair
{
	/// <summary>
	/// Gets or sets the first member.
	/// </summary>
	public ConformanceSharedNode? First { get; set; }

	/// <summary>
	/// Gets or sets the second member.
	/// </summary>
	public ConformanceSharedNode? Second { get; set; }
}

/// <summary>
/// An object with defaulted members, used by the default-value policy cases.
/// </summary>
[GenerateShape]
public partial record ConformanceDefaults
{
	/// <summary>
	/// Gets or sets a member whose default is a non-null string.
	/// </summary>
	public string? Text { get; set; }

	/// <summary>
	/// Gets or sets a member whose default is zero.
	/// </summary>
	public int Number { get; set; }
}

/// <summary>
/// The shape witness for the standalone types the conformance suites serialize.
/// </summary>
[GenerateShapeFor<bool>]
[GenerateShapeFor<int>]
[GenerateShapeFor<long>]
[GenerateShapeFor<double>]
[GenerateShapeFor<string>]
[GenerateShapeFor<byte[]>]
[GenerateShapeFor<int[]>]
[GenerateShapeFor<List<int>>]
[GenerateShapeFor<List<string>>]
[GenerateShapeFor<Dictionary<string, int>>]
[GenerateShapeFor<ShapeShiftValue>]
public partial class ConformanceWitness;
