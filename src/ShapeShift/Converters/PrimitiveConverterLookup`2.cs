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
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _BooleanConverter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _CharConverter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _ByteConverter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _SByteConverter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _Int16Converter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _UInt16Converter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _Int32Converter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _UInt32Converter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _Int64Converter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _UInt64Converter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _HalfConverter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _SingleConverter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _DoubleConverter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _DecimalConverter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _DateTimeConverter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _DateTimeOffsetConverter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _StringConverter;
	private static IShapeShiftConverterInternal<TEncoder, TDecoder>? _StringConverterReferencePreserving;

	/// <summary>
	/// Gets a built-in converter for the given type, if one is available.
	/// </summary>
	/// <typeparam name="T">The type to get a converter for.</typeparam>
	/// <param name="referencePreserving">Indicates whether a reference-preserving converter is requested.</param>
	/// <param name="converter">Receives the converter, if one is available.</param>
	/// <returns><see langword="true" /> if a converter was found; <see langword="false" /> otherwise.</returns>
	internal static bool TryGetPrimitiveConverter<T>(ReferencePreservationMode referencePreserving, [NotNullWhen(true)] out ShapeShiftConverter<T, TEncoder, TDecoder>? converter)
	{
		if (typeof(T) == typeof(bool))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_BooleanConverter ??= new BooleanConverter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(char))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_CharConverter ??= new CharConverter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(byte))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_ByteConverter ??= new ByteConverter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(sbyte))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_SByteConverter ??= new SByteConverter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(short))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_Int16Converter ??= new Int16Converter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(ushort))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_UInt16Converter ??= new UInt16Converter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(int))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_Int32Converter ??= new Int32Converter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(uint))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_UInt32Converter ??= new UInt32Converter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(long))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_Int64Converter ??= new Int64Converter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(ulong))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_UInt64Converter ??= new UInt64Converter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(Half))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_HalfConverter ??= new HalfConverter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(float))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_SingleConverter ??= new SingleConverter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(double))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_DoubleConverter ??= new DoubleConverter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(decimal))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_DecimalConverter ??= new DecimalConverter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(DateTime))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_DateTimeConverter ??= new DateTimeConverter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(DateTimeOffset))
		{
			converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_DateTimeOffsetConverter ??= new DateTimeOffsetConverter<TEncoder, TDecoder>());
			return true;
		}

		if (typeof(T) == typeof(string))
		{
			if (referencePreserving != ReferencePreservationMode.Off)
			{
				converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_StringConverterReferencePreserving ??= new StringConverter<TEncoder, TDecoder>().WrapWithReferencePreservation());
			}
			else
			{
				converter = (ShapeShiftConverter<T, TEncoder, TDecoder>)(_StringConverter ??= new StringConverter<TEncoder, TDecoder>());
			}

			return true;
		}

		converter = null;
		return false;
	}
}
