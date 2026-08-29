// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using ShapeShift.MsgPack;

namespace MsgPackAsyncStreaming;

internal static partial class MsgPackAsyncStreamingSample
{
    internal static async Task RunAsync()
    {
        #region MsgPackAsyncStreaming
        var serializer = new MsgPackSerializer();
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

        // MessagePack values are self-delimiting and require no separator between them, so
        // DeserializeAllAsync can enumerate a stream of concatenated top-level values one at a
        // time, buffering only as much of the underlying pipe as each individual value requires.
        using var concatenatedStream = new MemoryStream();
        foreach (Person p in new[] { new Person("Ada"), new Person("Grace"), new Person("Katherine") })
        {
            concatenatedStream.Write(serializer.Serialize(p));
        }

        concatenatedStream.Position = 0;
        PipeReader concatenatedReader = PipeReader.Create(concatenatedStream);

        List<Person?> people = [];
        await foreach (Person? item in serializer.DeserializeAllAsync<Person>(concatenatedReader))
        {
            people.Add(item);
        }
        #endregion

        Console.WriteLine(copy);
        Console.WriteLine(fromPipe);
        Console.WriteLine(string.Join(", ", people));
    }

    [GenerateShape]
    internal partial record Person(string Name);
}
