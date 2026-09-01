// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Provides the services a converter needs in order to describe its serialized form.
/// </summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
/// <remarks>
/// An instance of this type is handed to <see cref="ShapeShiftConverter{TEncoder, TDecoder}.GetContract"/>.
/// </remarks>
public sealed class ContractContext<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly ContractVisitor<TEncoder, TDecoder> visitor;

	/// <summary>
	/// Initializes a new instance of the <see cref="ContractContext{TEncoder, TDecoder}"/> class.
	/// </summary>
	/// <param name="visitor">The visitor that describes types.</param>
	/// <param name="typeShape"><inheritdoc cref="TypeShape" path="/summary"/></param>
	internal ContractContext(ContractVisitor<TEncoder, TDecoder> visitor, ITypeShape? typeShape)
	{
		this.visitor = visitor;
		this.TypeShape = typeShape;
	}

	/// <summary>
	/// Gets the shape of the type being described, when one is available.
	/// </summary>
	public ITypeShape? TypeShape { get; }

	/// <summary>
	/// Describes another type, so that a converter can compose its own contract from the contracts of its parts.
	/// </summary>
	/// <param name="typeShape">The shape of the type to describe.</param>
	/// <returns>The contract for <paramref name="typeShape"/>.</returns>
	/// <remarks>
	/// The returned contract may still be under construction when the described type participates in a
	/// reference cycle with the type being described. Store the reference, but do not inspect its members
	/// until after <see cref="ShapeShiftConverter{TEncoder, TDecoder}.GetContract"/> returns.
	/// </remarks>
	public DataContract GetContract(ITypeShape typeShape)
	{
		Requires.NotNull(typeShape);
		return this.visitor.GetContract(typeShape);
	}
}
