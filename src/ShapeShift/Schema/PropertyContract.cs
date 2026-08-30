// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes one named member of an <see cref="ObjectContract"/>.
/// </summary>
/// <param name="name">The name of the property as it appears in the serialized payload, after any naming policy has been applied.</param>
/// <param name="type">The contract for the property's value.</param>
public sealed class PropertyContract(string name, DataContract type)
{
	/// <summary>
	/// Gets the name of the property as it appears in the serialized payload.
	/// </summary>
	public string Name { get; } = Requires.NotNull(name, nameof(name));

	/// <summary>
	/// Gets the contract for the property's value.
	/// </summary>
	public DataContract Type { get; } = Requires.NotNull(type, nameof(type));

	/// <summary>
	/// Gets the name declared by the type shape, before the serializer's naming policy was applied.
	/// </summary>
	/// <remarks>
	/// This reflects any <c>PropertyShapeAttribute.Name</c> override, but not
	/// <see cref="ShapeShiftNamingPolicy"/>.
	/// </remarks>
	public string? DeclaredName { get; init; }

	/// <summary>
	/// Gets a value indicating whether the deserializer requires this property to be present in the payload.
	/// </summary>
	public bool IsRequired { get; init; }

	/// <summary>
	/// Gets a value indicating whether <see langword="null" /> is an acceptable value for this property.
	/// </summary>
	/// <value>The default value is <see langword="true" />.</value>
	public bool IsNullable { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether this property is included when serializing.
	/// </summary>
	/// <value>The default value is <see langword="true" />.</value>
	/// <remarks>
	/// This is <see langword="false" /> for members that can only be assigned during deserialization (e.g. write-only members).
	/// </remarks>
	public bool IsReadable { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether this property is accepted when deserializing.
	/// </summary>
	/// <value>The default value is <see langword="true" />.</value>
	/// <remarks>
	/// When <see langword="false" />, the property appears in serialized payloads but the deserializer ignores it.
	/// </remarks>
	public bool IsWritable { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether this property is always written when serializing.
	/// </summary>
	/// <value>The default value is <see langword="true" />.</value>
	/// <remarks>
	/// When <see langword="false" />, <see cref="SerializeDefaultValuesPolicy"/> allows the serializer to
	/// omit this property when its value matches <see cref="DefaultValue"/>.
	/// </remarks>
	public bool IsAlwaysWritten { get; init; } = true;

	/// <summary>
	/// Gets the default value that the deserializer assumes when this property is absent from the payload,
	/// when that value can be expressed in the data model.
	/// </summary>
	public ShapeShiftValue? DefaultValue { get; init; }

	/// <inheritdoc/>
	public override string ToString() => $"{this.Name}: {this.Type}";
}
