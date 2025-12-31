// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ShapeShift.Converters;

namespace ShapeShift;

/// <summary>
/// Tracks all inputs to converter construction and caches the results of construction itself.
/// </summary>
/// <typeparam name="TEncoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="ShapeShiftSerializer{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
/// <param name="configuration">An immutable configuration that this cache builds upon.</param>
/// <remarks>
/// <para>
/// This type is observably immutable and thread-safe.
/// </para>
/// <para>
/// This type offers something of an information barrier to converter construction.
/// The <see cref="ShapeVisitor{TEncoder, TDecoder}"/> only gets a reference to this object,
/// and this object does <em>not</em> have a reference to <see cref="ShapeShiftSerializer{TEncoder, TDecoder}"/>.
/// This ensures that properties on <see cref="ShapeShiftSerializer{TEncoder, TDecoder}"/> cannot serve as inputs to the converters.
/// Thus, the only properties that should reset the <see cref="CachedConverters"/> are those declared on this type.
/// </para>
/// </remarks>
internal class ConverterCache<TEncoder, TDecoder>(SerializerConfiguration<TEncoder, TDecoder> configuration)
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <summary>
	/// An optimization that avoids the dictionary lookup to start serialization
	/// when the caller repeatedly serializes the same type.
	/// </summary>
	private object? lastConverter;

	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.PreserveReferences"/>
	internal ReferencePreservationMode PreserveReferences => configuration.PreserveReferences;

	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.InternStrings"/>
	internal bool InternStrings => configuration.InternStrings;

	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.PropertyNamingPolicy"/>
	internal ShapeShiftNamingPolicy? PropertyNamingPolicy => configuration.PropertyNamingPolicy;

	/// <summary>
	/// Gets all the converters this instance knows about so far.
	/// </summary>
	private MultiProviderTypeCache CachedConverters
	{
		get
		{
			if (field is null)
			{
				field = new()
				{
					DelayedValueFactory = new DelayedConverterFactory<TEncoder, TDecoder>(),
					ValueBuilderFactory = ctx =>
					{
						ShapeVisitor<TEncoder, TDecoder> standardVisitor = new(this, ctx);
						if (this.PreserveReferences == ReferencePreservationMode.Off)
						{
							return standardVisitor;
						}

						ReferencePreservingVisitor<TEncoder, TDecoder> visitor = new(standardVisitor);
						standardVisitor.OutwardVisitor = visitor;
						return standardVisitor;
					},
				};
			}

			return field;
		}
	}

	/// <summary>
	/// Gets a converter for the given type shape.
	/// An existing converter is reused if one is found in the cache.
	/// If a converter must be created, it is added to the cache for lookup next time.
	/// </summary>
	/// <typeparam name="T">The data type to convert.</typeparam>
	/// <param name="shape">The shape of the type to convert.</param>
	/// <returns>A msgpack converter.</returns>
	internal ConverterResult<TEncoder, TDecoder> GetOrAddConverter<T>(ITypeShape<T> shape)
		=> (ConverterResult<TEncoder, TDecoder>)(this.lastConverter is ShapeShiftConverter<T, TEncoder, TDecoder> lastConverter ? lastConverter : (this.lastConverter = this.CachedConverters.GetOrAdd(shape)!));

	/// <summary>
	/// Gets a converter for the given type shape.
	/// An existing converter is reused if one is found in the cache.
	/// If a converter must be created, it is added to the cache for lookup next time.
	/// </summary>
	/// <param name="shape">The shape of the type to convert.</param>
	/// <returns>A msgpack converter.</returns>
	internal ConverterResult<TEncoder, TDecoder> GetOrAddConverter(ITypeShape shape)
		=> (ConverterResult<TEncoder, TDecoder>)this.CachedConverters.GetOrAdd(shape)!;

	/// <summary>
	/// Gets a converter for the given type shape.
	/// An existing converter is reused if one is found in the cache.
	/// If a converter must be created, it is added to the cache for lookup next time.
	/// </summary>
	/// <typeparam name="T">The type to convert.</typeparam>
	/// <param name="provider">The type shape provider.</param>
	/// <returns>A msgpack converter.</returns>
	internal ConverterResult<TEncoder, TDecoder> GetOrAddConverter<T>(ITypeShapeProvider provider)
		=> (ConverterResult<TEncoder, TDecoder>)this.CachedConverters.GetOrAddOrThrow(typeof(T), provider);

	/// <summary>
	/// Gets a converter for the given type shape.
	/// An existing converter is reused if one is found in the cache.
	/// If a converter must be created, it is added to the cache for lookup next time.
	/// </summary>
	/// <param name="type">The type to convert.</param>
	/// <param name="provider">The type shape provider.</param>
	/// <returns>A msgpack converter.</returns>
	internal ConverterResult<TEncoder, TDecoder> GetOrAddConverter(Type type, ITypeShapeProvider provider)
		=> (ConverterResult<TEncoder, TDecoder>)this.CachedConverters.GetOrAddOrThrow(type, provider);

	/// <summary>
	/// Gets a user-defined converter for the specified type if one is available from
	/// converters the user has supplied <em>at runtime</em>.
	/// </summary>
	/// <param name="type">The type to be converted.</param>
	/// <param name="typeShape">The shape of the data type that requires a converter, if available.</param>
	/// <param name="shapeProvider">The shape provider used for this conversion overall (which may not have a shape available if <paramref name="typeShape" /> is <see langword="null" />.</param>
	/// <param name="converter">Receives the converter, if the user provided one.</param>
	/// <returns>A value indicating whether a customer converter exists.</returns>
	/// <remarks>
	/// <para>
	/// This method only searches <see cref="SerializerConfiguration{TEncoder, TDecoder}.Converters"/>,
	/// <see cref="SerializerConfiguration{TEncoder, TDecoder}.ConverterTypes"/> and <see cref="SerializerConfiguration{TEncoder, TDecoder}.ConverterFactories"/>
	/// for matches.
	/// <see cref="ShapeShiftConverterAttribute"/> is <em>not</em> considered.
	/// </para>
	/// <para>
	/// A converter returned from this method will be wrapped with reference-preservation logic when appropriate.
	/// </para>
	/// </remarks>
	internal bool TryGetRuntimeProfferedConverter(Type type, ITypeShape? typeShape, ITypeShapeProvider shapeProvider, [NotNullWhen(true)] out ShapeShiftConverter<TEncoder, TDecoder>? converter)
	{
		converter = null;
		if (!configuration.Converters.TryGetConverter(type, out converter))
		{
			if (configuration.ConverterTypes.TryGetConverterType(type, out Type? converterType) ||
				(type.IsGenericType && configuration.ConverterTypes.TryGetConverterType(type.GetGenericTypeDefinition(), out converterType)))
			{
				if ((typeShape?.GetAssociatedTypeShape(converterType) as IObjectTypeShape)?.GetDefaultConstructor() is Func<object> factory)
				{
					converter = (ShapeShiftConverter<TEncoder, TDecoder>)factory();
				}
				else if (!converterType.IsGenericTypeDefinition)
				{
					// Try to find a constructor that takes a ConverterContext parameter
					ConstructorInfo? converterContextCtor = converterType.GetConstructor([typeof(ConverterContext<TEncoder, TDecoder>)]);
					if (converterContextCtor is not null)
					{
						ConverterContext<TEncoder, TDecoder> context = new(this, shapeProvider, this.PreserveReferences);
						converter = (ShapeShiftConverter<TEncoder, TDecoder>)converterContextCtor.Invoke([context]);
					}
					else
					{
						// Fall back to parameterless constructor
						converter = (ShapeShiftConverter<TEncoder, TDecoder>)Activator.CreateInstance(converterType)!;
					}
				}
				else
				{
					throw new ShapeShiftSerializationException($"Unable to activate converter {converterType} for {type}. Did you forget to define the attribute [assembly: {nameof(TypeShapeExtensionAttribute)}({nameof(TypeShapeExtensionAttribute.AssociatedTypes)} = [typeof(dataType<>), typeof(converterType<>)])]?");
				}
			}
			else
			{
				ConverterContext<TEncoder, TDecoder> context = new(this, shapeProvider, this.PreserveReferences);
				foreach (IShapeShiftConverterFactory<TEncoder, TDecoder> factory in configuration.ConverterFactories)
				{
					if ((converter = factory.CreateConverter(type, typeShape, context)) is not null)
					{
						break;
					}
				}
			}
		}

		if (converter is not null && configuration.PreserveReferences != ReferencePreservationMode.Off)
		{
			converter = ((IShapeShiftConverterInternal<TEncoder, TDecoder>)converter).WrapWithReferencePreservation();
		}

		return converter is not null;
	}

	/// <summary>
	/// Gets the property name that should be used when serializing a property.
	/// </summary>
	/// <param name="name">The original property name as given by <see cref="IPropertyShape"/>.</param>
	/// <param name="attributeProvider">The attribute provider for the property.</param>
	/// <returns>The serialized property name to use.</returns>
	internal string GetSerializedPropertyName(string name, IGenericCustomAttributeProvider? attributeProvider)
	{
		if (this.PropertyNamingPolicy is null)
		{
			return name;
		}

		// If the property was decorated with [PropertyShape(Name = "...")], do *not* meddle with the property name.
		if (attributeProvider?.GetCustomAttributes<PropertyShapeAttribute>(inherit: false).FirstOrDefault() is PropertyShapeAttribute { Name: not null })
		{
			return name;
		}

		return this.PropertyNamingPolicy.ConvertName(name);
	}
}
