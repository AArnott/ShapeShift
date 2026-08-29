// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Json;

namespace TargetedDeserialization;

internal static partial class TargetedDeserializationSample
{
    internal static void Run()
    {
        #region TargetedDeserialization
        var serializer = new JsonSerializer();

        string json = """
            {
                "Name": "Ada",
                "Address": { "City": "London", "Zip": "E1" },
                "Tags": ["mathematician", "programmer"]
            }
            """;

        // Deserialize just one nested, strongly typed value, without allocating
        // or converting the rest of the document.
        bool found = serializer.TryDeserializeFragment<string, Witness>(
            json,
            new ShapeShiftPath("Address", "City"),
            out string? city);

        // Or deserialize a whole sub-object located at a path.
        Address? address = serializer.DeserializeFragment<Address>(json, new ShapeShiftPath("Address"));
        #endregion

        Console.WriteLine($"found: {found}, city: {city}");
        Console.WriteLine(address);
    }

    [GenerateShape]
    internal partial record Address(string City, string Zip);

    [GenerateShapeFor<string>]
    private partial class Witness;
}
