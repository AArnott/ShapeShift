// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ShapeShift;

/// <summary>
/// Context in which a <see cref="IShapeShiftConverterFactory{TEncoder, TDecoder}"/> is invoked.
/// </summary>
/// <typeparam name="TEncoder"><inheritdoc cref="SerializerBase{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="SerializerBase{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
/// <remarks>
/// Provides access to other converters that may be required by the requested converter.
/// </remarks>
public struct ConverterContext<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly ConverterCache<TEncoder, TDecoder> cache;
	private readonly bool preserveReferences;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConverterContext{TEncoder, TDecoder}"/> struct.
	/// </summary>
	/// <param name="converterCache">The converter cache.</param>
	/// <param name="typeShapeProvider">The type shape provider.</param>
	/// <param name="referencePreservationMode">The reference preservation mode.</param>
	internal ConverterContext(ConverterCache<TEncoder, TDecoder> converterCache, ITypeShapeProvider typeShapeProvider, ReferencePreservationMode referencePreservationMode)
	{
		this.cache = converterCache;
		this.TypeShapeProvider = typeShapeProvider;
		this.preserveReferences = referencePreservationMode != ReferencePreservationMode.Off;
	}

	/// <summary>
	/// Gets the <see cref="ITypeShapeProvider"/> that can provide shapes for given types.
	/// </summary>
	public ITypeShapeProvider TypeShapeProvider { get; }

#if NET
	/// <summary>
	/// Gets a converter for some type.
	/// </summary>
	/// <typeparam name="T">The type for which a converter is required.</typeparam>
	/// <returns>The converter.</returns>
	public ShapeShiftConverter<T, TEncoder, TDecoder> GetConverter<T>()
		where T : IShapeable<T>
	{
		Verify.Operation(this.cache is not null, "No serialization operation is in progress.");
		ShapeShiftConverter<TEncoder, TDecoder> result = this.cache.GetOrAddConverter(T.GetTypeShape()).ValueOrThrow;
		return (ShapeShiftConverter<T, TEncoder, TDecoder>)(this.preserveReferences ? ((IShapeShiftConverterInternal<TEncoder, TDecoder>)result).WrapWithReferencePreservation() : result);
	}

	/// <summary>
	/// Gets a converter for some type.
	/// </summary>
	/// <typeparam name="T">The type for which a converter is required.</typeparam>
	/// <typeparam name="TProvider">The provider of the type's shape.</typeparam>
	/// <returns>The converter.</returns>
	public ShapeShiftConverter<T, TEncoder, TDecoder> GetConverter<T, TProvider>()
		where TProvider : IShapeable<T>
	{
		Verify.Operation(this.cache is not null, "No serialization operation is in progress.");
		ShapeShiftConverter<TEncoder, TDecoder> result = this.cache.GetOrAddConverter(TProvider.GetTypeShape()).ValueOrThrow;
		return (ShapeShiftConverter<T, TEncoder, TDecoder>)(this.preserveReferences ? ((IShapeShiftConverterInternal<TEncoder, TDecoder>)result).WrapWithReferencePreservation() : result);
	}
#endif

	/// <summary>
	/// Gets a converter for a specific type.
	/// </summary>
	/// <typeparam name="T">The type to be converted.</typeparam>
	/// <param name="provider">
	/// <inheritdoc cref="SerializerBase{TEncoder, TDecoder}.CreateSerializationContext(ITypeShapeProvider, CancellationToken)" path="/param[@name='provider']"/>
	/// It can also come from <see cref="TypeShapeProvider"/>.
	/// A <see langword="null" /> value will be filled in with <see cref="TypeShapeProvider"/>.
	/// </param>
	/// <returns>The converter.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no serialization operation is in progress.</exception>
	/// <remarks>
	/// This method is intended only for use by custom converters in order to delegate conversion of sub-values.
	/// </remarks>
	[OverloadResolutionPriority(1)] // for null values, prefer this method over the one that takes ITypeShape.
	public ShapeShiftConverter<T, TEncoder, TDecoder> GetConverter<T>(ITypeShapeProvider? provider)
	{
		Verify.Operation(this.cache is not null, "No serialization operation is in progress.");
		ShapeShiftConverter<TEncoder, TDecoder> result = this.cache.GetOrAddConverter<T>(provider ?? this.TypeShapeProvider ?? throw new UnreachableException()).ValueOrThrow;
		return (ShapeShiftConverter<T, TEncoder, TDecoder>)(this.preserveReferences ? ((IShapeShiftConverterInternal<TEncoder, TDecoder>)result).WrapWithReferencePreservation() : result);
	}

	/// <summary>
	/// Gets a converter for a given type shape.
	/// </summary>
	/// <param name="typeShape">The shape of the type to be converted.</param>
	/// <returns>The converter.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no serialization operation is in progress.</exception>
	/// <remarks>
	/// This method is intended only for use by custom converters in order to delegate conversion of sub-values.
	/// </remarks>
	public ShapeShiftConverter<TEncoder, TDecoder> GetConverter(ITypeShape typeShape)
	{
		Requires.NotNull(typeShape);
		Verify.Operation(this.cache is not null, "No serialization operation is in progress.");
		ShapeShiftConverter<TEncoder, TDecoder> result = this.cache.GetOrAddConverter(typeShape).ValueOrThrow;
		return this.preserveReferences ? ((IShapeShiftConverterInternal<TEncoder, TDecoder>)result).WrapWithReferencePreservation() : result;
	}

	/// <summary>
	/// Gets a converter for a given type shape.
	/// </summary>
	/// <typeparam name="T">The type to be converted.</typeparam>
	/// <param name="typeShape">The shape of the type to be converted.</param>
	/// <returns>The converter.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no serialization operation is in progress.</exception>
	/// <remarks>
	/// This method is intended only for use by custom converters in order to delegate conversion of sub-values.
	/// </remarks>
	public ShapeShiftConverter<T, TEncoder, TDecoder> GetConverter<T>(ITypeShape<T> typeShape)
	{
		Requires.NotNull(typeShape);
		Verify.Operation(this.cache is not null, "No serialization operation is in progress.");
		ShapeShiftConverter<TEncoder, TDecoder> result = this.cache.GetOrAddConverter(typeShape).ValueOrThrow;
		return (ShapeShiftConverter<T, TEncoder, TDecoder>)(this.preserveReferences ? ((IShapeShiftConverterInternal<TEncoder, TDecoder>)result).WrapWithReferencePreservation() : result);
	}

	/// <summary>
	/// Gets a converter for a given type shape.
	/// </summary>
	/// <param name="type">The type to be converted.</param>
	/// <param name="provider"><inheritdoc cref="GetConverter{T}(ITypeShapeProvider?)" path="/param[@name='provider']"/></param>
	/// <returns>The converter.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no serialization operation is in progress.</exception>
	/// <remarks>
	/// This method is intended only for use by custom converters in order to delegate conversion of sub-values.
	/// </remarks>
	public ShapeShiftConverter<TEncoder, TDecoder> GetConverter(Type type, ITypeShapeProvider? provider = null)
	{
		Requires.NotNull(type);
		Verify.Operation(this.cache is not null, "No serialization operation is in progress.");
		var result = (IShapeShiftConverterInternal<TEncoder, TDecoder>)this.cache.GetOrAddConverter(type, provider ?? this.TypeShapeProvider ?? throw new UnreachableException()).ValueOrThrow;
		return this.preserveReferences ? result.WrapWithReferencePreservation() : (ShapeShiftConverter<TEncoder, TDecoder>)result;
	}
}
