// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace ShapeShift.Converters;

/// <summary>
/// Converts rectangular arrays using a dimensions vector followed by a flat values vector.
/// </summary>
/// <typeparam name="TArray">The array type.</typeparam>
/// <typeparam name="TElement">The element type.</typeparam>
/// <typeparam name="TEncoder">The encoder type.</typeparam>
/// <typeparam name="TDecoder">The decoder type.</typeparam>
internal sealed class MultidimensionalArrayConverter<TArray, TElement, TEncoder, TDecoder>(
	ShapeShiftConverter<TElement, TEncoder, TDecoder> elementConverter,
	int rank) : ShapeShiftConverter<TArray, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "TArray statically roots the exact rectangular array type instantiated here.")]
	public override TArray? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			decoder.ReadNull();
			return default;
		}

		context.DepthStep();
		int? envelopeCount = decoder.ReadStartVector();
		if (envelopeCount is not null and not 2)
		{
			throw new ShapeShiftSerializationException("Expected a two-element multidimensional array envelope.");
		}

		int? dimensionsCount = decoder.ReadStartVector();
		if (dimensionsCount is not null && dimensionsCount != rank)
		{
			throw new ShapeShiftSerializationException($"Expected {rank} array dimensions but found {dimensionsCount}.");
		}

		int[] dimensions = new int[rank];
		long elementCount = 1;
		for (int i = 0; i < rank; i++)
		{
			if (decoder.NextTokenType == TokenType.EndVector)
			{
				throw new ShapeShiftSerializationException($"Expected {rank} array dimensions but found {i}.");
			}

			int dimension = checked((int)decoder.ReadInt64());
			if (dimension < 0)
			{
				throw new ShapeShiftSerializationException("Array dimensions cannot be negative.");
			}

			dimensions[i] = dimension;
			elementCount = checked(elementCount * dimension);
			if (elementCount > context.MaxCollectionLength)
			{
				throw new ShapeShiftSerializationException($"Array length {elementCount} exceeds the configured maximum of {context.MaxCollectionLength}.");
			}
		}

		decoder.ReadEndVector();
		int? valuesCount = decoder.ReadStartVector();
		if (valuesCount is not null && valuesCount != elementCount)
		{
			throw new ShapeShiftSerializationException($"Expected {elementCount} array values but found {valuesCount}.");
		}

		Array array = Array.CreateInstance(typeof(TElement), dimensions);
		Span<TElement> elements = AsSpan(array);
		for (int i = 0; i < elements.Length; i++)
		{
			if (decoder.NextTokenType == TokenType.EndVector)
			{
				throw new ShapeShiftSerializationException($"Expected {elements.Length} array values but found {i}.");
			}

			try
			{
				elements[i] = elementConverter.Read(ref decoder, context)!;
			}
			catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(i) && ex.AddEnclosingPathElement(1))
			{
				throw;
			}
			catch (Exception ex) when (SerializationErrors.IsAugmentable(ex))
			{
				throw SerializationErrors.WrapEntry(ex, 1, i, typeof(TArray), serializing: false);
			}
		}

		decoder.ReadEndVector();
		decoder.ReadEndVector();
		return (TArray)(object)array;
	}

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in TArray? value, SerializationContext<TEncoder, TDecoder> context)
	{
		Array? array = (Array?)(object?)value;
		if (array is null)
		{
			encoder.WriteNull();
			return;
		}

		context.DepthStep();
		if (array.Length > context.MaxCollectionLength)
		{
			throw new ShapeShiftSerializationException($"Array length {array.Length} exceeds the configured maximum of {context.MaxCollectionLength}.");
		}

		encoder.WriteStartVector(2);
		encoder.WriteStartVector(rank);
		for (int i = 0; i < rank; i++)
		{
			encoder.WriteValue(array.GetLength(i));
		}

		encoder.WriteEndVector();
		encoder.WriteStartVector(array.Length);
		int elementIndex = 0;
		foreach (TElement element in AsSpan(array))
		{
			try
			{
				elementConverter.Write(ref encoder, element, context);
			}
			catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(elementIndex) && ex.AddEnclosingPathElement(1))
			{
				throw;
			}
			catch (Exception ex) when (SerializationErrors.IsAugmentable(ex))
			{
				throw SerializationErrors.WrapEntry(ex, 1, elementIndex, typeof(TArray), serializing: true);
			}

			elementIndex++;
		}

		encoder.WriteEndVector();
		encoder.WriteEndVector();
	}

	private static Span<TElement> AsSpan(Array array)
		=> MemoryMarshal.CreateSpan(ref Unsafe.As<byte, TElement>(ref MemoryMarshal.GetArrayDataReference(array)), array.Length);
}
