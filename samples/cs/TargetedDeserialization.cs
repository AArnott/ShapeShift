// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Json;

namespace TargetedDeserialization;

/// <summary>
/// Shows how to deserialize just one value out of a larger document.
/// </summary>
public static partial class TargetedDeserializationSample
{
    /// <summary>
    /// Locates values with typed expressions and with a raw path.
    /// </summary>
    /// <returns>The values found at each location.</returns>
    public static (bool Found, string? City, string? Tag, string? Zip, string? SomeTag) Run()
    {
        #region TargetedDeserialization
        var serializer = new JsonSerializer { PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase };

        string json = """
            {
                "name": "Ada",
                "address": { "city": "London", "zip": "E1" },
                "tags": ["mathematician", "programmer"]
            }
            """;

        // Describe the location with an ordinary C# expression. The serializer translates it
        // through its own contract for Person, so the naming policy above (and any
        // [PropertyShape(Name = "...")] alias) is applied for you.
        ShapeShiftPath cityPath = serializer.GetPath((Person p) => p.Address.City);

        // Deserialize just that one nested, strongly typed value, without allocating
        // or converting the rest of the document.
        bool found = serializer.TryDeserializeFragment<string, Witness>(json, cityPath, out string? city);

        // Constant collection indexes work too, and so do whole sub-objects.
        string? tag = serializer.DeserializeFragment<string, Witness>(json, serializer.GetPath((Person p) => p.Tags[1]));
        Address? address = serializer.DeserializeFragment<Address>(json, serializer.GetPath((Person p) => p.Address));
        #endregion

        #region TargetedDeserializationRawPath
        // A raw path remains the right tool when the location is payload-driven rather than
        // type-driven: an index chosen at runtime, or a property no .NET type declares.
        int which = json.Contains("mathematician", StringComparison.Ordinal) ? 0 : 1;
        string? someTag = serializer.DeserializeFragment<string, Witness>(json, new ShapeShiftPath("tags", which));
        #endregion

        return (found, city, tag, address?.Zip, someTag);
    }

    [GenerateShape]
    internal partial record Address(string City, string Zip);

    [GenerateShape]
    internal partial record Person(string Name, Address Address, string[] Tags);

    [GenerateShapeFor<string>]
    private partial class Witness;
}
