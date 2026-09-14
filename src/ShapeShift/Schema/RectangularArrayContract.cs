// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes a multidimensional (rectangular) array.
/// </summary>
/// <remarks>
/// Such arrays are encoded as a two-element vector: the first element is a vector of
/// <see cref="Rank"/> lengths (one per dimension) and the second is a vector of all elements
/// in row-major order.
/// </remarks>
public sealed class RectangularArrayContract : DataContract
{
	private DataContract? elementType;

	/// <summary>
	/// Initializes a new instance of the <see cref="RectangularArrayContract"/> class.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	/// <param name="elementType">The contract describing each element.</param>
	/// <param name="rank">The number of dimensions in the array.</param>
	public RectangularArrayContract(Type dataType, DataContract elementType, int rank)
		: base(dataType)
	{
		Requires.Range(rank > 1, nameof(rank));
		this.elementType = Requires.NotNull(elementType, nameof(elementType));
		this.Rank = rank;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RectangularArrayContract"/> class that must be
	/// completed with a call to <see cref="Complete"/> before it is published.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	/// <param name="rank"><inheritdoc cref="Rank" path="/summary"/></param>
	internal RectangularArrayContract(Type dataType, int rank)
		: base(dataType)
	{
		this.Rank = rank;
	}

	/// <summary>
	/// Gets the contract describing each element.
	/// </summary>
	public DataContract ElementType => this.elementType ?? throw ThrowIncomplete();

	/// <summary>
	/// Gets the number of dimensions in the array.
	/// </summary>
	public int Rank { get; }

	/// <inheritdoc/>
	public override DataContractKind Kind => DataContractKind.RectangularArray;

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
