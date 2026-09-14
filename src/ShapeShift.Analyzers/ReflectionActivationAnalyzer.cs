// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ShapeShift.Analyzers;

/// <summary>
/// Reports SHIFT007 where a program opts into reflection-based activation of converter types or type shapes.
/// </summary>
/// <remarks>
/// These opt-ins are supported, but they are not trimming or NativeAOT safe. The diagnostic is reported at
/// <see cref="DiagnosticSeverity.Info"/> so that the opt-in stays visible without failing builds that
/// deliberately use it.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReflectionActivationAnalyzer : DiagnosticAnalyzer
{
	private const string ReflectionOptInMethodName = "WithReflectionConverterTypes";

	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Diagnostics.ReflectionActivationRequiresOptIn];

	/// <inheritdoc/>
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new ArgumentNullException(nameof(context));
		}

		context.EnableConcurrentExecution();

		// Generated code cannot be edited by the user, so it is neither analyzed nor reported on.
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStart =>
		{
			if (KnownSymbols.TryCreate(compilationStart.Compilation) is not { } symbols)
			{
				return;
			}

			compilationStart.RegisterOperationAction(
				operationContext => AnalyzeInvocation(operationContext, symbols),
				OperationKind.Invocation);

			if (symbols.ReflectionTypeShapeProvider is { } reflectionProvider)
			{
				compilationStart.RegisterOperationAction(
					operationContext => AnalyzeReflectionProvider(operationContext, reflectionProvider),
					OperationKind.PropertyReference,
					OperationKind.ObjectCreation);
			}
		});
	}

	private static void AnalyzeInvocation(OperationAnalysisContext context, KnownSymbols symbols)
	{
		var invocation = (IInvocationOperation)context.Operation;
		IMethodSymbol method = invocation.TargetMethod;
		if (method.Name != ReflectionOptInMethodName ||
			SymbolHelpers.FindBaseType(method.ContainingType, symbols.SerializerBase) is null)
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(
			Diagnostics.ReflectionActivationRequiresOptIn,
			invocation.Syntax.GetLocation(),
			$"{method.ContainingType.Name}.{method.Name}"));
	}

	private static void AnalyzeReflectionProvider(OperationAnalysisContext context, INamedTypeSymbol reflectionProvider)
	{
		ITypeSymbol? referencedType = context.Operation switch
		{
			IPropertyReferenceOperation property => property.Property.ContainingType,
			IObjectCreationOperation creation => creation.Type,
			_ => null,
		};

		if (!SymbolEqualityComparer.Default.Equals(referencedType, reflectionProvider))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(
			Diagnostics.ReflectionActivationRequiresOptIn,
			context.Operation.Syntax.GetLocation(),
			reflectionProvider.Name));
	}
}
