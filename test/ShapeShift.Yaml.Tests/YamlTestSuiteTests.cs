// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace ShapeShift.Yaml.Tests;

/// <summary>
/// Checks accept/reject behavior against the YAML Test Suite catalogs used by the compliance suite.
/// </summary>
/// <remarks>
/// The embedded catalogs are derived from the YAML Test Suite (https://github.com/yaml/yaml-test-suite),
/// Copyright (c) 2016-2020 Ingy döt Net, MIT License. The license text is Data/yaml-test-suite-LICENSE.txt.
/// A case tagged fail or error must be rejected. Every other case must be accepted. The check reads one
/// value and then requires <see cref="TokenType.EndDocument"/>, which is how a single-document consumer
/// stops at the next document in a stream.
/// </remarks>
public class YamlTestSuiteTests
{
	/// <summary>
	/// Original catalog extras that ask for a stricter reading than the official suite.
	/// <c>yaml122-duplicate-key</c> rejects repeated keys. YAML 1.2 calls that an error, and the
	/// official suite still accepts the same situation (<c>yts-2JQS-1</c>, <c>yts-X38W-1</c>).
	/// <see cref="YamlLoadOptions.RejectDuplicateKeys"/> opts into the stricter check.
	/// </summary>
	private static readonly HashSet<string> KnownExclusions = ["yaml122-duplicate-key"];

	[Test]
	[Arguments("yaml-1.1-suite.json", 417)]
	[Arguments("yaml-1.2-suite.json", 415)]
	[Arguments("yaml-1.2.2-suite.json", 413)]
	public async Task CatalogMatchesRequiredAcceptance(string resourceName, int expectedTotal)
	{
		await using Stream stream = OpenResource(resourceName);
		using JsonDocument document = JsonDocument.Parse(stream);
		JsonElement cases = document.RootElement.GetProperty("cases");
		int total = 0;
		List<string> failures = [];
		List<string> exclusions = [];
		foreach (JsonElement testCase in cases.EnumerateArray())
		{
			total++;
			string id = testCase.GetProperty("id").GetString()!;
			string expect = testCase.GetProperty("expect").GetString()!;
			string input = testCase.GetProperty("input").GetString()!;
			bool accepted = TryAccept(input, out string error);
			bool ok = expect == "accept" ? accepted : !accepted;
			if (KnownExclusions.Contains(id))
			{
				exclusions.Add(id);
				continue;
			}

			if (!ok)
			{
				failures.Add($"{id} expected {expect} but {(accepted ? "accepted" : error)}");
			}
		}

		await Assert.That(total).IsEqualTo(expectedTotal);
		await Assert.That(string.Join(',', exclusions)).IsEqualTo(resourceName == "yaml-1.2.2-suite.json" ? "yaml122-duplicate-key" : string.Empty);
		await Assert.That(failures).IsEmpty();
	}

	private static Stream OpenResource(string resourceName)
	{
		Stream? stream = typeof(YamlTestSuiteTests).Assembly.GetManifestResourceStream(resourceName);
		if (stream is null)
		{
			throw new InvalidOperationException($"Missing embedded resource {resourceName}.");
		}

		return stream;
	}

	private static bool TryAccept(string yaml, out string error)
	{
		try
		{
			YamlDecoder decoder = new(new StringReader(yaml));
			ReadValue(ref decoder);
			if (decoder.NextTokenType != TokenType.EndDocument)
			{
				error = "trailing " + decoder.NextTokenType;
				return false;
			}

			error = string.Empty;
			return true;
		}
		catch (Exception ex)
		{
			error = ex.GetType().Name + ": " + ex.Message;
			return false;
		}
	}

	private static void ReadValue(ref YamlDecoder decoder)
	{
		switch (decoder.NextTokenType)
		{
			case TokenType.Null:
				decoder.ReadNull();
				return;
			case TokenType.Boolean:
				_ = decoder.ReadBoolean();
				return;
			case TokenType.String:
				_ = decoder.ReadString();
				return;
			case TokenType.Number:
				_ = decoder.ReadDynamicNumber();
				return;
			case TokenType.Binary:
				_ = decoder.ReadByteArray();
				return;
			case TokenType.StartVector:
				decoder.ReadStartVector();
				while (decoder.NextTokenType != TokenType.EndVector)
				{
					ReadValue(ref decoder);
				}

				decoder.ReadEndVector();
				return;
			case TokenType.StartMap:
				decoder.ReadStartMap();
				while (decoder.NextTokenType != TokenType.EndMap)
				{
					if (decoder.NextTokenType == TokenType.PropertyName)
					{
						_ = decoder.ReadPropertyName();
					}
					else
					{
						ReadValue(ref decoder);
					}

					ReadValue(ref decoder);
				}

				decoder.ReadEndMap();
				return;
			default:
				throw new InvalidOperationException("Unsupported token " + decoder.NextTokenType);
		}
	}
}
