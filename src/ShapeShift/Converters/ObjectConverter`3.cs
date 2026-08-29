// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

internal abstract class ObjectConverter<T, TEncoder, TDecoder> : ShapeShiftConverter<T, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal required IReadOnlyDictionary<string, ObjectPropertyWriter<T, TEncoder, TDecoder>> PropertyWriters { get; init; }

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

		int count = 0;
		foreach (ObjectPropertyWriter<T, TEncoder, TDecoder> property in this.PropertyWriters.Values)
		{
			if (property.ShouldWrite is null || property.ShouldWrite(value))
			{
				count++;
			}
		}

		encoder.WriteStartMap(count);
		foreach ((string name, ObjectPropertyWriter<T, TEncoder, TDecoder> property) in this.PropertyWriters)
		{
			if (property.ShouldWrite is not null && !property.ShouldWrite(value))
			{
				continue;
			}

			encoder.WritePropertyName(name);
			property.Write(ref encoder, in value, context);
		}

		encoder.WriteEndMap();
		callbacks?.OnAfterSerialize();
	}
}
