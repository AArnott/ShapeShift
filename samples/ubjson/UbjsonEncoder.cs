// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Ubjson;

/// <summary>
/// Writes the ShapeShift data model as UBJSON (Universal Binary JSON) Draft 12.
/// </summary>
/// <param name="writer">The buffer the encoded bytes are written to.</param>
/// <remarks>
/// <para>
/// An encoder is a thin, allocation-light projection of the format-neutral write operations onto one
/// wire format. It holds no converter knowledge: the shared converter layer decides <em>what</em> to
/// write, and the encoder decides only <em>how</em>.
/// </para>
/// <para>
/// Like every ShapeShift encoder this is a <see langword="ref" /> struct, so it can hold spans and
/// cannot escape to the heap or cross an <see langword="await" />. It is passed by
/// <see langword="ref" /> everywhere so that the mutations each write makes are seen by the caller.
/// </para>
/// </remarks>
public ref struct UbjsonEncoder(IBufferWriter<byte> writer) : IEncoder
{
    private readonly IBufferWriter<byte> writer = writer;
    private bool[] containerIsMap = new bool[8];
    private int depth;

    /// <inheritdoc/>
    public void WriteStartMap(int? propertyCount)
    {
        this.WriteMarker(UbjsonMarkers.ObjectStart);
        this.Push(isMap: true);
    }

    /// <inheritdoc/>
    public void WriteEndMap()
    {
        this.Pop(isMap: true);
        this.WriteMarker(UbjsonMarkers.ObjectEnd);
    }

    /// <inheritdoc/>
    public void WriteStartVector(int? itemCount)
    {
        this.WriteMarker(UbjsonMarkers.ArrayStart);
        this.Push(isMap: false);
    }

    /// <inheritdoc/>
    public void WriteEndVector()
    {
        this.Pop(isMap: false);
        this.WriteMarker(UbjsonMarkers.ArrayEnd);
    }

    #region EncoderPropertyName
    /// <inheritdoc/>
    /// <remarks>
    /// An object key is a length-prefixed UTF-8 string with no <c>S</c> marker: its type is implied
    /// by its position. Writing the marker anyway is the single most common UBJSON interoperability bug.
    /// </remarks>
    public void WritePropertyName(scoped ReadOnlySpan<char> name)
    {
        if (this.depth == 0 || !this.containerIsMap[this.depth - 1])
        {
            throw new InvalidOperationException("A property name may only be written inside a map.");
        }

        this.WriteTextWithLength(name);
    }

    /// <inheritdoc/>
    public void WritePropertyName(scoped ReadOnlySpan<char> name, object? preparedName) => this.WritePropertyName(name);

    #endregion

    /// <inheritdoc/>
    public void WriteNull() => this.WriteMarker(UbjsonMarkers.Null);

    /// <inheritdoc/>
    public void WriteValue(bool value) => this.WriteMarker(value ? UbjsonMarkers.True : UbjsonMarkers.False);

    #region EncoderIntegers
    /// <inheritdoc/>
    public void WriteValue(long value)
    {
        switch (value)
        {
            case >= 0 and <= byte.MaxValue:
                this.WriteMarker(UbjsonMarkers.UInt8);
                this.GetSpan(1)[0] = (byte)value;
                break;
            case >= sbyte.MinValue and < 0:
                this.WriteMarker(UbjsonMarkers.Int8);
                this.GetSpan(1)[0] = unchecked((byte)(sbyte)value);
                break;
            case >= short.MinValue and <= short.MaxValue:
                this.WriteMarker(UbjsonMarkers.Int16);
                BinaryPrimitives.WriteInt16BigEndian(this.GetSpan(2), (short)value);
                break;
            case >= int.MinValue and <= int.MaxValue:
                this.WriteMarker(UbjsonMarkers.Int32);
                BinaryPrimitives.WriteInt32BigEndian(this.GetSpan(4), (int)value);
                break;
            default:
                this.WriteMarker(UbjsonMarkers.Int64);
                BinaryPrimitives.WriteInt64BigEndian(this.GetSpan(8), value);
                break;
        }
    }
    #endregion

    /// <inheritdoc/>
    /// <remarks>
    /// UBJSON has no unsigned 64-bit type, so values above <see cref="long.MaxValue"/> are written as
    /// high-precision numbers rather than silently truncated.
    /// </remarks>
    public void WriteValue(ulong value)
    {
        if (value <= long.MaxValue)
        {
            this.WriteValue((long)value);
        }
        else
        {
            this.WriteHighPrecision(value.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <inheritdoc/>
    public void WriteValue(Int128 value)
    {
        if (value >= long.MinValue && value <= long.MaxValue)
        {
            this.WriteValue((long)value);
        }
        else
        {
            this.WriteHighPrecision(value.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <inheritdoc/>
    public void WriteValue(UInt128 value)
    {
        if (value <= long.MaxValue)
        {
            this.WriteValue((long)value);
        }
        else
        {
            this.WriteHighPrecision(value.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <inheritdoc/>
    public void WriteValue(BigInteger value)
    {
        if (value >= long.MinValue && value <= long.MaxValue)
        {
            this.WriteValue((long)value);
        }
        else
        {
            this.WriteHighPrecision(value.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A <see cref="Half"/> widens to <see cref="float"/> exactly, so binary32 round-trips it
    /// without loss and without inventing a marker no other UBJSON implementation would understand.
    /// </remarks>
    public void WriteValue(Half value) => this.WriteValue((float)value);

    /// <inheritdoc/>
    public void WriteValue(float value)
    {
        this.WriteMarker(UbjsonMarkers.Float32);
        BinaryPrimitives.WriteSingleBigEndian(this.GetSpan(4), value);
    }

    /// <inheritdoc/>
    public void WriteValue(double value)
    {
        this.WriteMarker(UbjsonMarkers.Float64);
        BinaryPrimitives.WriteDoubleBigEndian(this.GetSpan(8), value);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="decimal"/> has no binary UBJSON type, and neither binary32 nor binary64 can hold it
    /// exactly, so it travels as a high-precision number, which preserves both its digits and its scale.
    /// </remarks>
    public void WriteValue(decimal value) => this.WriteHighPrecision(value.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc/>
    /// <remarks>
    /// UBJSON has no timestamp type. The round-trip ("O") form preserves the tick and the
    /// <see cref="DateTimeKind"/>, which a Unix-epoch integer would not.
    /// </remarks>
    public void WriteValue(DateTime value) => this.WriteValue(value.ToString("O", CultureInfo.InvariantCulture).AsSpan());

    /// <inheritdoc/>
    public void WriteValue(TimeSpan value) => this.WriteValue(value.ToString("c", CultureInfo.InvariantCulture).AsSpan());

    /// <inheritdoc/>
    public void WriteValue(string value) => this.WriteValue(value.AsSpan());

    /// <inheritdoc/>
    public void WriteValue(scoped ReadOnlySpan<char> value)
    {
        this.WriteMarker(UbjsonMarkers.String);
        this.WriteTextWithLength(value);
    }

    #region EncoderBinary
    /// <inheritdoc/>
    /// <remarks>
    /// UBJSON has no binary type either, but it does have optimized containers: <c>[$U#n</c> declares
    /// an array of exactly <c>n</c> unsigned bytes whose payload follows with no per-element markers
    /// and no closing <c>]</c>. That is the conventional UBJSON binary representation, and it costs
    /// the same as a length-prefixed blob.
    /// </remarks>
    public void WriteValue(scoped ReadOnlySpan<byte> value)
    {
        this.WriteMarker(UbjsonMarkers.ArrayStart);
        this.WriteMarker(UbjsonMarkers.ContainerType);
        this.WriteMarker(UbjsonMarkers.UInt8);
        this.WriteMarker(UbjsonMarkers.ContainerCount);
        this.WriteLength(value.Length);
        value.CopyTo(this.GetSpan(value.Length));
    }
    #endregion

    #region EncoderNativeChar
    /// <summary>
    /// Writes a single ASCII character using UBJSON's native <c>C</c> marker.
    /// </summary>
    /// <param name="value">The character to write. It must be in the ASCII range.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is not ASCII.</exception>
    /// <remarks>
    /// <para>
    /// This is a <em>format-specific</em> encoder method, deliberately not a member of
    /// <see cref="IEncoder"/>. UBJSON's <c>C</c> is two bytes on the wire where the shared vocabulary's
    /// nearest equivalent -- a one-character string -- costs four (<c>S</c>, a length marker, the
    /// length, and the byte). Nothing in the shared token vocabulary can name the distinction, and no
    /// other format is obliged to grow a concept because UBJSON has one.
    /// </para>
    /// <para>
    /// The type that reaches for it is <see cref="UbjsonCharConverter"/>, which is typed over the
    /// concrete <see cref="UbjsonEncoder"/> and so can call anything this type declares.
    /// </para>
    /// </remarks>
    public void WriteChar(char value)
    {
        if (value > 0x7F)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "UBJSON's char type carries one ASCII byte.");
        }

        this.WriteMarker(UbjsonMarkers.Char);
        this.GetSpan(1)[0] = (byte)value;
    }
    #endregion

    private Span<byte> GetSpan(int length)
    {
        Span<byte> span = this.writer.GetSpan(length)[..length];
        this.writer.Advance(length);
        return span;
    }

    private void WriteMarker(byte marker) => this.GetSpan(1)[0] = marker;

    private void WriteLength(int length) => this.WriteValue((long)length);

    private void WriteHighPrecision(string text)
    {
        this.WriteMarker(UbjsonMarkers.HighPrecision);
        this.WriteTextWithLength(text.AsSpan());
    }

    private void WriteTextWithLength(scoped ReadOnlySpan<char> text)
    {
        int byteCount = Encoding.UTF8.GetByteCount(text);
        this.WriteLength(byteCount);
        if (byteCount > 0)
        {
            Encoding.UTF8.GetBytes(text, this.GetSpan(byteCount));
        }
    }

    private void Push(bool isMap)
    {
        if (this.depth == this.containerIsMap.Length)
        {
            Array.Resize(ref this.containerIsMap, this.containerIsMap.Length * 2);
        }

        this.containerIsMap[this.depth++] = isMap;
    }

    private void Pop(bool isMap)
    {
        if (this.depth == 0 || this.containerIsMap[this.depth - 1] != isMap)
        {
            throw new InvalidOperationException($"There is no open {(isMap ? "map" : "vector")} to close.");
        }

        this.depth--;
    }
}
