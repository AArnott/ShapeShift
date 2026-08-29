// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Json;

namespace JsonSerialization;

internal static partial class JsonSerializationSample
{
    internal static void Run()
    {
        #region JsonSerialization
        var serializer = new JsonSerializer
        {
            PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase,
            Indented = true,
        };

        var person = new Person("Ada", ["mathematics", "programming"]);
        string json = serializer.Serialize(person);
        Person? copy = serializer.Deserialize<Person>(json);
        #endregion

        Console.WriteLine(json);
        Console.WriteLine(copy);
    }

    [GenerateShape]
    internal partial record Person(string Name, List<string> Interests);
}
