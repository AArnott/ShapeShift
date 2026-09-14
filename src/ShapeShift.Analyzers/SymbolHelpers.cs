// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace ShapeShift.Analyzers;

/// <summary>
/// Symbol helpers shared by the ShapeShift analyzers.
/// </summary>
internal static class SymbolHelpers
{
	/// <summary>
	/// Finds the constructed base type whose original definition matches <paramref name="openBaseType"/>.
	/// </summary>
	/// <param name="type">The type whose base types are searched, including <paramref name="type"/> itself.</param>
	/// <param name="openBaseType">The unbound base type definition to look for.</param>
	/// <returns>The matching constructed base type, or <see langword="null" /> when there is none.</returns>
	internal static INamedTypeSymbol? FindBaseType(ITypeSymbol? type, INamedTypeSymbol? openBaseType)
	{
		if (openBaseType is null)
		{
			return null;
		}

		for (INamedTypeSymbol? current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, openBaseType))
			{
				return current;
			}
		}

		return null;
	}

	/// <summary>
	/// Gets the first attribute of a given type applied to a symbol.
	/// </summary>
	/// <param name="symbol">The symbol to inspect.</param>
	/// <param name="attributeType">The attribute type to look for.</param>
	/// <returns>The attribute data, or <see langword="null" /> when the attribute is absent.</returns>
	internal static AttributeData? FindAttribute(ISymbol symbol, INamedTypeSymbol? attributeType)
	{
		if (attributeType is null)
		{
			return null;
		}

		foreach (AttributeData attribute in symbol.GetAttributes())
		{
			if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
			{
				return attribute;
			}
		}

		return null;
	}

	/// <summary>
	/// Gets the source location that best identifies an attribute application.
	/// </summary>
	/// <param name="attribute">The attribute application.</param>
	/// <param name="fallback">The symbol whose declaration is used when the attribute has no source syntax.</param>
	/// <returns>A location suitable for reporting a diagnostic.</returns>
	internal static Location GetLocation(AttributeData attribute, ISymbol fallback)
		=> attribute.ApplicationSyntaxReference is { } reference
			? Location.Create(reference.SyntaxTree, reference.Span)
			: GetLocation(fallback);

	/// <summary>
	/// Gets the source location of a symbol's declaration.
	/// </summary>
	/// <param name="symbol">The symbol.</param>
	/// <returns>The first source location, or <see cref="Location.None"/> when the symbol has none.</returns>
	internal static Location GetLocation(ISymbol symbol)
		=> symbol.Locations.FirstOrDefault(l => l.IsInSource) ?? Location.None;

	/// <summary>
	/// Determines whether a type has a public parameterless instance constructor
	/// that <see cref="System.Activator"/> can invoke.
	/// </summary>
	/// <param name="type">The type to inspect.</param>
	/// <returns><see langword="true" /> when such a constructor exists.</returns>
	internal static bool HasPublicDefaultConstructor(INamedTypeSymbol type)
	{
		if (type.IsValueType)
		{
			return true;
		}

		foreach (IMethodSymbol constructor in type.InstanceConstructors)
		{
			if (constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility == Accessibility.Public)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Finds a parameterless instance constructor regardless of its accessibility.
	/// </summary>
	/// <param name="type">The type to inspect.</param>
	/// <returns>The constructor, or <see langword="null" /> when the type declares none.</returns>
	internal static IMethodSymbol? FindParameterlessConstructor(INamedTypeSymbol type)
	{
		foreach (IMethodSymbol constructor in type.InstanceConstructors)
		{
			if (constructor.Parameters.Length == 0)
			{
				return constructor;
			}
		}

		return null;
	}

	/// <summary>
	/// Determines whether a type is fully known: not an error type and not left open by unbound type arguments.
	/// </summary>
	/// <param name="type">The type to inspect.</param>
	/// <returns><see langword="true" /> when the analyzer can reason about the type with confidence.</returns>
	internal static bool IsFullyBound(ITypeSymbol? type)
	{
		switch (type)
		{
			case null:
			case IErrorTypeSymbol:
			case ITypeParameterSymbol:
				return false;
			case IArrayTypeSymbol array:
				return IsFullyBound(array.ElementType);
			case INamedTypeSymbol named:
				if (named.IsUnboundGenericType)
				{
					return false;
				}

				foreach (ITypeSymbol argument in named.TypeArguments)
				{
					if (!IsFullyBound(argument))
					{
						return false;
					}
				}

				return true;
			default:
				return true;
		}
	}
}
