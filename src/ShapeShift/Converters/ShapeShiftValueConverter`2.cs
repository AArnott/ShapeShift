// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Equality;

namespace ShapeShift.Converters;

#pragma warning disable SA1204 // Helpers are ordered by their use in the converter.

/// <summary>
/// Converts the format-neutral dynamic value model.
/// </summary>
/// <typeparam name="TEncoder">The encoder type.</typeparam>
/// <typeparam name="TDecoder">The decoder type.</typeparam>
internal sealed class ShapeShiftValueConverter<TEncoder, TDecoder> : ShapeShiftConverter<ShapeShiftValue, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private static readonly ShapeShiftValueComparer KeyComparer = new(HashingPolicy.Deterministic);

	/// <inheritdoc/>
	public override ShapeShiftValue Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		return decoder.NextTokenType switch
		{
			TokenType.Null => ReadNull(ref decoder),
			TokenType.Boolean => new ShapeShiftBoolean(decoder.ReadBoolean()),
			TokenType.Number => decoder.ReadDynamicNumber(),
			TokenType.String => ReadString(ref decoder, context),
			TokenType.Binary => ReadBinary(ref decoder, context),
			TokenType.StartVector => this.ReadArray(ref decoder, context),
			TokenType.StartMap => this.ReadMap(ref decoder, context),
			_ => throw new ShapeShiftSerializationException($"Cannot read a dynamic value from token {decoder.NextTokenType}."),
		};
	}

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in ShapeShiftValue? value, SerializationContext<TEncoder, TDecoder> context)
	{
		switch (value)
		{
			case null or ShapeShiftNull:
				encoder.WriteNull();
				break;
			case ShapeShiftBoolean item:
				encoder.WriteValue(item.Value);
				break;
			case ShapeShiftInteger item:
				encoder.WriteValue(item.Value);
				break;
			case ShapeShiftUnsignedInteger item:
				encoder.WriteValue(item.Value);
				break;
			case ShapeShiftBigInteger item:
				encoder.WriteValue(item.Value);
				break;
			case ShapeShiftFloat item:
				encoder.WriteValue(item.Value);
				break;
			case ShapeShiftDecimal item:
				encoder.WriteValue(item.Value);
				break;
			case ShapeShiftString item:
				ValidateLength(item.Value.Length, context.MaxStringLength, "String");
				encoder.WriteValue(item.Value);
				break;
			case ShapeShiftBinary item:
				ValidateLength(item.Value.Length, context.MaxBinaryLength, "Binary");
				encoder.WriteValue(item.Value.Span);
				break;
			case ShapeShiftArray item:
				this.WriteArray(ref encoder, item, context);
				break;
			case ShapeShiftMap item:
				this.WriteMap(ref encoder, item, context);
				break;
			case ShapeShiftDataMap item:
				this.WriteDataMap(ref encoder, item, context);
				break;
			default:
				throw new ShapeShiftSerializationException($"Unrecognized dynamic value type {value.GetType().FullName}.");
		}
	}

	private static ShapeShiftValue ReadNull(ref TDecoder decoder)
	{
		decoder.ReadNull();
		return ShapeShiftValue.Null;
	}

	private static ShapeShiftValue ReadString(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		string value = decoder.ReadString();
		ValidateLength(value.Length, context.MaxStringLength, "String");
		return new ShapeShiftString(value);
	}

	private static ShapeShiftValue ReadBinary(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		byte[] value = decoder.ReadByteArray();
		ValidateLength(value.Length, context.MaxBinaryLength, "Binary");
		return new ShapeShiftBinary(value);
	}

	private ShapeShiftArray ReadArray(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		context.DepthStep();
		int? count = decoder.ReadStartVector();
		ValidateLength(count, context.MaxCollectionLength, "Collection");
		List<ShapeShiftValue> items = count is int length ? new(length) : [];
		while (decoder.NextTokenType != TokenType.EndVector)
		{
			items.Add(this.Read(ref decoder, context));
			ValidateLength(items.Count, context.MaxCollectionLength, "Collection");
		}

		decoder.ReadEndVector();
		return new(items);
	}

	private ShapeShiftValue ReadMap(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		context.DepthStep();
		int? count = decoder.ReadStartMap();
		ValidateLength(count, context.MaxCollectionLength, "Collection");
		Dictionary<string, ShapeShiftValue>? properties = count is int length ? new(length, StringComparer.Ordinal) : new(StringComparer.Ordinal);
		List<ShapeShiftDataMapEntry>? entries = null;
		Dictionary<ShapeShiftValue, int>? seen = null;
		while (decoder.NextTokenType != TokenType.EndMap)
		{
			if (entries is null && decoder.NextTokenType == TokenType.PropertyName)
			{
				string name = decoder.ReadPropertyName().ToString();
				ValidateLength(name.Length, context.MaxStringLength, "String");
				ShapeShiftValue item = this.Read(ref decoder, context);
				if (!properties!.TryAdd(name, item))
				{
					throw new ShapeShiftSerializationException($"Dynamic map property '{name}' appears more than once.");
				}

				ValidateLength(properties.Count, context.MaxCollectionLength, "Collection");
				continue;
			}

			if (entries is null)
			{
				entries = new(properties!.Count + 1);
				seen = new(properties.Count + 1, KeyComparer);
				foreach ((string name, ShapeShiftValue existing) in properties)
				{
					ShapeShiftString key = new(name);
					seen.Add(key, entries.Count);
					entries.Add(new(key, existing));
				}

				properties = null;
			}

			ShapeShiftValue keyValue = this.Read(ref decoder, context);
			ShapeShiftValue itemValue = this.Read(ref decoder, context);
			if (!seen!.TryAdd(keyValue, entries.Count))
			{
				throw new ShapeShiftSerializationException("A dynamic map key appears more than once.");
			}

			entries.Add(new(keyValue, itemValue));
			ValidateLength(entries.Count, context.MaxCollectionLength, "Collection");
		}

		decoder.ReadEndMap();
		return entries is null ? new ShapeShiftMap(properties!) : new ShapeShiftDataMap(entries);
	}

	private void WriteArray(ref TEncoder encoder, ShapeShiftArray value, SerializationContext<TEncoder, TDecoder> context)
	{
		context.DepthStep();
		ValidateLength(value.Items.Count, context.MaxCollectionLength, "Collection");
		encoder.WriteStartVector(value.Items.Count);
		foreach (ShapeShiftValue item in value.Items)
		{
			this.Write(ref encoder, item, context);
		}

		encoder.WriteEndVector();
	}

	private void WriteMap(ref TEncoder encoder, ShapeShiftMap value, SerializationContext<TEncoder, TDecoder> context)
	{
		context.DepthStep();
		ValidateLength(value.Properties.Count, context.MaxCollectionLength, "Collection");
		encoder.WriteStartMap(value.Properties.Count);
		foreach ((string name, ShapeShiftValue item) in value.Properties)
		{
			ValidateLength(name.Length, context.MaxStringLength, "String");
			encoder.WritePropertyName(name);
			this.Write(ref encoder, item, context);
		}

		encoder.WriteEndMap();
	}

	private void WriteDataMap(ref TEncoder encoder, ShapeShiftDataMap value, SerializationContext<TEncoder, TDecoder> context)
	{
		context.DepthStep();
		IReadOnlyList<ShapeShiftDataMapEntry> entries = value.Entries ?? throw new ShapeShiftSerializationException("A dynamic map is missing its entries.");
		ValidateLength(entries.Count, context.MaxCollectionLength, "Collection");
		if (!encoder.ArbitraryMapKeys)
		{
			foreach (ShapeShiftDataMapEntry entry in entries)
			{
				if (entry.Key is not ShapeShiftString)
				{
					throw new ShapeShiftSerializationException("This format cannot encode a map key that is not a text string.");
				}
			}
		}

		RejectDuplicateKeys(entries);
		encoder.WriteStartMap(entries.Count);
		foreach (ShapeShiftDataMapEntry entry in entries)
		{
			ShapeShiftValue key = entry.Key ?? ShapeShiftValue.Null;
			if (encoder.ArbitraryMapKeys)
			{
				this.Write(ref encoder, key, context);
			}
			else
			{
				string name = ((ShapeShiftString)key).Value;
				ValidateLength(name.Length, context.MaxStringLength, "String");
				encoder.WritePropertyName(name);
			}

			this.Write(ref encoder, entry.Value, context);
		}

		encoder.WriteEndMap();
	}

	private static void RejectDuplicateKeys(IReadOnlyList<ShapeShiftDataMapEntry> entries)
	{
		if (entries.Count < 2)
		{
			return;
		}

		Dictionary<ShapeShiftValue, int> seen = new(entries.Count, KeyComparer);
		foreach (ShapeShiftDataMapEntry entry in entries)
		{
			ShapeShiftValue key = entry.Key ?? ShapeShiftValue.Null;
			if (!seen.TryAdd(key, 0))
			{
				throw new ShapeShiftSerializationException("A dynamic map key appears more than once.");
			}
		}
	}

	private static void ValidateLength(int? length, int maximum, string kind)
	{
		if (length > maximum)
		{
			throw new ShapeShiftSerializationException($"{kind} length {length} exceeds the configured maximum of {maximum}.");
		}
	}
}
