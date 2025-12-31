// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace ShapeShift;

internal record SerializerConfiguration<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <summary>
	/// Gets the default configuration.
	/// </summary>
	public static readonly SerializerConfiguration<TEncoder, TDecoder> Default = new();

	private ConverterCache<TEncoder, TDecoder>? converterCache;

	private SerializerConfiguration()
	{
	}

	/// <summary>
	/// Gets an array of <see cref="ShapeShiftConverter{T, TEncoder, TDecoder}"/> objects that should be used for their designated data types.
	/// </summary>
	/// <remarks>
	/// Converters in this collection are searched first when creating a converter for a given type, before <see cref="ConverterTypes"/> and <see cref="ConverterFactories"/>.
	/// </remarks>
	public ConverterCollection Converters
	{
		get => field;
		init => this.ChangeSetting(ref field, value);
	}

	/// <summary>
	/// Gets a collection of <see cref="ShapeShiftConverter{T, TEncoder, TDecoder}"/> types that should be used for their designated data types.
	/// </summary>
	/// <remarks>
	/// The types in this collection are searched after matching <see cref="Converters"/> and before <see cref="ConverterFactories"/> when creating a converter for a given type.
	/// </remarks>
	public ConverterTypeCollection ConverterTypes
	{
		get => field;
		init => this.ChangeSetting(ref field, value);
	}

	/// <summary>
	/// Gets an array of converter factories to consult when creating a converter for a given type.
	/// </summary>
	/// <remarks>
	/// Factories are the last resort for creating a custom converter, coming after <see cref="Converters"/> and <see cref="ConverterTypes"/>.
	/// </remarks>
	public ImmutableArray<IShapeShiftConverterFactory<TEncoder, TDecoder>> ConverterFactories
	{
		get => field;
		init => this.ChangeSetting(ref field, value);
	}

	/// <summary>
	/// Gets a setting that determines how references to objects are preserved during serialization and deserialization.
	/// </summary>
	/// <value>
	/// The default value is <see cref="ReferencePreservationMode.Off" />
	/// because it requires no msgpack extensions, is compatible with all msgpack readers,
	/// adds no security considerations and is the most performant.
	/// </value>
	/// <remarks>
	/// Preserving references impacts the serialized result and can hurt interoperability if the other party is not using the same feature.
	/// </remarks>
	public ReferencePreservationMode PreserveReferences
	{
		get => field;
		init => this.ChangeSetting(ref field, value);
	}

	/// <summary>
	/// Gets a value indicating whether to intern strings during deserialization.
	/// </summary>
	/// <remarks>
	/// <para>
	/// String interning means that a string that appears multiple times (within a single deserialization or across many)
	/// in the msgpack data will be deserialized as the same <see cref="string"/> instance, reducing GC pressure.
	/// </para>
	/// <para>
	/// When enabled, all deserialized strings are retained with a weak reference, allowing them to be garbage collected
	/// while also being reusable for future deserializations as long as they are in memory.
	/// </para>
	/// <para>
	/// This feature has a positive impact on memory usage but may have a negative impact on performance due to searching
	/// through previously deserialized strings to find a match.
	/// If your application is performance sensitive, you should measure the impact of this feature on your application.
	/// </para>
	/// <para>
	/// This feature is orthogonal and complementary to <see cref="PreserveReferences"/>.
	/// Preserving references impacts the serialized result and can hurt interoperability if the other party is not using the same feature.
	/// Preserving references also does not guarantee that equal strings will be reused because the original serialization may have had
	/// multiple string objects for the same value, so deserialization would produce the same result.
	/// Preserving references alone will never reuse strings across top-level deserialization operations either.
	/// Interning strings however, has no impact on the serialized result and is always safe to use.
	/// Interning strings will guarantee string objects are reused within and across deserialization operations so long as their values are equal.
	/// The combination of the two features will ensure the most compact msgpack, and will produce faster deserialization times than string interning alone.
	/// Combining the two features also activates special behavior to ensure that serialization only writes a string once
	/// and references that string later in that same serialization, even if the equal strings were unique objects.
	/// </para>
	/// </remarks>
	public bool InternStrings
	{
		get => field;
		init => this.ChangeSetting(ref field, value);
	}

	/// <summary>
	/// Gets the <see cref="ShapeShift.ConverterCache"/> object based on this configuration.
	/// </summary>
	internal ConverterCache<TEncoder, TDecoder> ConverterCache => this.converterCache ??= new ConverterCache<TEncoder, TDecoder>(this);

	private bool ChangeSetting<T>(ref T location, T value)
	{
		if (!EqualityComparer<T>.Default.Equals(location, value))
		{
			this.converterCache = null;
			location = value;
			return true;
		}

		return false;
	}
}
