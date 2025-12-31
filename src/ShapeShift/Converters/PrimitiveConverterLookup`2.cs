// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/* THIS (.cs) FILE IS GENERATED. DO NOT CHANGE IT.
 * CHANGE THE .tt FILE INSTEAD. */

#pragma warning disable SA1306 // Field names should begin with lower-case letter
#pragma warning disable SA1309 // Field names should not begin with underscore

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ShapeShift.Converters;

/// <summary>
/// Provides access to built-in converters for primitive types.
/// </summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
/// <remarks>
/// <para>This class is carefully crafted to avoid assembly loads by testing type names
/// rather than types directly for types declared in assemblies that may not be loaded.</para>
/// <para>On .NET, this class is also carefully crafted to help trimming be effective by avoiding type references
/// to types that are not used in the application.
/// Although the retrieval method references all the types, the fact that it is generic gives the
/// JIT/AOT compiler the opportunity to only reference types that match the type argument
/// (at least for the value types).
/// The generic method itself leads to more methods to JIT at runtime when NativeAOT is *not* used.
/// It's a trade-off, which is why we never use the generic method on .NET Framework where NativeAOT isn't even an option.
/// </para>
/// </remarks>
internal static class PrimitiveConverterLookup<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _Int32Converter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _StringConverter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _StringConverterReferencePreserving;

#if NET
	/// <summary>
	/// Gets a built-in converter for the given type, if one is available.
	/// </summary>
	/// <typeparam name="T">The type to get a converter for.</typeparam>
	/// <param name="referencePreserving">Indicates whether a reference-preserving converter is requested.</param>
	/// <param name="converter">Receives the converter, if one is available.</param>
	/// <returns><see langword="true" /> if a converter was found; <see langword="false" /> otherwise.</returns>
	internal static bool TryGetPrimitiveConverter<T>(ReferencePreservationMode referencePreserving, [NotNullWhen(true)] out ShapeShiftConverter<T, TEncoder, TDecoder>? converter)
#else
	/// <summary>
	/// Gets a built-in converter for the given type, if one is available.
	/// </summary>
	/// <param name="type">The type to get a converter for.</param>
	/// <param name="referencePreserving">Indicates whether a reference-preserving converter is requested.</param>
	/// <param name="converter">Receives the converter, if one is available.</param>
	/// <returns><see langword="true" /> if a converter was found; <see langword="false" /> otherwise.</returns>
	internal static bool TryGetPrimitiveConverter(Type type, ReferencePreservationMode referencePreserving, [NotNullWhen(true)] out ShapeShiftConverter<TEncoder, TDecoder>? converter)
#endif
	{
#if NET
		if (typeof(T) == typeof(int))
#else
		if (type == typeof(int))
#endif
		{
#if NET
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_Int32Converter ??= new Int32Converter<TEncoder, TDecoder>());
#else
			converter = (MessagePackConverter)(_Int32Converter ??= new Int32Converter<TEncoder, TDecoder>());
#endif
			return true;
		}

#if NET
		if (typeof(T) == typeof(string))
#else
		if (type == typeof(string))
#endif
		{
			if (referencePreserving != ReferencePreservationMode.Off)
			{
#if NET
				converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_StringConverterReferencePreserving ??= new StringConverter<TEncoder, TDecoder>().WrapWithReferencePreservation());
#else
				converter = (MessagePackConverter)(_StringConverterReferencePreserving ??= new StringConverter<TEncoder, TDecoder>().WrapWithReferencePreservation());
#endif
			}
			else
			{
#if NET
				converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_StringConverter ??= new StringConverter<TEncoder, TDecoder>());
#else
				converter = (MessagePackConverter)(_StringConverter ??= new StringConverter<TEncoder, TDecoder>());
#endif
			}

			return true;
		}


#if NET
		string primitiveTypeName = typeof(T).Name;
#else
		string primitiveTypeName = type.Name;
#endif
		string? primitiveTypeNamespace = null;

		converter = null;
		return false;
	}
}
