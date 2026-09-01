// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;

namespace Ubjson;

/// <summary>
/// Serializes to and deserializes from UBJSON.
/// </summary>
/// <remarks>
/// <para>
/// A format's serializer exists only to bind <see cref="ShapeShiftSerializer{TEncoder, TDecoder}"/> to
/// concrete encoder and decoder types and to offer the buffer shapes that are natural for the format.
/// All of the interesting behavior -- converters, naming policies, limits, callbacks -- is inherited.
/// </para>
/// <para>
/// It is a <see langword="record" /> because the base type is: configuration is immutable and copied
/// with <see langword="with" /> expressions, so a serializer instance is safe to share across threads.
/// </para>
/// </remarks>
public record UbjsonSerializer : ShapeShiftSerializer<UbjsonEncoder, UbjsonDecoder>
{
    /// <summary>
    /// The number of bytes an asynchronous read will buffer for one value before giving up.
    /// </summary>
    private const long DefaultMaxBufferedValueSize = 16 * 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="UbjsonSerializer"/> class.
    /// </summary>
    /// <remarks>
    /// The format's own converters are registered here. Everything else -- objects, collections,
    /// enums, unions, surrogates, dynamic values -- comes from the shared, format-neutral layer.
    /// A converter registered here wins over the shared layer's converter for the same type, which
    /// is how a format claims a primitive it can represent better than the shared token vocabulary
    /// can. <see cref="UbjsonCharConverter"/> is that case.
    /// </remarks>
    public UbjsonSerializer()
    {
        this.Converters = [new UbjsonBinaryConverter(), new UbjsonCharConverter()];
    }

    /// <summary>
    /// Serializes a value to a new UBJSON byte array.
    /// </summary>
    /// <typeparam name="T">The self-describing type to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The UBJSON payload.</returns>
    public byte[] Serialize<T>(in T? value)
        where T : IShapeable<T> => this.Serialize<T, T>(value);

    /// <summary>
    /// Serializes a value to a new UBJSON byte array using an external shape provider.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <typeparam name="TProvider">The witness that describes <typeparamref name="T"/>.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The UBJSON payload.</returns>
    public byte[] Serialize<T, TProvider>(in T? value)
        where TProvider : IShapeable<T>
    {
        ArrayBufferWriter<byte> buffer = new();
        this.Serialize(buffer, value, TProvider.GetTypeShape());
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Serializes a value into a caller-supplied buffer.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <param name="destination">The buffer to write to.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="typeShape">The shape of <typeparamref name="T"/>.</param>
    public void Serialize<T>(IBufferWriter<byte> destination, in T? value, ITypeShape<T> typeShape)
    {
        ArgumentNullException.ThrowIfNull(destination);
        UbjsonEncoder encoder = new(destination);
        this.Serialize(ref encoder, value, typeShape);
    }

    /// <summary>
    /// Deserializes a value from a UBJSON payload.
    /// </summary>
    /// <typeparam name="T">The self-describing type to deserialize.</typeparam>
    /// <param name="payload">The UBJSON payload.</param>
    /// <returns>The deserialized value.</returns>
    public T? Deserialize<T>(ReadOnlySpan<byte> payload)
        where T : IShapeable<T> => this.Deserialize<T, T>(payload);

    /// <summary>
    /// Deserializes a value from a UBJSON payload using an external shape provider.
    /// </summary>
    /// <typeparam name="T">The type to deserialize.</typeparam>
    /// <typeparam name="TProvider">The witness that describes <typeparamref name="T"/>.</typeparam>
    /// <param name="payload">The UBJSON payload.</param>
    /// <returns>The deserialized value.</returns>
    public T? Deserialize<T, TProvider>(ReadOnlySpan<byte> payload)
        where TProvider : IShapeable<T>
    {
        UbjsonDecoder decoder = new(payload);
        return this.Deserialize(ref decoder, TProvider.GetTypeShape());
    }

    #region AsyncAdapter
    /// <summary>
    /// Asynchronously deserializes one value from a pipe, buffering only as much input as that value requires.
    /// </summary>
    /// <typeparam name="T">The self-describing type to deserialize.</typeparam>
    /// <param name="reader">The source pipe. The caller retains ownership of it.</param>
    /// <param name="maxBufferedSize">The most bytes one value may occupy before the read is abandoned.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The deserialized value.</returns>
    /// <remarks>
    /// <para>
    /// A <see cref="UbjsonDecoder"/> is a <see langword="ref" /> struct, so it cannot live across an
    /// <see langword="await" /> and a partially decoded value cannot be paused and resumed. The shared
    /// <see cref="PipeReaderExtensions.ReadValueAsync{T}"/> loop therefore uses a
    /// <see cref="UbjsonValueBoundaryScanner"/> to buffer input only until one complete top-level value
    /// is present, and then runs the ordinary synchronous decode exactly once over those bytes.
    /// </para>
    /// <para>
    /// That division is the whole of a format's asynchronous story: supply a scanner, and the
    /// stream, pipe, and sequence APIs follow from it.
    /// </para>
    /// </remarks>
    public async ValueTask<T?> DeserializeAsync<T>(PipeReader reader, long maxBufferedSize = DefaultMaxBufferedValueSize, CancellationToken cancellationToken = default)
        where T : IShapeable<T>
    {
        ArgumentNullException.ThrowIfNull(reader);
        (bool hasValue, T? value) = await reader.ReadValueAsync(
            new UbjsonValueBoundaryScanner(),
            sequence => this.DeserializeSequence<T, T>(sequence),
            maxBufferedSize,
            cancellationToken).ConfigureAwait(false);
        return hasValue ? value : throw new DecoderException("The input did not contain any value to deserialize.");
    }
    #endregion

    /// <summary>
    /// Asynchronously deserializes every remaining top-level value from a pipe.
    /// </summary>
    /// <typeparam name="T">The self-describing type of each value.</typeparam>
    /// <param name="reader">The source pipe. The caller retains ownership of it.</param>
    /// <param name="maxBufferedSize">The most bytes any one value may occupy before the read is abandoned.</param>
    /// <param name="cancellationToken">A cancellation token that applies throughout the enumeration.</param>
    /// <returns>The values, ending gracefully when the pipe reaches its end.</returns>
    public async IAsyncEnumerable<T?> DeserializeAllAsync<T>(
        PipeReader reader,
        long maxBufferedSize = DefaultMaxBufferedValueSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : IShapeable<T>
    {
        ArgumentNullException.ThrowIfNull(reader);
        UbjsonValueBoundaryScanner scanner = new();
        while (true)
        {
            (bool hasValue, T? value) = await reader.ReadValueAsync(
                scanner,
                sequence => this.DeserializeSequence<T, T>(sequence),
                maxBufferedSize,
                cancellationToken).ConfigureAwait(false);
            if (!hasValue)
            {
                yield break;
            }

            yield return value;
        }
    }

    private T? DeserializeSequence<T, TProvider>(ReadOnlySequence<byte> sequence)
        where TProvider : IShapeable<T>
        => sequence.IsSingleSegment
            ? this.Deserialize<T, TProvider>(sequence.FirstSpan)
            : this.Deserialize<T, TProvider>(sequence.ToArray());
}
