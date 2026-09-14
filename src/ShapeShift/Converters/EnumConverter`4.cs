// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Converters;

/// <summary>
/// Converts enum values by name when possible, with ordinal fallback.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
/// <typeparam name="TUnderlying">The enum's underlying integer type.</typeparam>
/// <typeparam name="TEncoder">The encoder type.</typeparam>
/// <typeparam name="TDecoder">The decoder type.</typeparam>
internal sealed class EnumConverter<TEnum, TUnderlying, TEncoder, TDecoder> : ShapeShiftConverter<TEnum, TEncoder, TDecoder>
	where TEnum : struct, Enum
	where TUnderlying : unmanaged
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly IReadOnlyDictionary<string, TUnderlying> valuesByName;
	private readonly IReadOnlyDictionary<TUnderlying, string> namesByValue;
	private readonly ShapeShiftConverter<TUnderlying, TEncoder, TDecoder> underlyingConverter;
	private readonly bool serializeByName;

	internal EnumConverter(
		ShapeShiftConverter<TUnderlying, TEncoder, TDecoder> underlyingConverter,
		IReadOnlyDictionary<string, TUnderlying> members,
		bool serializeByName)
	{
		this.underlyingConverter = underlyingConverter;
		this.serializeByName = serializeByName;
		this.valuesByName = new Dictionary<string, TUnderlying>(members, StringComparer.OrdinalIgnoreCase);
		Dictionary<TUnderlying, string> names = new();
		foreach ((string name, TUnderlying value) in members)
		{
			names.TryAdd(value, name);
		}

		this.namesByValue = names;
	}

	/// <inheritdoc/>
	public override TEnum Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.NextTokenType == TokenType.String)
		{
			string name = decoder.ReadString();
			if (!this.valuesByName.TryGetValue(name, out TUnderlying value))
			{
				throw new ShapeShiftSerializationException($"Unrecognized {typeof(TEnum).FullName} value name '{name}'.");
			}

			return (TEnum)(object)value!;
		}

		return (TEnum)(object)this.underlyingConverter.Read(ref decoder, context)!;
	}

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in TEnum value, SerializationContext<TEncoder, TDecoder> context)
	{
		TUnderlying underlying = (TUnderlying)(object)value;
		if (this.serializeByName && this.namesByValue.TryGetValue(underlying, out string? name))
		{
			encoder.WriteValue(name);
		}
		else
		{
			this.underlyingConverter.Write(ref encoder, underlying, context);
		}
	}
}
