// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes a type that is serialized as a variable-length vector of homogeneous elements.
/// </summary>
public sealed class SequenceContract : DataContract
{
	private DataContract? elementType;
	private bool isSet;

	/// <summary>
	/// Initializes a new instance of the <see cref="SequenceContract"/> class.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	/// <param name="elementType">The contract describing each element.</param>
	public SequenceContract(Type dataType, DataContract elementType)
		: base(dataType)
	{
		this.elementType = Requires.NotNull(elementType, nameof(elementType));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SequenceContract"/> class that must be completed
	/// with a call to <see cref="Complete"/> before it is published.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	internal SequenceContract(Type dataType)
		: base(dataType)
	{
	}

	/// <summary>
	/// Gets the contract describing each element.
	/// </summary>
	public DataContract ElementType => this.elementType ?? throw ThrowIncomplete();

	/// <summary>
	/// Gets a value indicating whether the elements are guaranteed to be distinct.
	/// </summary>
	public bool IsSet
	{
		get => this.isSet;
		init => this.isSet = value;
	}

	/// <inheritdoc/>
	public override DataContractKind Kind => DataContractKind.Sequence;

	/// <inheritdoc/>
	public override IEnumerable<DataContract> ReferencedContracts => [this.ElementType];

	/// <summary>
	/// Completes construction of a contract created with the recursion-safe constructor.
	/// </summary>
	/// <param name="elementType"><inheritdoc cref="ElementType" path="/summary"/></param>
	/// <param name="isSet"><inheritdoc cref="IsSet" path="/summary"/></param>
	internal void Complete(DataContract elementType, bool isSet)
	{
		Assumes.True(this.elementType is null);
		this.elementType = elementType;
		this.isSet = isSet;
	}
}
