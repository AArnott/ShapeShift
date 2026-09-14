// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes a value whose serialized representation is deliberately left unspecified.
/// </summary>
/// <remarks>
/// <para>
/// ShapeShift emits this contract rather than guessing at a representation it cannot know.
/// The most common cause is a custom <see cref="ShapeShiftConverter{TEncoder, TDecoder}"/> that
/// does not override <see cref="ShapeShiftConverter{TEncoder, TDecoder}.GetContract"/>.
/// </para>
/// <para>
/// Consumers should treat this as "any value" and surface <see cref="Reason"/> to their users
/// so that the gap can be closed at its source.
/// </para>
/// </remarks>
/// <param name="dataType"><inheritdoc cref="DataContract(Type)" path="/param[@name='dataType']"/></param>
/// <param name="reason">A human-readable explanation of why the representation is unknown.</param>
public sealed class UndocumentedContract(Type dataType, string reason) : DataContract(dataType)
{
	/// <summary>
	/// Gets a human-readable explanation of why the representation is unknown.
	/// </summary>
	public string Reason { get; } = Requires.NotNull(reason, nameof(reason));

	/// <summary>
	/// Gets the converter responsible for the representation, when one is known.
	/// </summary>
	public Type? ConverterType { get; init; }

	/// <inheritdoc/>
	public override DataContractKind Kind => DataContractKind.Undocumented;

	/// <inheritdoc/>
	public override string ToString() => $"{this.Kind}: {this.DataType.Name} ({this.Reason})";
}
