// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ShapeShift.Analyzers.Tests;

/// <summary>
/// Verifies SHIFT001, SHIFT002 and SHIFT003 reported by <see cref="ConverterAttributeAnalyzer"/>.
/// </summary>
public class ConverterAttributeAnalyzerTests
{
	private const string ValidConverter = /* lang=c#-test */ """
		public class PersonConverter : ShapeShiftConverter<Person, JsonEncoder, JsonDecoder>
		{
			public override Person? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => null;

			public override void Write(ref JsonEncoder encoder, in Person? value, SerializationContext<JsonEncoder, JsonDecoder> context) { }
		}
		""";

	[Test]
	public async Task ValidConverterOnType_ReportsNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync($$"""
			[ShapeShiftConverter(typeof(PersonConverter))]
			public class Person { }

			{{ValidConverter}}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task ValidConverterOnProperty_ReportsNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync($$"""
			public class Person { }

			public class Holder
			{
				[ShapeShiftConverter(typeof(PersonConverter))]
				public Person? Value { get; set; }
			}

			{{ValidConverter}}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task NonConverterType_ReportsShift001()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[[|ShapeShiftConverter(typeof(NotAConverter))|]]
			public class Person { }

			public class NotAConverter { }
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT001");
		await Assert.That(diagnostics[0].GetMessage())
			.IsEqualTo("'NotAConverter' does not derive from ShapeShiftConverter<T, TEncoder, TDecoder> and cannot be used as a ShapeShift converter");
		await Assert.That(diagnostics[0].Descriptor.HelpLinkUri).IsEqualTo("https://aarnott.github.io/ShapeShift/analyzers/SHIFT001.html");
	}

	[Test]
	public async Task PrivateConstructor_ReportsShift002()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[[|ShapeShiftConverter(typeof(PersonConverter))|]]
			public class Person { }

			public class PersonConverter : ShapeShiftConverter<Person, JsonEncoder, JsonDecoder>
			{
				private PersonConverter() { }

				public override Person? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => null;

				public override void Write(ref JsonEncoder encoder, in Person? value, SerializationContext<JsonEncoder, JsonDecoder> context) { }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT002");
		await Assert.That(diagnostics[0].GetMessage())
			.IsEqualTo("ShapeShift cannot activate the converter 'PersonConverter' because it has no public parameterless constructor");
	}

	[Test]
	public async Task ParameterizedConstructorOnly_ReportsShift002()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[[|ShapeShiftConverter(typeof(PersonConverter))|]]
			public class Person { }

			public class PersonConverter : ShapeShiftConverter<Person, JsonEncoder, JsonDecoder>
			{
				public PersonConverter(int unused) { }

				public override Person? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => null;

				public override void Write(ref JsonEncoder encoder, in Person? value, SerializationContext<JsonEncoder, JsonDecoder> context) { }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT002");
	}

	[Test]
	public async Task AbstractConverter_ReportsShift002()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[[|ShapeShiftConverter(typeof(PersonConverter))|]]
			public class Person { }

			public abstract class PersonConverter : ShapeShiftConverter<Person, JsonEncoder, JsonDecoder>
			{
				public override Person? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => null;

				public override void Write(ref JsonEncoder encoder, in Person? value, SerializationContext<JsonEncoder, JsonDecoder> context) { }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT002");
		await Assert.That(diagnostics[0].GetMessage()).Contains("abstract");
	}

	[Test]
	public async Task MismatchedDataType_ReportsShift003()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync($$"""
			[[|ShapeShiftConverter(typeof(PersonConverter))|]]
			public class Animal { }

			public class Person { }

			{{ValidConverter}}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT003");
		await Assert.That(diagnostics[0].GetMessage())
			.IsEqualTo("'PersonConverter' converts 'Person', which is not compatible with 'Animal'");
	}

	[Test]
	public async Task MismatchedPropertyType_ReportsShift003()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync($$"""
			public class Person { }

			public class Holder
			{
				[ShapeShiftConverter(typeof(PersonConverter))]
				public int Value { get; set; }
			}

			{{ValidConverter}}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT003");
	}

	[Test]
	public async Task DerivedConverterOfBaseType_ReportsShift003()
	{
		// The runtime casts the converter to ShapeShiftConverter<TDeclaredType, ...>, which is invariant,
		// so a converter for a base type cannot serve a derived type.
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync($$"""
			[ShapeShiftConverter(typeof(PersonConverter))]
			public class Employee : Person { }

			public class Person { }

			{{ValidConverter}}
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT003");
	}

	[Test]
	public async Task NullableAnnotationDifference_ReportsNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync($$"""
			public class Person { }

			public class Holder
			{
				[ShapeShiftConverter(typeof(PersonConverter))]
				public Person Value { get; set; } = new();
			}

			{{ValidConverter}}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task OpenGenericConverter_ReportsNothing()
	{
		// Open generic converters are activated through PolyType associated type shapes,
		// which the analyzer deliberately does not attempt to evaluate.
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[ShapeShiftConverter(typeof(BoxConverter<>))]
			public class Box<T> { }

			public class BoxConverter<T> : ShapeShiftConverter<Box<T>, JsonEncoder, JsonDecoder>
			{
				private BoxConverter() { }

				public override Box<T>? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => null;

				public override void Write(ref JsonEncoder encoder, in Box<T>? value, SerializationContext<JsonEncoder, JsonDecoder> context) { }
			}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task ConverterOnParameter_IsAnalyzed()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			public class Person
			{
				public Person([ShapeShiftConverter(typeof(NotAConverter))] int value) { }
			}

			public class NotAConverter { }
			""");

		await TestSources.AssertIdsAsync(diagnostics, "SHIFT001");
	}

	[Test]
	public async Task NoAttribute_ReportsNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync($$"""
			public class Person { }

			{{ValidConverter}}
			""");

		await TestSources.AssertIdsAsync(diagnostics);
	}

	[Test]
	public async Task IndirectConverterBase_IsAccepted()
	{
		// A converter may derive from ShapeShiftConverter<T, ...> through an intermediate base class.
		ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
			[ShapeShiftConverter(typeof(PersonConverter))]
			public class Person { }

			public abstract class PersonConverterBase : ShapeShiftConverter<Person, JsonEncoder, JsonDecoder>
			{
				public override Person? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => null;

				public override void Write(ref JsonEncoder encoder, in Person? value, SerializationContext<JsonEncoder, JsonDecoder> context) { }
			}

			public class PersonConverter : PersonConverterBase { }
			""");

		await TestSources.AssertIdsAsync(diagnostics);
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

	[Test]
	public async Task MakeConstructorPublic_FixAllUpdatesDistinctConverters()
	{
		string? fixedSource = await AnalyzerHarness.ApplyFixAllAsync(
			new ConverterAttributeAnalyzer(),
			new MakeConverterConstructorPublicCodeFixProvider(),
			TestSources.Source("""
				[ShapeShiftConverter(typeof(PersonConverter))]
				public class Person { }

				[ShapeShiftConverter(typeof(AddressConverter))]
				public class Address { }

				public class PersonConverter : ShapeShiftConverter<Person, JsonEncoder, JsonDecoder>
				{
					private PersonConverter() { }

					public override Person? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => null;

					public override void Write(ref JsonEncoder encoder, in Person? value, SerializationContext<JsonEncoder, JsonDecoder> context) { }
				}

				public class AddressConverter : ShapeShiftConverter<Address, JsonEncoder, JsonDecoder>
				{
					private AddressConverter() { }

					public override Address? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context) => null;

					public override void Write(ref JsonEncoder encoder, in Address? value, SerializationContext<JsonEncoder, JsonDecoder> context) { }
				}
				"""));

		await Assert.That(fixedSource).IsNotNull();
		await Assert.That(fixedSource!).Contains("public PersonConverter()");
		await Assert.That(fixedSource!).Contains("public AddressConverter()");
	}

	[Test]
	public async Task GeneratedConverterAttribute_ReportsNothing()
	{
		ImmutableArray<Diagnostic> diagnostics = await AnalyzerHarness.GetGeneratedCodeDiagnosticsAsync(
			new ConverterAttributeAnalyzer(),
			TestSources.Source("""
				[ShapeShiftConverter(typeof(NotAConverter))]
				public class Person { }

				public class NotAConverter { }
				"""));

		await TestSources.AssertIdsAsync(diagnostics);
	}

	private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
		[System.Diagnostics.CodeAnalysis.StringSyntax("c#-test")] string body)
		=> AnalyzerHarness.GetDiagnosticsAsync(new ConverterAttributeAnalyzer(), TestSources.Source(body));
}
