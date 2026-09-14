// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using ShapeShift.Schema;

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
	/// <exception cref="NotSupportedException">
	/// Thrown when set to anything but <see cref="ReferencePreservationMode.Off"/> on a serializer whose format
	/// does not implement <see cref="IReferencePreservingSerializer{TEncoder, TDecoder}"/>. Preserving references
	/// requires a format-specific way to write and recognize a back-reference, so a format that has not opted in
	/// cannot honor the request. Failing here reports that at configuration time rather than partway through the
	/// first serialization.
	/// </exception>
	public ReferencePreservationMode PreserveReferences
	{
		get => this.configuration.PreserveReferences;
		init
		{
			if (value != ReferencePreservationMode.Off && this is not IReferencePreservingSerializer<TEncoder, TDecoder>)
			{
				throw new NotSupportedException($"{this.GetType().Name} does not support reference preservation because it does not implement {nameof(IReferencePreservingSerializer<TEncoder, TDecoder>)}.");
			}

			this.configuration = this.configuration with { PreserveReferences = value };
		}
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

	/// <summary>
	/// Describes the serialized form of a type in a format-neutral way.
	/// </summary>
	/// <param name="typeShape">The shape of the type to describe.</param>
	/// <returns>The contract describing how values of this type are written and read.</returns>
	/// <exception cref="NotSupportedException">
	/// Thrown when <see cref="PreserveReferences"/> is enabled, because reference preservation
	/// replaces repeated values with references in a way that cannot be described statically.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The returned contract reflects this serializer's configuration, including
	/// <see cref="PropertyNamingPolicy"/>, <see cref="SerializeDefaultValues"/>,
	/// <see cref="DeserializeDefaultValues"/>, <see cref="SerializeEnumValuesByName"/> and any
	/// registered <see cref="Converters"/>.
	/// </para>
	/// <para>
	/// Custom converters that do not override <see cref="ShapeShiftConverter{TEncoder, TDecoder}.GetContract"/>
	/// are described with an <see cref="UndocumentedContract"/> instead of a guess.
	/// </para>
	/// </remarks>
	public DataContract GetContract(ITypeShape typeShape)
	{
		Requires.NotNull(typeShape);
		if (this.PreserveReferences != ReferencePreservationMode.Off)
		{
			throw new NotSupportedException($"Contracts cannot be described while {nameof(this.PreserveReferences)} is enabled.");
		}

		return this.ConverterCache.GetOrAddContract(typeShape);
	}

	/// <inheritdoc cref="GetContract(ITypeShape)"/>
	/// <typeparam name="T">The type to describe.</typeparam>
	public DataContract GetContract<T>()
		where T : IShapeable<T> => this.GetContract(T.GetTypeShape());

	/// <inheritdoc cref="GetContract(ITypeShape)"/>
	/// <typeparam name="T">The type to describe.</typeparam>
	/// <typeparam name="TProvider">The witness class that provides the shape for <typeparamref name="T"/>.</typeparam>
	public DataContract GetContract<T, TProvider>()
		where TProvider : IShapeable<T> => this.GetContract(TProvider.GetTypeShape());

	/// <summary>
	/// Translates an expression that walks CLR members into the <see cref="ShapeShiftPath"/> that locates
	/// the same value in a document this serializer reads or writes.
	/// </summary>
	/// <typeparam name="TRoot">The type at the root of the document.</typeparam>
	/// <typeparam name="TValue">The type of the value the expression selects.</typeparam>
	/// <param name="path">
	/// An expression whose single parameter is the root of the document, e.g. <c>person =&gt; person.Address.City</c>.
	/// </param>
	/// <param name="typeShape">The shape of <typeparamref name="TRoot"/>.</param>
	/// <returns>The path to the selected value, ready to pass to <see cref="TryDeserializeFragment{T}(ref TDecoder, ShapeShiftPath, ITypeShape{T}, out T, CancellationToken)"/> or the <c>TrySeek</c> decoder extension member.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when a step of <paramref name="path"/> names something that is not part of the serialized
	/// contract, such as a member the shape ignores or an extension-data member. The message quotes the
	/// failing step.
	/// </exception>
	/// <exception cref="NotSupportedException">
	/// Thrown when a step of <paramref name="path"/> uses a construct with no path equivalent (a method call,
	/// a computed index, or a conversion that changes which contract applies), or reaches a value whose
	/// representation is not described — for example one produced by a custom converter that does not
	/// override <see cref="ShapeShiftConverter{TEncoder, TDecoder}.GetContract"/>. The message quotes the
	/// failing step. Also thrown when <see cref="PreserveReferences"/> is enabled, for the reason
	/// <see cref="GetContract(ITypeShape)"/> documents.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The path is resolved against this serializer's own contract for <typeparamref name="TRoot"/>, so the
	/// caller never has to know the serialized names: <see cref="PropertyNamingPolicy"/> and any
	/// <c>PropertyShapeAttribute.Name</c> alias are applied for them, and a positionally encoded object
	/// (such as a MessagePack array contract) contributes indexes instead of names.
	/// </para>
	/// <para>The expression is only inspected — never compiled — so this is safe for trimming and NativeAOT.</para>
	/// <para>These expression forms are supported:</para>
	/// <list type="bullet">
	/// <item><description>The parameter itself (<c>x =&gt; x</c>), which is <see cref="ShapeShiftPath.Root"/>.</description></item>
	/// <item><description>Member access, to any depth (<c>x =&gt; x.Address.City</c>), including through the null-forgiving operator.</description></item>
	/// <item><description>Constant, non-negative indexes into arrays and lists (<c>x =&gt; x.Tags[1]</c>).</description></item>
	/// <item><description>Constant string keys into string-keyed dictionaries (<c>x =&gt; x.Attributes["hue"]</c>).</description></item>
	/// <item><description><see cref="Nullable{T}.Value"/>, and boxing or widening reference conversions, both of which are stepped over.</description></item>
	/// </list>
	/// <para>
	/// Anything else — computed indexes, method calls, narrowing or user-defined conversions, and dictionaries
	/// whose keys are not strings — is rejected rather than guessed at. Build a <see cref="ShapeShiftPath"/>
	/// directly for those, and for locations that only exist in a particular payload (an index chosen at
	/// runtime, or a property no .NET type declares).
	/// </para>
	/// <para>
	/// Targeted deserialization keeps taking a <see cref="ShapeShiftPath"/> rather than gaining an expression
	/// overload of its own on every input type. A fragment API is already generic over the fragment's type and
	/// its shape provider; adding a root type and root shape provider to each one would multiply the overloads
	/// on every serializer without expressing anything that
	/// <c>serializer.TryDeserializeFragment&lt;City, Witness&gt;(json, serializer.GetPath((Person p) =&gt; p.Address.City), out City? city)</c>
	/// does not already say. Computing the path once and reusing it is also cheaper, since a path is
	/// immutable and independent of the payload.
	/// </para>
	/// </remarks>
	public ShapeShiftPath GetPath<TRoot, TValue>(Expression<Func<TRoot, TValue>> path, ITypeShape<TRoot> typeShape)
	{
		Requires.NotNull(path);
		Requires.NotNull(typeShape);
		return ExpressionPathTranslator.Translate(path, this.GetContract(typeShape), nameof(path));
	}

	/// <inheritdoc cref="GetPath{TRoot, TValue}(Expression{Func{TRoot, TValue}}, ITypeShape{TRoot})"/>
	public ShapeShiftPath GetPath<TRoot, TValue>(Expression<Func<TRoot, TValue>> path)
		where TRoot : IShapeable<TRoot> => this.GetPath(path, TRoot.GetTypeShape());

	/// <inheritdoc cref="GetPath{TRoot, TValue}(Expression{Func{TRoot, TValue}}, ITypeShape{TRoot})" path="/*[not(self::typeparam)]"/>
	/// <typeparam name="TRoot">The type at the root of the document.</typeparam>
	/// <typeparam name="TValue">The type of the value the expression selects.</typeparam>
	/// <typeparam name="TProvider">The witness class that provides the shape for <typeparamref name="TRoot"/>.</typeparam>
	public ShapeShiftPath GetPath<TRoot, TValue, TProvider>(Expression<Func<TRoot, TValue>> path)
		where TProvider : IShapeable<TRoot> => this.GetPath(path, TProvider.GetTypeShape());

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
	/// Attempts to deserialize the value found at a given <see cref="ShapeShiftPath"/>, skipping over
	/// everything else in the document without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the fragment as.</typeparam>
	/// <param name="decoder">The decoder, positioned wherever <paramref name="path"/> should be considered relative to (typically the start of the document).</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="typeShape">The shape of <typeparamref name="T"/>.</param>
	/// <param name="value">Receives the deserialized value if this method returns <see langword="true" />; otherwise <see langword="default" />.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns><see langword="true" /> if <paramref name="path"/> was found and <paramref name="value"/> was populated; <see langword="false" /> otherwise.</returns>
	/// <exception cref="DecoderException">Thrown when a step along <paramref name="path"/> expects a map or vector but finds some other, non-null token.</exception>
	/// <remarks>
	/// After this method returns, the decoder's position is exactly as documented for the <c>TrySeek</c> decoder extension member
	/// on which this method is built: on success, positioned at the start of the fragment's value's successor;
	/// on failure, positioned immediately after whichever container could not produce the next step in the path.
	/// </remarks>
	public bool TryDeserializeFragment<T>(ref TDecoder decoder, ShapeShiftPath path, ITypeShape<T> typeShape, out T? value, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(typeShape);
		if (!DecoderExtensions.TrySeekCore(ref decoder, path))
		{
			value = default;
			return false;
		}

		value = this.Deserialize(ref decoder, typeShape, cancellationToken);
		return true;
	}

	/// <summary>
	/// Deserializes the value found at a given <see cref="ShapeShiftPath"/>, skipping over
	/// everything else in the document without fully parsing or buffering it.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the fragment as.</typeparam>
	/// <param name="decoder">The decoder, positioned wherever <paramref name="path"/> should be considered relative to (typically the start of the document).</param>
	/// <param name="path">The location of the value to deserialize.</param>
	/// <param name="typeShape">The shape of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="ShapeShiftSerializationException">Thrown when <paramref name="path"/> could not be found.</exception>
	/// <exception cref="DecoderException">Thrown when a step along <paramref name="path"/> expects a map or vector but finds some other, non-null token.</exception>
	public T? DeserializeFragment<T>(ref TDecoder decoder, ShapeShiftPath path, ITypeShape<T> typeShape, CancellationToken cancellationToken = default)
	{
		if (!this.TryDeserializeFragment(ref decoder, path, typeShape, out T? value, cancellationToken))
		{
			throw new ShapeShiftSerializationException($"No value was found at path \"{path}\".");
		}

		return value;
	}

	/// <summary>
	/// Creates a reader that incrementally enumerates the elements of a vector,
	/// whether that vector is the root of a document or reached by first seeking into an enclosing document.
	/// </summary>
	/// <typeparam name="T">The type of each element in the vector.</typeparam>
	/// <param name="typeShape">The shape of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token that applies throughout the lifetime of the reader.</param>
	/// <returns>The reader. Callers should dispose of it (or use a <see langword="using" /> statement) when done.</returns>
	public ShapeShiftSequenceReader<T, TEncoder, TDecoder> CreateSequenceReader<T>(ITypeShape<T> typeShape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(typeShape);
		SerializationContext<TEncoder, TDecoder> context = this.StartingContext.Start(this, this.ConverterCache, typeShape.Provider, cancellationToken);
		return new(this.GetConverter(typeShape), context);
	}

	/// <summary>
	/// Creates a reader that incrementally enumerates a sequence of whole top-level values sharing one decoder,
	/// such as newline-delimited JSON (NDJSON) or a buffer containing several concatenated values.
	/// </summary>
	/// <typeparam name="T">The type of each top-level value.</typeparam>
	/// <param name="typeShape">The shape of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token that applies throughout the lifetime of the reader.</param>
	/// <returns>The reader. Callers should dispose of it (or use a <see langword="using" /> statement) when done.</returns>
	public ShapeShiftDocumentReader<T, TEncoder, TDecoder> CreateDocumentReader<T>(ITypeShape<T> typeShape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(typeShape);
		SerializationContext<TEncoder, TDecoder> context = this.StartingContext.Start(this, this.ConverterCache, typeShape.Provider, cancellationToken);
		return new(this.GetConverter(typeShape), context);
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
