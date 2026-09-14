// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ShapeShift.Analyzers;

/// <summary>
/// Reports SHIFT004 when a ShapeShift call site supplies a type argument that has no
/// PolyType source-generated shape.
/// </summary>
/// <remarks>
/// The C# compiler reports the unsatisfied <c>IShapeable&lt;T&gt;</c> constraint with a generic message.
/// This analyzer explains the ShapeShift-specific remedy and carries the information the code fix
/// needs to apply <c>[GenerateShape]</c> to the offending type.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeShapeRequirementAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic property that identifies the type that needs a generated shape,
	/// expressed as a documentation comment declaration ID.
	/// </summary>
	public const string MissingShapeTypeIdProperty = "MissingShapeTypeId";

	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Diagnostics.MissingGeneratedShape];

	/// <inheritdoc/>
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new ArgumentNullException(nameof(context));
		}

		context.EnableConcurrentExecution();

		// PolyType's own generated code satisfies these constraints by construction, and users cannot
		// edit generated files, so generated code is neither analyzed nor reported on.
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStart =>
		{
			if (KnownSymbols.TryCreate(compilationStart.Compilation) is not { Shapeable: { } shapeable })
			{
				return;
			}

			// A syntax node action is used rather than an operation action because Roslyn produces an
			// IInvalidOperation (not an IInvocationOperation) for a call whose generic constraint is not
			// satisfied, which is precisely the case this analyzer exists to explain.
			compilationStart.RegisterSyntaxNodeAction(
				syntaxContext => AnalyzeInvocation(syntaxContext, shapeable),
				SyntaxKind.InvocationExpression);
		});
	}

	private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol shapeable)
	{
		var invocation = (InvocationExpressionSyntax)context.Node;
		SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
		if ((symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault()) is not IMethodSymbol method)
		{
			return;
		}

		if (method.TypeArguments.Length == 0 || method.TypeParameters.Length != method.TypeArguments.Length)
		{
			return;
		}

		for (int i = 0; i < method.TypeParameters.Length; i++)
		{
			ITypeParameterSymbol parameter = method.TypeParameters[i];
			ITypeSymbol argument = method.TypeArguments[i];
			if (!SymbolHelpers.IsFullyBound(argument))
			{
				continue;
			}

			foreach (ITypeSymbol constraint in parameter.ConstraintTypes)
			{
				if (constraint is not INamedTypeSymbol { TypeArguments: [ITypeSymbol shapedType] } named ||
					!SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, shapeable))
				{
					continue;
				}

				if (Substitute(shapedType, method) is not { } required || !SymbolHelpers.IsFullyBound(required))
				{
					continue;
				}

				if (Implements(argument, shapeable, required))
				{
					continue;
				}

				context.ReportDiagnostic(Diagnostic.Create(
					Diagnostics.MissingGeneratedShape,
					GetLocation(invocation, i),
					CreateProperties(required),
					argument.ToDisplayString(),
					required.ToDisplayString()));
			}
		}
	}

	private static ImmutableDictionary<string, string?> CreateProperties(ITypeSymbol required)
		=> DocumentationCommentId.CreateDeclarationId(required) is { } id
			? ImmutableDictionary<string, string?>.Empty.Add(MissingShapeTypeIdProperty, id)
			: ImmutableDictionary<string, string?>.Empty;

	private static ITypeSymbol? Substitute(ITypeSymbol constraintArgument, IMethodSymbol method)
	{
		if (constraintArgument is not ITypeParameterSymbol typeParameter)
		{
			return constraintArgument;
		}

		for (int i = 0; i < method.TypeParameters.Length; i++)
		{
			if (SymbolEqualityComparer.Default.Equals(method.TypeParameters[i], typeParameter))
			{
				return method.TypeArguments[i];
			}
		}

		return null;
	}

	private static bool Implements(ITypeSymbol argument, INamedTypeSymbol shapeable, ITypeSymbol required)
	{
		foreach (INamedTypeSymbol candidate in argument.AllInterfaces)
		{
			if (SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, shapeable) &&
				candidate.TypeArguments is [ITypeSymbol actual] &&
				SymbolEqualityComparer.Default.Equals(actual, required))
			{
				return true;
			}
		}

		return false;
	}

	private static Location GetLocation(InvocationExpressionSyntax invocation, int typeArgumentIndex)
	{
		SimpleNameSyntax? name = invocation.Expression switch
		{
			MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
			MemberBindingExpressionSyntax binding => binding.Name,
			SimpleNameSyntax simpleName => simpleName,
			_ => null,
		};

		if (name is GenericNameSyntax { TypeArgumentList.Arguments: { } arguments } && typeArgumentIndex < arguments.Count)
		{
			return arguments[typeArgumentIndex].GetLocation();
		}

		return name?.GetLocation() ?? invocation.GetLocation();
	}
}
