// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// Verifies SHIFT004 reported by <see cref="TypeShapeRequirementAnalyzer"/>.
/// </summary>
/// <remarks>
/// PolyType's source generator does not run in the harness, so shape-bearing types implement
/// <c>IShapeable&lt;T&gt;</c> explicitly, exactly as the generated code does.
/// </remarks>
public class TypeShapeRequirementAnalyzerTests
{
	private const string ShapedPerson = """
		public partial class Person : IShapeable<Person>
		{
			static ITypeShape<Person> IShapeable<Person>.GetTypeShape() => throw new NotImplementedException();
		}
		""";

	private const string PlainPerson = """
		public partial class Person { }
		""";

	private const string Witness = """
		public partial class Witness : IShapeable<Person>
		{
			static ITypeShape<Person> IShapeable<Person>.GetTypeShape() => throw new NotImplementedException();
		}
		""";

	[Test]
	public async Task ShapedType_ReportsNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync($$"""
			{{ShapedPerson}}

			public class Caller
			{
				public string Run(JsonSerializer serializer, Person person) => serializer.Serialize(person);
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task UnshapedType_ReportsShift004()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
			$$"""
			{{PlainPerson}}

			public class Caller
			{
				public string Run(JsonSerializer serializer, Person person) => serializer.Serialize(person);
			}
			""",
			"CS0311");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT004");
		await Assert.That(diagnostics[0].GetMessage()).Contains("Person");
		await Assert.That(diagnostics[0].Descriptor.HelpLinkUri).IsEqualTo("https://aarnott.github.io/ShapeShift/analyzers/SHIFT004.html");
		await Assert.That(diagnostics[0].Properties.ContainsKey(TypeShapeRequirementAnalyzer.MissingShapeTypeIdProperty)).IsTrue();
	}

	[Test]
	public async Task WitnessOverload_AcceptsUnshapedType()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync($$"""
			{{PlainPerson}}

			{{Witness}}

			public class Caller
			{
				public string Run(JsonSerializer serializer, Person person) => serializer.Serialize<Person, Witness>(person);
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task WitnessForWrongType_ReportsShift004()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
			$$"""
			{{PlainPerson}}

			public partial class Animal { }

			{{Witness}}

			public class Caller
			{
				public string Run(JsonSerializer serializer, Animal animal) => serializer.Serialize<Animal, Witness>(animal);
			}
			""",
			"CS0311");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT004");
		await Assert.That(diagnostics[0].GetMessage()).Contains("Witness");
		await Assert.That(diagnostics[0].GetMessage()).Contains("Animal");
	}

	[Test]
	public async Task GenericCallerTypeArgument_ReportsNothing()
	{
		// The type argument is itself a type parameter, so the analyzer cannot know the final type.
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public class Caller
			{
				public string Run<T>(JsonSerializer serializer, T value)
					where T : IShapeable<T> => serializer.Serialize(value);
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task NonGenericInvocation_ReportsNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public class Caller
			{
				public string Run() => "no shapes involved".ToUpperInvariant();
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task DeserializeCallSite_ReportsShift004()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
			$$"""
			{{PlainPerson}}

			public class Caller
			{
				public Person? Run(JsonSerializer serializer, string json) => serializer.Deserialize<Person>(json);
			}
			""",
			"CS0311");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT004");
	}

	[Test]
	public async Task DiagnosticLocation_PointsAtTypeArgument()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
			$$"""
			{{PlainPerson}}

			public class Caller
			{
				public Person? Run(JsonSerializer serializer, string json) => serializer.Deserialize<Person>(json);
			}
			""",
			"CS0311");

		Location location = diagnostics[0].Location;
		string text = (await location.SourceTree!.GetTextAsync()).ToString(location.SourceSpan);
		await Assert.That(text).IsEqualTo("Person");
	}

	private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string body, params string[] expectedCompilerErrorIds)
		=> AnalyzerHarness.GetDiagnosticsAsync(new TypeShapeRequirementAnalyzer(), TestSources.Source(body), expectedCompilerErrorIds);
}
