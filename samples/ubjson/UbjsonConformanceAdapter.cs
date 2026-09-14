// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ShapeShift.Conformance;

namespace Ubjson;

/// <summary>
/// Describes UBJSON to the shared <see cref="ConformanceSuite"/>.
/// </summary>
/// <remarks>
/// The adapter is the only thing a format author writes in order to run several hundred assertions
/// about token semantics, decoder state, skipping, path traversal, primitive fidelity, malformed
/// input, security limits, converter interactions, and the asynchronous boundary scanner. Anything the
/// format genuinely cannot represent is declared in <see cref="Options"/>, and the affected cases
/// report themselves as skipped rather than failing.
/// </remarks>
public sealed class UbjsonConformanceAdapter : FormatConformanceAdapter<UbjsonEncoder, UbjsonDecoder>
{
    #region AdapterOptions
    /// <inheritdoc/>
    public override string FormatName => "Ubjson";

    /// <inheritdoc/>
    public override FormatConformanceOptions Options { get; } = new()
    {
        // This encoder always writes the unoptimized container forms, whose length is not known until
        // the closing bracket is read. The decoder still reports the count of an optimized container
        // written by some other UBJSON implementation.
        ReportsContainerCounts = false,

        // Reference preservation needs a format-specific back-reference token. UBJSON has no extension
        // mechanism to carve one out of, so this format declines the feature rather than colliding with
        // ordinary data.
        SupportsReferencePreservation = false,
    };
    #endregion

    #region AdapterHarness
    /// <inheritdoc/>
    public override ShapeShiftSerializer<UbjsonEncoder, UbjsonDecoder> CreateSerializer() => new UbjsonSerializer();

    /// <inheritdoc/>
    public override IValueBoundaryScanner CreateValueBoundaryScanner() => new UbjsonValueBoundaryScanner();

    /// <inheritdoc/>
    public override byte[] Encode(EncodeAction<UbjsonEncoder> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArrayBufferWriter<byte> buffer = new();
        UbjsonEncoder encoder = new(buffer);
        action(ref encoder);
        return buffer.WrittenSpan.ToArray();
    }

    /// <inheritdoc/>
    public override TResult Decode<TResult>(ReadOnlyMemory<byte> payload, DecodeFunc<UbjsonDecoder, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        UbjsonDecoder decoder = new(payload.Span);
        return func(ref decoder);
    }
    #endregion

    /// <inheritdoc/>
    /// <remarks>
    /// The built-in suites cover the shared contract. These cases cover what is true of UBJSON alone,
    /// and they run, report, and filter exactly like the built-in ones.
    /// </remarks>
    public override void AddFormatSpecificTests(ConformanceTestCollector<UbjsonEncoder, UbjsonDecoder> collector)
    {
        ArgumentNullException.ThrowIfNull(collector);

        collector.Add("BinaryUsesTheOptimizedUInt8ArrayForm", adapter =>
        {
            byte[] payload = adapter.Encode(static (ref UbjsonEncoder encoder) => encoder.WriteValue(new byte[] { 1, 2, 3 }.AsSpan()));
            ConformanceAssert.EqualBytes(
                [(byte)'[', (byte)'$', (byte)'U', (byte)'#', (byte)'U', 3, 1, 2, 3],
                payload,
                "the UBJSON encoding of a three-byte binary value");
        });

        #region AdapterFormatSpecific
        collector.Add("ReadsOptimizedContainersWrittenByOtherImplementations", adapter =>
        {
            // [$i#U3 followed by three int8 payloads: an optimized array with neither per-element
            // markers nor a closing bracket. The decoder must synthesize the end token itself.
            byte[] payload = [(byte)'[', (byte)'$', (byte)'i', (byte)'#', (byte)'U', 3, 10, 20, 30];
            adapter.Decode(payload, static (ref UbjsonDecoder decoder) =>
            {
                ConformanceAssert.Equal(TokenType.StartVector, decoder.NextTokenType, "the token that begins an optimized array");
                ConformanceAssert.Equal<int?>(3, decoder.ReadStartVector(), "the declared element count");
                ConformanceAssert.Equal(10L, decoder.ReadInt64(), "the first optimized element");
                ConformanceAssert.Equal(20L, decoder.ReadInt64(), "the second optimized element");
                ConformanceAssert.Equal(30L, decoder.ReadInt64(), "the third optimized element");
                ConformanceAssert.Equal(TokenType.EndVector, decoder.NextTokenType, "the synthesized end of an optimized array");
                decoder.ReadEndVector();
                ConformanceAssert.Equal(TokenType.EndDocument, decoder.NextTokenType, "the token after an optimized array");
            });
        });
        #endregion

        collector.Add("IgnoresNoOpFiller", adapter =>
        {
            // 'N' is a no-op byte a producer may use as filler wherever a value is expected.
            byte[] payload = [(byte)'[', (byte)'N', (byte)'U', 7, (byte)'N', (byte)']'];
            adapter.Decode(payload, static (ref UbjsonDecoder decoder) =>
            {
                decoder.ReadStartVector();
                ConformanceAssert.Equal(7L, decoder.ReadInt64(), "the only real element of an array padded with no-ops");
                ConformanceAssert.Equal(TokenType.EndVector, decoder.NextTokenType, "the token after trailing no-op filler");
                decoder.ReadEndVector();
            });
        });

        collector.Add("RejectsContainersOfContainersByType", adapter =>
        {
            // '$[' would declare an array of arrays, whose elements carry their own optional headers.
            // This decoder rejects that rather than half-supporting it.
            byte[] payload = [(byte)'[', (byte)'$', (byte)'[', (byte)'#', (byte)'U', 1];
            ConformanceAssert.FailsCleanly(
                () => adapter.Decode(payload, static (ref UbjsonDecoder decoder) => decoder.ReadStartVector()),
                "reading an array whose declared element type is itself a container");
        });

        collector.Add("HugeDeclaredLengthFailsCleanly", adapter =>
        {
            // A hostile length header must be rejected against the bytes actually available rather
            // than trusted into an allocation.
            byte[] payload = [(byte)'S', (byte)'l', 0x7F, 0xFF, 0xFF, 0xFF, (byte)'a'];
            ConformanceAssert.FailsCleanly(
                () => adapter.Decode(payload, static (ref UbjsonDecoder decoder) => decoder.ReadString()),
                "reading a string whose declared length exceeds the payload");
        });
    }
}
