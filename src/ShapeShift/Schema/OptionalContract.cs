// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes a type whose value may be absent, such as <see cref="Nullable{T}"/>.
/// </summary>
/// <remarks>
/// An absent value is encoded as <see langword="null" />.
/// </remarks>
public sealed class OptionalContract : DataContract
{
	private DataContract? elementType;

	/// <summary>
	/// Initializes a new instance of the <see cref="OptionalContract"/> class.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	/// <param name="elementType">The contract describing the value when it is present.</param>
	public OptionalContract(Type dataType, DataContract elementType)
		: base(dataType)
	{
		this.elementType = Requires.NotNull(elementType, nameof(elementType));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OptionalContract"/> class that must be completed
	/// with a call to <see cref="Complete"/> before it is published.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	internal OptionalContract(Type dataType)
		: base(dataType)
	{
	}

	/// <summary>
	/// Gets the contract describing the value when it is present.
	/// </summary>
	public DataContract ElementType => this.elementType ?? throw ThrowIncomplete();

	/// <inheritdoc/>
	public override DataContractKind Kind => DataContractKind.Optional;

	/// <inheritdoc/>
	public override IEnumerable<DataContract> ReferencedContracts => [this.ElementType];

	/// <summary>
	/// Completes construction of a contract created with the recursion-safe constructor.
	/// </summary>
	/// <param name="elementType"><inheritdoc cref="ElementType" path="/summary"/></param>
	internal void Complete(DataContract elementType)
	{
		Assumes.True(this.elementType is null);
		this.elementType = elementType;
	}
}
