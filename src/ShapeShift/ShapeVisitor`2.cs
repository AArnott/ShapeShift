// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using ShapeShift.Converters;

namespace ShapeShift;

/// <summary>
/// A visitor that prepares type converters.
/// </summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
internal class ShapeVisitor<TEncoder, TDecoder> : TypeShapeVisitor, ITypeShapeFunc
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private static readonly InterningStringConverter<TEncoder, TDecoder> InterningStringConverter = new();
	private static readonly ShapeShiftConverter<string, TEncoder, TDecoder> ReferencePreservingInterningStringConverter = InterningStringConverter.WrapWithReferencePreservation();

	private readonly ConverterCache<TEncoder, TDecoder> owner;
	private readonly TypeGenerationContext context;

	/// <summary>
	/// Initializes a new instance of the <see cref="ShapeVisitor{TEncoder, TDecoder}"/> class.
	/// </summary>
	/// <param name="owner">The serializer that created this instance. Usable for obtaining settings that may influence the generated converter.</param>
	/// <param name="context">Context for a generation of a particular data model.</param>
	internal ShapeVisitor(ConverterCache<TEncoder, TDecoder> owner, TypeGenerationContext context)
	{
		this.owner = owner;
		this.context = context;
		this.OutwardVisitor = this;
	}

	/// <summary>
	/// Gets or sets the visitor that will be used to generate converters for new types that are encountered.
	/// </summary>
	/// <value>Defaults to <see langword="this" />.</value>
	/// <remarks>
	/// This may be changed to a wrapping visitor implementation to implement features such as reference preservation.
	/// </remarks>
	internal TypeShapeVisitor OutwardVisitor { get; set; }

	/// <inheritdoc/>
	object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state)
	{
		object? result = typeShape.Accept(this.OutwardVisitor, state);
		Debug.Assert(result is null or ConverterResult<TEncoder, TDecoder>, $"We should not be returning raw converters, but we got one from {typeShape}.");
		return result;
	}

	public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
	{
		if (this.TryGetCustomOrPrimitiveConverter(objectShape, objectShape.AttributeProvider, out ConverterResult<TEncoder, TDecoder>? customConverter))
		{
			return customConverter;
		}

		return objectShape.Constructor?.Accept(this) ?? ConverterResult<TEncoder, TDecoder>.Err($"Unconstructable type: {typeof(T).FullName}");
	}

	public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null)
	{
		IObjectTypeShape<TDeclaringType> objectShape = constructorShape.DeclaringType;
		ObjectConverter<TDeclaringType, TEncoder, TDecoder> converter;
		if (constructorShape.Parameters is [])
		{
			Dictionary<string, ObjectPropertyReader<TDeclaringType, TEncoder, TDecoder>> propertyReaders = new(objectShape.Properties.Count, StringComparer.Ordinal);
			Dictionary<string, ObjectPropertyWriter<TDeclaringType, TEncoder, TDecoder>> propertyWriters = new(objectShape.Properties.Count);
			ExtensionDataProperty<TDeclaringType, TEncoder, TDecoder>? extensionData = null;
			foreach (var property in objectShape.Properties)
			{
				if (property.AttributeProvider.GetCustomAttribute<ShapeShiftExtensionDataAttribute>() is not null)
				{
					if (extensionData is not null)
					{
						throw new ShapeShiftSerializationException($"{typeof(TDeclaringType).FullName} declares more than one extension-data member.");
					}

					extensionData = (ExtensionDataProperty<TDeclaringType, TEncoder, TDecoder>)property.Accept(this, ExtensionDataMarker.Instance)!;
					continue;
				}

				string name = this.owner.GetSerializedPropertyName(property.Name, property.AttributeProvider);
				var converters = (PropertyConverter<TDeclaringType, TEncoder, TDecoder>)property.Accept(this)!;
				if (converters.Read is not null)
				{
					propertyReaders.Add(name, new(converters.Read, propertyReaders.Count));
				}

				if (converters.Write is not null)
				{
					propertyWriters.Add(name, new(converters.Write, converters.ShouldWrite));
				}
			}

			converter = new ObjectConverterWithDefaultCtor<TDeclaringType, TEncoder, TDecoder>(constructorShape.GetDefaultConstructor())
			{
				PropertyReaders = propertyReaders,
				PropertyWriters = propertyWriters,
				ExtensionData = extensionData,
			};
		}
		else
		{
			Dictionary<string, ObjectPropertyReader<TArgumentState, TEncoder, TDecoder>> propertyReaders = new(constructorShape.Parameters.Count, StringComparer.Ordinal);
			Dictionary<string, ObjectPropertyWriter<TDeclaringType, TEncoder, TDecoder>> propertyWriters = new(objectShape.Properties.Count);
			Dictionary<string, IParameterShape> parametersByName = constructorShape.Parameters.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
			foreach (var property in objectShape.Properties)
			{
				if (property.AttributeProvider.GetCustomAttribute<ShapeShiftExtensionDataAttribute>() is not null)
				{
					throw new ShapeShiftSerializationException($"Extension data on {typeof(TDeclaringType).FullName} requires a parameterless deserialization constructor.");
				}

				string name = this.owner.GetSerializedPropertyName(property.Name, property.AttributeProvider);
				parametersByName.TryGetValue(property.Name, out IParameterShape? matchingParameter);
				var converters = (PropertyConverter<TDeclaringType, TEncoder, TDecoder>)property.Accept(this, matchingParameter)!;
				if (converters.Write is not null)
				{
					propertyWriters.Add(name, new(converters.Write, converters.ShouldWrite));
				}
			}

			foreach (var parameter in constructorShape.Parameters)
			{
				string name = this.owner.GetSerializedPropertyName(parameter.Name, parameter.AttributeProvider);
				var propertyReader = (ReadProperty<TArgumentState, TEncoder, TDecoder>)parameter.Accept(this, parameter)!;
				propertyReaders.Add(name, new(propertyReader, propertyReaders.Count));
			}

			converter = new ObjectConverterWithNonDefaultCtor<TDeclaringType, TArgumentState, TEncoder, TDecoder>(constructorShape.GetArgumentStateConstructor(), constructorShape.GetParameterizedConstructor())
			{
				PropertyReaders = propertyReaders,
				PropertyWriters = propertyWriters,
				Parameters = constructorShape.Parameters,
				DefaultValuesPolicy = this.owner.DeserializeDefaultValues,
			};
		}

		return ConverterResult.Ok(converter);
	}

	/// <inheritdoc/>
	public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null)
	{
		ConverterResult<TEncoder, TDecoder> baseConverter = (ConverterResult<TEncoder, TDecoder>)unionShape.BaseType.Accept(this)!;
		if (baseConverter.TryPrepareFailPath("union base type", out ConverterResult<TEncoder, TDecoder>? failure))
		{
			return failure;
		}

		List<UnionCase<TUnion, TEncoder, TDecoder>> cases = new(unionShape.UnionCases.Count);
		foreach (IUnionCaseShape unionCase in unionShape.UnionCases)
		{
			ConverterResult<TEncoder, TDecoder> caseConverter = (ConverterResult<TEncoder, TDecoder>)unionCase.Accept(this)!;
			if (caseConverter.TryPrepareFailPath(unionCase, out failure))
			{
				return failure;
			}

			cases.Add(new(
				unionCase.Name,
				unionCase.Tag,
				unionCase.IsTagSpecified,
				(ShapeShiftConverter<TUnion, TEncoder, TDecoder>)caseConverter.ValueOrThrow));
		}

		return ConverterResult.Ok(new UnionConverter<TUnion, TEncoder, TDecoder>(
			(ShapeShiftConverter<TUnion, TEncoder, TDecoder>)baseConverter.ValueOrThrow,
			unionShape.GetGetUnionCaseIndex(),
			cases));
	}

	/// <inheritdoc/>
	public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state = null)
	{
		ConverterResult<TEncoder, TDecoder> caseConverter = (ConverterResult<TEncoder, TDecoder>)unionCaseShape.UnionCaseType.Accept(this)!;
		if (caseConverter.TryPrepareFailPath(unionCaseShape, out ConverterResult<TEncoder, TDecoder>? failure))
		{
			return failure;
		}

		return ConverterResult.Ok(new UnionCaseConverter<TUnionCase, TUnion, TEncoder, TDecoder>(
			(ShapeShiftConverter<TUnionCase, TEncoder, TDecoder>)caseConverter.ValueOrThrow,
			unionCaseShape.Marshaler));
	}

	public override object? VisitParameter<TArgumentState, TParameterType>(IParameterShape<TArgumentState, TParameterType> parameterShape, object? state = null)
	{
		ConverterResult<TEncoder, TDecoder> converter = this.GetConverterForMemberOrParameter(parameterShape.ParameterType, parameterShape.AttributeProvider);

		Setter<TArgumentState, TParameterType> setter = parameterShape.GetSetter();

		return new ReadProperty<TArgumentState, TEncoder, TDecoder>((ref decoder, ref argumentState, context) => setter(ref argumentState, ((ShapeShiftConverter<TParameterType, TEncoder, TDecoder>)converter.ValueOrThrow).Read(ref decoder, context)!));
	}

	public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
	{
		if (ReferenceEquals(state, ExtensionDataMarker.Instance))
		{
			return this.CreateExtensionDataProperty(propertyShape);
		}

		IParameterShape? parameterShape = state as IParameterShape;
		ConverterResult<TEncoder, TDecoder> converter = this.GetConverterForMemberOrParameter(propertyShape.PropertyType, propertyShape.AttributeProvider);

		Getter<TDeclaringType, TPropertyType>? getter = propertyShape.HasGetter ? propertyShape.GetGetter() : null;
		Setter<TDeclaringType, TPropertyType>? setter = propertyShape.HasSetter ? propertyShape.GetSetter() : null;
		ShouldWriteProperty<TDeclaringType>? shouldWrite = null;
		if (getter is not null && this.owner.SerializeDefaultValues != SerializeDefaultValuesPolicy.Always)
		{
			bool required = parameterShape?.IsRequired is true;
			bool includeByPolicy = (required && (this.owner.SerializeDefaultValues & SerializeDefaultValuesPolicy.Required) != 0)
				|| (typeof(TPropertyType).IsValueType
					? (this.owner.SerializeDefaultValues & SerializeDefaultValuesPolicy.ValueTypes) != 0
					: (this.owner.SerializeDefaultValues & SerializeDefaultValuesPolicy.ReferenceTypes) != 0);
			if (!includeByPolicy)
			{
				EqualityComparer<TPropertyType> comparer = EqualityComparer<TPropertyType>.Default;
				TPropertyType? defaultValue = parameterShape?.HasDefaultValue is true ? (TPropertyType?)parameterShape.DefaultValue : default;
				shouldWrite = (in TDeclaringType target) => !comparer.Equals(getter(ref Unsafe.AsRef(in target)), defaultValue!);
			}
		}

		bool rejectNull = (this.owner.DeserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties) == 0
			&& ((!propertyShape.HasGetter || propertyShape.IsGetterNonNullable)
				&& (!propertyShape.HasSetter || propertyShape.IsSetterNonNullable)
				&& (parameterShape is null || parameterShape.IsNonNullable));
		return new PropertyConverter<TDeclaringType, TEncoder, TDecoder>
		{
			Write = getter is null ? null : (ref encoder, in target, context) => ((ShapeShiftConverter<TPropertyType, TEncoder, TDecoder>)converter.ValueOrThrow).Write(ref encoder, getter(ref Unsafe.AsRef(in target)), context),
			Read = setter is null ? null : Read,
			ShouldWrite = shouldWrite,
		};

		void Read(ref TDecoder decoder, ref TDeclaringType target, SerializationContext<TEncoder, TDecoder> context)
		{
			TPropertyType? value = ((ShapeShiftConverter<TPropertyType, TEncoder, TDecoder>)converter.ValueOrThrow).Read(ref decoder, context);
			if (rejectNull && value is null)
			{
				throw new ShapeShiftSerializationException($"Cannot assign null to non-nullable property '{propertyShape.Name}' on {typeof(TDeclaringType).FullName}.");
			}

			setter!(ref target, value!);
		}
	}

	/// <inheritdoc/>
	public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state = null)
	{
		ConverterResult<TEncoder, TDecoder> elementConverter = this.GetConverter(optionalShape.ElementType);
		if (elementConverter.TryPrepareFailPath(optionalShape, out ConverterResult<TEncoder, TDecoder>? failure))
		{
			return failure;
		}

		return ConverterResult.Ok(new OptionalConverter<TOptional, TElement, TEncoder, TDecoder>(
			(ShapeShiftConverter<TElement, TEncoder, TDecoder>)elementConverter.ValueOrThrow,
			optionalShape.GetDeconstructor(),
			optionalShape.GetNoneConstructor(),
			optionalShape.GetSomeConstructor()));
	}

	/// <inheritdoc/>
	public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null)
	{
		if (this.TryGetCustomOrPrimitiveConverter(dictionaryShape, dictionaryShape.AttributeProvider, out ConverterResult<TEncoder, TDecoder>? customConverter))
		{
			return customConverter;
		}

		ConverterResult<TEncoder, TDecoder> keyConverter = this.GetConverter(dictionaryShape.KeyType);
		if (keyConverter.TryPrepareFailPath("key", out ConverterResult<TEncoder, TDecoder>? keyFailure))
		{
			return keyFailure;
		}

		ConverterResult<TEncoder, TDecoder> valueConverter = this.GetConverter(dictionaryShape.ValueType);
		if (valueConverter.TryPrepareFailPath("value", out ConverterResult<TEncoder, TDecoder>? valueFailure))
		{
			return valueFailure;
		}

		return ConverterResult.Ok(new DictionaryConverter<TDictionary, TKey, TValue, TEncoder, TDecoder>(
			dictionaryShape,
			(ShapeShiftConverter<TKey, TEncoder, TDecoder>)keyConverter.ValueOrThrow,
			(ShapeShiftConverter<TValue, TEncoder, TDecoder>)valueConverter.ValueOrThrow));
	}

	public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null)
	{
		if (this.TryGetCustomOrPrimitiveConverter(enumerableShape, enumerableShape.AttributeProvider, out ConverterResult<TEncoder, TDecoder>? customConverter))
		{
			return customConverter;
		}

		var elementConverter = this.GetConverter(enumerableShape.ElementType);
		if (enumerableShape.Type.IsArray && enumerableShape.Rank > 1)
		{
			return ConverterResult.Ok(new MultidimensionalArrayConverter<TEnumerable, TElement, TEncoder, TDecoder>(
				(ShapeShiftConverter<TElement, TEncoder, TDecoder>)elementConverter.ValueOrThrow,
				enumerableShape.Rank));
		}

		return ConverterResult.Ok(new EnumerableConverter<TEnumerable, TElement, TEncoder, TDecoder>(enumerableShape, (ShapeShiftConverter<TElement, TEncoder, TDecoder>)elementConverter.ValueOrThrow));
	}

	/// <inheritdoc/>
	public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null)
	{
		if (this.TryGetCustomOrPrimitiveConverter(enumShape, enumShape.AttributeProvider, out ConverterResult<TEncoder, TDecoder>? customConverter))
		{
			return customConverter;
		}

		ConverterResult<TEncoder, TDecoder> underlyingConverter = this.GetConverter(enumShape.UnderlyingType);
		if (underlyingConverter.TryPrepareFailPath(enumShape, out ConverterResult<TEncoder, TDecoder>? failure))
		{
			return failure;
		}

		return ConverterResult.Ok(new EnumConverter<TEnum, TUnderlying, TEncoder, TDecoder>(
			(ShapeShiftConverter<TUnderlying, TEncoder, TDecoder>)underlyingConverter.ValueOrThrow,
			enumShape.Members,
			this.owner.SerializeEnumValuesByName));
	}

	/// <inheritdoc/>
	public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
	{
		if (this.TryGetCustomOrPrimitiveConverter<T>(surrogateShape.Type, null, surrogateShape.Provider, surrogateShape.AttributeProvider, out ConverterResult<TEncoder, TDecoder>? customConverter))
		{
			return customConverter;
		}

		ConverterResult<TEncoder, TDecoder> surrogateConverter = this.GetConverter(surrogateShape.SurrogateType, state: state);
		if (surrogateConverter.TryPrepareFailPath(surrogateShape, out ConverterResult<TEncoder, TDecoder>? failure))
		{
			return failure;
		}

		return ConverterResult.Ok(new SurrogateConverter<T, TSurrogate, TEncoder, TDecoder>(
			surrogateShape,
			(ShapeShiftConverter<TSurrogate, TEncoder, TDecoder>)surrogateConverter.ValueOrThrow));
	}

	/// <inheritdoc/>
	public override object? VisitFunction<TFunction, TArgumentState, TResult>(IFunctionTypeShape<TFunction, TArgumentState, TResult> functionShape, object? state = null)
		=> ConverterResult<TEncoder, TDecoder>.Err("Delegate types cannot be serialized.");

	/// <summary>
	/// Gets or creates a converter for the given type shape.
	/// </summary>
	/// <param name="shape">The type shape.</param>
	/// <param name="memberAttributes">
	/// The attribute provider on the member that requires this converter.
	/// This is used to look for <see cref="UseComparerAttribute"/> which may customize the converter we return.
	/// </param>
	/// <param name="state">An optional state object to pass to the converter.</param>
	/// <returns>The converter.</returns>
	/// <remarks>
	/// This is the main entry point for getting converters on behalf of other functions,
	/// e.g. converting the key or value in a dictionary.
	/// It does <em>not</em> take <see cref="ShapeShiftConverterAttribute"/> into account
	/// if it were to appear in <paramref name="memberAttributes"/>.
	/// Callers that want to respect that attribute must call <see cref="TryGetConverterFromAttribute"/> first.
	/// </remarks>
	protected ConverterResult<TEncoder, TDecoder> GetConverter(ITypeShape shape, IGenericCustomAttributeProvider? memberAttributes = null, object? state = null)
	{
		if (memberAttributes is not null)
		{
			if (state is not null)
			{
				throw new ArgumentException("Providing both attributes and state are not supported because we reuse the state parameter for attribute influence.");
			}

			if (memberAttributes.GetCustomAttribute<UseComparerAttribute>() is { } attribute)
			{
				MemberConverterInfluence memberInfluence = new()
				{
					ComparerSource = attribute.ComparerType,
					ComparerSourceMemberName = attribute.MemberName,
				};

				// PERF: Ideally, we can store and retrieve member influenced converters
				// just like we do for non-member influenced ones.
				// We'd probably use a separate dictionary dedicated to member-influenced converters.
				return (ConverterResult<TEncoder, TDecoder>)shape.Accept(this.OutwardVisitor, memberInfluence)!;
			}
		}

		return (ConverterResult<TEncoder, TDecoder>)this.context.GetOrAdd(shape, state)!;
	}

	private ExtensionDataProperty<TDeclaringType, TEncoder, TDecoder> CreateExtensionDataProperty<TDeclaringType, TPropertyType>(
		IPropertyShape<TDeclaringType, TPropertyType> propertyShape)
	{
		if (typeof(TPropertyType) != typeof(Dictionary<string, ShapeShiftValue>))
		{
			throw new ShapeShiftSerializationException($"Extension-data member '{propertyShape.Name}' on {typeof(TDeclaringType).FullName} must have type Dictionary<string, ShapeShiftValue>.");
		}

		if (!propertyShape.HasGetter)
		{
			throw new ShapeShiftSerializationException($"Extension-data member '{propertyShape.Name}' on {typeof(TDeclaringType).FullName} must have a getter.");
		}

		Getter<TDeclaringType, TPropertyType> getter = propertyShape.GetGetter();
		Setter<TDeclaringType, TPropertyType>? setter = propertyShape.HasSetter ? propertyShape.GetSetter() : null;
		return new(
			target => (Dictionary<string, ShapeShiftValue>?)(object?)getter(ref target),
			Read);

		void Read(ref TDecoder decoder, ref TDeclaringType target, string propertyName, SerializationContext<TEncoder, TDecoder> context)
		{
			Dictionary<string, ShapeShiftValue>? values = (Dictionary<string, ShapeShiftValue>?)(object?)getter(ref target);
			if (values is null)
			{
				if (setter is null)
				{
					throw new ShapeShiftSerializationException($"Extension-data member '{propertyShape.Name}' on {typeof(TDeclaringType).FullName} returned null and has no setter.");
				}

				values = new(StringComparer.Ordinal);
				TPropertyType propertyValue = (TPropertyType)(object)values;
				setter(ref target, propertyValue);
			}

			values.Add(propertyName, context.GetConverter<ShapeShiftValue>().Read(ref decoder, context)!);
		}
	}

	private bool TryGetCustomOrPrimitiveConverter<T>(ITypeShape<T> typeShape, IGenericCustomAttributeProvider attributeProvider, [NotNullWhen(true)] out ConverterResult<TEncoder, TDecoder>? converter)
		=> this.TryGetCustomOrPrimitiveConverter(typeShape.Type, typeShape, typeShape.Provider, attributeProvider, out converter);

	/// <summary>
	/// Retrieves a converter for the given type shape from runtime-supplied user sources, primitive converters, or attribute-specified converters.
	/// </summary>
	/// <param name="type">The type to be converted.</param>
	/// <param name="typeShape">The shape for the type to be converted.</param>
	/// <param name="shapeProvider">The shape provider used for this conversion overall (which may not have a shape available if <paramref name="typeShape" /> is <see langword="null" />).</param>
	/// <param name="attributeProvider"><inheritdoc cref="TryGetConverterFromAttribute" path="/param[@name='attributeProvider']"/></param>
	/// <param name="converter">Receives the converter if one is found.</param>
	/// <returns>A value indicating whether a match was found.</returns>
	private bool TryGetCustomOrPrimitiveConverter<T>(Type type, ITypeShape<T>? typeShape, ITypeShapeProvider shapeProvider, IGenericCustomAttributeProvider attributeProvider, [NotNullWhen(true)] out ConverterResult<TEncoder, TDecoder>? converter)
	{
		// Check if the type has a custom converter.
		if (this.owner.TryGetRuntimeProfferedConverter(type, typeShape, shapeProvider, out ShapeShiftConverter<TEncoder, TDecoder>? proferredConverter))
		{
			converter = ConverterResult.Ok(proferredConverter);
			return true;
		}

		if (this.owner.InternStrings && type == typeof(string))
		{
			converter = ConverterResult.Ok((ShapeShiftConverter<TEncoder, TDecoder>)(object)(this.owner.PreserveReferences != ReferencePreservationMode.Off ? ReferencePreservingInterningStringConverter : InterningStringConverter));
			return true;
		}

		if (type == typeof(ShapeShiftValue))
		{
			converter = ConverterResult.Ok((ShapeShiftConverter<TEncoder, TDecoder>)(object)new ShapeShiftValueConverter<TEncoder, TDecoder>());
			return true;
		}

		// Check if the type has a built-in converter.
		if (PrimitiveConverterLookup<TEncoder, TDecoder>.TryGetPrimitiveConverter(this.owner.PreserveReferences, out ShapeShiftConverter<T, TEncoder, TDecoder>? primitiveConverter))
		{
			converter = ConverterResult.Ok(primitiveConverter);
			return true;
		}

		return this.TryGetConverterFromAttribute(type, typeShape, attributeProvider, out converter);
	}

	private ConverterResult<TEncoder, TDecoder> GetConverterForMemberOrParameter(ITypeShape typeShape, IGenericCustomAttributeProvider attributeProvider)
	{
		try
		{
			return this.TryGetConverterFromAttribute(typeShape.Type, typeShape, attributeProvider, out ConverterResult<TEncoder, TDecoder>? converter)
				? converter
				: this.GetConverter(typeShape, attributeProvider);
		}
		catch (Exception ex)
		{
			return ConverterResult<TEncoder, TDecoder>.Err(ex);
		}
	}

	/// <summary>
	/// Activates a converter for the given shape if a <see cref="ShapeShiftConverterAttribute"/> is present on the type or member.
	/// </summary>
	/// <param name="type">The type to be converted.</param>
	/// <param name="typeShape">The shape of the type to be serialized.</param>
	/// <param name="attributeProvider">
	/// The source of the attributes.
	/// This will typically be the attributes on the type itself, but may be the attributes on the requesting property or parameter.
	/// </param>
	/// <param name="converter">Receives the converter, if applicable.</param>
	/// <returns>A value indicating whether a converter was found.</returns>
	/// <exception cref="ShapeShiftSerializationException">Thrown if the prescribed converter has no default constructor.</exception>
	private bool TryGetConverterFromAttribute(Type type, ITypeShape? typeShape, IGenericCustomAttributeProvider attributeProvider, [NotNullWhen(true)] out ConverterResult<TEncoder, TDecoder>? converter)
	{
		if (this.owner.TryGetConverterFromAttribute(type, typeShape, attributeProvider, out ShapeShiftConverter<TEncoder, TDecoder>? attributeConverter))
		{
			converter = ConverterResult.Ok(attributeConverter);
			return true;
		}

		converter = null;
		return false;
	}

	/// <summary>
	/// Captures the influence of a member on a converter.
	/// </summary>
	/// <remarks>
	/// This must be hashable/equatable so that we can cache converters based on this influence.
	/// </remarks>
	private record MemberConverterInfluence
	{
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
		private Type? comparerSource;

		/// <summary>
		/// Gets the type that provides the comparer, if specified by the member.
		/// </summary>
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
		public Type? ComparerSource { get => this.comparerSource; init => this.comparerSource = value; }

		/// <summary>
		/// Gets the name of the property on <see cref="ComparerSource"/> that provides the comparer, if specified by the member.
		/// </summary>
		public string? ComparerSourceMemberName { get; init; }

		/// <summary>
		/// Gets the equality comparer for the specified type, if a comparer source is specified.
		/// </summary>
		/// <typeparam name="T">The type to be compared.</typeparam>
		/// <returns>The equality comparer, if available.</returns>
		public IEqualityComparer<T>? GetEqualityComparer<T>() => this.ComparerSource is null ? null : (IEqualityComparer<T>)this.ActivateComparer();

		/// <summary>
		/// Gets the comparer for the specified type, if a comparer source is specified.
		/// </summary>
		/// <typeparam name="T">The type to be compared.</typeparam>
		/// <returns>The comparer, if available.</returns>
		public IComparer<T>? GetComparer<T>() => this.ComparerSource is null ? null : (IComparer<T>)this.ActivateComparer();

		/// <summary>
		/// Gets the comparer from the specified type and member.
		/// </summary>
		/// <returns>The comparer.</returns>
		/// <exception cref="InvalidOperationException">Thrown if something goes wrong in obtaining the comparer from the given type and member.</exception>
		private object ActivateComparer()
		{
			Verify.Operation(this.ComparerSource is not null, "Comparer source is not specified.");

			MethodInfo? propertyGetter = null;
			if (this.ComparerSourceMemberName is not null)
			{
				PropertyInfo? property = this.ComparerSource.GetProperty(this.ComparerSourceMemberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
				if (property is not { GetMethod: { } getter })
				{
					throw new InvalidOperationException($"Unable to find public property '{this.ComparerSourceMemberName}' on type '{this.ComparerSource.FullName}' with getter.");
				}

				if (getter.IsStatic)
				{
					return getter.Invoke(null, null) ?? throw CreateNullPropertyValueError();
				}

				propertyGetter = getter;
			}

			object? instance = Activator.CreateInstance(this.ComparerSource) ?? throw new InvalidOperationException($"Unable to activate {this.ComparerSource}.");

			return propertyGetter is null ? instance : propertyGetter.Invoke(instance, null) ?? CreateNullPropertyValueError();

			InvalidOperationException CreateNullPropertyValueError() => new InvalidOperationException($"{this.ComparerSource.FullName}.{this.ComparerSourceMemberName} produced a null value.");
		}
	}

	private sealed class ExtensionDataMarker
	{
		internal static readonly ExtensionDataMarker Instance = new();
	}
}
