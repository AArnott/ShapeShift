// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Yaml.Tests;

public partial class YamlSyntaxTests
{
	[Test]
	public async Task FlowCollectionsAndCoreSchema()
	{
		await Assert.That(string.Join('|', ReadFlow())).IsEqualTo("1|true|no|16|8|Infinity");
	}

	[Test]
	public async Task FlowMapping()
	{
		await Assert.That(string.Join('|', ReadFlowMap())).IsEqualTo("name|Ada|count|2");
	}

	[Test]
	public async Task BlockScalars()
	{
		await Assert.That(ReadString("|\n  line1\n  line2\n")).IsEqualTo("line1\nline2\n");
		await Assert.That(ReadString(">\n  hello\n  world\n")).IsEqualTo("hello world\n");
		await Assert.That(ReadString("|-\n  kept\n")).IsEqualTo("kept");
		await Assert.That(ReadString("|2\n  text\n")).IsEqualTo("text\n");
		await Assert.That(ReadString("|\n  text")).IsEqualTo("text");
		await Assert.That(ReadString("|+\n  text")).IsEqualTo("text");
		await Assert.That(ReadString("|+\n  text\n\n")).IsEqualTo("text\n\n");
		await Assert.That(ReadString(">\n  hello\n\n  world\n")).IsEqualTo("hello\nworld\n");
		await Assert.That(ReadString(">\n  foo \n  bar\n")).IsEqualTo("foo  bar\n");
		await Assert.That(ReadString(">\n\n  folded\n  text\n")).IsEqualTo("\nfolded text\n");
	}

	[Test]
	public async Task AnchorsAliasesAndAliasKeys()
	{
		await Assert.That(string.Join('|', ReadMapStrings("key: &a value\ncopy: *a\n"))).IsEqualTo("key|value|copy|value");
		await Assert.That(string.Join('|', ReadMapStrings("&a: key: &a value\nfoo:\n  *a:\n"))).IsEqualTo("key|value|foo|key");
	}

	[Test]
	public async Task TagsDirectivesAndMultipleDocuments()
	{
		await Assert.That(ReadString("!!str 1\n")).IsEqualTo("1");
		await Assert.That(ReadString("%TAG !e! tag:example.com,2002:\n---\n!e!widget 1\n")).IsEqualTo("1");

		IReadOnlyList<int> values = new YamlSerializer().DeserializeAll<int, Witness>("---\n1\n---\n2\n");
		await Assert.That(values.Count).IsEqualTo(2);
		await Assert.That(values[0]).IsEqualTo(1);
		await Assert.That(values[1]).IsEqualTo(2);
	}

	[Test]
	public async Task SequenceMayStartAtTheParentKeyColumn()
	{
		await Assert.That(string.Join('|', ReadParentSequence())).IsEqualTo("2|3");
	}

	[Test]
	public async Task ExplicitKeysMayHoldZeroIndentedSequences()
	{
		await Assert.That(string.Join('|', ReadZeroIndentSequences())).IsEqualTo("a|b|c|d");
	}

	[Test]
	public async Task PlainScalarsKeepIndicatorsAndTabOnlyLines()
	{
		await Assert.That(ReadString("---\nk:#foo\n &a !t s\n")).IsEqualTo("k:#foo &a !t s");
		await Assert.That(string.Join('|', ReadMapStrings("key:\n  value\n  with\n  \t\n  tabs\n"))).IsEqualTo("key|value with\ntabs");
	}

	[Test]
	public async Task TrailingSpacesAfterAFlowCollectionAreNotContent()
	{
		await Assert.That(string.Join('|', ReadSpacedFlow())).IsEqualTo("1|2|3");
	}

	[Test]
	public async Task EmptyTaggedScalarsAndCompactExplicitMappings()
	{
		await Assert.That(string.Join('|', ReadTaggedScalars())).IsEqualTo("s:|p:|s:a|p:b|s:|p:|null");
	}

	[Test]
	public async Task DuplicateKeysAreRejectedUnlessAllowed()
	{
		await Assert.That(() => Open(": a\n: b\n")).Throws<DecoderException>();
		await Assert.That(() => Open("name: Ada\nname: Grace\n")).Throws<DecoderException>();
		await Assert.That(string.Join('|', ReadMapStrings(": a\n: b\n", new YamlLoadOptions { AllowDuplicateKeys = true }))).IsEqualTo("|a||b");
		await Assert.That(() => Open("1: a\n0x1: b\n")).Throws<DecoderException>();
		await Assert.That(string.Join('|', ReadMapStrings("1: a\n\"1\": b\n"))).IsEqualTo("1|a|1|b");
		await Assert.That(ReadDistinctSequenceKeys()).IsEqualTo("a|b|first|a;S=b|second");
		await Assert.That(() => Open(AliasBombKeys(), new YamlLoadOptions { MaxAliasExpansions = 8 })).Throws<DecoderException>();
	}

	[Test]
	public async Task ExplicitFloatAndNonSpecificTags()
	{
		YamlDecoder floated = Open("!!float 1\n");
		await Assert.That(floated.ReadDouble()).IsEqualTo(1d);
		YamlDecoder quoted = Open("!!float \"12\"\n");
		await Assert.That(quoted.ReadDouble()).IsEqualTo(12d);
		YamlDecoder integer = Open("1\n");
		await Assert.That(integer.ReadInt32()).IsEqualTo(1);
		await Assert.That(ReadString("! true\n")).IsEqualTo("true");
		await Assert.That(ReadString("! 12\n")).IsEqualTo("12");
		await Assert.That(ReadString("! null\n")).IsEqualTo("null");
	}

	[Test]
	public async Task DecimalsKeepScalarText()
	{
		const string precise = "0.1234567890123456789012345678";
		YamlDecoder decoder = Open(precise + "\n");
		await Assert.That(decoder.ReadDecimal()).IsEqualTo(decimal.Parse(precise, System.Globalization.CultureInfo.InvariantCulture));
		YamlDecoder dynamic = Open(precise + "\n");
		await Assert.That(dynamic.ReadDynamicNumber()).IsEqualTo(new ShapeShiftDecimal(decimal.Parse(precise, System.Globalization.CultureInfo.InvariantCulture)));
		await Assert.That(() => Open("1e-100\n").ReadDecimal()).Throws<DecoderException>();
		YamlDecoder tiny = Open("1e-100\n");
		await Assert.That(tiny.ReadDynamicNumber()).IsEqualTo(new ShapeShiftFloat(double.Parse("1e-100", System.Globalization.CultureInfo.InvariantCulture)));
	}

	[Test]
	public async Task WriterQuotesScalarsTheReaderWouldRetype()
	{
		YamlSerializer serializer = new();
		foreach (string value in new[] { "0x10", "0o10", ".inf", ".nan", "[a]", "{a}" })
		{
			string yaml = serializer.Serialize<string, StringWitness>(value);
			await Assert.That(serializer.Deserialize<string, StringWitness>(yaml)).IsEqualTo(value);
		}
	}

	[Test]
	public async Task FlowLookaheadHonorsMaxDepth()
	{
		string shallow = "[[[1]]]\n";
		YamlDecoder decoder = Open(shallow, new YamlLoadOptions { MaxDepth = 4 });
		decoder.ReadStartVector();
		decoder.ReadStartVector();
		decoder.ReadStartVector();
		await Assert.That(decoder.ReadInt32()).IsEqualTo(1);
		string deep = new string('[', 32) + "1" + new string(']', 32) + "\n";
		await Assert.That(() => Open(deep, new YamlLoadOptions { MaxDepth = 4 })).Throws<DecoderException>();
	}

	[Test]
	public async Task InvalidStructureIsRejected()
	{
		await Assert.That(() => Open("map:\n  key: value\n bad\n")).Throws<DecoderException>();
		await Assert.That(() => Open("%YAML 2.0\n---\n1\n")).Throws<DecoderException>();
		await Assert.That(() => Open("key: &a *a\n")).Throws<DecoderException>();
	}

	[Test]
	public async Task CommentOnlyStreamIsAnEmptyDocumentList()
	{
		(int count, bool sawNull) = ReadCommentOnly();
		await Assert.That(count).IsEqualTo(0);
		await Assert.That(sawNull).IsTrue();
		await Assert.That(new YamlSerializer().DeserializeAll<int, Witness>("# nothing\n").Count).IsEqualTo(0);
	}

	[Test]
	public async Task WriterStillEmitsIndentedBlockStyle()
	{
		string yaml = new YamlSerializer().Serialize<Person, Person>(new Person { FirstName = "Ada", LastName = "Lovelace" });
		await Assert.That(yaml.Replace("\r\n", "\n", StringComparison.Ordinal)).IsEqualTo("  FirstName: Ada\n  LastName: Lovelace\n");
	}

	private static List<string> ReadFlow()
	{
		YamlDecoder decoder = Open("[1, true, no, 0x10, 0o10, .inf]");
		decoder.ReadStartVector();
		List<string> values =
		[
			decoder.ReadInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
			decoder.ReadBoolean() ? "true" : "false",
			decoder.ReadString(),
			decoder.ReadInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
			decoder.ReadInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
			decoder.ReadDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
		];
		decoder.ReadEndVector();
		if (decoder.NextTokenType != TokenType.EndDocument)
		{
			throw new InvalidOperationException(decoder.NextTokenType.ToString());
		}

		return values;
	}

	private static List<string> ReadFlowMap()
	{
		YamlDecoder decoder = Open("{name: Ada, count: 2}");
		decoder.ReadStartMap();
		List<string> values =
		[
			decoder.ReadPropertyName().ToString(),
			decoder.ReadString(),
			decoder.ReadPropertyName().ToString(),
			decoder.ReadInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
		];
		decoder.ReadEndMap();
		return values;
	}

	private static string ReadString(string yaml)
	{
		YamlDecoder decoder = Open(yaml);
		string value = decoder.ReadString();
		if (decoder.NextTokenType != TokenType.EndDocument)
		{
			throw new InvalidOperationException(decoder.NextTokenType.ToString());
		}

		return value;
	}

	private static List<string> ReadMapStrings(string yaml, YamlLoadOptions? options = null)
	{
		YamlDecoder decoder = Open(yaml, options);
		decoder.ReadStartMap();
		List<string> values = [];
		while (decoder.NextTokenType != TokenType.EndMap)
		{
			values.Add(decoder.ReadPropertyName().ToString());
			values.Add(decoder.NextTokenType == TokenType.String ? decoder.ReadString() : decoder.ReadInt32().ToString(System.Globalization.CultureInfo.InvariantCulture));
		}

		decoder.ReadEndMap();
		return values;
	}

	private static List<int> ReadParentSequence()
	{
		YamlDecoder decoder = Open("one:\n- 2\n- 3\n");
		decoder.ReadStartMap();
		_ = decoder.ReadPropertyName();
		decoder.ReadStartVector();
		List<int> values = [decoder.ReadInt32(), decoder.ReadInt32()];
		decoder.ReadEndVector();
		decoder.ReadEndMap();
		return values;
	}

	private static List<string> ReadZeroIndentSequences()
	{
		YamlDecoder decoder = Open("---\n?\n- a\n- b\n:\n- c\n- d\n");
		decoder.ReadStartMap();
		List<string> values = [];
		decoder.ReadStartVector();
		values.Add(decoder.ReadString());
		values.Add(decoder.ReadString());
		decoder.ReadEndVector();
		decoder.ReadStartVector();
		values.Add(decoder.ReadString());
		values.Add(decoder.ReadString());
		decoder.ReadEndVector();
		decoder.ReadEndMap();
		if (decoder.NextTokenType != TokenType.EndDocument)
		{
			throw new InvalidOperationException(decoder.NextTokenType.ToString());
		}

		return values;
	}

	private static List<int> ReadSpacedFlow()
	{
		YamlDecoder decoder = Open("  [1, 2, 3]  \n  ");
		decoder.ReadStartVector();
		List<int> values = [decoder.ReadInt32(), decoder.ReadInt32(), decoder.ReadInt32()];
		decoder.ReadEndVector();
		if (decoder.NextTokenType != TokenType.EndDocument)
		{
			throw new InvalidOperationException(decoder.NextTokenType.ToString());
		}

		return values;
	}

	private static List<string> ReadTaggedScalars()
	{
		YamlDecoder decoder = Open("- !!str\n-\n  !!null : a\n  b: !!str\n- !!str : !!null\n");
		decoder.ReadStartVector();
		List<string> values = ["s:" + decoder.ReadString()];
		decoder.ReadStartMap();
		values.Add("p:" + decoder.ReadPropertyName().ToString());
		values.Add("s:" + decoder.ReadString());
		values.Add("p:" + decoder.ReadPropertyName().ToString());
		values.Add("s:" + decoder.ReadString());
		decoder.ReadEndMap();
		decoder.ReadStartMap();
		values.Add("p:" + decoder.ReadPropertyName().ToString());
		values.Add(decoder.TryReadNull() ? "null" : "not-null");
		decoder.ReadEndMap();
		decoder.ReadEndVector();
		return values;
	}

	private static (int Count, bool SawNull) ReadCommentOnly()
	{
		YamlDecoder decoder = Open("# nothing\n");
		bool sawNull = decoder.TryReadNull();
		if (decoder.NextTokenType != TokenType.EndDocument)
		{
			throw new InvalidOperationException(decoder.NextTokenType.ToString());
		}

		return (decoder.DocumentCount, sawNull);
	}

	private static string ReadDistinctSequenceKeys()
	{
		YamlDecoder decoder = Open("{[a, b]: first, ['a;S=b']: second}\n");
		decoder.ReadStartMap();
		List<string> values = [];
		decoder.ReadStartVector();
		values.Add(decoder.ReadString());
		values.Add(decoder.ReadString());
		decoder.ReadEndVector();
		values.Add(decoder.ReadString());
		decoder.ReadStartVector();
		values.Add(decoder.ReadString());
		decoder.ReadEndVector();
		values.Add(decoder.ReadString());
		decoder.ReadEndMap();
		return string.Join('|', values);
	}

	private static string AliasBombKeys()
	{
		return """
			a: &a [1, 2]
			b: &b [*a, *a, *a, *a]
			c: &c [*b, *b, *b, *b]
			d: &d [*c, *c, *c, *c]
			*d: 1
			*d: 2
			""";
	}

	private static YamlDecoder Open(string yaml, YamlLoadOptions? options = null) => new(new StringReader(yaml), options);

	[GenerateShape]
	internal partial record Person
	{
		public string? FirstName { get; set; }

		public string? LastName { get; set; }
	}

	[GenerateShapeFor<int>]
	private partial class Witness;

	[GenerateShapeFor<string>]
	private partial class StringWitness;
}
