// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes a type that associates keys with values.
/// </summary>
public sealed class MapContract : DataContract
{
	private DataContract? keyType;
	private DataContract? valueType;

	/// <summary>
	/// Initializes a new instance of the <see cref="MapContract"/> class.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	/// <param name="keyType">The contract describing each key.</param>
	/// <param name="valueType">The contract describing each value.</param>
	/// <param name="encoding">The encoding used for the map.</param>
	public MapContract(Type dataType, DataContract keyType, DataContract valueType, MapEncoding encoding)
		: base(dataType)
	{
		this.keyType = Requires.NotNull(keyType, nameof(keyType));
		this.valueType = Requires.NotNull(valueType, nameof(valueType));
		this.Encoding = encoding;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MapContract"/> class that must be completed
	/// with a call to <see cref="Complete"/> before it is published.
	/// </summary>
	/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
	/// <param name="encoding"><inheritdoc cref="Encoding" path="/summary"/></param>
	internal MapContract(Type dataType, MapEncoding encoding)
		: base(dataType)
	{
		this.Encoding = encoding;
	}

	/// <summary>
	/// Gets the contract describing each key.
	/// </summary>
	public DataContract KeyType => this.keyType ?? throw ThrowIncomplete();

	/// <summary>
	/// Gets the contract describing each value.
	/// </summary>
	public DataContract ValueType => this.valueType ?? throw ThrowIncomplete();

	/// <summary>
	/// Gets the encoding used for the map.
	/// </summary>
	public MapEncoding Encoding { get; }

	/// <inheritdoc/>
	public override DataContractKind Kind => DataContractKind.Map;

	/// <inheritdoc/>
	public override IEnumerable<DataContract> ReferencedContracts => [this.KeyType, this.ValueType];

	/// <summary>
	/// Completes construction of a contract created with the recursion-safe constructor.
	/// </summary>
	/// <param name="keyType"><inheritdoc cref="KeyType" path="/summary"/></param>
	/// <param name="valueType"><inheritdoc cref="ValueType" path="/summary"/></param>
	internal void Complete(DataContract keyType, DataContract valueType)
	{
		Assumes.True(this.keyType is null);
		this.keyType = keyType;
		this.valueType = valueType;
	}
}
