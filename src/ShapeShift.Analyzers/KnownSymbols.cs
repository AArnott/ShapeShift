// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace ShapeShift.Analyzers;

/// <summary>
/// The ShapeShift and PolyType symbols that the analyzers key off of, resolved once per compilation.
/// </summary>
/// <remarks>
/// Instances are immutable and therefore safe to share across the concurrent callbacks that Roslyn
/// invokes after <see cref="Microsoft.CodeAnalysis.Diagnostics.AnalysisContext.EnableConcurrentExecution"/>.
/// </remarks>
internal sealed class KnownSymbols
{
	private KnownSymbols(
		INamedTypeSymbol converterAttribute,
		INamedTypeSymbol untypedConverter,
		INamedTypeSymbol? typedConverter,
		INamedTypeSymbol? extensionDataAttribute,
		INamedTypeSymbol? shapeShiftValue,
		INamedTypeSymbol? dictionary,
		INamedTypeSymbol? serializerBase,
		INamedTypeSymbol? shapeable,
		INamedTypeSymbol? propertyShapeAttribute,
		INamedTypeSymbol? generateShapeAttribute,
		INamedTypeSymbol? reflectionTypeShapeProvider)
	{
		this.ConverterAttribute = converterAttribute;
		this.UntypedConverter = untypedConverter;
		this.TypedConverter = typedConverter;
		this.ExtensionDataAttribute = extensionDataAttribute;
		this.ShapeShiftValue = shapeShiftValue;
		this.Dictionary = dictionary;
		this.SerializerBase = serializerBase;
		this.Shapeable = shapeable;
		this.PropertyShapeAttribute = propertyShapeAttribute;
		this.GenerateShapeAttribute = generateShapeAttribute;
		this.ReflectionTypeShapeProvider = reflectionTypeShapeProvider;
	}

	/// <summary>Gets the <c>ShapeShift.ShapeShiftConverterAttribute</c> symbol.</summary>
	internal INamedTypeSymbol ConverterAttribute { get; }

	/// <summary>Gets the <c>ShapeShift.ShapeShiftConverter&lt;TEncoder, TDecoder&gt;</c> symbol.</summary>
	internal INamedTypeSymbol UntypedConverter { get; }

	/// <summary>Gets the <c>ShapeShift.ShapeShiftConverter&lt;T, TEncoder, TDecoder&gt;</c> symbol.</summary>
	internal INamedTypeSymbol? TypedConverter { get; }

	/// <summary>Gets the <c>ShapeShift.ShapeShiftExtensionDataAttribute</c> symbol.</summary>
	internal INamedTypeSymbol? ExtensionDataAttribute { get; }

	/// <summary>Gets the <c>ShapeShift.ShapeShiftValue</c> symbol.</summary>
	internal INamedTypeSymbol? ShapeShiftValue { get; }

	/// <summary>Gets the <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/> symbol.</summary>
	internal INamedTypeSymbol? Dictionary { get; }

	/// <summary>Gets the <c>ShapeShift.ShapeShiftSerializer&lt;TEncoder, TDecoder&gt;</c> symbol.</summary>
	internal INamedTypeSymbol? SerializerBase { get; }

	/// <summary>Gets the <c>PolyType.IShapeable&lt;T&gt;</c> symbol.</summary>
	internal INamedTypeSymbol? Shapeable { get; }

	/// <summary>Gets the <c>PolyType.PropertyShapeAttribute</c> symbol.</summary>
	internal INamedTypeSymbol? PropertyShapeAttribute { get; }

	/// <summary>Gets the <c>PolyType.GenerateShapeAttribute</c> symbol.</summary>
	internal INamedTypeSymbol? GenerateShapeAttribute { get; }

	/// <summary>Gets the <c>PolyType.ReflectionProvider.ReflectionTypeShapeProvider</c> symbol.</summary>
	internal INamedTypeSymbol? ReflectionTypeShapeProvider { get; }

	/// <summary>
	/// Resolves the ShapeShift symbols in a compilation.
	/// </summary>
	/// <param name="compilation">The compilation to inspect.</param>
	/// <returns>
	/// The resolved symbols, or <see langword="null" /> when the compilation does not reference ShapeShift
	/// and therefore cannot produce any ShapeShift diagnostic.
	/// </returns>
	internal static KnownSymbols? TryCreate(Compilation compilation)
	{
		INamedTypeSymbol? converterAttribute = compilation.GetTypeByMetadataName("ShapeShift.ShapeShiftConverterAttribute");
		INamedTypeSymbol? untypedConverter = compilation.GetTypeByMetadataName("ShapeShift.ShapeShiftConverter`2");
		if (converterAttribute is null || untypedConverter is null)
		{
			return null;
		}

		return new KnownSymbols(
			converterAttribute,
			untypedConverter,
			compilation.GetTypeByMetadataName("ShapeShift.ShapeShiftConverter`3"),
			compilation.GetTypeByMetadataName("ShapeShift.ShapeShiftExtensionDataAttribute"),
			compilation.GetTypeByMetadataName("ShapeShift.ShapeShiftValue"),
			compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2"),
			compilation.GetTypeByMetadataName("ShapeShift.ShapeShiftSerializer`2"),
			compilation.GetTypeByMetadataName("PolyType.IShapeable`1"),
			compilation.GetTypeByMetadataName("PolyType.PropertyShapeAttribute"),
			compilation.GetTypeByMetadataName("PolyType.GenerateShapeAttribute"),
			compilation.GetTypeByMetadataName("PolyType.ReflectionProvider.ReflectionTypeShapeProvider"));
	}
}
