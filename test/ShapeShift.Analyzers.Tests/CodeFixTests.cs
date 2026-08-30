// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// Verifies the code fixes that ShapeShift offers.
/// </summary>
/// <remarks>
/// Only fixes that are mechanical and cannot change the serialized form are offered, so the assertions
/// here also document the deliberate absence of a fix for the remaining diagnostics.
/// </remarks>
public class CodeFixTests
{
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

		await Assert.That(fixedSource).IsNotNull();
		await Assert.That(fixedSource!).Contains("[PolyType.GenerateShape]");
		await Assert.That(fixedSource!).Contains("public partial class Person");
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
		// System.Uri is defined in a referenced assembly, so there is nothing safe to edit.
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
	public async Task MakeConstructorPublic_WidensPrivateConstructor()
	{
		string? fixedSource = await AnalyzerHarness.ApplyFixAsync(
			new ConverterAttributeAnalyzer(),
			new MakeConverterConstructorPublicCodeFixProvider(),
			TestSources.Source("""
				[ShapeShiftConverter(typeof(PersonConverter))]
				public class Person { }

				public class PersonConverter : ShapeShiftConverter<Person, JsonEncoder, JsonDecoder>
				{
					private PersonConverter() { }

					public override Person? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => null;

					public override void Write(ref JsonEncoder encoder, in Person? value, SerializationContext<JsonEncoder, JsonDecoder> context) { }
				}
				"""));

		await Assert.That(fixedSource).IsNotNull();
		await Assert.That(fixedSource!).Contains("public PersonConverter()");
		await Assert.That(fixedSource!).DoesNotContain("private PersonConverter()");
	}

	[Test]
	public async Task MakeConstructorPublic_NotOfferedWhenNoParameterlessConstructorExists()
	{
		// Inventing a constructor could skip initialization the author requires, so no fix is offered.
		string? fixedSource = await AnalyzerHarness.ApplyFixAsync(
			new ConverterAttributeAnalyzer(),
			new MakeConverterConstructorPublicCodeFixProvider(),
			TestSources.Source("""
				[ShapeShiftConverter(typeof(PersonConverter))]
				public class Person { }

				public class PersonConverter : ShapeShiftConverter<Person, JsonEncoder, JsonDecoder>
				{
					public PersonConverter(int unused) { }

					public override Person? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => null;

					public override void Write(ref JsonEncoder encoder, in Person? value, SerializationContext<JsonEncoder, JsonDecoder> context) { }
				}
				"""));

		await Assert.That(fixedSource).IsNull();
	}

	[Test]
	public async Task MakeConstructorPublic_NotOfferedForAbstractConverter()
	{
		string? fixedSource = await AnalyzerHarness.ApplyFixAsync(
			new ConverterAttributeAnalyzer(),
			new MakeConverterConstructorPublicCodeFixProvider(),
			TestSources.Source("""
				[ShapeShiftConverter(typeof(PersonConverter))]
				public class Person { }

				public abstract class PersonConverter : ShapeShiftConverter<Person, JsonEncoder, JsonDecoder>
				{
					protected PersonConverter() { }

					public override Person? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => null;

					public override void Write(ref JsonEncoder encoder, in Person? value, SerializationContext<JsonEncoder, JsonDecoder> context) { }
				}
				"""));

		await Assert.That(fixedSource).IsNull();
	}
}
