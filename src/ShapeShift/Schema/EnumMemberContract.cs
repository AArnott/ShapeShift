// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes one named member of an <see cref="EnumContract"/>.
/// </summary>
/// <param name="name">The name of the enum member.</param>
/// <param name="value">The underlying numeric value of the enum member.</param>
public sealed class EnumMemberContract(string name, ShapeShiftValue value)
{
	/// <summary>
	/// Gets the name of the enum member.
	/// </summary>
	public string Name { get; } = Requires.NotNull(name, nameof(name));

	/// <summary>
	/// Gets the underlying numeric value of the enum member.
	/// </summary>
	public ShapeShiftValue Value { get; } = Requires.NotNull(value, nameof(value));

	/// <inheritdoc/>
	public override string ToString() => $"{this.Name} = {this.Value}";
}
