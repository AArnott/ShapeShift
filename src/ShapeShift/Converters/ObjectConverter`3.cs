// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

internal abstract class ObjectConverter<T, TEncoder, TDecoder> : ShapeShiftConverter<T, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal required IReadOnlyDictionary<string, WriteProperty<T, TEncoder, TDecoder>> PropertyWriters { get; init; }

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

		encoder.WriteStartMap(this.PropertyWriters.Count);
		foreach ((string name, var propertyWriter) in this.PropertyWriters)
		{
			encoder.WritePropertyName(name);
			propertyWriter(ref encoder, in value, context);
		}

		encoder.WriteEndMap();
		callbacks?.OnAfterSerialize();
	}
}
