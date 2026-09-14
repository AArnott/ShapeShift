// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using ShapeShift.Json;

namespace StreamingDeserialization;

internal static partial class StreamingDeserializationSample
{
    internal static void Run()
    {
        #region StreamingDeserialization
        var serializer = new JsonSerializer();

        // ShapeShiftDocumentReader<T> enumerates whole top-level values one at a time,
        // reading each into a strongly typed value without buffering the others.
        // This works for newline-delimited JSON (NDJSON) and other streams of
        // concatenated top-level values.
        string ndjson = """
            {"Name":"Ada"}
            {"Name":"Grace"}
            {"Name":"Katherine"}
            """;

        JsonDecoder decoder = new(Encoding.UTF8.GetBytes(ndjson));
        using ShapeShiftDocumentReader<Person, JsonEncoder, JsonDecoder> documentReader = serializer.CreateDocumentReader<Person>();

        List<Person?> people = [];
        while (documentReader.MoveNext(ref decoder))
        {
            people.Add(documentReader.Current);
        }

        // ShapeShiftSequenceReader<T> is similar, but for elements of a JSON array (or
        // MessagePack vector) rather than concatenated top-level values. It can be
        // combined with TrySeek to enumerate a vector nested anywhere in a larger document.
        string json = """{"Team":"Analytical Engine","Members":["Ada","Grace","Katherine"]}""";
        JsonDecoder nestedDecoder = new(Encoding.UTF8.GetBytes(json));
        bool foundMembers = nestedDecoder.TrySeek(new ShapeShiftPath("Members"));
        using ShapeShiftSequenceReader<string, JsonEncoder, JsonDecoder> sequenceReader = serializer.CreateSequenceReader<string, Witness>();

        List<string?> members = [];
        while (foundMembers && sequenceReader.MoveNext(ref nestedDecoder))
        {
            members.Add(sequenceReader.Current);
        }
        #endregion

        Console.WriteLine(string.Join(", ", people));
        Console.WriteLine(string.Join(", ", members));
    }

    [GenerateShape]
    internal partial record Person(string Name);

    [GenerateShapeFor<string>]
    private partial class Witness;
}
