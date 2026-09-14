// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Ubjson;

/// <summary>
/// Finds the end of one complete top-level UBJSON value inside a growing buffer.
/// </summary>
/// <remarks>
/// <para>
/// This is the only piece a format must write in order to get the asynchronous
/// <see cref="System.IO.Pipelines.PipeReader"/> and <see cref="System.IO.Stream"/> APIs: it walks the
/// wire format's framing without converting anything, so the shared loop knows when it has buffered
/// enough bytes to hand to the ordinary synchronous decoder.
/// </para>
/// <para>
/// This implementation is deliberately stateless: every call re-walks the buffer from its start. That
/// costs a rescan per chunk but it is easy to prove correct, and it is why <c>examined</c> is reported
/// as the start of the buffer whenever the value is incomplete. A scanner that remembers its progress
/// (as <c>ShapeShift.Json</c>'s does) may report a larger <c>examined</c> position, but must never
/// report one past bytes that belong to a value that has already begun, because the eventual decode
/// step still needs them.
/// </para>
/// </remarks>
public sealed class UbjsonValueBoundaryScanner : IValueBoundaryScanner
{
    private const int MaxNestingDepth = 200;

    #region ScannerTryScan
    /// <inheritdoc/>
    public bool TryScan(in ReadOnlySequence<byte> buffer, bool isFinalBlock, out SequencePosition end, out SequencePosition examined)
    {
        SequenceReader<byte> reader = new(buffer);
        if (TryScanValue(ref reader, 0))
        {
            end = reader.Position;
            examined = end;
            return true;
        }

        // Incomplete input is not an error: the caller supplies more bytes and asks again. Because this
        // scanner keeps no state, nothing in the buffer may be released yet.
        end = default;
        examined = buffer.Start;
        return false;
    }
    #endregion

    private static void SkipNoOps(ref SequenceReader<byte> reader)
    {
        while (reader.TryPeek(out byte next) && next == UbjsonMarkers.NoOp)
        {
            reader.Advance(1);
        }
    }

    private static bool TryAdvance(ref SequenceReader<byte> reader, long count)
    {
        if (reader.Remaining < count)
        {
            return false;
        }

        reader.Advance(count);
        return true;
    }

    private static bool TryReadLength(ref SequenceReader<byte> reader, out long length)
    {
        length = 0;
        if (!reader.TryRead(out byte marker))
        {
            return false;
        }

        switch (marker)
        {
            case UbjsonMarkers.Int8:
                if (!reader.TryRead(out byte int8))
                {
                    return false;
                }

                length = (sbyte)int8;
                break;
            case UbjsonMarkers.UInt8:
                if (!reader.TryRead(out byte uint8))
                {
                    return false;
                }

                length = uint8;
                break;
            case UbjsonMarkers.Int16:
                if (!reader.TryReadBigEndian(out short int16))
                {
                    return false;
                }

                length = int16;
                break;
            case UbjsonMarkers.Int32:
                if (!reader.TryReadBigEndian(out int int32))
                {
                    return false;
                }

                length = int32;
                break;
            case UbjsonMarkers.Int64:
                if (!reader.TryReadBigEndian(out long int64))
                {
                    return false;
                }

                length = int64;
                break;
            default:
                throw new DecoderException($"A UBJSON length may not be introduced by the marker {UbjsonMarkers.Describe(marker)}.");
        }

        return length >= 0 ? true : throw new DecoderException($"{length} is not a valid UBJSON length.");
    }

    private static bool TrySkipScalarPayload(ref SequenceReader<byte> reader, byte marker)
    {
        switch (marker)
        {
            case UbjsonMarkers.Null:
            case UbjsonMarkers.True:
            case UbjsonMarkers.False:
                return true;
            case UbjsonMarkers.Int8:
            case UbjsonMarkers.UInt8:
            case UbjsonMarkers.Char:
                return TryAdvance(ref reader, 1);
            case UbjsonMarkers.Int16:
                return TryAdvance(ref reader, 2);
            case UbjsonMarkers.Int32:
            case UbjsonMarkers.Float32:
                return TryAdvance(ref reader, 4);
            case UbjsonMarkers.Int64:
            case UbjsonMarkers.Float64:
                return TryAdvance(ref reader, 8);
            case UbjsonMarkers.String:
            case UbjsonMarkers.HighPrecision:
                return TryReadLength(ref reader, out long length) && TryAdvance(ref reader, length);
            default:
                throw new DecoderException($"Unrecognized UBJSON type marker {UbjsonMarkers.Describe(marker)}.");
        }
    }

    private static bool TrySkipKey(ref SequenceReader<byte> reader)
        => TryReadLength(ref reader, out long length) && TryAdvance(ref reader, length);

    private static bool TryScanValue(ref SequenceReader<byte> reader, int nesting)
    {
        if (nesting >= MaxNestingDepth)
        {
            throw new DecoderException($"The UBJSON input nests containers more than {MaxNestingDepth} deep.");
        }

        SkipNoOps(ref reader);
        if (!reader.TryRead(out byte marker))
        {
            return false;
        }

        return marker switch
        {
            UbjsonMarkers.ArrayStart => TryScanContainer(ref reader, isMap: false, nesting),
            UbjsonMarkers.ObjectStart => TryScanContainer(ref reader, isMap: true, nesting),
            _ => TrySkipScalarPayload(ref reader, marker),
        };
    }

    private static bool TryScanContainer(ref SequenceReader<byte> reader, bool isMap, int nesting)
    {
        byte elementType = 0;
        if (!reader.TryPeek(out byte next))
        {
            return false;
        }

        if (next == UbjsonMarkers.ContainerType)
        {
            reader.Advance(1);
            if (!reader.TryRead(out elementType))
            {
                return false;
            }

            if (!UbjsonMarkers.IsScalarMarker(elementType))
            {
                throw new DecoderException($"This scanner does not support containers whose declared element type is {UbjsonMarkers.Describe(elementType)}.");
            }

            if (!reader.TryPeek(out next))
            {
                return false;
            }

            if (next != UbjsonMarkers.ContainerCount)
            {
                throw new DecoderException("A UBJSON container that declares an element type must also declare a count.");
            }
        }

        if (reader.TryPeek(out next) && next == UbjsonMarkers.ContainerCount)
        {
            reader.Advance(1);
            if (!TryReadLength(ref reader, out long declared))
            {
                return false;
            }

            for (long i = 0; i < declared; i++)
            {
                if (isMap && !TrySkipKey(ref reader))
                {
                    return false;
                }

                bool scanned = elementType == 0
                    ? TryScanValue(ref reader, nesting + 1)
                    : TrySkipScalarPayload(ref reader, elementType);
                if (!scanned)
                {
                    return false;
                }
            }

            return true;
        }

        byte terminator = isMap ? UbjsonMarkers.ObjectEnd : UbjsonMarkers.ArrayEnd;
        while (true)
        {
            SkipNoOps(ref reader);
            if (!reader.TryPeek(out byte candidate))
            {
                return false;
            }

            if (candidate == terminator)
            {
                reader.Advance(1);
                return true;
            }

            if (isMap && !TrySkipKey(ref reader))
            {
                return false;
            }

            if (!TryScanValue(ref reader, nesting + 1))
            {
                return false;
            }
        }
    }
}
