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
/// <typeparam name="TEncoder">The type of encoder to use.</typeparam>
/// <typeparam name="TDecoder">The type of decoder to use.</typeparam>
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
		if (BuiltInConverters.TryGetBuiltInConverter<T, TEncoder, TDecoder>(out var builtin))
		{
			return builtin;
		}

		Dictionary<string, PropertyConverter<T, TEncoder, TDecoder>> properties = new(objectShape.Properties.Count);
		foreach (var property in objectShape.Properties)
		{
			properties.Add(property.Name, (PropertyConverter<T, TEncoder, TDecoder>)property.Accept(this)!);
		}

		return ConverterResult.Ok(new ObjectConverter<T, TEncoder, TDecoder>(objectShape, properties));
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

		// Check if the type has a built-in converter.
		if (PrimitiveConverterLookup<TEncoder, TDecoder>.TryGetPrimitiveConverter(this.owner.PreserveReferences, out ShapeShiftConverter<T, TEncoder, TDecoder>? primitiveConverter))
		{
			converter = ConverterResult.Ok(primitiveConverter);
			return true;
		}

		return this.TryGetConverterFromAttribute(type, typeShape, attributeProvider, out converter);
	}

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
	/// It does <em>not</em> take <see cref="MessagePackConverterAttribute"/> into account
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

			////if (memberAttributes.GetCustomAttribute<UseComparerAttribute>() is { } attribute)
			////{
			////	MemberConverterInfluence memberInfluence = new()
			////	{
			////		ComparerSource = attribute.ComparerType,
			////		ComparerSourceMemberName = attribute.MemberName,
			////	};

			////	// PERF: Ideally, we can store and retrieve member influenced converters
			////	// just like we do for non-member influenced ones.
			////	// We'd probably use a separate dictionary dedicated to member-influenced converters.
			////	return (ConverterResult<TEncoder, TDecoder>)shape.Accept(this.OutwardVisitor, memberInfluence)!;
			////}
		}

		return (ConverterResult<TEncoder, TDecoder>)this.context.GetOrAdd(shape, state)!;
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
		if (attributeProvider.GetCustomAttribute<ShapeShiftConverterAttribute>() is not { } customConverterAttribute)
		{
			converter = null;
			return false;
		}

		Type converterType = customConverterAttribute.ConverterType;
		if ((typeShape?.GetAssociatedTypeShape(converterType) as IObjectTypeShape)?.GetDefaultConstructor() is Func<object> converterFactory)
		{
			ShapeShiftConverter<TEncoder, TDecoder> intermediateConverter = (ShapeShiftConverter<TEncoder, TDecoder>)converterFactory();
			if (this.owner.PreserveReferences != ReferencePreservationMode.Off)
			{
				intermediateConverter = ((IShapeShiftConverterInternal<TEncoder, TDecoder>)intermediateConverter).WrapWithReferencePreservation();
			}

			converter = ConverterResult.Ok(intermediateConverter);
			return true;
		}

		if (converterType.GetConstructor(Type.EmptyTypes) is not ConstructorInfo ctor)
		{
			throw new ShapeShiftSerializationException($"{type.FullName} has {typeof(ShapeShiftConverterAttribute)} that refers to {customConverterAttribute.ConverterType.FullName} but that converter has no default constructor.");
		}

		converter = ConverterResult.Ok((ShapeShiftConverter<TEncoder, TDecoder>)ctor.Invoke(Array.Empty<object?>()));
		return true;
	}

	public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
	{
		IParameterShape? constructorParameterShape = (IParameterShape?)state;

		ConverterResult<TEncoder, TDecoder> converter = this.GetConverterForMemberOrParameter(propertyShape.PropertyType, propertyShape.AttributeProvider);

		Getter<TDeclaringType, TPropertyType> getter = propertyShape.GetGetter();
		Setter<TDeclaringType, TPropertyType> setter = propertyShape.GetSetter();
		return new PropertyConverter<TDeclaringType, TEncoder, TDecoder>
		{
			Write = (ref encoder, in target, context) => ((ShapeShiftConverter<TPropertyType, TEncoder, TDecoder>)converter.ValueOrThrow).Write(ref encoder, getter(ref Unsafe.AsRef(in target)), context),
			Read = (ref decoder, ref target, context) => setter(ref target, ((ShapeShiftConverter<TPropertyType, TEncoder, TDecoder>)converter.ValueOrThrow).Read(ref decoder, context)!),
		};
	}
}
