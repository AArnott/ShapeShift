// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Converters;

/// <summary>
/// Converts dictionaries using a map for string keys and key/value pair vectors for other key types.
/// </summary>
/// <typeparam name="TDictionary">The dictionary type.</typeparam>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
/// <typeparam name="TEncoder">The encoder type.</typeparam>
/// <typeparam name="TDecoder">The decoder type.</typeparam>
internal sealed class DictionaryConverter<TDictionary, TKey, TValue, TEncoder, TDecoder> : ShapeShiftConverter<TDictionary, TEncoder, TDecoder>
	where TKey : notnull
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary;
	private readonly ShapeShiftConverter<TKey, TEncoder, TDecoder> keyConverter;
	private readonly ShapeShiftConverter<TValue, TEncoder, TDecoder> valueConverter;
	private readonly CollectionConstructionStrategy constructionStrategy;
	private readonly DictionaryInserter<TDictionary, TKey, TValue>? inserter;
	private readonly MutableCollectionConstructor<TKey, TDictionary>? mutableConstructor;
	private readonly ParameterizedCollectionConstructor<TKey, KeyValuePair<TKey, TValue>, TDictionary>? parameterizedConstructor;

	internal DictionaryConverter(
		IDictionaryTypeShape<TDictionary, TKey, TValue> shape,
		ShapeShiftConverter<TKey, TEncoder, TDecoder> keyConverter,
		ShapeShiftConverter<TValue, TEncoder, TDecoder> valueConverter)
	{
		this.getDictionary = shape.GetGetDictionary();
		this.keyConverter = keyConverter;
		this.valueConverter = valueConverter;
		this.constructionStrategy = shape.ConstructionStrategy;
		if (this.constructionStrategy == CollectionConstructionStrategy.Mutable)
		{
			this.inserter = shape.GetInserter(DictionaryInsertionMode.Throw);
			this.mutableConstructor = shape.GetDefaultConstructor();
		}
		else if (this.constructionStrategy == CollectionConstructionStrategy.Parameterized)
		{
			this.parameterizedConstructor = shape.GetParameterizedConstructor();
		}
	}

	/// <inheritdoc/>
	public override TDictionary? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			decoder.ReadNull();
			return default;
		}

		context.DepthStep();
		List<KeyValuePair<TKey, TValue>> entries = [];
		if (typeof(TKey) == typeof(string))
		{
			int? count = decoder.ReadStartMap();
			ValidateCount(count, context);
			while (decoder.NextTokenType != TokenType.EndMap)
			{
				string propertyName = decoder.ReadPropertyName().ToString();
				TKey key = (TKey)(object)propertyName;
				TValue value = this.valueConverter.Read(ref decoder, context)!;
				entries.Add(new(key, value));
				ValidateCount(entries.Count, context);
			}

			decoder.ReadEndMap();
		}
		else
		{
			int? count = decoder.ReadStartVector();
			ValidateCount(count, context);
			while (decoder.NextTokenType != TokenType.EndVector)
			{
				int? pairCount = decoder.ReadStartVector();
				if (pairCount is not null and not 2)
				{
					throw new ShapeShiftSerializationException("Expected a two-element dictionary entry.");
				}

				TKey key = this.keyConverter.Read(ref decoder, context)!;
				TValue value = this.valueConverter.Read(ref decoder, context)!;
				decoder.ReadEndVector();
				entries.Add(new(key, value));
				ValidateCount(entries.Count, context);
			}

			decoder.ReadEndVector();
		}

		return this.Construct(entries);
	}

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in TDictionary? value, SerializationContext<TEncoder, TDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
			return;
		}

		context.DepthStep();
		IReadOnlyDictionary<TKey, TValue> dictionary = this.getDictionary(value);
		ValidateCount(dictionary.Count, context);
		if (typeof(TKey) == typeof(string))
		{
			encoder.WriteStartMap(dictionary.Count);
			foreach ((TKey key, TValue itemValue) in dictionary)
			{
				encoder.WritePropertyName((string)(object)key);
				this.valueConverter.Write(ref encoder, itemValue, context);
			}

			encoder.WriteEndMap();
		}
		else
		{
			encoder.WriteStartVector(dictionary.Count);
			foreach ((TKey key, TValue itemValue) in dictionary)
			{
				encoder.WriteStartVector(2);
				this.keyConverter.Write(ref encoder, key, context);
				this.valueConverter.Write(ref encoder, itemValue, context);
				encoder.WriteEndVector();
			}

			encoder.WriteEndVector();
		}
	}

	private static void ValidateCount(int? count, SerializationContext<TEncoder, TDecoder> context)
	{
		if (count > context.MaxCollectionLength)
		{
			throw new ShapeShiftSerializationException($"Collection length {count} exceeds the configured maximum of {context.MaxCollectionLength}.");
		}
	}

	private TDictionary Construct(List<KeyValuePair<TKey, TValue>> entries)
	{
		switch (this.constructionStrategy)
		{
			case CollectionConstructionStrategy.Mutable:
				TDictionary result = this.mutableConstructor!(new CollectionConstructionOptions<TKey> { Capacity = entries.Count });
				foreach ((TKey key, TValue value) in entries)
				{
					this.inserter!(ref result, key, value);
				}

				return result;
			case CollectionConstructionStrategy.Parameterized:
				return this.parameterizedConstructor!(CollectionsMarshal.AsSpan(entries), new CollectionConstructionOptions<TKey> { Capacity = entries.Count });
			default:
				throw new NotSupportedException($"{typeof(TDictionary).FullName} does not support deserialization.");
		}
	}
}
