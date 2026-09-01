// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace ShapeShift.Analyzers;

/// <summary>
/// Fixes SHIFT002 by widening an existing non-public parameterless constructor of a converter to
/// <see langword="public"/>, which is the accessibility ShapeShift requires in order to activate it.
/// </summary>
/// <remarks>
/// The fix is offered only when the converter already declares a parameterless constructor, so it never
/// invents initialization logic. When no such constructor exists the diagnostic has no automatic fix
/// because only the author knows how the converter should be constructed.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeConverterConstructorPublicCodeFixProvider))]
[Shared]
public sealed class MakeConverterConstructorPublicCodeFixProvider : CodeFixProvider
{
	/// <inheritdoc/>
	public override ImmutableArray<string> FixableDiagnosticIds { get; } = [Diagnostics.ConverterTypeIsNotActivatable.Id];

	/// <inheritdoc/>
	public override FixAllProvider? GetFixAllProvider() => Microsoft.CodeAnalysis.CodeFixes.WellKnownFixAllProviders.BatchFixer;

	/// <inheritdoc/>
	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		Document document = context.Document;
		if (await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false) is not { } root ||
			await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false) is not { } semanticModel)
		{
			return;
		}

		foreach (Diagnostic diagnostic in context.Diagnostics)
		{
			if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not { } node ||
				node.FirstAncestorOrSelf<AttributeSyntax>() is not { } attribute ||
				attribute.ArgumentList?.Arguments is not [{ Expression: TypeOfExpressionSyntax typeOf }])
			{
				continue;
			}

			if (semanticModel.GetSymbolInfo(typeOf.Type, context.CancellationToken).Symbol is not INamedTypeSymbol converterType ||
				converterType.IsAbstract)
			{
				continue;
			}

			IMethodSymbol? constructor = null;
			foreach (IMethodSymbol candidate in converterType.InstanceConstructors)
			{
				if (candidate.Parameters.Length == 0 && candidate.DeclaredAccessibility != Accessibility.Public && !candidate.IsImplicitlyDeclared)
				{
					constructor = candidate;
					break;
				}
			}

			if (constructor?.DeclaringSyntaxReferences is not [SyntaxReference reference] ||
				document.Project.Solution.GetDocumentId(reference.SyntaxTree) is not { } targetDocumentId ||
				await reference.GetSyntaxAsync(context.CancellationToken).ConfigureAwait(false) is not ConstructorDeclarationSyntax)
			{
				continue;
			}

			Solution solution = document.Project.Solution;
			context.RegisterCodeFix(
				CodeAction.Create(
					$"Make the '{converterType.Name}' constructor public",
					cancellationToken => ApplyAsync(solution, targetDocumentId, reference, cancellationToken),
					equivalenceKey: nameof(MakeConverterConstructorPublicCodeFixProvider)),
				diagnostic);
		}
	}

	private static async Task<Solution> ApplyAsync(
		Solution solution,
		DocumentId documentId,
		SyntaxReference reference,
		CancellationToken cancellationToken)
	{
		if (solution.GetDocument(documentId) is not { } document ||
			await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) is not ConstructorDeclarationSyntax declaration ||
			await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) is not { } root)
		{
			return solution;
		}

		SyntaxTokenList modifiers = declaration.Modifiers;
		SyntaxTokenList retained = SyntaxFactory.TokenList(
			modifiers.Where(m => !m.IsKind(SyntaxKind.PrivateKeyword) && !m.IsKind(SyntaxKind.ProtectedKeyword) && !m.IsKind(SyntaxKind.InternalKeyword)));

		SyntaxToken publicToken = SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space);
		SyntaxTriviaList leadingTrivia = modifiers.Count > 0 ? modifiers[0].LeadingTrivia : declaration.GetLeadingTrivia();

		ConstructorDeclarationSyntax updated = declaration
			.WithModifiers(retained.Insert(0, publicToken.WithLeadingTrivia(leadingTrivia)))
			.WithAdditionalAnnotations(Formatter.Annotation);

		if (modifiers.Count == 0)
		{
			updated = updated.WithLeadingTrivia(SyntaxTriviaList.Empty);
		}

		return solution.WithDocumentSyntaxRoot(documentId, root.ReplaceNode(declaration, updated));
	}
}
