// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// A format-agnostic base class for serializers that use specific encoders and decoders.
/// </summary>
/// <typeparam name="TEncoder">The type of encoder to use.</typeparam>
/// <typeparam name="TDecoder">The type of decoder to use.</typeparam>
public abstract record ShapeShiftSerializer<TEncoder, TDecoder> : IShapeShiftSerializer
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private SerializerConfiguration<TEncoder, TDecoder> configuration = SerializerConfiguration<TEncoder, TDecoder>.Default;

	/// <summary>
	/// Gets the starting context to begin (de)serializations with.
	/// </summary>
	public SerializationContext<TEncoder, TDecoder> StartingContext { get; init; } = new();

	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.InternStrings"/>
	public bool InternStrings
	{
		get => this.configuration.InternStrings;
		init => this.configuration = this.configuration with { InternStrings = value };
	}

	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.Converters"/>
	public ConverterCollection<TEncoder, TDecoder> Converters
	{
		get => this.configuration.Converters;
		init => this.configuration = this.configuration with { Converters = value };
	}

	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.ConverterFactories"/>
	public ImmutableArray<IShapeShiftConverterFactory<TEncoder, TDecoder>> ConverterFactories
	{
		get => this.configuration.ConverterFactories;
		init => this.configuration = this.configuration with { ConverterFactories = value };
	}

	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.SerializeDefaultValues"/>
	public SerializeDefaultValuesPolicy SerializeDefaultValues
	{
		get => this.configuration.SerializeDefaultValues;
		init => this.configuration = this.configuration with { SerializeDefaultValues = value };
	}

	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.DeserializeDefaultValues"/>
	public DeserializeDefaultValuesPolicy DeserializeDefaultValues
	{
		get => this.configuration.DeserializeDefaultValues;
		init => this.configuration = this.configuration with { DeserializeDefaultValues = value };
	}

	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.SerializeEnumValuesByName"/>
	public bool SerializeEnumValuesByName
	{
		get => this.configuration.SerializeEnumValuesByName;
		init => this.configuration = this.configuration with { SerializeEnumValuesByName = value };
	}

	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.PropertyNamingPolicy"/>
	public ShapeShiftNamingPolicy? PropertyNamingPolicy
	{
		get => this.configuration.PropertyNamingPolicy;
		init => this.configuration = this.configuration with { PropertyNamingPolicy = value };
	}

	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.PreserveReferences"/>
	public ReferencePreservationMode PreserveReferences
	{
		get => this.configuration.PreserveReferences;
		init => this.configuration = this.configuration with { PreserveReferences = value };
	}

	/// <inheritdoc cref="SerializerConfiguration{TEncoder, TDecoder}.ConverterCache"/>
	internal ConverterCache<TEncoder, TDecoder> ConverterCache => this.configuration.ConverterCache;

	/// <summary>
	/// Creates a serializer configuration that activates converter types through reflection.
	/// </summary>
	/// <param name="converterTypes">The converter types to activate.</param>
	/// <returns>A serializer configuration with reflection-based converter activation enabled.</returns>
	/// <remarks>
	/// This opt-in is not trimming-safe or NativeAOT-safe unless every converter constructor is explicitly preserved.
	/// Prefer <see cref="Converters"/> or <see cref="ConverterFactories"/> in NativeAOT applications.
	/// </remarks>
	[RequiresDynamicCode("Activating converter types may require constructing closed generic converter types at runtime.")]
	[RequiresUnreferencedCode("Converter constructors supplied as Type objects may be removed by trimming.")]
	public ShapeShiftSerializer<TEncoder, TDecoder> WithReflectionConverterTypes(ConverterTypeCollection converterTypes)
	{
		Requires.NotNull(converterTypes);
		return this with { configuration = this.configuration with { ConverterTypes = converterTypes } };
	}

	public void Serialize<T>(ref TEncoder encoder, in T? value, ITypeShape<T> typeShape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(typeShape);
		using DisposableSerializationContext context = this.CreateSerializationContext(typeShape.Provider, cancellationToken);
		this.GetConverter(typeShape).Write(ref encoder, value, context.Value);
	}

	public T? Deserialize<T>(ref TDecoder decoder, ITypeShape<T> typeShape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(typeShape);
		using DisposableSerializationContext context = this.CreateSerializationContext(typeShape.Provider, cancellationToken);
		return this.GetConverter(typeShape).Read(ref decoder, context.Value);
	}

	/// <summary>
	/// Creates a new serialization context that is ready to process a serialization job.
	/// </summary>
	/// <param name="provider">
	/// The shape provider for the type(s) to be serialized.
	/// This might be <see cref="PolyType.ReflectionProvider.ReflectionTypeShapeProvider.Default"/> to use reflection-based shapes.
	/// It might also be the value of the <c>GeneratedTypeShapeProvider</c> static property on a witness class
	/// (a class on which <see cref="GenerateShapeForAttribute{T}"/> has been applied), although for source generated shapes,
	/// overloads that do not take an <see cref="ITypeShapeProvider"/> offer better performance.
	/// </param>
	/// <param name="cancellationToken">A cancellation token for the operation.</param>
	/// <returns>The serialization context.</returns>
	/// <remarks>
	/// Callers should be sure to always call <see cref="DisposableSerializationContext.Dispose"/> when done with the context.
	/// </remarks>
	protected DisposableSerializationContext CreateSerializationContext(ITypeShapeProvider provider, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(provider);
		return new(this.StartingContext.Start(this, this.ConverterCache, provider, cancellationToken));
	}

	private ShapeShiftConverter<T, TEncoder, TDecoder> GetConverter<T>(ITypeShape<T> typeShape) => (ShapeShiftConverter<T, TEncoder, TDecoder>)this.ConverterCache.GetOrAddConverter(typeShape).ValueOrThrow;

	/// <summary>
	/// A wrapper around <see cref="SerializationContext{TEncoder, TDecoder}"/> that makes disposal easier.
	/// </summary>
	/// <param name="context">The <see cref="SerializationContext{TEncoder, TDecoder}"/> to wrap.</param>
	protected struct DisposableSerializationContext(SerializationContext<TEncoder, TDecoder> context) : IDisposable
	{
		/// <summary>
		/// Gets the actual <see cref="SerializationContext{TEncoder, TDecoder}"/>.
		/// </summary>
		public SerializationContext<TEncoder, TDecoder> Value => context;

		/// <summary>
		/// Disposes of any resources held by the serialization context.
		/// </summary>
		public void Dispose() => context.End();
	}
}
