// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes a type that is serialized by first converting it to a surrogate type.
/// </summary>
/// <remarks>
/// The serialized form is exactly that of <see cref="SurrogateType"/>.
/// This contract exists so that consumers can recognize the .NET type that a payload maps onto.
/// </remarks>
public sealed class SurrogateContract : DataContract
{
	private DataContract? surrogateType;

	/// <summary>
	/// Initializes a new instance of the <see cref="SurrogateContract"/> class.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	/// <param name="surrogateType">The contract of the type that is actually serialized.</param>
	public SurrogateContract(Type dataType, DataContract surrogateType)
		: base(dataType)
	{
		this.surrogateType = Requires.NotNull(surrogateType, nameof(surrogateType));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SurrogateContract"/> class that must be completed
	/// with a call to <see cref="Complete"/> before it is published.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	internal SurrogateContract(Type dataType)
		: base(dataType)
	{
	}

	/// <summary>
	/// Gets the contract of the type that is actually serialized.
	/// </summary>
	public DataContract SurrogateType => this.surrogateType ?? throw ThrowIncomplete();

	/// <inheritdoc/>
	public override DataContractKind Kind => DataContractKind.Surrogate;

	/// <inheritdoc/>
	public override IEnumerable<DataContract> ReferencedContracts => [this.SurrogateType];

	/// <summary>
	/// Completes construction of a contract created with the recursion-safe constructor.
	/// </summary>
	/// <param name="surrogateType"><inheritdoc cref="SurrogateType" path="/summary"/></param>
	internal void Complete(DataContract surrogateType)
	{
		Assumes.True(this.surrogateType is null);
		this.surrogateType = surrogateType;
	}
}
