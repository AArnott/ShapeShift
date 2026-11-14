// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Yaml;

namespace YamlSerialization;

internal static partial class YamlSerializationSample
{
    internal static void Run()
    {
        #region YamlSerialization
        YamlSerializer serializer = new();
        Person person = new("Ada", ["mathematics", "programming"]);

        string yaml = serializer.Serialize(person);
        Person? copy = serializer.Deserialize<Person>(yaml);
        #endregion

        Console.WriteLine(yaml);
        Console.WriteLine(copy);
    }

    internal static void ReadDocuments()
    {
        #region YamlDocuments
        YamlSerializer serializer = new();
        IReadOnlyList<Note?> notes = serializer.DeserializeAll<Note>("Text: one\n---\nText: two\n");
        #endregion

        Console.WriteLine(notes.Count);
    }

    internal static void ConfigureLoad()
    {
        #region YamlLoadOptions
        YamlSerializer serializer = new()
        {
            LoadOptions = new YamlLoadOptions
            {
                RejectDuplicateKeys = true,
                MaxDepth = 64,
                MaxAliasExpansions = 1024,
            },
        };
        #endregion

        _ = serializer;
    }

    [GenerateShape]
    internal partial record Person(string Name, List<string> Interests);

    [GenerateShape]
    internal partial record Note(string Text);
}
