// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Toml;

namespace TomlSerialization;

internal static partial class TomlSerializationSample
{
    internal static void Run()
    {
        #region TomlSerialization
        TomlSerializer serializer = new();
        Person person = new("Ada", ["mathematics", "programming"]);

        string toml = serializer.Serialize(person);
        Person? copy = serializer.Deserialize<Person>(toml);
        #endregion

        Console.WriteLine(toml);
        Console.WriteLine(copy);
    }

    [GenerateShape]
    internal partial record Person(string Name, List<string> Interests);
}
