// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ShapeShift.Analyzers;

/// <summary>
/// Reports serialized-name collisions among the members of a type that opts into shape generation:
/// exact collisions produced by <c>[PropertyShape(Name = ...)]</c> (SHIFT005) and collisions that
/// appear once a <c>ShapeShiftNamingPolicy</c> normalizes letter casing (SHIFT006).
/// </summary>
/// <remarks>
/// Only types annotated with PolyType's <c>[GenerateShape]</c> are examined, and only the members that
/// ShapeShift serializes by default. Names supplied through <c>[PropertyShape(Name = ...)]</c> are never
/// transformed by a naming policy, matching the run-time behavior.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WireNameAnalyzer : DiagnosticAnalyzer
{
	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
	[
		Diagnostics.AmbiguousWireName,
		Diagnostics.AmbiguousWireNameUnderNamingPolicy,
	];

	/// <inheritdoc/>
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new ArgumentNullException(nameof(context));
		}

		context.EnableConcurrentExecution();

		// Shape-bearing types are authored by hand; generated partial declarations add no serializable members.
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStart =>
		{
			if (KnownSymbols.TryCreate(compilationStart.Compilation) is not { GenerateShapeAttribute: not null } symbols)
			{
				return;
			}

			compilationStart.RegisterSymbolAction(symbolContext => Analyze(symbolContext, symbols), SymbolKind.NamedType);
		});
	}

	private static void Analyze(SymbolAnalysisContext context, KnownSymbols symbols)
	{
		var type = (INamedTypeSymbol)context.Symbol;
		if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface) ||
			SymbolHelpers.FindAttribute(type, symbols.GenerateShapeAttribute) is null)
		{
			return;
		}

		Dictionary<string, ISymbol> byExactName = new(StringComparer.Ordinal);
		Dictionary<string, ISymbol> byCaseInsensitiveName = new(StringComparer.OrdinalIgnoreCase);

		foreach (ISymbol member in EnumerateSerializedMembers(type, symbols))
		{
			string wireName = GetWireName(member, symbols, out bool explicitName);

			if (byExactName.TryGetValue(wireName, out ISymbol? exactConflict))
			{
				context.ReportDiagnostic(Diagnostic.Create(
					Diagnostics.AmbiguousWireName,
					SymbolHelpers.GetLocation(member),
					exactConflict.Name,
					member.Name,
					wireName));
				continue;
			}

			byExactName.Add(wireName, member);

			if (explicitName)
			{
				// A name supplied by an attribute is used verbatim, so no naming policy can introduce a collision.
				continue;
			}

			if (byCaseInsensitiveName.TryGetValue(wireName, out ISymbol? caseConflict))
			{
				context.ReportDiagnostic(Diagnostic.Create(
					Diagnostics.AmbiguousWireNameUnderNamingPolicy,
					SymbolHelpers.GetLocation(member),
					caseConflict.Name,
					member.Name,
					wireName.ToLowerInvariant()));
			}
			else
			{
				byCaseInsensitiveName.Add(wireName, member);
			}
		}
	}

	private static IEnumerable<ISymbol> EnumerateSerializedMembers(INamedTypeSymbol type, KnownSymbols symbols)
	{
		foreach (ISymbol member in type.GetMembers())
		{
			if (member.IsStatic || member.IsImplicitlyDeclared || member.DeclaredAccessibility != Accessibility.Public)
			{
				continue;
			}

			switch (member)
			{
				case IPropertySymbol { Parameters.Length: 0, GetMethod: not null } property
					when property.GetMethod.DeclaredAccessibility == Accessibility.Public:
					break;
				case IFieldSymbol { IsConst: false }:
					break;
				default:
					continue;
			}

			if (SymbolHelpers.FindAttribute(member, symbols.ExtensionDataAttribute) is not null)
			{
				continue;
			}

			if (IsIgnored(member, symbols))
			{
				continue;
			}

			yield return member;
		}
	}

	private static bool IsIgnored(ISymbol member, KnownSymbols symbols)
	{
		if (SymbolHelpers.FindAttribute(member, symbols.PropertyShapeAttribute) is not { } attribute)
		{
			return false;
		}

		foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
		{
			if (argument.Key == "Ignore" && argument.Value.Value is true)
			{
				return true;
			}
		}

		return false;
	}

	private static string GetWireName(ISymbol member, KnownSymbols symbols, out bool explicitName)
	{
		explicitName = false;
		if (SymbolHelpers.FindAttribute(member, symbols.PropertyShapeAttribute) is { } attribute)
		{
			foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
			{
				if (argument.Key == "Name" && argument.Value.Value is string name && name.Length > 0)
				{
					explicitName = true;
					return name;
				}
			}
		}

		return member.Name;
	}
}
