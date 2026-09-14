// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShapeShift.Json.Tests;

/// <summary>
/// A deliberately small JSON Schema 2020-12 validator that covers exactly the keyword subset
/// that <see cref="JsonSchema"/> emits, so that tests can prove generated schemas actually accept
/// the documents the serializer produces (and reject documents it rejects).
/// </summary>
internal static class JsonSchemaValidator
{
	/// <summary>
	/// Validates an instance against a schema.
	/// </summary>
	/// <param name="schema">The root schema document.</param>
	/// <param name="instance">The instance to validate.</param>
	/// <returns>The validation errors, which is empty when the instance is valid.</returns>
	internal static IReadOnlyList<string> Validate(JsonObject schema, JsonNode? instance)
	{
		List<string> errors = new();
		Validate(schema, schema, instance, "#", errors);
		return errors;
	}

	/// <summary>
	/// Validates the JSON text of a serialized value against a schema.
	/// </summary>
	/// <param name="schema">The root schema document.</param>
	/// <param name="json">The JSON text to validate.</param>
	/// <returns>The validation errors, which is empty when the document is valid.</returns>
	internal static IReadOnlyList<string> Validate(JsonObject schema, string json)
		=> Validate(schema, JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true }));

	private static void Validate(JsonObject root, JsonNode? schemaNode, JsonNode? instance, string path, List<string> errors)
	{
		if (schemaNode is JsonValue booleanSchema)
		{
			if (booleanSchema.GetValueKind() == JsonValueKind.False)
			{
				errors.Add($"{path}: the schema rejects all values.");
			}

			return;
		}

		if (schemaNode is not JsonObject schema)
		{
			return;
		}

		if (schema["$ref"] is JsonNode reference)
		{
			Validate(root, Resolve(root, (string)reference!), instance, path, errors);
		}

		if (schema["type"] is JsonNode typeNode && !MatchesType(typeNode, instance))
		{
			errors.Add($"{path}: expected type {typeNode.ToJsonString()} but found {Describe(instance)}.");
			return;
		}

		if (schema["const"] is JsonNode constant && !JsonNode.DeepEquals(constant, instance))
		{
			errors.Add($"{path}: expected the constant {constant.ToJsonString()}.");
		}

		if (schema["enum"] is JsonArray allowed && !allowed.Any(candidate => JsonNode.DeepEquals(candidate, instance)))
		{
			errors.Add($"{path}: {Describe(instance)} is not one of {allowed.ToJsonString()}.");
		}

		ValidateComposition(root, schema, instance, path, errors);
		ValidateObject(root, schema, instance, path, errors);
		ValidateArray(root, schema, instance, path, errors);
		ValidateScalar(schema, instance, path, errors);
	}

	private static void ValidateComposition(JsonObject root, JsonObject schema, JsonNode? instance, string path, List<string> errors)
	{
		if (schema["allOf"] is JsonArray all)
		{
			foreach (JsonNode? branch in all)
			{
				Validate(root, branch, instance, path, errors);
			}
		}

		if (schema["anyOf"] is JsonArray any && !any.Any(branch => Matches(root, branch, instance)))
		{
			errors.Add($"{path}: {Describe(instance)} matched none of the anyOf branches.");
		}

		if (schema["oneOf"] is JsonArray one)
		{
			int matches = one.Count(branch => Matches(root, branch, instance));
			if (matches != 1)
			{
				errors.Add($"{path}: {Describe(instance)} matched {matches} oneOf branches; exactly one is required.");
			}
		}
	}

	private static void ValidateObject(JsonObject root, JsonObject schema, JsonNode? instance, string path, List<string> errors)
	{
		if (instance is not JsonObject obj)
		{
			return;
		}

		JsonObject? properties = schema["properties"] as JsonObject;
		if (schema["required"] is JsonArray required)
		{
			foreach (JsonNode? name in required)
			{
				if (!obj.ContainsKey((string)name!))
				{
					errors.Add($"{path}: missing required property '{(string)name!}'.");
				}
			}
		}

		if (schema["maxProperties"] is JsonNode maxProperties && obj.Count > (int)maxProperties)
		{
			errors.Add($"{path}: has {obj.Count} properties but at most {(int)maxProperties} are allowed.");
		}

		foreach (KeyValuePair<string, JsonNode?> property in obj)
		{
			if (properties?[property.Key] is JsonNode propertySchema)
			{
				Validate(root, propertySchema, property.Value, $"{path}/{property.Key}", errors);
			}
			else if (schema["additionalProperties"] is JsonNode additional)
			{
				Validate(root, additional, property.Value, $"{path}/{property.Key}", errors);
			}
		}
	}

	private static void ValidateArray(JsonObject root, JsonObject schema, JsonNode? instance, string path, List<string> errors)
	{
		if (instance is not JsonArray array)
		{
			return;
		}

		int prefixCount = 0;
		if (schema["prefixItems"] is JsonArray prefixItems)
		{
			prefixCount = prefixItems.Count;
			for (int i = 0; i < prefixItems.Count && i < array.Count; i++)
			{
				Validate(root, prefixItems[i], array[i], $"{path}/{i}", errors);
			}
		}

		if (schema["items"] is JsonNode items)
		{
			for (int i = prefixCount; i < array.Count; i++)
			{
				Validate(root, items, array[i], $"{path}/{i}", errors);
			}
		}

		if (schema["minItems"] is JsonNode minItems && array.Count < (int)minItems)
		{
			errors.Add($"{path}: has {array.Count} items but at least {(int)minItems} are required.");
		}

		if (schema["maxItems"] is JsonNode maxItems && array.Count > (int)maxItems)
		{
			errors.Add($"{path}: has {array.Count} items but at most {(int)maxItems} are allowed.");
		}

		if (schema["uniqueItems"] is JsonNode unique && (bool)unique)
		{
			HashSet<string> seen = new(StringComparer.Ordinal);
			foreach (JsonNode? item in array)
			{
				if (!seen.Add(item?.ToJsonString() ?? "null"))
				{
					errors.Add($"{path}: contains duplicate items.");
					break;
				}
			}
		}
	}

	private static void ValidateScalar(JsonObject schema, JsonNode? instance, string path, List<string> errors)
	{
		if (instance is not JsonValue value)
		{
			return;
		}

		if (value.GetValueKind() == JsonValueKind.String)
		{
			string text = (string)value!;
			if (schema["minLength"] is JsonNode minLength && text.Length < (int)minLength)
			{
				errors.Add($"{path}: '{text}' is shorter than {(int)minLength} characters.");
			}

			if (schema["maxLength"] is JsonNode maxLength && text.Length > (int)maxLength)
			{
				errors.Add($"{path}: '{text}' is longer than {(int)maxLength} characters.");
			}
		}

		if (value.GetValueKind() == JsonValueKind.Number)
		{
			if (schema["minimum"] is JsonNode minimum && Compare(value, minimum) < 0)
			{
				errors.Add($"{path}: {value.ToJsonString()} is less than {minimum.ToJsonString()}.");
			}

			if (schema["maximum"] is JsonNode maximum && Compare(value, maximum) > 0)
			{
				errors.Add($"{path}: {value.ToJsonString()} is greater than {maximum.ToJsonString()}.");
			}
		}
	}

	private static bool Matches(JsonObject root, JsonNode? schema, JsonNode? instance)
	{
		List<string> errors = new();
		Validate(root, schema, instance, "#", errors);
		return errors.Count == 0;
	}

	private static JsonNode? Resolve(JsonObject root, string reference)
	{
		JsonNode? node = root;
		foreach (string segment in reference.Split('/'))
		{
			if (segment is "#" or "")
			{
				continue;
			}

			node = node?[segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)];
		}

		return node;
	}

	private static bool MatchesType(JsonNode typeNode, JsonNode? instance)
		=> typeNode switch
		{
			JsonArray types => types.Any(t => MatchesType(t!, instance)),
			_ => MatchesType((string)typeNode!, instance),
		};

	private static bool MatchesType(string type, JsonNode? instance)
		=> type switch
		{
			"null" => instance is null || instance.GetValueKind() == JsonValueKind.Null,
			"boolean" => instance?.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
			"string" => instance?.GetValueKind() == JsonValueKind.String,
			"object" => instance is JsonObject,
			"array" => instance is JsonArray,
			"number" => instance?.GetValueKind() == JsonValueKind.Number,
			"integer" => instance?.GetValueKind() == JsonValueKind.Number && IsIntegral(instance),
			_ => false,
		};

	private static bool IsIntegral(JsonNode node)
		=> BigInteger.TryParse(node.ToJsonString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

	private static int Compare(JsonNode left, JsonNode right)
	{
		string leftText = left.ToJsonString();
		string rightText = right.ToJsonString();
		return BigInteger.TryParse(leftText, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger leftInteger)
			&& BigInteger.TryParse(rightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger rightInteger)
			? leftInteger.CompareTo(rightInteger)
			: double.Parse(leftText, CultureInfo.InvariantCulture).CompareTo(double.Parse(rightText, CultureInfo.InvariantCulture));
	}

	private static string Describe(JsonNode? instance) => instance?.ToJsonString() ?? "null";
}
