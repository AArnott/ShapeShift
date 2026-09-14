// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ShapeShift.Analyzers;

/// <summary>
/// Validates every <c>ShapeShiftConverterAttribute</c> application: that the named type really is a
/// ShapeShift converter (SHIFT001), that ShapeShift can activate it (SHIFT002),
/// and that it converts the annotated data type (SHIFT003).
/// </summary>
/// <remarks>
/// Open generic converter types are skipped because ShapeShift resolves them through PolyType
/// associated type shapes, which this analyzer cannot evaluate statically.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConverterAttributeAnalyzer : DiagnosticAnalyzer
{
	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
	[
		Diagnostics.ConverterTypeIsNotAConverter,
		Diagnostics.ConverterTypeIsNotActivatable,
		Diagnostics.ConverterTypeConvertsDifferentType,
	];

	/// <inheritdoc/>
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new ArgumentNullException(nameof(context));
		}

		context.EnableConcurrentExecution();

		// ShapeShift never generates source that carries these attributes, and users should not be
		// asked to fix generated code, so generated code is neither analyzed nor reported on.
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStart =>
		{
			if (KnownSymbols.TryCreate(compilationStart.Compilation) is not { } symbols)
			{
				return;
			}

			compilationStart.RegisterSymbolAction(
				symbolContext => Analyze(symbolContext, symbols),
				SymbolKind.NamedType,
				SymbolKind.Property,
				SymbolKind.Field,
				SymbolKind.Parameter);
		});
	}

	private static void Analyze(SymbolAnalysisContext context, KnownSymbols symbols)
	{
		ISymbol symbol = context.Symbol;
		if (SymbolHelpers.FindAttribute(symbol, symbols.ConverterAttribute) is not { } attribute)
		{
			return;
		}

		if (attribute.ConstructorArguments is not [{ Kind: TypedConstantKind.Type, Value: INamedTypeSymbol converterType }])
		{
			return;
		}

		if (!SymbolHelpers.IsFullyBound(converterType) || converterType.IsUnboundGenericType || converterType.IsGenericType)
		{
			// Generic converters are activated through PolyType associated type shapes.
			return;
		}

		Location location = SymbolHelpers.GetLocation(attribute, symbol);

		if (SymbolHelpers.FindBaseType(converterType, symbols.UntypedConverter) is null)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				Diagnostics.ConverterTypeIsNotAConverter,
				location,
				converterType.ToDisplayString()));
			return;
		}

		if (converterType.IsAbstract)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				Diagnostics.ConverterTypeIsNotActivatable,
				location,
				converterType.ToDisplayString(),
				"it is abstract"));
		}
		else if (!SymbolHelpers.HasPublicDefaultConstructor(converterType))
		{
			context.ReportDiagnostic(Diagnostic.Create(
				Diagnostics.ConverterTypeIsNotActivatable,
				location,
				converterType.ToDisplayString(),
				"it has no public parameterless constructor"));
		}

		if (SymbolHelpers.FindBaseType(converterType, symbols.TypedConverter) is not { TypeArguments: [ITypeSymbol convertedType, _, _] })
		{
			return;
		}

		if (GetAnnotatedType(symbol) is not { } annotatedType || !SymbolHelpers.IsFullyBound(annotatedType) || !SymbolHelpers.IsFullyBound(convertedType))
		{
			return;
		}

		if (!SymbolEqualityComparer.Default.Equals(annotatedType, convertedType))
		{
			context.ReportDiagnostic(Diagnostic.Create(
				Diagnostics.ConverterTypeConvertsDifferentType,
				location,
				converterType.ToDisplayString(),
				convertedType.ToDisplayString(),
				annotatedType.ToDisplayString()));
		}
	}

	private static ITypeSymbol? GetAnnotatedType(ISymbol symbol) => symbol switch
	{
		INamedTypeSymbol type => type,
		IPropertySymbol property => property.Type,
		IFieldSymbol field => field.Type,
		IParameterSymbol parameter => parameter.Type,
		_ => null,
	};
}
