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
/// Fixes SHIFT004 by applying PolyType's <c>[GenerateShape]</c> attribute to the type that lacks a shape,
/// making the declaration <see langword="partial"/> because the source generator requires it.
/// </summary>
/// <remarks>
/// This fix is purely additive: adding the <c>partial</c> modifier does not change the meaning of an
/// existing declaration, and the attribute only asks PolyType to generate a shape for the type.
/// The fix is offered only when the type has exactly one declaration in the current solution,
/// so there is never a question about which file to edit.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddGenerateShapeCodeFixProvider))]
[Shared]
public sealed class AddGenerateShapeCodeFixProvider : CodeFixProvider
{
	private const string GenerateShapeAttributeName = "PolyType.GenerateShape";

	/// <inheritdoc/>
	public override ImmutableArray<string> FixableDiagnosticIds { get; } = [Diagnostics.MissingGeneratedShape.Id];

	/// <inheritdoc/>
	public override FixAllProvider? GetFixAllProvider() => Microsoft.CodeAnalysis.CodeFixes.WellKnownFixAllProviders.BatchFixer;

	/// <inheritdoc/>
	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		Document document = context.Document;
		if (await document.Project.GetCompilationAsync(context.CancellationToken).ConfigureAwait(false) is not { } compilation)
		{
			return;
		}

		foreach (Diagnostic diagnostic in context.Diagnostics)
		{
			if (!diagnostic.Properties.TryGetValue(TypeShapeRequirementAnalyzer.MissingShapeTypeIdProperty, out string? declarationId) ||
				declarationId is null)
			{
				continue;
			}

			if (DocumentationCommentId.GetFirstSymbolForDeclarationId(declarationId, compilation) is not INamedTypeSymbol type ||
				type.DeclaringSyntaxReferences is not [SyntaxReference reference] ||
				document.Project.Solution.GetDocumentId(reference.SyntaxTree) is not { } targetDocumentId)
			{
				continue;
			}

			if (await reference.GetSyntaxAsync(context.CancellationToken).ConfigureAwait(false) is not TypeDeclarationSyntax)
			{
				continue;
			}

			Solution solution = document.Project.Solution;
			context.RegisterCodeFix(
				CodeAction.Create(
					$"Apply [GenerateShape] to '{type.Name}'",
					cancellationToken => ApplyAsync(solution, targetDocumentId, reference, cancellationToken),
					equivalenceKey: $"{nameof(AddGenerateShapeCodeFixProvider)}:{declarationId}"),
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
			await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) is not TypeDeclarationSyntax declaration ||
			await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) is not { } root)
		{
			return solution;
		}

		TypeDeclarationSyntax updated = declaration;
		if (!updated.Modifiers.Any(SyntaxKind.PartialKeyword))
		{
			updated = updated.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space));
		}

		if (!HasGenerateShapeAttribute(declaration))
		{
			AttributeListSyntax attributeList = SyntaxFactory
				.AttributeList(SyntaxFactory.SingletonSeparatedList(
					SyntaxFactory.Attribute(SyntaxFactory.ParseName(GenerateShapeAttributeName))))
				.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

			SyntaxTriviaList leadingTrivia = updated.GetLeadingTrivia();
			updated = updated
				.WithoutLeadingTrivia()
				.AddAttributeLists(attributeList)
				.WithLeadingTrivia(leadingTrivia);
		}

		updated = updated.WithAdditionalAnnotations(Formatter.Annotation);
		return solution.WithDocumentSyntaxRoot(documentId, root.ReplaceNode(declaration, updated));
	}

	private static bool HasGenerateShapeAttribute(TypeDeclarationSyntax declaration)
	{
		foreach (AttributeListSyntax list in declaration.AttributeLists)
		{
			foreach (AttributeSyntax attribute in list.Attributes)
			{
				string name = attribute.Name.ToString();
				if (name is "GenerateShape" or "GenerateShapeAttribute" ||
					name.EndsWith(".GenerateShape", StringComparison.Ordinal) ||
					name.EndsWith(".GenerateShapeAttribute", StringComparison.Ordinal))
				{
					return true;
				}
			}
		}

		return false;
	}
}
