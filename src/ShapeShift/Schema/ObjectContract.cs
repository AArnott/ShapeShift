// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes a type that is serialized as a map of named properties.
/// </summary>
/// <remarks>
/// Properties that appear in a payload but are not described by <see cref="Properties"/> are
/// silently ignored by the deserializer unless <see cref="HasExtensionData"/> is <see langword="true" />,
/// in which case they are preserved.
/// </remarks>
public sealed class ObjectContract : DataContract
{
	private ImmutableArray<PropertyContract> properties;
	private bool hasExtensionData;

	/// <summary>
	/// Initializes a new instance of the <see cref="ObjectContract"/> class.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	/// <param name="properties">The properties of the object.</param>
	public ObjectContract(Type dataType, IEnumerable<PropertyContract> properties)
		: base(dataType)
	{
		Requires.NotNull(properties);
		this.properties = [.. properties];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ObjectContract"/> class that must be completed
	/// with a call to <see cref="Complete"/> before it is published.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	internal ObjectContract(Type dataType)
		: base(dataType)
	{
	}

	/// <summary>
	/// Gets the properties of the object, in the order they are written.
	/// </summary>
	public ImmutableArray<PropertyContract> Properties => this.properties.IsDefault ? throw ThrowIncomplete() : this.properties;

	/// <summary>
	/// Gets a value indicating whether unrecognized properties are captured in an extension data member.
	/// </summary>
	public bool HasExtensionData
	{
		get => this.hasExtensionData;
		init => this.hasExtensionData = value;
	}

	/// <inheritdoc/>
	public override DataContractKind Kind => DataContractKind.Object;

	/// <inheritdoc/>
	public override IEnumerable<DataContract> ReferencedContracts => this.Properties.Select(p => p.Type);

	/// <summary>
	/// Completes construction of a contract created with the recursion-safe constructor.
	/// </summary>
	/// <param name="properties">The properties of the object.</param>
	/// <param name="hasExtensionData"><inheritdoc cref="HasExtensionData" path="/summary"/></param>
	internal void Complete(ImmutableArray<PropertyContract> properties, bool hasExtensionData)
	{
		Assumes.True(this.properties.IsDefault);
		this.properties = properties;
		this.hasExtensionData = hasExtensionData;
	}
}
