// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;
using ShapeShift.Json;

namespace JsonAsyncStreaming;

internal static partial class JsonAsyncStreamingSample
{
    internal static async Task RunAsync()
    {
        #region JsonAsyncStreaming
        var serializer = new JsonSerializer();
        var person = new Person("Ada");

        // SerializeAsync/DeserializeAsync incrementally fill/drain a bounded buffer around the
        // existing synchronous conversion. They never buffer an entire document up front, never
        // block a thread waiting on I/O, and honor cancellation throughout.
        using var stream = new MemoryStream();
        await serializer.SerializeAsync(stream, person);
        stream.Position = 0;
        Person? copy = await serializer.DeserializeAsync<Person>(stream);

        // The same APIs work directly against a PipeWriter/PipeReader, e.g. the ends of a
        // System.IO.Pipelines.Pipe, or a transport's own pipe.
        var pipe = new Pipe();
        await serializer.SerializeAsync(pipe.Writer, person);
        await pipe.Writer.CompleteAsync();
        Person? fromPipe = await serializer.DeserializeAsync<Person>(pipe.Reader);

        // DeserializeAllAsync enumerates a stream of concatenated top-level values -- such as
        // newline-delimited JSON (NDJSON) -- one at a time, buffering only as much of the
        // underlying pipe as each individual value requires.
        string ndjson = "{\"Name\":\"Ada\"}\n{\"Name\":\"Grace\"}\n{\"Name\":\"Katherine\"}\n";
        using var ndjsonStream = new MemoryStream(Encoding.UTF8.GetBytes(ndjson));
        PipeReader ndjsonReader = PipeReader.Create(ndjsonStream);

        List<Person?> people = [];
        await foreach (Person? p in serializer.DeserializeAllAsync<Person>(ndjsonReader))
        {
            people.Add(p);
        }
        #endregion

        Console.WriteLine(copy);
        Console.WriteLine(fromPipe);
        Console.WriteLine(string.Join(", ", people));
    }

    [GenerateShape]
    internal partial record Person(string Name);
}
