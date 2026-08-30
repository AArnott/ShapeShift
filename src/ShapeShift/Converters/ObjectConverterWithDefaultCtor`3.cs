// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

internal class ObjectConverterWithDefaultCtor<T, TEncoder, TDecoder>(Func<T> ctor) : ObjectConverter<T, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal required IReadOnlyDictionary<string, ReadProperty<T, TEncoder, TDecoder>> PropertyReaders { get; init; }

	public override T? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			decoder.ReadNull();
			return default;
		}

		context.DepthStep();
		T value = ctor();
		var callbacks = value as IShapeShiftSerializationCallbacks;
		callbacks?.OnBeforeDeserialize();

		if (!typeof(T).IsValueType)
		{
			context.ReportObjectConstructed(value);
		}

		decoder.ReadStartMap();
		HashSet<string> encounteredProperties = new(StringComparer.Ordinal);
		while (decoder.NextTokenType != TokenType.EndMap)
		{
			string propertyName = decoder.ReadPropertyName().ToString();
			if (!encounteredProperties.Add(propertyName))
			{
				throw new ShapeShiftSerializationException($"Property '{propertyName}' appears more than once while deserializing {typeof(T).FullName}.", null, new ShapeShiftPath(propertyName));
			}

			if (this.PropertyReaders.TryGetValue(propertyName, out var propertyReader))
			{
				try
				{
					propertyReader(ref decoder, ref value, context);
				}
				catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(propertyName))
				{
					throw;
				}
				catch (Exception ex) when (SerializationErrors.IsAugmentable(ex))
				{
					throw SerializationErrors.Wrap(ex, propertyName, typeof(T), serializing: false);
				}
			}
			else
			{
				if (this.ExtensionData is { } extensionData)
				{
					try
					{
						extensionData.Read(ref decoder, ref value, propertyName, context);
					}
					catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(propertyName))
					{
						throw;
					}
					catch (Exception ex) when (SerializationErrors.IsAugmentable(ex))
					{
						throw SerializationErrors.Wrap(ex, propertyName, typeof(T), serializing: false);
					}
				}
				else
				{
					decoder.Skip();
				}
			}
		}

		decoder.ReadEndMap();

		callbacks?.OnAfterDeserialize();
		return value;
	}
}
