// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace ShapeShift;

/// <summary>
/// Identifies a single step within a <see cref="ShapeShiftPath"/>: either the name of a map property,
/// or the 0-based index of an element within a vector.
/// </summary>
public readonly struct ShapeShiftPathElement : IEquatable<ShapeShiftPathElement>
{
	private readonly string? propertyName;
	private readonly int index;

	private ShapeShiftPathElement(string? propertyName, int index)
	{
		this.propertyName = propertyName;
		this.index = index;
	}

	/// <summary>
	/// Gets a value indicating whether this element identifies a map property by name.
	/// </summary>
	/// <value><see langword="true" /> if this element was created by <see cref="Property(string)"/>; <see langword="false" /> if it was created by <see cref="Vector(int)"/>.</value>
	public bool IsPropertyName => this.propertyName is not null;

	/// <summary>
	/// Gets the property name that this element identifies.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when <see cref="IsPropertyName"/> is <see langword="false" />.</exception>
	public string PropertyName => this.propertyName ?? throw new InvalidOperationException("This path element identifies a vector index, not a map property name.");

	/// <summary>
	/// Gets the 0-based index within a vector that this element identifies.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when <see cref="IsPropertyName"/> is <see langword="true" />.</exception>
	public int Index => this.propertyName is null ? this.index : throw new InvalidOperationException("This path element identifies a map property name, not a vector index.");

	/// <inheritdoc cref="Property(string)"/>
	public static implicit operator ShapeShiftPathElement(string propertyName) => Property(propertyName);

	/// <inheritdoc cref="Vector(int)"/>
	public static implicit operator ShapeShiftPathElement(int index) => Vector(index);

	/// <summary>
	/// Checks two path elements for equality.
	/// </summary>
	/// <param name="left">The first element.</param>
	/// <param name="right">The second element.</param>
	/// <returns><see langword="true" /> if the elements are equal.</returns>
	public static bool operator ==(ShapeShiftPathElement left, ShapeShiftPathElement right) => left.Equals(right);

	/// <summary>
	/// Checks two path elements for inequality.
	/// </summary>
	/// <param name="left">The first element.</param>
	/// <param name="right">The second element.</param>
	/// <returns><see langword="true" /> if the elements are not equal.</returns>
	public static bool operator !=(ShapeShiftPathElement left, ShapeShiftPathElement right) => !left.Equals(right);

	/// <summary>
	/// Creates a path element that identifies a value stored at the given <paramref name="propertyName"/> within a map.
	/// </summary>
	/// <param name="propertyName">The name of the property, exactly as it is expected to appear in the serialized data (i.e. after any naming policy has already been applied).</param>
	/// <returns>The path element.</returns>
	public static ShapeShiftPathElement Property(string propertyName)
	{
		Requires.NotNull(propertyName);
		return new(propertyName, 0);
	}

	/// <summary>
	/// Creates a path element that identifies the value at a given 0-based <paramref name="index"/> within a vector.
	/// </summary>
	/// <param name="index">The 0-based index of the element within the vector.</param>
	/// <returns>The path element.</returns>
	public static ShapeShiftPathElement Vector(int index)
	{
		Requires.Argument(index >= 0, nameof(index), "Index must not be negative.");
		return new(null, index);
	}

	/// <inheritdoc/>
	public bool Equals(ShapeShiftPathElement other) => this.index == other.index && string.Equals(this.propertyName, other.propertyName, StringComparison.Ordinal);

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is ShapeShiftPathElement other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() => this.propertyName is not null ? StringComparer.Ordinal.GetHashCode(this.propertyName) : this.index;

	/// <inheritdoc/>
	public override string ToString() => this.propertyName ?? this.index.ToString(CultureInfo.InvariantCulture);
}
