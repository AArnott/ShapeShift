// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes one case of a <see cref="UnionContract"/>.
/// </summary>
/// <param name="name">The name of the union case.</param>
/// <param name="tag">The integer tag assigned to the union case.</param>
/// <param name="type">The contract for the value carried by this case.</param>
public sealed class UnionCaseContract(string name, int tag, DataContract type)
{
	/// <summary>
	/// Gets the name of the union case.
	/// </summary>
	public string Name { get; } = Requires.NotNull(name, nameof(name));

	/// <summary>
	/// Gets the integer tag assigned to the union case.
	/// </summary>
	public int Tag { get; } = tag;

	/// <summary>
	/// Gets the contract for the value carried by this case.
	/// </summary>
	public DataContract Type { get; } = Requires.NotNull(type, nameof(type));

	/// <summary>
	/// Gets a value indicating whether <see cref="Tag"/> was explicitly assigned and is therefore
	/// used as the discriminator when writing.
	/// </summary>
	/// <remarks>
	/// When <see langword="false" />, <see cref="Name"/> is written as the discriminator instead.
	/// Deserialization accepts either form.
	/// </remarks>
	public bool IsTagSpecified { get; init; }

	/// <inheritdoc/>
	public override string ToString() => $"{this.Name} ({this.Tag}): {this.Type}";
}
