// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

internal class ObjectConverterWithNonDefaultCtor<T, TArgumentState, TEncoder, TDecoder>(Func<TArgumentState> argStateCtor, Constructor<TArgumentState, T> ctor) : ObjectConverter<T, TEncoder, TDecoder>
	where TArgumentState : IArgumentState
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal required IReadOnlyDictionary<string, ReadProperty<TArgumentState, TEncoder, TDecoder>> PropertyReaders { get; init; }

	internal required IReadOnlyList<IParameterShape> Parameters { get; init; }

	internal required DeserializeDefaultValuesPolicy DefaultValuesPolicy { get; init; }

	public override T? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			decoder.ReadNull();
			return default;
		}

		context.DepthStep();
		TArgumentState argState = argStateCtor();

		decoder.ReadStartMap();
		HashSet<string> encounteredProperties = new(StringComparer.Ordinal);
		while (decoder.NextTokenType != TokenType.EndMap)
		{
			string propertyName = decoder.ReadPropertyName().ToString();
			if (!encounteredProperties.Add(propertyName))
			{
				throw new ShapeShiftSerializationException($"Property '{propertyName}' appears more than once while deserializing {typeof(T).FullName}.");
			}

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

		if ((this.DefaultValuesPolicy & DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties) == 0 && !argState.AreRequiredArgumentsSet)
		{
			List<string> missing = [];
			foreach (IParameterShape parameter in this.Parameters)
			{
				if (parameter.IsRequired && !argState.IsArgumentSet(parameter.Position))
				{
					missing.Add(parameter.Name);
				}
			}

			throw new ShapeShiftSerializationException($"Missing required properties: {string.Join(", ", missing)}.");
		}

		T value = ctor(ref argState);

		(value as IShapeShiftSerializationCallbacks)?.OnAfterDeserialize();

		return value;
	}
}
