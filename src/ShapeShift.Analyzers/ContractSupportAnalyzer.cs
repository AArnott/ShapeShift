// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ShapeShift.Analyzers;

/// <summary>
/// Reports SHIFT008 for extension-data contracts that ShapeShift cannot build a converter for.
/// </summary>
/// <remarks>
/// Each condition detected here corresponds to a <c>ShapeShiftSerializationException</c> that ShapeShift
/// throws while preparing the converter graph. The analyzer only moves the feedback to build time.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ContractSupportAnalyzer : DiagnosticAnalyzer
{
	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Diagnostics.UnsupportedContract];

	/// <inheritdoc/>
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new ArgumentNullException(nameof(context));
		}

		context.EnableConcurrentExecution();

		// Extension-data members are hand authored; generated code is neither analyzed nor reported on.
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStart =>
		{
			if (KnownSymbols.TryCreate(compilationStart.Compilation) is not { ExtensionDataAttribute: not null } symbols)
			{
				return;
			}

			compilationStart.RegisterSymbolAction(symbolContext => Analyze(symbolContext, symbols), SymbolKind.NamedType);
		});
	}

	private static void Analyze(SymbolAnalysisContext context, KnownSymbols symbols)
	{
		var type = (INamedTypeSymbol)context.Symbol;
		if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
		{
			return;
		}

		int extensionDataCount = 0;
		foreach (ISymbol member in type.GetMembers())
		{
			if (SymbolHelpers.FindAttribute(member, symbols.ExtensionDataAttribute) is not { } attribute)
			{
				continue;
			}

			extensionDataCount++;
			Location location = SymbolHelpers.GetLocation(attribute, member);

			if (extensionDataCount > 1)
			{
				Report(context, location, $"'{type.ToDisplayString()}' declares more than one extension-data member. ShapeShift supports at most one.");
				continue;
			}

			if (GetMemberType(member) is not { } memberType)
			{
				continue;
			}

			if (!IsExtensionDataDictionary(memberType, symbols))
			{
				Report(context, location, $"Extension-data member '{member.Name}' must have type Dictionary<string, ShapeShiftValue>.");
			}

			if (member is IPropertySymbol { GetMethod: null })
			{
				Report(context, location, $"Extension-data member '{member.Name}' must have a getter.");
			}

			if (type.TypeKind == TypeKind.Class && !HasAccessibleParameterlessConstructor(type))
			{
				Report(context, location, $"Extension data on '{type.ToDisplayString()}' requires a parameterless deserialization constructor.");
			}
		}
	}

	private static void Report(SymbolAnalysisContext context, Location location, string message)
		=> context.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnsupportedContract, location, message));

	private static ITypeSymbol? GetMemberType(ISymbol member) => member switch
	{
		IPropertySymbol property => property.Type,
		IFieldSymbol field => field.Type,
		_ => null,
	};

	private static bool IsExtensionDataDictionary(ITypeSymbol memberType, KnownSymbols symbols)
	{
		if (symbols.Dictionary is null || symbols.ShapeShiftValue is null)
		{
			// Without both symbols the analyzer cannot prove the type is wrong, so say nothing.
			return true;
		}

		return memberType is INamedTypeSymbol named &&
			SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, symbols.Dictionary) &&
			named.TypeArguments is [{ SpecialType: SpecialType.System_String }, ITypeSymbol valueType] &&
			SymbolEqualityComparer.Default.Equals(valueType, symbols.ShapeShiftValue);
	}

	private static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol type)
	{
		foreach (IMethodSymbol constructor in type.InstanceConstructors)
		{
			if (constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility is not (Accessibility.Private or Accessibility.NotApplicable))
			{
				return true;
			}
		}

		return false;
	}
}
