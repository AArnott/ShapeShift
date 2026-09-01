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
	/// Gets the name of the CLR property or field that this entry was derived from.
	/// </summary>
	/// <value>
	/// The CLR member name, or <see langword="null" /> when this entry does not come from a member that
	/// a caller could name in C# (for example a constructor parameter that has no matching property).
	/// </value>
	/// <remarks>
	/// <para>
	/// This is the only name on this type that is never influenced by a <c>PropertyShapeAttribute.Name</c>
	/// alias or by <see cref="ShapeShiftNamingPolicy"/>. It exists so that a CLR member reached through an
	/// expression such as <c>person =&gt; person.Address.City</c> can be matched to the entry that describes
	/// it, whose <see cref="Name"/> then supplies the name that actually appears in the payload.
	/// </para>
	/// <para>
	/// Use <see cref="DeclaredName"/> instead when you want the name the shape declares, alias included.
	/// </para>
	/// </remarks>
	public string? MemberName { get; init; }

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

	/// <summary>
	/// Gets the 0-based position this property occupies when its declaring type uses
	/// <see cref="ObjectEncoding.Positional"/>.
	/// </summary>
	/// <value>The default value is <see langword="null" />, meaning the property is identified by <see cref="Name"/> alone.</value>
	/// <remarks>
	/// A position is a permanent part of the contract: it may be retired but never reused for a different member,
	/// and members may only be appended after the highest position already in use.
	/// </remarks>
	public int? Position { get; init; }

	/// <summary>
	/// Gets the name of the CLR property or field that a property shape represents, ignoring any
	/// <c>PropertyShapeAttribute.Name</c> alias.
	/// </summary>
	/// <param name="property">The property shape to inspect.</param>
	/// <returns>
	/// The CLR member name, or <see langword="null" /> when the shape does not expose the underlying member
	/// (as is the case for tuple elements synthesized by some shape providers).
	/// </returns>
	/// <remarks>
	/// Format packages that build their own <see cref="ObjectContract"/> should populate
	/// <see cref="MemberName"/> from this method so that expression-based paths work with their contracts.
	/// The underlying member is only consulted when an alias is present, because without one the shape's
	/// own name is already the CLR member name.
	/// </remarks>
	public static string? GetMemberName(IPropertyShape property)
	{
		Requires.NotNull(property);
		return property.AttributeProvider.GetCustomAttribute<PropertyShapeAttribute>() is { Name: not null }
			? property.MemberInfo?.Name
			: property.Name;
	}

	/// <inheritdoc/>
	public override string ToString() => $"{this.Name}: {this.Type}";
}
