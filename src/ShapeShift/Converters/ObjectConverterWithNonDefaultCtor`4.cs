// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

internal class ObjectConverterWithNonDefaultCtor<T, TArgumentState, TEncoder, TDecoder>(Func<TArgumentState> argStateCtor, Constructor<TArgumentState, T> ctor) : ObjectConverter<T, TEncoder, TDecoder>
	where TArgumentState : IArgumentState
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal required Dictionary<string, ObjectPropertyReader<TArgumentState, TEncoder, TDecoder>> PropertyReaders { get; init; }

	internal required IReadOnlyList<IParameterShape> Parameters { get; init; }

	internal required DeserializeDefaultValuesPolicy DefaultValuesPolicy { get; init; }

	public override T? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			return default;
		}

		context.DepthStep();
		TArgumentState argState = argStateCtor();

		decoder.ReadStartMap();
		ulong encounteredKnownProperties = 0;
		HashSet<string>? encounteredOtherProperties = null;
		while (decoder.NextTokenType != TokenType.EndMap)
		{
			ReadOnlySpan<char> propertyName = decoder.ReadPropertyName();

			if (this.PropertyReaders.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(propertyName, out var propertyConverter))
			{
				if (propertyConverter.Index < 64)
				{
					ulong bit = 1UL << propertyConverter.Index;
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
					propertyConverter.Read(ref decoder, ref argState, context);
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
