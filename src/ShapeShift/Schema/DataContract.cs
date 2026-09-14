// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Describes the serialized form of a .NET type in a format-neutral way.
/// </summary>
/// <remarks>
/// <para>
/// A contract describes the <em>shape</em> of the data as ShapeShift writes and reads it
/// (maps, vectors, scalars and their relationships) without committing to any particular
/// encoding. Format-specific projections such as JSON Schema are built on top of this model.
/// </para>
/// <para>
/// Contracts form an object graph that may contain cycles when the described types are recursive.
/// For this reason contracts use reference equality and must not be compared structurally.
/// Use <see cref="ReferencedContracts"/> to walk the graph, guarding against revisiting a node.
/// </para>
/// <para>
/// Contract instances are immutable and safe for concurrent use once they have been returned
/// to a caller.
/// </para>
/// </remarks>
public abstract class DataContract
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DataContract"/> class.
	/// </summary>
	/// <param name="dataType">The .NET type that this contract describes.</param>
	private protected DataContract(Type dataType)
	{
		Requires.NotNull(dataType);
		this.DataType = dataType;
	}

	/// <summary>
	/// Gets the .NET type that this contract describes.
	/// </summary>
	public Type DataType { get; }

	/// <summary>
	/// Gets the kind of this contract, which identifies the concrete subclass.
	/// </summary>
	public abstract DataContractKind Kind { get; }

	/// <summary>
	/// Gets the contracts that this contract directly refers to.
	/// </summary>
	/// <remarks>
	/// The sequence may contain the same contract more than once, and (for recursive types)
	/// may transitively include this contract itself.
	/// </remarks>
	public virtual IEnumerable<DataContract> ReferencedContracts => [];

	/// <inheritdoc/>
	public override string ToString() => $"{this.Kind}: {this.DataType.Name}";

	/// <summary>
	/// Throws an exception indicating that a contract was observed before its construction completed.
	/// </summary>
	/// <returns>Never returns; always throws.</returns>
	private protected static Exception ThrowIncomplete()
		=> new InvalidOperationException("This contract is still under construction. Contracts must not be inspected until they are returned to the caller.");
}
