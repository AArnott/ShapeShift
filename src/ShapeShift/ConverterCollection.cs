// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ShapeShift;

/// <summary>
/// An immutable collection of converters.
/// </summary>
/// <typeparam name="TEncoder"><inheritdoc cref="SerializerBase{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="SerializerBase{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
[CollectionBuilder(typeof(ConverterCollection), nameof(ConverterCollection.Create))]
public class ConverterCollection<TEncoder, TDecoder> : IReadOnlyCollection<ShapeShiftConverter<TEncoder, TDecoder>>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal ConverterCollection(FrozenDictionary<Type, ShapeShiftConverter<TEncoder, TDecoder>> map)
	{
		this.Map = map;
	}

	/// <inheritdoc/>
	public int Count => this.Map.Count;

	private FrozenDictionary<Type, ShapeShiftConverter<TEncoder, TDecoder>> Map { get; }

	/// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
	public ImmutableArray<ShapeShiftConverter<TEncoder, TDecoder>>.Enumerator GetEnumerator() => this.Map.Values.GetEnumerator();

	/// <inheritdoc/>
	IEnumerator<ShapeShiftConverter<TEncoder, TDecoder>> IEnumerable<ShapeShiftConverter<TEncoder, TDecoder>>.GetEnumerator() => ((IReadOnlyList<ShapeShiftConverter<TEncoder, TDecoder>>)this.Map.Values).GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ShapeShiftConverter<TEncoder, TDecoder>>)this).GetEnumerator();

	/// <summary>
	/// Retrieves a converter for a given data type, if the user supplied one.
	/// </summary>
	/// <param name="dataType">The data type.</param>
	/// <param name="converter">Receives the converter, if available.</param>
	/// <returns>A value indicating whether a converter was available.</returns>
	internal bool TryGetConverter(Type dataType, [NotNullWhen(true)] out ShapeShiftConverter<TEncoder, TDecoder>? converter)
	{
		if (this.Map.TryGetValue(dataType, out converter))
		{
			return true;
		}

		converter = default;
		return false;
	}
}

public static class ConverterCollection
{
	/// <summary>
	/// Creates a new instance of <see cref="ConverterCollection"/> from the specified converters.
	/// </summary>
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
