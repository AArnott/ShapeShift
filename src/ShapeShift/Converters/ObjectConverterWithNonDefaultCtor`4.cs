// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

internal class ObjectConverterWithNonDefaultCtor<T, TArgumentState, TEncoder, TDecoder>(Func<TArgumentState> argStateCtor, Constructor<TArgumentState, T> ctor) : ObjectConverter<T, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal required IReadOnlyDictionary<string, ReadProperty<TArgumentState, TEncoder, TDecoder>> PropertyReaders { get; init; }

	public override T? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			return default;
		}

		context.DepthStep();
		TArgumentState argState = argStateCtor();

		decoder.ReadStartMap();
		while (decoder.NextTokenType != TokenType.EndMap)
		{
			string propertyName = decoder.ReadPropertyName().ToString();
			if (this.PropertyReaders.TryGetValue(propertyName, out var propertyConverter))
			{
				propertyConverter(ref decoder, ref argState, context);
			}
			else
			{
				decoder.Skip();
			}
		}

		decoder.ReadEndMap();

		T value = ctor(ref argState);

		(value as IShapeShiftSerializationCallbacks)?.OnAfterDeserialize();

		return value;
	}
}
