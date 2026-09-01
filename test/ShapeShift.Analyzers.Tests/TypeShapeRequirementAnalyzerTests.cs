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
	private const string ShapedPerson = /* lang=c#-test */ """
		public partial class Person : IShapeable<Person>
		{
			static ITypeShape<Person> IShapeable<Person>.GetTypeShape() => throw new NotImplementedException();
		}
		""";

	private const string PlainPerson = /* lang=c#-test */ """
		public partial class Person { }
		""";

	private const string Witness = /* lang=c#-test */ """
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
		await Assert.That(diagnostics[0].GetMessage())
			.IsEqualTo("'Person' does not provide a source-generated shape for 'Person'; apply [GenerateShape] to 'Person' or pass a witness class annotated with [GenerateShapeFor<Person>]");
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
				public Person? Run(JsonSerializer serializer, string json) => serializer.Deserialize<[|Person|]>(json);
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

	[Test]
	public async Task AddGenerateShape_AddsAttributeAndPartialModifier()
	{
		string? fixedSource = await AnalyzerHarness.ApplyFixAsync(
			new TypeShapeRequirementAnalyzer(),
			new AddGenerateShapeCodeFixProvider(),
			TestSources.Source("""
				public class Person { }

				public class Caller
				{
					public string Run(JsonSerializer serializer, Person person) => serializer.Serialize(person);
				}
				"""),
			"CS0311");

		const string ExpectedBody = /* lang=c#-test */ """
			[PolyType.GenerateShape]
			public partial class Person
			{ }

			public class Caller
			{
				public string Run(JsonSerializer serializer, Person person) => serializer.Serialize(person);
			}
			""";
		await Assert.That(fixedSource).IsNotNull();
		await Assert.That(TestSources.NormalizeLineEndings(fixedSource!))
			.IsEqualTo(TestSources.NormalizeLineEndings(TestSources.Source(ExpectedBody)).Replace("\t", "    ", StringComparison.Ordinal));
	}

	[Test]
	public async Task AddGenerateShape_KeepsExistingPartialModifier()
	{
		string? fixedSource = await AnalyzerHarness.ApplyFixAsync(
			new TypeShapeRequirementAnalyzer(),
			new AddGenerateShapeCodeFixProvider(),
			TestSources.Source("""
				public partial class Person { }

				public class Caller
				{
					public string Run(JsonSerializer serializer, Person person) => serializer.Serialize(person);
				}
				"""),
			"CS0311");

		await Assert.That(fixedSource).IsNotNull();
		await Assert.That(fixedSource!).Contains("[PolyType.GenerateShape]");
		await Assert.That(fixedSource!.Split("partial").Length - 1).IsEqualTo(1);
	}

	[Test]
	public async Task AddGenerateShape_PreservesExistingAttributes()
	{
		string? fixedSource = await AnalyzerHarness.ApplyFixAsync(
			new TypeShapeRequirementAnalyzer(),
			new AddGenerateShapeCodeFixProvider(),
			TestSources.Source("""
				[System.Obsolete]
				public class Person { }

				public class Caller
				{
					public string Run(JsonSerializer serializer, Person person) => serializer.Serialize(person);
				}
				"""),
			"CS0311",
			"CS0612",
			"CS0618");

		await Assert.That(fixedSource).IsNotNull();
		await Assert.That(fixedSource!).Contains("[System.Obsolete]");
		await Assert.That(fixedSource!).Contains("[PolyType.GenerateShape]");
	}

	[Test]
	public async Task AddGenerateShape_NotOfferedForTypesOutsideTheSolution()
	{
		string? fixedSource = await AnalyzerHarness.ApplyFixAsync(
			new TypeShapeRequirementAnalyzer(),
			new AddGenerateShapeCodeFixProvider(),
			TestSources.Source("""
				public class Caller
				{
					public string Run(JsonSerializer serializer, Uri value) => serializer.Serialize(value);
				}
				"""),
			"CS0311");

		await Assert.That(fixedSource).IsNull();
	}

	[Test]
	public async Task AddGenerateShape_FixAllUpdatesDistinctTypes()
	{
		string? fixedSource = await AnalyzerHarness.ApplyFixAllAsync(
			new TypeShapeRequirementAnalyzer(),
			new AddGenerateShapeCodeFixProvider(),
			TestSources.Source("""
				public class Person { }

				public class Address { }

				public class Caller
				{
					public string WritePerson(JsonSerializer serializer, Person value) => serializer.Serialize(value);

					public string WriteAddress(JsonSerializer serializer, Address value) => serializer.Serialize(value);
				}
				"""),
			"CS0311");

		await Assert.That(fixedSource).IsNotNull();
		await Assert.That(fixedSource!.Split("[PolyType.GenerateShape]").Length - 1).IsEqualTo(2);
		await Assert.That(fixedSource!).Contains("public partial class Person");
		await Assert.That(fixedSource!).Contains("public partial class Address");
	}

	[Test]
	public async Task GeneratedMissingShapeCall_ReportsNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzerHarness.GetGeneratedCodeDiagnosticsAsync(
			new TypeShapeRequirementAnalyzer(),
			TestSources.Source($$"""
				{{PlainPerson}}

				public class Caller
				{
					public Person? Run(JsonSerializer serializer, string json) => serializer.Deserialize<Person>(json);
				}
				"""),
			"CS0311");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
		[System.Diagnostics.CodeAnalysis.StringSyntax("c#-test")] string body,
		params string[] expectedCompilerErrorIds)
		=> AnalyzerHarness.GetDiagnosticsAsync(new TypeShapeRequirementAnalyzer(), TestSources.Source(body), expectedCompilerErrorIds);
}
