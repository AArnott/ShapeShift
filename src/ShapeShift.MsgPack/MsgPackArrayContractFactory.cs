// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type

namespace ShapeShift.MsgPack;

/// <summary>
/// Supplies positional (array) converters for types that declare <see cref="MsgPackArrayContractAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// This factory is always present on <see cref="MsgPackSerializer.ConverterFactories"/>, and always last, so that
/// a caller's own factories take precedence over it and cannot accidentally remove it.
/// </para>
/// <para>
/// Positional converters are built entirely from PolyType shapes, so nothing beyond attribute lookup is resolved
/// by reflection and the result is safe for trimming and NativeAOT.
/// </para>
/// </remarks>
internal sealed class MsgPackArrayContractFactory : IShapeShiftConverterFactory<MsgPackEncoder, MsgPackDecoder>
{
	/// <inheritdoc/>
	public ShapeShiftConverter<MsgPackEncoder, MsgPackDecoder>? CreateConverter(Type type, ITypeShape? shape, in ConverterContext<MsgPackEncoder, MsgPackDecoder> context)
	{
		if (shape is not IObjectTypeShape objectShape ||
			objectShape.AttributeProvider.GetCustomAttribute<MsgPackArrayContractAttribute>() is null)
		{
			return null;
		}

		MsgPackArrayContractBuilder builder = new(context.SerializeDefaultValues, context.DeserializeDefaultValues);
		return (ShapeShiftConverter<MsgPackEncoder, MsgPackDecoder>)objectShape.Accept(builder)!;
	}
}

/// <summary>
/// Turns a PolyType object shape into a positional MessagePack converter.
/// </summary>
/// <param name="serializeDefaultValues">The policy that decides whether trailing default values may be elided.</param>
/// <param name="deserializeDefaultValues">The policy that decides how strictly missing and null values are rejected.</param>
internal sealed class MsgPackArrayContractBuilder(SerializeDefaultValuesPolicy serializeDefaultValues, DeserializeDefaultValuesPolicy deserializeDefaultValues) : TypeShapeVisitor
{
	/// <inheritdoc/>
	public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
		=> objectShape.Constructor is IConstructorShape constructorShape
			? constructorShape.Accept(this, state)
			: throw new ShapeShiftSerializationException($"{typeof(T).FullName} declares [{nameof(MsgPackArrayContractAttribute)}] but cannot be constructed, so it has no positional contract.");

	/// <inheritdoc/>
	public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null)
	{
		IObjectTypeShape<TDeclaringType> objectShape = constructorShape.DeclaringType;
		bool parameterized = constructorShape.Parameters.Count > 0;
		Dictionary<string, IParameterShape> parametersByName = constructorShape.Parameters.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
		Dictionary<string, IPropertyShape> propertiesByName = new(StringComparer.OrdinalIgnoreCase);
		foreach (IPropertyShape property in objectShape.Properties)
		{
			propertiesByName[property.Name] = property;
		}

		Dictionary<int, MsgPackArrayWriteSlot<TDeclaringType>> writers = new();
		Dictionary<int, MsgPackArrayReadSlot<TDeclaringType>> propertyReaders = new();
		Dictionary<int, MsgPackArraySlotDescription> descriptions = new();
		Dictionary<int, string> claimedBy = new();
		int highestPosition = -1;

		foreach (IPropertyShape property in objectShape.Properties)
		{
			if (property.AttributeProvider.GetCustomAttribute<ShapeShiftExtensionDataAttribute>() is not null)
			{
				throw new ShapeShiftSerializationException(
					$"{typeof(TDeclaringType).FullName} declares [{nameof(MsgPackArrayContractAttribute)}] and an extension-data member. A positional contract has no property names under which unrecognized data could be retained.");
			}

			RejectMemberConverterAttribute(property.AttributeProvider, property.Name, typeof(TDeclaringType));
			parametersByName.TryGetValue(property.Name, out IParameterShape? matchingParameter);
			int position = ResolvePosition(property.Name, property.AttributeProvider, matchingParameter?.AttributeProvider, typeof(TDeclaringType), claimedBy);
			highestPosition = Math.Max(highestPosition, position);

			var member = (MsgPackArrayMember<TDeclaringType, TDeclaringType>)property.Accept(this, matchingParameter)!;
			descriptions[position] = member.Description with { Position = position };
			if (member.Write is not null)
			{
				writers[position] = new MsgPackArrayWriteSlot<TDeclaringType>(property.Name, member.Write, member.ShouldWrite);
			}

			if (!parameterized && member.Read is not null)
			{
				propertyReaders[position] = new MsgPackArrayReadSlot<TDeclaringType>(property.Name, member.Read);
			}
		}

		if (!parameterized)
		{
			return new MsgPackArrayObjectConverterWithDefaultCtor<TDeclaringType>(constructorShape.GetDefaultConstructor())
			{
				WriteSlots = ToSlots(writers, highestPosition),
				ReadSlots = ToSlots(propertyReaders, highestPosition),
				Descriptions = ToSlots(descriptions, highestPosition),
			};
		}

		Dictionary<int, MsgPackArrayReadSlot<TArgumentState>> parameterReaders = new();
		foreach (IParameterShape parameter in constructorShape.Parameters)
		{
			RejectMemberConverterAttribute(parameter.AttributeProvider, parameter.Name, typeof(TDeclaringType));
			propertiesByName.TryGetValue(parameter.Name, out IPropertyShape? matchingProperty);

			// A parameter that has a matching property shares that property's position, which the loop above
			// already claimed; only a parameter with no property of its own claims a position here.
			int position = ResolvePosition(parameter.Name, parameter.AttributeProvider, matchingProperty?.AttributeProvider, typeof(TDeclaringType), matchingProperty is null ? claimedBy : null);
			highestPosition = Math.Max(highestPosition, position);

			var reader = (ReadArrayElement<TArgumentState>)parameter.Accept(this, parameter)!;
			parameterReaders[position] = new MsgPackArrayReadSlot<TArgumentState>(parameter.Name, reader);
			descriptions[position] = descriptions.TryGetValue(position, out MsgPackArraySlotDescription? description)
				? description with { IsRequired = parameter.IsRequired, IsWritable = true }
				: new MsgPackArraySlotDescription(position, parameter.Name, parameter.ParameterType)
				{
					IsRequired = parameter.IsRequired,
					IsNullable = !parameter.IsNonNullable,
					IsReadable = false,
				};
		}

		return new MsgPackArrayObjectConverterWithCtor<TDeclaringType, TArgumentState>(constructorShape.GetArgumentStateConstructor(), constructorShape.GetParameterizedConstructor())
		{
			WriteSlots = ToSlots(writers, highestPosition),
			ReadSlots = ToSlots(parameterReaders, highestPosition),
			Descriptions = ToSlots(descriptions, highestPosition),
			Parameters = constructorShape.Parameters,
			DefaultValuesPolicy = deserializeDefaultValues,
		};
	}

	/// <inheritdoc/>
	public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
	{
		IParameterShape? parameterShape = state as IParameterShape;
		LazyConverter<TPropertyType> converter = new(propertyShape.PropertyType);

		Getter<TDeclaringType, TPropertyType>? getter = propertyShape.HasGetter ? propertyShape.GetGetter() : null;
		Setter<TDeclaringType, TPropertyType>? setter = propertyShape.HasSetter ? propertyShape.GetSetter() : null;
		bool required = parameterShape?.IsRequired is true;

		// A required member is never elided, even at the tail: a shorter array would leave a reader unable to
		// reconstruct the object at all.
		ShouldWriteArrayElement<TDeclaringType>? shouldWrite = null;
		if (getter is not null && serializeDefaultValues != SerializeDefaultValuesPolicy.Always && !required)
		{
			bool includeByPolicy = typeof(TPropertyType).IsValueType
				? (serializeDefaultValues & SerializeDefaultValuesPolicy.ValueTypes) != 0
				: (serializeDefaultValues & SerializeDefaultValuesPolicy.ReferenceTypes) != 0;
			if (!includeByPolicy)
			{
				EqualityComparer<TPropertyType> comparer = EqualityComparer<TPropertyType>.Default;
				TPropertyType? defaultValue = parameterShape?.HasDefaultValue is true ? (TPropertyType?)parameterShape.DefaultValue : default;
				shouldWrite = (in TDeclaringType target) => !comparer.Equals(getter(ref Unsafe.AsRef(in target)), defaultValue!);
			}
		}

		bool rejectNull = (deserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties) == 0
			&& ((!propertyShape.HasGetter || propertyShape.IsGetterNonNullable)
				&& (!propertyShape.HasSetter || propertyShape.IsSetterNonNullable)
				&& (parameterShape is null || parameterShape.IsNonNullable));

		return new MsgPackArrayMember<TDeclaringType, TDeclaringType>
		{
			Write = getter is null ? null : (ref MsgPackEncoder encoder, in TDeclaringType target, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
				=> converter.Get(context).Write(ref encoder, getter(ref Unsafe.AsRef(in target)), context),
			Read = setter is null ? null : Read,
			ShouldWrite = shouldWrite,
			Description = new MsgPackArraySlotDescription(0, propertyShape.Name, propertyShape.PropertyType)
			{
				IsRequired = required,
				IsNullable = !rejectNull,
				IsReadable = getter is not null,
				IsWritable = setter is not null,
				IsAlwaysWritten = shouldWrite is null,
			},
		};

		void Read(ref MsgPackDecoder decoder, ref TDeclaringType target, SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
		{
			TPropertyType? value = converter.Get(context).Read(ref decoder, context);
			if (rejectNull && value is null)
			{
				throw new ShapeShiftSerializationException($"Cannot assign null to non-nullable property '{propertyShape.Name}' on {typeof(TDeclaringType).FullName}.");
			}

			setter!(ref target, value!);
		}
	}

	/// <inheritdoc/>
	public override object? VisitParameter<TArgumentState, TParameterType>(IParameterShape<TArgumentState, TParameterType> parameterShape, object? state = null)
	{
		LazyConverter<TParameterType> converter = new(parameterShape.ParameterType);
		Setter<TArgumentState, TParameterType> setter = parameterShape.GetSetter();
		bool rejectNull = (deserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties) == 0 && parameterShape.IsNonNullable;

		return new ReadArrayElement<TArgumentState>((ref MsgPackDecoder decoder, ref TArgumentState argumentState, SerializationContext<MsgPackEncoder, MsgPackDecoder> context) =>
		{
			TParameterType? value = converter.Get(context).Read(ref decoder, context);
			if (rejectNull && value is null)
			{
				throw new ShapeShiftSerializationException($"Cannot assign null to non-nullable parameter '{parameterShape.Name}'.");
			}

			setter(ref argumentState, value!);
		});
	}

	private static ImmutableArray<TSlot?> ToSlots<TSlot>(Dictionary<int, TSlot> byPosition, int highestPosition)
		where TSlot : class
	{
		TSlot?[] slots = new TSlot?[highestPosition + 1];
		foreach ((int position, TSlot slot) in byPosition)
		{
			slots[position] = slot;
		}

		return [.. slots];
	}

	private static void RejectMemberConverterAttribute(IGenericCustomAttributeProvider attributeProvider, string memberName, Type declaringType)
	{
		if (attributeProvider.GetCustomAttribute<ShapeShiftConverterAttribute>() is not null)
		{
			throw new ShapeShiftSerializationException(
				$"'{memberName}' on {declaringType.FullName} applies [{nameof(ShapeShiftConverterAttribute)}] to a member of a positional MessagePack contract, which is not supported. Apply the converter to the member's type instead, or register it with the serializer.");
		}
	}

	private static int? GetPosition(IGenericCustomAttributeProvider? attributeProvider)
		=> attributeProvider?.GetCustomAttribute<MsgPackKeyAttribute>()?.Index;

	private static int ResolvePosition(string memberName, IGenericCustomAttributeProvider primary, IGenericCustomAttributeProvider? secondary, Type declaringType, Dictionary<int, string>? claimedBy)
	{
		int? fromPrimary = GetPosition(primary);
		int? fromSecondary = GetPosition(secondary);
		if (fromPrimary is int a && fromSecondary is int b && a != b)
		{
			throw new ShapeShiftSerializationException($"'{memberName}' on {declaringType.FullName} declares conflicting [{nameof(MsgPackKeyAttribute)}] positions {a} and {b}.");
		}

		int position = fromPrimary ?? fromSecondary ?? throw new ShapeShiftSerializationException(
			$"'{memberName}' on {declaringType.FullName} has no [{nameof(MsgPackKeyAttribute)}]. Every member of a positional MessagePack contract needs an explicit, permanent position.");

		if (position < 0 || position > MsgPackKeyAttribute.MaxIndex)
		{
			throw new ShapeShiftSerializationException($"'{memberName}' on {declaringType.FullName} declares position {position}, which is outside the supported range of 0 to {MsgPackKeyAttribute.MaxIndex}.");
		}

		if (claimedBy is not null)
		{
			if (claimedBy.TryGetValue(position, out string? other))
			{
				throw new ShapeShiftSerializationException($"'{memberName}' and '{other}' on {declaringType.FullName} both declare position {position}. Positions must be unique.");
			}

			claimedBy.Add(position, memberName);
		}

		return position;
	}

	/// <summary>
	/// Resolves (once) and then remembers the converter for one member's type.
	/// </summary>
	/// <typeparam name="TValue">The member's type.</typeparam>
	/// <param name="shape">The shape of <typeparamref name="TValue"/>.</param>
	/// <remarks>
	/// Resolution is deferred to the first (de)serialization rather than performed while this converter is being
	/// built, so that a type which refers to itself does not recurse through the converter cache before the cache
	/// has an entry to hand back. Because a converter is cached per serializer configuration, the remembered
	/// converter is always the right one for every operation that reaches it.
	/// </remarks>
	private sealed class LazyConverter<TValue>(ITypeShape<TValue> shape)
	{
		private ShapeShiftConverter<TValue, MsgPackEncoder, MsgPackDecoder>? converter;

		internal ShapeShiftConverter<TValue, MsgPackEncoder, MsgPackDecoder> Get(SerializationContext<MsgPackEncoder, MsgPackDecoder> context)
			=> this.converter ??= context.GetConverter(shape);
	}
}
