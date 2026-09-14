// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes a value whose structure is chosen at runtime rather than by its .NET type,
/// such as <see cref="ShapeShiftValue"/>.
/// </summary>
/// <remarks>
/// Any value the format can represent is valid for this contract.
/// </remarks>
/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
public sealed class DynamicContract(Type dataType) : DataContract(dataType)
{
	/// <inheritdoc/>
	public override DataContractKind Kind => DataContractKind.Dynamic;
}
