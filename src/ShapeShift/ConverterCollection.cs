// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace ShapeShift;

public static class ConverterCollection
{
	/// <summary>
	/// Creates a new instance of <see cref="ConverterCollection"/> from the specified converters.
	/// </summary>
	/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
	/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
	/// <param name="converters">The converters to fill the collection with.</param>
	/// <returns>The initialized collection.</returns>
	public static ConverterCollection<TEncoder, TDecoder> Create<TEncoder, TDecoder>(ReadOnlySpan<ShapeShiftConverter<TEncoder, TDecoder>> converters)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
	{
		Dictionary<Type, ShapeShiftConverter<TEncoder, TDecoder>> map = [];
		foreach (ShapeShiftConverter<TEncoder, TDecoder> converter in converters)
		{
			map.Add(converter.DataType, converter);
		}

		return new(map.ToFrozenDictionary());
	}
}
