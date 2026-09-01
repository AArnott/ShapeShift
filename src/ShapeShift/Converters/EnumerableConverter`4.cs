// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Converters;

internal class EnumerableConverter<TEnumerable, TElement, TEncoder, TDecoder> : ShapeShiftConverter<TEnumerable, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly Func<TEnumerable, IEnumerable<TElement>> getEnumerable;
	private readonly ShapeShiftConverter<TElement, TEncoder, TDecoder> elementConverter;
	private readonly CollectionConstructionStrategy constructionStrategy;
	private readonly MutableCollectionConstructor<TElement, TEnumerable>? mutableConstructor;
	private readonly EnumerableAppender<TEnumerable, TElement>? appender;
	private readonly ParameterizedCollectionConstructor<TElement, TElement, TEnumerable>? parameterizedConstructor;

	public EnumerableConverter(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, ShapeShiftConverter<TElement, TEncoder, TDecoder> elementConverter)
	{
		this.getEnumerable = enumerableShape.GetGetEnumerable();
		this.elementConverter = elementConverter;
		this.constructionStrategy = enumerableShape.ConstructionStrategy;
		if (this.constructionStrategy == CollectionConstructionStrategy.Mutable)
		{
			this.mutableConstructor = enumerableShape.GetDefaultConstructor();
			this.appender = enumerableShape.GetAppender();
		}
		else if (this.constructionStrategy == CollectionConstructionStrategy.Parameterized)
		{
			this.parameterizedConstructor = enumerableShape.GetParameterizedConstructor();
		}
	}

	public override TEnumerable? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			return default;
		}

		context.DepthStep();
		int? length = decoder.ReadStartVector();
		if (length > context.MaxCollectionLength)
		{
			throw new ShapeShiftSerializationException($"Collection length {length} exceeds the configured maximum of {context.MaxCollectionLength}.");
		}

		List<TElement> elements = length is int count ? new(count) : [];
		while (decoder.NextTokenType != TokenType.EndVector)
		{
			int index = elements.Count;
			try
			{
				elements.Add(this.elementConverter.Read(ref decoder, context)!);
			}
			catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(index))
			{
				throw;
			}
			catch (Exception ex) when (SerializationErrors.IsAugmentable(ex))
			{
				throw SerializationErrors.Wrap(ex, index, typeof(TEnumerable), serializing: false);
			}

			if (elements.Count > context.MaxCollectionLength)
			{
				throw new ShapeShiftSerializationException($"Collection length exceeds the configured maximum of {context.MaxCollectionLength}.");
			}
		}

		decoder.ReadEndVector();
		return this.Construct(elements);
	}

	public override void Write(ref TEncoder encoder, in TEnumerable? value, SerializationContext<TEncoder, TDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
			return;
		}

		context.DepthStep();
		IEnumerable<TElement> enumerable = this.getEnumerable(value);
		bool success = enumerable.TryGetNonEnumeratedCount(out int count);
		if (success && count > context.MaxCollectionLength)
		{
			throw new ShapeShiftSerializationException($"Collection length {count} exceeds the configured maximum of {context.MaxCollectionLength}.");
		}

		encoder.WriteStartVector(success ? count : null);

		int index = 0;
		foreach (TElement element in enumerable)
		{
			if (++index > context.MaxCollectionLength)
			{
				throw new ShapeShiftSerializationException($"Collection length exceeds the configured maximum of {context.MaxCollectionLength}.");
			}

			try
			{
				this.elementConverter.Write(ref encoder, element, context);
			}
			catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(index - 1))
			{
				throw;
			}
			catch (Exception ex) when (SerializationErrors.IsAugmentable(ex))
			{
				throw SerializationErrors.Wrap(ex, index - 1, typeof(TEnumerable), serializing: true);
			}
		}

		encoder.WriteEndVector();
	}

	private TEnumerable Construct(List<TElement> elements)
	{
		switch (this.constructionStrategy)
		{
			case CollectionConstructionStrategy.Mutable:
				TEnumerable result = this.mutableConstructor!(new CollectionConstructionOptions<TElement> { Capacity = elements.Count });
				foreach (TElement element in elements)
				{
					this.appender!(ref result, element);
				}

				return result;
			case CollectionConstructionStrategy.Parameterized:
				return this.parameterizedConstructor!(CollectionsMarshal.AsSpan(elements), new CollectionConstructionOptions<TElement> { Capacity = elements.Count });
			default:
				throw new NotSupportedException($"{typeof(TEnumerable).FullName} does not support deserialization.");
		}
	}
}
