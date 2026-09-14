// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes a type with several known subtypes, each identified by a discriminator.
/// </summary>
/// <remarks>
/// A union value is encoded as a two-element vector: the discriminator followed by the value.
/// A <see langword="null" /> discriminator selects <see cref="BaseType"/>.
/// </remarks>
public sealed class UnionContract : DataContract
{
	private DataContract? baseType;
	private ImmutableArray<UnionCaseContract> cases;

	/// <summary>
	/// Initializes a new instance of the <see cref="UnionContract"/> class.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	/// <param name="baseType">The contract used when the discriminator is <see langword="null" />.</param>
	/// <param name="cases">The known cases of the union.</param>
	public UnionContract(Type dataType, DataContract baseType, IEnumerable<UnionCaseContract> cases)
		: base(dataType)
	{
		Requires.NotNull(cases);
		this.baseType = Requires.NotNull(baseType, nameof(baseType));
		this.cases = [.. cases];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UnionContract"/> class that must be completed
	/// with a call to <see cref="Complete"/> before it is published.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	internal UnionContract(Type dataType)
		: base(dataType)
	{
	}

	/// <summary>
	/// Gets the contract used when the discriminator is <see langword="null" />.
	/// </summary>
	public DataContract BaseType => this.baseType ?? throw ThrowIncomplete();

	/// <summary>
	/// Gets the known cases of the union.
	/// </summary>
	public ImmutableArray<UnionCaseContract> Cases => this.cases.IsDefault ? throw ThrowIncomplete() : this.cases;

	/// <inheritdoc/>
	public override DataContractKind Kind => DataContractKind.Union;

	/// <inheritdoc/>
	public override IEnumerable<DataContract> ReferencedContracts => this.Cases.Select(c => c.Type).Prepend(this.BaseType);

	/// <summary>
	/// Completes construction of a contract created with the recursion-safe constructor.
	/// </summary>
	/// <param name="baseType"><inheritdoc cref="BaseType" path="/summary"/></param>
	/// <param name="cases"><inheritdoc cref="Cases" path="/summary"/></param>
	internal void Complete(DataContract baseType, ImmutableArray<UnionCaseContract> cases)
	{
		Assumes.True(this.baseType is null);
		this.baseType = baseType;
		this.cases = cases;
	}
}
