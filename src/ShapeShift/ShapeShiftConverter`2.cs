// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Schema;

namespace ShapeShift;

public abstract class ShapeShiftConverter<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <summary>
	/// Gets the data type that this converter can serialize and deserialize.
	/// </summary>
	internal abstract Type DataType { get; }

	public abstract void WriteObject(ref TEncoder encoder, object? value, SerializationContext<TEncoder, TDecoder> context);

	public abstract object? ReadObject(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context);

	/// <summary>
	/// Describes the serialized form that this converter produces and accepts.
	/// </summary>
	/// <param name="context">Services for describing other types that this converter composes with.</param>
	/// <returns>
	/// The contract for the data this converter reads and writes,
	/// or <see langword="null" /> if this converter does not describe itself.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The default implementation returns <see langword="null" />, which causes ShapeShift to describe
	/// the type with an <see cref="UndocumentedContract"/> rather than guess at a representation.
	/// Override this method so that schema consumers can understand your converter's output.
	/// </para>
	/// <para>
	/// The returned contract must describe exactly what <see cref="WriteObject"/> emits.
	/// </para>
	/// </remarks>
	public virtual DataContract? GetContract(ContractContext<TEncoder, TDecoder> context) => null;
}
