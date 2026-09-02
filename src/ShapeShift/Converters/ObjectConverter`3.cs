// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

internal abstract class ObjectConverter<T, TEncoder, TDecoder> : ShapeShiftConverter<T, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal required KeyValuePair<string, ObjectPropertyWriter<T, TEncoder, TDecoder>>[] PropertyWriters { get; init; }

	/// <summary>
	/// Gets a value indicating whether any declared property may be omitted during serialization.
	/// </summary>
	internal required bool HasConditionalProperties { get; init; }

	internal ExtensionDataProperty<T, TEncoder, TDecoder>? ExtensionData { get; init; }

	public override void Write(ref TEncoder encoder, in T? value, SerializationContext<TEncoder, TDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
			return;
		}

		var callbacks = value as IShapeShiftSerializationCallbacks;
		callbacks?.OnBeforeSerialize();

		context.DepthStep();

		int count = this.PropertyWriters.Length;
		if (this.HasConditionalProperties)
		{
			count = 0;
			foreach ((_, ObjectPropertyWriter<T, TEncoder, TDecoder> property) in this.PropertyWriters)
			{
				if (property.ShouldWrite is null || property.ShouldWrite(value))
				{
					count++;
				}
			}
		}

		IReadOnlyDictionary<string, ShapeShiftValue>? extensionData = this.ExtensionData?.GetValues(value);
		if (extensionData is not null)
		{
			foreach (string name in extensionData.Keys)
			{
				foreach ((string declaredName, _) in this.PropertyWriters)
				{
					if (string.Equals(name, declaredName, StringComparison.Ordinal))
					{
						throw new ShapeShiftSerializationException($"Extension property '{name}' conflicts with a declared property on {typeof(T).FullName}.", null, new ShapeShiftPath(name));
					}
				}
			}

			count = checked(count + extensionData.Count);
		}

		encoder.WriteStartMap(count);
		foreach ((string name, ObjectPropertyWriter<T, TEncoder, TDecoder> property) in this.PropertyWriters)
		{
			if (property.ShouldWrite is not null && !property.ShouldWrite(value))
			{
				continue;
			}

			encoder.WritePropertyName(name, property.PreparedName);
			try
			{
				property.Write(ref encoder, in value, context);
			}
			catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(name))
			{
				throw;
			}
			catch (Exception ex) when (SerializationErrors.IsAugmentable(ex))
			{
				throw SerializationErrors.Wrap(ex, name, typeof(T), serializing: true);
			}
		}

		if (extensionData is not null)
		{
			ShapeShiftConverter<ShapeShiftValue, TEncoder, TDecoder> valueConverter = context.GetConverter<ShapeShiftValue>();
			foreach ((string name, ShapeShiftValue extensionValue) in extensionData)
			{
				encoder.WritePropertyName(name);
				try
				{
					valueConverter.Write(ref encoder, extensionValue, context);
				}
				catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement(name))
				{
					throw;
				}
				catch (Exception ex) when (SerializationErrors.IsAugmentable(ex))
				{
					throw SerializationErrors.Wrap(ex, name, typeof(T), serializing: true);
				}
			}
		}

		encoder.WriteEndMap();
		callbacks?.OnAfterSerialize();
	}
}
