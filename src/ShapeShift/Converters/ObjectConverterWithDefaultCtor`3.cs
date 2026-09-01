// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

internal class ObjectConverterWithDefaultCtor<T, TEncoder, TDecoder>(Func<T> ctor) : ObjectConverter<T, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal required Dictionary<string, ObjectPropertyReader<T, TEncoder, TDecoder>> PropertyReaders { get; init; }

	public override T? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
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
		ulong encounteredKnownProperties = 0;
		HashSet<string>? encounteredOtherProperties = null;
		while (decoder.NextTokenType != TokenType.EndMap)
		{
			ReadOnlySpan<char> propertyName = decoder.ReadPropertyName();

			if (this.PropertyReaders.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(propertyName, out var propertyReader))
			{
				if (propertyReader.Index < 64)
				{
					ulong bit = 1UL << propertyReader.Index;
					if ((encounteredKnownProperties & bit) != 0)
					{
						throw new ShapeShiftSerializationException($"Property '{propertyName}' appears more than once while deserializing {typeof(T).FullName}.", null, new ShapeShiftPath(propertyName.ToString()));
					}

					encounteredKnownProperties |= bit;
				}
				else if (!(encounteredOtherProperties ??= new(StringComparer.Ordinal)).Add(propertyName.ToString()))
				{
					throw new ShapeShiftSerializationException($"Property '{propertyName}' appears more than once while deserializing {typeof(T).FullName}.", null, new ShapeShiftPath(propertyName.ToString()));
				}

				try
				{
					propertyReader.Read(ref decoder, ref value, context);
				}
				catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(propertyName.ToString()))
				{
					throw;
				}
				catch (Exception ex) when (SerializationErrors.IsAugmentable(ex))
				{
					throw SerializationErrors.Wrap(ex, propertyName.ToString(), typeof(T), serializing: false);
				}
			}
			else
			{
				string propertyNameString = propertyName.ToString();
				if (!(encounteredOtherProperties ??= new(StringComparer.Ordinal)).Add(propertyNameString))
				{
					throw new ShapeShiftSerializationException($"Property '{propertyNameString}' appears more than once while deserializing {typeof(T).FullName}.", null, new ShapeShiftPath(propertyNameString));
				}

				if (this.ExtensionData is { } extensionData)
				{
					try
					{
						extensionData.Read(ref decoder, ref value, propertyNameString, context);
					}
					catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(propertyNameString))
					{
						throw;
					}
					catch (Exception ex) when (SerializationErrors.IsAugmentable(ex))
					{
						throw SerializationErrors.Wrap(ex, propertyNameString, typeof(T), serializing: false);
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
