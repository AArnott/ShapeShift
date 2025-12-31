// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

internal class ObjectConverter<T, TEncoder, TDecoder> : ShapeShiftConverter<T, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly Func<T> ctor;
	private readonly IReadOnlyDictionary<string, PropertyConverter<T, TEncoder, TDecoder>> properties;

	internal ObjectConverter(IObjectTypeShape<T> objectShape, IReadOnlyDictionary<string, PropertyConverter<T, TEncoder, TDecoder>> properties)
	{
		this.ctor = objectShape.GetDefaultConstructor() ?? throw new NotSupportedException();
		this.properties = properties;
	}

	public override T? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			return default;
		}

		T result = this.ctor();
		int? remaining = decoder.ReadStartMap();
		while (decoder.NextTokenType != TokenType.EndMap)
		{
			string propertyName = decoder.ReadPropertyName().ToString();
			if (this.properties.TryGetValue(propertyName, out var propertyConverter))
			{
				propertyConverter.Read(ref decoder, ref result, context);
			}
			else
			{
				decoder.Skip();
			}
		}

		decoder.ReadEndMap();

		return result;
	}

	public override void Write(ref TEncoder encoder, in T? value, SerializationContext<TEncoder, TDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
			return;
		}

		encoder.WriteStartMap(this.properties.Count);
		foreach (var property in this.properties)
		{
			encoder.WritePropertyName(property.Key);
			property.Value.Write(ref encoder, in value, context);
		}

		encoder.WriteEndMap();
	}
}
