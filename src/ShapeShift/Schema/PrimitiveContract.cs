// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes a scalar value that ShapeShift encodes natively.
/// </summary>
/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
/// <param name="primitiveType">The primitive data type that describes the encoding.</param>
public sealed class PrimitiveContract(Type dataType, PrimitiveDataType primitiveType) : DataContract(dataType)
{
	/// <summary>
	/// Gets the primitive data type that describes the encoding.
	/// </summary>
	public PrimitiveDataType PrimitiveType { get; } = primitiveType;

	/// <inheritdoc/>
	public override DataContractKind Kind => DataContractKind.Primitive;

	/// <inheritdoc/>
	public override string ToString() => $"{this.Kind}: {this.PrimitiveType}";
}
