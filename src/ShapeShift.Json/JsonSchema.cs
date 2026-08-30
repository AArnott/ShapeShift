// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft;
using ShapeShift.Schema;

namespace ShapeShift.Json;

/// <summary>
/// Projects format-neutral <see cref="DataContract"/> graphs onto JSON Schema documents.
/// </summary>
/// <remarks>
/// <para>
/// The generated documents conform to the
/// <see href="https://json-schema.org/draft/2020-12/schema">2020-12</see> dialect.
/// </para>
/// <para>
/// Representations that JSON Schema cannot express are annotated with
/// <c>x-shapeshift-*</c> and (for <see cref="JsonSchemaProfile.MessagePack"/>) <c>x-msgpack-*</c> keywords.
/// Unknown extension keywords are ignored by conforming validators, so the documents remain valid
/// and usable by ordinary JSON Schema tooling.
/// </para>
/// </remarks>
public static class JsonSchema
{
	/// <summary>
	/// The URI of the JSON Schema dialect that this class emits.
	/// </summary>
	public const string Dialect = "https://json-schema.org/draft/2020-12/schema";

	/// <summary>
	/// Creates a JSON Schema document that describes a contract.
	/// </summary>
	/// <param name="contract">The contract to describe.</param>
	/// <param name="options">Options that influence the projection. May be <see langword="null" /> for defaults.</param>
	/// <returns>A mutable JSON Schema document.</returns>
	public static JsonObject Create(DataContract contract, JsonSchemaOptions? options = null)
	{
		Requires.NotNull(contract);
		return new Projector(options ?? JsonSchemaOptions.Default).Project(contract);
	}

	/// <summary>
	/// Builds JSON Schema documents from contract graphs.
	/// </summary>
	/// <param name="options">The options that influence the projection.</param>
	private sealed class Projector(JsonSchemaOptions options)
	{
		private readonly Dictionary<DataContract, int> referenceCounts = new();
		private readonly HashSet<DataContract> visiting = new();
		private readonly HashSet<DataContract> recursive = new();
		private readonly Dictionary<DataContract, string> definitionNames = new();
		private readonly HashSet<string> usedNames = new(StringComparer.Ordinal);

		/// <summary>
		/// Projects a contract graph onto a JSON Schema document.
		/// </summary>
		/// <param name="root">The root contract.</param>
		/// <returns>The document.</returns>
		internal JsonObject Project(DataContract root)
		{
			this.Analyze(root);

			// Assign stable names in a deterministic order before any schema is built,
			// so that $ref targets exist regardless of traversal order.
			foreach (DataContract contract in this.EnumerateDefinitions(root))
			{
				this.definitionNames[contract] = this.CreateDefinitionName(contract);
			}

			JsonObject definitions = new();
			foreach (KeyValuePair<DataContract, string> pair in this.definitionNames)
			{
				definitions[pair.Value] = this.Build(pair.Key, asDefinition: true);
			}

			JsonObject schema = this.Build(root, asDefinition: false);
			if (options.IncludeSchemaKeyword)
			{
				schema.Insert(0, "$schema", Dialect);
			}

			if (definitions.Count > 0)
			{
				schema["$defs"] = definitions;
			}

			return schema;
		}

		private static JsonNode? ToJsonNode(ShapeShiftValue value)
			=> value switch
			{
				ShapeShiftNull => null,
				ShapeShiftBoolean b => JsonValue.Create(b.Value),
				ShapeShiftInteger i => JsonValue.Create(i.Value),
				ShapeShiftUnsignedInteger i => JsonValue.Create(i.Value),
				ShapeShiftFloat f => JsonValue.Create(f.Value),
				ShapeShiftDecimal d => JsonValue.Create(d.Value),
				ShapeShiftBigInteger b => JsonNode.Parse(b.Value.ToString(CultureInfo.InvariantCulture)),
				ShapeShiftString s => JsonValue.Create(s.Value),
				ShapeShiftBinary b => JsonValue.Create(Convert.ToBase64String(b.Value.Span)),
				_ => null,
			};

		private static JsonObject Integer(long minimum, long maximum)
			=> new() { ["type"] = "integer", ["minimum"] = minimum, ["maximum"] = maximum };

		private static JsonObject IntegerText(string minimum, string maximum)
			=> new() { ["type"] = "integer", ["minimum"] = JsonNode.Parse(minimum), ["maximum"] = JsonNode.Parse(maximum) };

		/// <summary>
		/// Produces a schema that accepts everything the given schema accepts, plus <see langword="null" />.
		/// </summary>
		/// <param name="schema">The schema to relax.</param>
		/// <returns>The relaxed schema.</returns>
		private static JsonObject AllowNull(JsonObject schema)
		{
			switch (schema["type"])
			{
				case JsonValue value when value.TryGetValue(out string? typeName):
					if (typeName == "null")
					{
						return schema;
					}

					schema["type"] = new JsonArray(typeName, "null");
					return schema;
				case JsonArray types:
					if (!types.Any(t => (string?)t == "null"))
					{
						types.Add((JsonNode)"null");
					}

					return schema;
				case null when schema.Count == 0 || IsUnconstrained(schema):
					// The schema already accepts any JSON value, including null.
					return schema;
				default:
					return new JsonObject { ["anyOf"] = new JsonArray(schema, new JsonObject { ["type"] = "null" }) };
			}
		}

		/// <summary>
		/// Determines whether a schema places no constraints on the instance.
		/// </summary>
		/// <param name="schema">The schema to test.</param>
		/// <returns><see langword="true" /> if every JSON value satisfies the schema.</returns>
		private static bool IsUnconstrained(JsonObject schema)
			=> schema.All(p => p.Key.StartsWith("x-", StringComparison.Ordinal) || p.Key == "$comment");

		private static string SanitizeName(Type type)
		{
			string name = type.Name;
			int tick = name.IndexOf('`', StringComparison.Ordinal);
			if (tick >= 0)
			{
				name = name[..tick];
			}

			if (type.IsGenericType)
			{
				name = $"{name}_{string.Join("_", type.GetGenericArguments().Select(SanitizeName))}";
			}
			else if (type.IsArray)
			{
				name = $"{SanitizeName(type.GetElementType()!)}_Array";
			}

			return string.Create(name.Length, name, static (span, source) =>
			{
				for (int i = 0; i < source.Length; i++)
				{
					span[i] = char.IsLetterOrDigit(source[i]) || source[i] == '_' ? source[i] : '_';
				}
			});
		}

		private void Analyze(DataContract contract)
		{
			if (this.referenceCounts.TryGetValue(contract, out int count))
			{
				this.referenceCounts[contract] = count + 1;
				if (this.visiting.Contains(contract))
				{
					this.recursive.Add(contract);
				}

				return;
			}

			this.referenceCounts[contract] = 1;
			this.visiting.Add(contract);
			foreach (DataContract child in contract.ReferencedContracts)
			{
				this.Analyze(child);
			}

			this.visiting.Remove(contract);
		}

		/// <summary>
		/// Enumerates the contracts that deserve a <c>$defs</c> entry, in a stable depth-first order.
		/// </summary>
		/// <param name="root">The root contract.</param>
		/// <returns>The contracts to hoist.</returns>
		private IEnumerable<DataContract> EnumerateDefinitions(DataContract root)
		{
			HashSet<DataContract> seen = new();
			Stack<DataContract> stack = new();
			stack.Push(root);
			List<DataContract> ordered = new();
			while (stack.Count > 0)
			{
				DataContract contract = stack.Pop();
				if (!seen.Add(contract))
				{
					continue;
				}

				ordered.Add(contract);
				foreach (DataContract child in contract.ReferencedContracts.Reverse())
				{
					stack.Push(child);
				}
			}

			return ordered.Where(this.DeservesDefinition);
		}

		private bool DeservesDefinition(DataContract contract)
			=> this.recursive.Contains(contract)
				|| (this.referenceCounts[contract] > 1 && contract.Kind is DataContractKind.Object or DataContractKind.Union or DataContractKind.Enum or DataContractKind.Surrogate);

		private string CreateDefinitionName(DataContract contract)
		{
			string baseName = SanitizeName(contract.DataType);
			string name = baseName;
			for (int i = 2; !this.usedNames.Add(name); i++)
			{
				name = $"{baseName}{i}";
			}

			return name;
		}

		private JsonObject Build(DataContract contract, bool asDefinition)
		{
			if (!asDefinition && this.definitionNames.TryGetValue(contract, out string? name))
			{
				return new JsonObject { ["$ref"] = $"#/$defs/{name}" };
			}

			JsonObject schema = contract switch
			{
				PrimitiveContract primitive => this.BuildPrimitive(primitive),
				ObjectContract objectContract => this.BuildObject(objectContract),
				SequenceContract sequence => this.BuildSequence(sequence),
				RectangularArrayContract array => this.BuildRectangularArray(array),
				MapContract map => this.BuildMap(map),
				EnumContract enumContract => this.BuildEnum(enumContract),
				OptionalContract optional => AllowNull(this.Build(optional.ElementType, asDefinition: false)),
				UnionContract union => this.BuildUnion(union),
				SurrogateContract surrogate => this.BuildSurrogate(surrogate),
				DynamicContract => this.BuildDynamic(),
				UndocumentedContract undocumented => this.BuildUndocumented(undocumented),
				_ => new JsonObject(),
			};

			return schema;
		}

		private JsonObject BuildDynamic()
		{
			JsonObject schema = new();
			this.Comment(schema, "Any value that the format can represent.");
			return schema;
		}

		private JsonObject BuildUndocumented(UndocumentedContract contract)
		{
			JsonObject schema = new()
			{
				["x-shapeshift-undocumented"] = true,
			};
			if (contract.ConverterType is Type converterType)
			{
				schema["x-shapeshift-converter"] = converterType.FullName;
			}

			this.Comment(schema, contract.Reason);
			return schema;
		}

		private JsonObject BuildSurrogate(SurrogateContract contract)
		{
			JsonObject schema = this.Build(contract.SurrogateType, asDefinition: false);
			if (schema["$ref"] is not null)
			{
				schema = new JsonObject { ["allOf"] = new JsonArray(schema) };
			}

			schema["x-shapeshift-surrogate-for"] = contract.DataType.FullName;
			return schema;
		}

		private JsonObject BuildPrimitive(PrimitiveContract contract)
		{
			bool msgpack = options.Profile == JsonSchemaProfile.MessagePack;
			switch (contract.PrimitiveType)
			{
				case PrimitiveDataType.Boolean:
					return new JsonObject { ["type"] = "boolean" };
				case PrimitiveDataType.Char:
					return new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 1 };
				case PrimitiveDataType.Rune:
					JsonObject rune = Integer(0, 0x10FFFF);
					this.Comment(rune, "A Unicode scalar value, serialized as its numeric code point.");
					return rune;
				case PrimitiveDataType.String:
					JsonObject str = new() { ["type"] = "string" };
					if (options.Limits is { } stringLimits)
					{
						str["maxLength"] = stringLimits.MaxStringLength;
					}

					return str;
				case PrimitiveDataType.Binary:
					JsonObject binary = new() { ["type"] = "string", ["contentEncoding"] = "base64" };
					if (msgpack)
					{
						binary["x-msgpack-type"] = "bin";
						this.Comment(binary, "MessagePack encodes this with the bin family; the base64 form shown here is the JSON equivalent.");
					}

					if (options.Limits is { } binaryLimits)
					{
						binary["x-shapeshift-max-binary-length"] = binaryLimits.MaxBinaryLength;
					}

					return binary;
				case PrimitiveDataType.SByte:
					return Integer(sbyte.MinValue, sbyte.MaxValue);
				case PrimitiveDataType.Byte:
					return Integer(byte.MinValue, byte.MaxValue);
				case PrimitiveDataType.Int16:
					return Integer(short.MinValue, short.MaxValue);
				case PrimitiveDataType.UInt16:
					return Integer(ushort.MinValue, ushort.MaxValue);
				case PrimitiveDataType.Int32:
					return Integer(int.MinValue, int.MaxValue);
				case PrimitiveDataType.UInt32:
					return Integer(uint.MinValue, uint.MaxValue);
				case PrimitiveDataType.Int64:
					return Integer(long.MinValue, long.MaxValue);
				case PrimitiveDataType.UInt64:
					return IntegerText("0", ulong.MaxValue.ToString(CultureInfo.InvariantCulture));
				case PrimitiveDataType.Int128:
					JsonObject int128 = IntegerText(Int128.MinValue.ToString(CultureInfo.InvariantCulture), Int128.MaxValue.ToString(CultureInfo.InvariantCulture));
					return this.Extension(int128, msgpack, 101, "Int128 is serialized as a big-endian 16-byte MessagePack extension.");
				case PrimitiveDataType.UInt128:
					JsonObject uint128 = IntegerText("0", UInt128.MaxValue.ToString(CultureInfo.InvariantCulture));
					return this.Extension(uint128, msgpack, 102, "UInt128 is serialized as a big-endian 16-byte MessagePack extension.");
				case PrimitiveDataType.BigInteger:
					JsonObject bigInteger = new() { ["type"] = "integer" };
					return this.Extension(bigInteger, msgpack, 103, "BigInteger is serialized as a big-endian two's complement MessagePack extension.");
				case PrimitiveDataType.Half:
				case PrimitiveDataType.Single:
				case PrimitiveDataType.Double:
					return this.Number();
				case PrimitiveDataType.Decimal:
					JsonObject dec = new() { ["type"] = "number" };
					this.Comment(dec, "A decimal value. JSON preserves the full precision as a number literal.");
					return this.Extension(dec, msgpack, 100, "decimal is serialized as a MessagePack extension carrying its four constituent 32-bit words.");
				case PrimitiveDataType.DateTime:
					JsonObject dateTime = new() { ["type"] = "string", ["format"] = "date-time" };
					return this.Extension(dateTime, msgpack, -1, "DateTime is serialized as the standard MessagePack timestamp extension.");
				case PrimitiveDataType.DateTimeOffset:
					JsonObject offset = new()
					{
						["type"] = "array",
						["prefixItems"] = new JsonArray(
							new JsonObject { ["type"] = "string", ["format"] = "date-time" },
							Integer(-840, 840)),
						["minItems"] = 2,
						["maxItems"] = 2,
						["items"] = false,
					};
					this.Comment(offset, "A two-element array: the UTC date and time, then the offset in minutes.");
					if (msgpack)
					{
						offset["prefixItems"]![0]!["x-msgpack-extension"] = -1;
					}

					return offset;
				case PrimitiveDataType.TimeSpan:
					JsonObject timeSpan = new() { ["type"] = "string" };
					this.Comment(timeSpan, "A time interval formatted with the invariant \"c\" specifier, for example \"1.02:03:04.0050000\".");
					if (msgpack)
					{
						timeSpan["type"] = "integer";
						timeSpan["x-msgpack-extension"] = 104;
						this.Comment(timeSpan, "MessagePack instead serializes the interval as a tick count carried by an extension.");
					}

					return timeSpan;
				default:
					return new JsonObject();
			}
		}

		private JsonObject Extension(JsonObject schema, bool msgpack, int extensionCode, string comment)
		{
			if (msgpack)
			{
				schema["x-msgpack-extension"] = extensionCode;
				this.Comment(schema, comment);
			}

			return schema;
		}

		private JsonObject Number()
		{
			JsonObject number = new() { ["type"] = "number" };
			if (options.Profile == JsonSchemaProfile.Json && options.AllowNamedFloatingPointValues)
			{
				return new JsonObject
				{
					["anyOf"] = new JsonArray(
						number,
						new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("NaN", "Infinity", "-Infinity") }),
				};
			}

			return number;
		}

		private JsonObject BuildObject(ObjectContract contract)
		{
			if (contract.Encoding == ObjectEncoding.Positional)
			{
				return this.BuildPositionalObject(contract);
			}

			JsonObject properties = new();
			JsonArray required = new();
			foreach (PropertyContract property in contract.Properties)
			{
				JsonObject propertySchema = this.Build(property.Type, asDefinition: false);
				if (property.IsNullable)
				{
					propertySchema = AllowNull(propertySchema);
				}

				if (property.DefaultValue is { } defaultValue)
				{
					propertySchema["default"] = ToJsonNode(defaultValue);
				}

				if (!property.IsWritable)
				{
					propertySchema["readOnly"] = true;
				}
				else if (!property.IsReadable)
				{
					propertySchema["writeOnly"] = true;
				}

				properties[property.Name] = propertySchema;
				if (property.IsRequired)
				{
					required.Add((JsonNode)property.Name);
				}
			}

			JsonObject schema = new()
			{
				["type"] = "object",
				["properties"] = properties,
			};

			if (required.Count > 0)
			{
				schema["required"] = required;
			}

			if (contract.HasExtensionData)
			{
				this.Comment(schema, "Unrecognized properties are round-tripped through this type's extension data property.");
			}
			else
			{
				schema["additionalProperties"] = false;
			}

			if (options.Limits is { } limits && contract.HasExtensionData)
			{
				schema["maxProperties"] = limits.MaxCollectionLength;
			}

			if (options.Profile == JsonSchemaProfile.MessagePack)
			{
				schema["x-msgpack-type"] = "map";
			}

			return schema;
		}

		private JsonObject BuildPositionalObject(ObjectContract contract)
		{
			// A positional contract is an array whose elements are identified by index, so the natural JSON Schema
			// projection is prefixItems. Positions no member claims (retired ones, or gaps a contract left for
			// future use) are described as nulls, because that is exactly what a writer emits for them.
			Dictionary<int, PropertyContract> byPosition = new();
			int highest = -1;
			foreach (PropertyContract property in contract.Properties)
			{
				if (property.Position is not int position)
				{
					continue;
				}

				byPosition[position] = property;
				highest = Math.Max(highest, position);
			}

			JsonArray prefixItems = new();
			int lastRequired = -1;
			for (int i = 0; i <= highest; i++)
			{
				if (!byPosition.TryGetValue(i, out PropertyContract? property))
				{
					JsonObject placeholder = new() { ["type"] = "null" };
					this.Comment(placeholder, $"Position {i} is not used by this contract; a writer emits null and a reader ignores whatever it finds.");
					prefixItems.Add(placeholder);
					continue;
				}

				JsonObject elementSchema = this.Build(property.Type, asDefinition: false);
				if (property.IsNullable)
				{
					elementSchema = AllowNull(elementSchema);
				}

				if (property.DefaultValue is { } defaultValue)
				{
					elementSchema["default"] = ToJsonNode(defaultValue);
				}

				elementSchema["title"] = property.Name;
				if (property.IsRequired)
				{
					lastRequired = i;
				}

				prefixItems.Add(elementSchema);
			}

			JsonObject schema = new()
			{
				["type"] = "array",
				["prefixItems"] = prefixItems,
				["minItems"] = lastRequired + 1,
			};
			this.Comment(
				schema,
				"A positional contract: each element is identified by its index rather than by a property name. A shorter array leaves the remaining members at their default values; a longer one carries members this contract does not know about.");

			if (options.Profile == JsonSchemaProfile.MessagePack)
			{
				schema["x-msgpack-type"] = "array";
			}

			return schema;
		}

		private JsonObject BuildSequence(SequenceContract contract)
		{
			JsonObject schema = new()
			{
				["type"] = "array",
				["items"] = this.Build(contract.ElementType, asDefinition: false),
			};

			if (contract.IsSet)
			{
				schema["uniqueItems"] = true;
			}

			if (options.Limits is { } limits)
			{
				schema["maxItems"] = limits.MaxCollectionLength;
			}

			if (options.Profile == JsonSchemaProfile.MessagePack)
			{
				schema["x-msgpack-type"] = "array";
			}

			return schema;
		}

		private JsonObject BuildRectangularArray(RectangularArrayContract contract)
		{
			JsonObject dimensions = new()
			{
				["type"] = "array",
				["items"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
				["minItems"] = contract.Rank,
				["maxItems"] = contract.Rank,
			};

			JsonObject values = new()
			{
				["type"] = "array",
				["items"] = this.Build(contract.ElementType, asDefinition: false),
			};

			JsonObject schema = new()
			{
				["type"] = "array",
				["prefixItems"] = new JsonArray(dimensions, values),
				["minItems"] = 2,
				["maxItems"] = 2,
				["items"] = false,
			};
			this.Comment(schema, $"A rank-{contract.Rank} array: the lengths of each dimension, followed by the elements in row-major order.");
			if (options.Profile == JsonSchemaProfile.MessagePack)
			{
				schema["x-msgpack-type"] = "array";
				dimensions["x-msgpack-type"] = "array";
				values["x-msgpack-type"] = "array";
			}

			return schema;
		}

		private JsonObject BuildMap(MapContract contract)
		{
			if (contract.Encoding == MapEncoding.StringKeyedMap)
			{
				JsonObject schema = new()
				{
					["type"] = "object",
					["additionalProperties"] = this.Build(contract.ValueType, asDefinition: false),
				};
				if (options.Limits is { } stringMapLimits)
				{
					schema["maxProperties"] = stringMapLimits.MaxCollectionLength;
				}

				if (options.Profile == JsonSchemaProfile.MessagePack)
				{
					schema["x-msgpack-type"] = "map";
				}

				return schema;
			}

			JsonObject entry = new()
			{
				["type"] = "array",
				["prefixItems"] = new JsonArray(
					this.Build(contract.KeyType, asDefinition: false),
					this.Build(contract.ValueType, asDefinition: false)),
				["minItems"] = 2,
				["maxItems"] = 2,
				["items"] = false,
			};

			JsonObject entries = new()
			{
				["type"] = "array",
				["items"] = entry,
			};
			this.Comment(entries, "Keys are not strings, so entries are written as an array of [key, value] pairs.");
			if (options.Limits is { } limits)
			{
				entries["maxItems"] = limits.MaxCollectionLength;
			}

			if (options.Profile == JsonSchemaProfile.MessagePack)
			{
				entries["x-msgpack-type"] = "array";
				entry["x-msgpack-type"] = "array";
			}

			return entries;
		}

		private JsonObject BuildEnum(EnumContract contract)
		{
			JsonArray names = new();
			foreach (EnumMemberContract member in contract.Members)
			{
				names.Add((JsonNode)member.Name);
			}

			JsonObject byName = new() { ["type"] = "string" };
			if (names.Count > 0)
			{
				byName["enum"] = names;
			}

			JsonObject byNumber = this.Build(contract.UnderlyingType, asDefinition: false);
			JsonObject schema = new()
			{
				["anyOf"] = contract.IsSerializedByName
					? new JsonArray(byName, byNumber)
					: new JsonArray(byNumber, byName),
				["x-shapeshift-enum-serialized-as"] = contract.IsSerializedByName ? "name" : "number",
			};

			if (contract.IsFlags)
			{
				schema["x-shapeshift-enum-flags"] = true;
				this.Comment(schema, "A flags enum. Combinations that do not match a declared member are written as a number.");
			}
			else
			{
				this.Comment(schema, "Written using the first form; both forms are accepted when reading. Names are matched case-insensitively.");
			}

			return schema;
		}

		private JsonObject BuildUnion(UnionContract contract)
		{
			JsonArray cases = new()
			{
				this.BuildUnionCase(null, contract.BaseType),
			};

			foreach (UnionCaseContract unionCase in contract.Cases)
			{
				JsonNode discriminator = unionCase.IsTagSpecified ? unionCase.Tag : unionCase.Name;
				cases.Add(this.BuildUnionCase(discriminator, unionCase.Type));
			}

			JsonObject schema = new() { ["oneOf"] = cases };
			this.Comment(schema, "A two-element array: a discriminator, then the value. A null discriminator selects the base type.");
			return schema;
		}

		private JsonObject BuildUnionCase(JsonNode? discriminator, DataContract valueContract)
		{
			JsonObject discriminatorSchema = discriminator is null
				? new JsonObject { ["type"] = "null" }
				: new JsonObject { ["const"] = discriminator };

			return new JsonObject
			{
				["type"] = "array",
				["prefixItems"] = new JsonArray(discriminatorSchema, this.Build(valueContract, asDefinition: false)),
				["minItems"] = 2,
				["maxItems"] = 2,
				["items"] = false,
			};
		}

		private void Comment(JsonObject schema, string comment)
		{
			if (options.IncludeComments)
			{
				schema["$comment"] = schema["$comment"] is JsonValue existing && existing.TryGetValue(out string? text)
					? $"{text} {comment}"
					: comment;
			}
		}
	}
}
