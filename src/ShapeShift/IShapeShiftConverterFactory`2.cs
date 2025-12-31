// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// A factory for <see cref="ShapeShiftConverter{TEncoder, TDecoder}"/> objects of arbitrary types.
/// </summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
/// <example>
/// <para>
/// A non-generic implementation of this interface is preferred when possible.
/// </para>
/// <code source="../../samples/cs/CustomConverterFactory.cs" region="NonGeneric" lang="C#" />
/// <para>
/// When a generic context is required, implement <see cref="ITypeShapeFunc"/> on the same class
/// and invoke into it after appropriate type checks.
/// </para>
/// <code source="../../samples/cs/CustomConverterFactory.cs" region="Generic" lang="C#" />
/// <para>
/// When generic type parameters are required for sub-elements of the type to be converted
/// (e.g. the element type of a collection), you can leverage a <see cref="TypeShapeVisitor"/> implementation
/// to obtain the generic type parameters.
/// </para>
/// <code source="../../samples/cs/CustomConverterFactory.cs" region="Visitor" lang="C#" />
/// </example>
public interface IShapeShiftConverterFactory<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <summary>
	/// Creates a converter for the given type if this factory is capable of it.
	/// </summary>
	/// <param name="type">The type to be serialized.</param>
	/// <param name="shape">
	/// The shape of the type to be serialized, if available.
	/// The shape will typically be available.
	/// The only known exception is when the type has a
	/// <see cref="TypeShapeAttribute.Marshaler">PolyType marshaler</see> defined.
	/// </param>
	/// <param name="context">The context in which this factory is being invoked. Provides access to other converters that may be required by the requested converter.</param>
	/// <returns>The converter for the data type, or <see langword="null" />.</returns>
	/// <remarks>
	/// Implementations that require a generic type parameter for the type to be converted should
	/// also implement <see cref="ITypeShapeFunc"/> with an <see cref="ITypeShapeFunc.Invoke{T}(ITypeShape{T}, object?)"/>
	/// method that creates the converter.
	/// The implementation of <em>this</em> method should perform any type checks necessary
	/// to determine whether this factory applies to the given shape, and if so,
	/// call <see cref="ShapeShiftConverterFactoryExtensions.Invoke{T, TEncoder, TDecoder}(T, ITypeShape, object?)"/>
	/// to forward the call to the generic <see cref="ITypeShapeFunc.Invoke{T}(ITypeShape{T}, object?)"/> method
	/// defined on that same class.
	/// </remarks>
	ShapeShiftConverter<TEncoder, TDecoder>? CreateConverter(Type type, ITypeShape? shape, in ConverterContext<TEncoder, TDecoder> context);
}

/// <summary>
/// Extension methods for the <see cref="IShapeShiftConverterFactory{TEncoder, TDecoder}"/> interface.
/// </summary>
public static class ShapeShiftConverterFactoryExtensions
{
	/// <summary>
	/// Calls the <see cref="ITypeShapeFunc.Invoke{T}(ITypeShape{T}, object?)"/> method on the given factory.
	/// </summary>
	/// <typeparam name="T">The concrete <see cref="IShapeShiftConverterFactory{TEncoder, TDecoder}"/> type.</typeparam>
	/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
	/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
	/// <param name="self">The instance of the factory.</param>
	/// <param name="shape">The shape to create a converter for.</param>
	/// <param name="state">Optional state to pass onto the inner method.</param>
	/// <returns>The converter.</returns>
	public static ShapeShiftConverter<TEncoder, TDecoder>? Invoke<T, TEncoder, TDecoder>(this T self, ITypeShape shape, object? state = null)
		where T : IShapeShiftConverterFactory<TEncoder, TDecoder>, ITypeShapeFunc
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
	{
		return (ShapeShiftConverter<TEncoder, TDecoder>?)Requires.NotNull(shape).Invoke(self, state);
	}
}
