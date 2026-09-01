// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using ShapeShift.MsgPack;

namespace MsgPackFramedStreaming;

internal static partial class MsgPackFramedStreamingSample
{
    internal static async Task RunAsync()
    {
        var serializer = new MsgPackSerializer();

        #region EndlessStreaming
        // An endless sequence of top-level values needs no framing at all, because MessagePack
        // values are self-delimiting. SerializeAllAsync flushes between values, so a slow consumer
        // applies backpressure instead of letting an unbounded buffer accumulate.
        var pipe = new Pipe();
        await serializer.SerializeAllAsync(pipe.Writer, ProduceAsync());
        await pipe.Writer.CompleteAsync();

        List<Reading?> readings = [];
        await foreach (Reading? reading in serializer.DeserializeAllAsync<Reading>(pipe.Reader))
        {
            readings.Add(reading);
        }
        #endregion

        #region Framing
        // Framing earns its four bytes when a transport must know a message's extent before
        // anything parses it: to hand a whole message to another component, to reject an
        // implausibly large message without decoding it, or to interleave MessagePack with other
        // content on one connection. maxFrameLength is checked against the length prefix alone,
        // before a single byte of the frame is buffered.
        using var stream = new MemoryStream();
        foreach (Reading reading in new[] { new Reading("t1", 21.5), new Reading("t2", 22.0) })
        {
            await serializer.SerializeFrameAsync(stream, reading);
        }

        stream.Position = 0;
        List<Reading?> framed = [];
        await foreach (Reading? reading in serializer.DeserializeAllFramesAsync<Reading>(stream, maxFrameLength: 4096))
        {
            framed.Add(reading);
        }
        #endregion

        #region TargetedAsyncRead
        // A targeted read buffers only the enclosing top-level value and then walks the path over
        // the pipe's own segments, skipping everything that is not on the path rather than
        // deserializing it, and never copying the buffer to make it contiguous.
        using var document = new MemoryStream();
        await serializer.SerializeAsync(document, new Envelope(new Header("2024-05-06"), [.. Enumerable.Range(0, 10_000)]));
        document.Position = 0;

        (bool found, string? timestamp) = await serializer.TryDeserializeFragmentAsync<string, Witness>(
            document,
            new ShapeShiftPath("Header", "Timestamp"));
        #endregion

        Console.WriteLine($"{readings.Count} streamed, {framed.Count} framed, header found: {found} ({timestamp})");

        static async IAsyncEnumerable<Reading?> ProduceAsync()
        {
            for (int i = 0; i < 3; i++)
            {
                await Task.Yield();
                yield return new Reading($"t{i}", 20 + i);
            }
        }
    }

    [GenerateShape]
    internal partial record Reading(string SensorId, double Celsius);

    [GenerateShape]
    internal partial record Header(string Timestamp);

    [GenerateShape]
    internal partial record Envelope(Header Header, List<int> Samples);

    [GenerateShapeFor<string>]
    internal partial class Witness;
}
