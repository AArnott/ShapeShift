// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Ubjson;

/// <summary>
/// Reads the ShapeShift data model from UBJSON (Universal Binary JSON) Draft 12.
/// </summary>
/// <remarks>
/// <para>
/// A decoder is a pull parser over one contiguous buffer. It answers <see cref="NextTokenType"/>
/// without consuming anything, and every <c>Read*</c> method consumes exactly one token (or, for
/// containers, exactly one start or end token) and leaves the decoder positioned on the next one.
/// Those two rules are the whole contract the format-neutral converter layer relies on.
/// </para>
/// <para>
/// The decoder reads more shapes than the encoder writes. UBJSON permits <em>optimized</em>
/// containers, which declare a shared element type (<c>$</c>) and an element count (<c>#</c>) and
/// then omit both the per-element markers and the closing bracket. This sample writes them only for
/// binary values, but reads them anywhere, so payloads produced by other UBJSON implementations are
/// understood. When a counted container's elements are exhausted, the decoder <em>synthesizes</em>
/// the end token that the wire format does not contain.
/// </para>
/// </remarks>
public ref struct UbjsonDecoder : IDecoder
{
    /// <summary>
    /// The deepest container nesting the decoder will enter.
    /// </summary>
    /// <remarks>
    /// <see cref="Skip"/> recurses, and the input is attacker-controlled, so the decoder needs a
    /// bound of its own rather than relying on <see cref="SerializationContext{TEncoder, TDecoder}.MaxDepth"/>,
    /// which only applies while converters are running.
    /// </remarks>
    private const int MaxNestingDepth = 200;

    private readonly ReadOnlySpan<byte> source;
    private Frame[] frames;
    private int depth;
    private int position;

    /// <summary>
    /// Initializes a new instance of the <see cref="UbjsonDecoder"/> struct.
    /// </summary>
    /// <param name="source">The complete UBJSON document, or a sequence of concatenated top-level values.</param>
    public UbjsonDecoder(ReadOnlySpan<byte> source)
    {
        this.source = source;
        this.frames = new Frame[8];
    }

    #region DecoderNextTokenType
    /// <inheritdoc/>
    /// <remarks>
    /// Reports <see cref="TokenType.EndDocument"/> once the input is exhausted rather than throwing,
    /// so a caller may always ask what comes next.
    /// </remarks>
    public TokenType NextTokenType
    {
        get
        {
            if (this.depth > 0)
            {
                Frame frame = this.frames[this.depth - 1];
                bool expectingKey = frame.IsMap && frame.ExpectKey;
                if (frame.Counted)
                {
                    if (frame.Remaining == 0)
                    {
                        return frame.IsMap ? TokenType.EndMap : TokenType.EndVector;
                    }

                    if (expectingKey)
                    {
                        return TokenType.PropertyName;
                    }
                }
                else
                {
                    if (!expectingKey)
                    {
                        this.SkipNoOps();
                    }

                    if (this.position >= this.source.Length)
                    {
                        return TokenType.EndDocument;
                    }

                    byte next = this.source[this.position];
                    if (frame.IsMap && next == UbjsonMarkers.ObjectEnd)
                    {
                        return TokenType.EndMap;
                    }

                    if (!frame.IsMap && next == UbjsonMarkers.ArrayEnd)
                    {
                        return TokenType.EndVector;
                    }

                    if (expectingKey)
                    {
                        return TokenType.PropertyName;
                    }
                }
            }
            else
            {
                this.SkipNoOps();
            }

            if (this.IsInTypedContainer)
            {
                return this.TokenForMarker(this.frames[this.depth - 1].ElementType);
            }

            return this.position >= this.source.Length
                ? TokenType.EndDocument
                : this.TokenForMarker(this.source[this.position]);
        }
    }
    #endregion

    /// <summary>
    /// Gets the number of bytes consumed so far, which is where the next top-level value begins.
    /// </summary>
    public readonly int Position => this.position;

    private readonly bool IsInTypedContainer => this.depth > 0 && this.frames[this.depth - 1].ElementType != 0;

    #region DecoderNull
    /// <inheritdoc/>
    /// <remarks>
    /// Conventional <c>Try</c> semantics: a <see langword="true" /> answer means the null token has
    /// been consumed. Code that needs to know what is coming <em>without</em> consuming it asks
    /// <see cref="NextTokenType"/>.
    /// </remarks>
    public bool TryReadNull()
    {
        if (this.NextTokenType != TokenType.Null)
        {
            return false;
        }

        this.ReadNull();
        return true;
    }

    /// <inheritdoc/>
    public void ReadNull()
    {
        byte marker = this.BeginValue();
        if (marker != UbjsonMarkers.Null)
        {
            throw Unexpected("a null", marker);
        }

        this.ValueRead();
    }
    #endregion

    /// <inheritdoc/>
    public int? ReadStartMap()
    {
        byte marker = this.BeginValue();
        if (marker != UbjsonMarkers.ObjectStart)
        {
            throw Unexpected("the start of a map", marker);
        }

        (byte elementType, int? count) = this.ReadContainerHeader();
        this.Push(isMap: true, elementType, count);
        return count;
    }

    /// <inheritdoc/>
    public void ReadEndMap() => this.ReadEndContainer(isMap: true);

    /// <inheritdoc/>
    public int? ReadStartVector()
    {
        byte marker = this.BeginValue();
        if (marker != UbjsonMarkers.ArrayStart)
        {
            throw Unexpected("the start of a vector", marker);
        }

        (byte elementType, int? count) = this.ReadContainerHeader();
        this.Push(isMap: false, elementType, count);
        return count;
    }

    /// <inheritdoc/>
    public void ReadEndVector() => this.ReadEndContainer(isMap: false);

    /// <inheritdoc/>
    /// <remarks>
    /// An object key carries a length prefix but no type marker, so this cannot be expressed as
    /// "read a string": the decoder must know it is at a key position, which the container frame records.
    /// </remarks>
    public ReadOnlySpan<char> ReadPropertyName()
    {
        if (this.depth == 0 || !this.frames[this.depth - 1].IsMap || !this.frames[this.depth - 1].ExpectKey)
        {
            throw new DecoderException("A property name may only be read at the start of a map entry.");
        }

        ref Frame frame = ref this.frames[this.depth - 1];
        bool exhausted = frame.Counted
            ? frame.Remaining == 0
            : this.position < this.source.Length && this.source[this.position] == UbjsonMarkers.ObjectEnd;
        if (exhausted)
        {
            throw new DecoderException("The map has no further entries.");
        }

        string name = this.ReadTextPayload();
        frame.ExpectKey = false;
        return name;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Scalars are skipped by their declared width rather than by decoding them, so skipping a large
    /// string or blob costs no allocation.
    /// </remarks>
    public void Skip() => this.SkipValue(0);

    /// <inheritdoc/>
    public bool ReadBoolean()
    {
        byte marker = this.BeginValue();
        bool value = marker switch
        {
            UbjsonMarkers.True => true,
            UbjsonMarkers.False => false,
            _ => throw Unexpected("a boolean", marker),
        };
        this.ValueRead();
        return value;
    }

    /// <inheritdoc/>
    public long ReadInt64()
    {
        byte marker = this.BeginValue();
        long value = marker == UbjsonMarkers.HighPrecision
            ? ParseInvariant<long>(this.ReadTextPayload())
            : this.ReadIntegerPayload(marker, "an integer");
        this.ValueRead();
        return value;
    }

    /// <inheritdoc/>
    public ulong ReadUInt64()
    {
        byte marker = this.BeginValue();
        ulong value;
        if (marker == UbjsonMarkers.HighPrecision)
        {
            value = ParseInvariant<ulong>(this.ReadTextPayload());
        }
        else
        {
            long signed = this.ReadIntegerPayload(marker, "an unsigned integer");
            value = signed >= 0 ? (ulong)signed : throw new DecoderException($"The integer {signed} cannot be read as an unsigned value.");
        }

        this.ValueRead();
        return value;
    }

    /// <inheritdoc/>
    public Int128 ReadInt128()
    {
        byte marker = this.BeginValue();
        Int128 value = marker == UbjsonMarkers.HighPrecision
            ? ParseInvariant<Int128>(this.ReadTextPayload())
            : this.ReadIntegerPayload(marker, "an integer");
        this.ValueRead();
        return value;
    }

    /// <inheritdoc/>
    public UInt128 ReadUInt128()
    {
        byte marker = this.BeginValue();
        UInt128 value;
        if (marker == UbjsonMarkers.HighPrecision)
        {
            value = ParseInvariant<UInt128>(this.ReadTextPayload());
        }
        else
        {
            long signed = this.ReadIntegerPayload(marker, "an unsigned integer");
            value = signed >= 0 ? (UInt128)signed : throw new DecoderException($"The integer {signed} cannot be read as an unsigned value.");
        }

        this.ValueRead();
        return value;
    }

    /// <inheritdoc/>
    public BigInteger ReadBigInteger()
    {
        byte marker = this.BeginValue();
        BigInteger value = marker == UbjsonMarkers.HighPrecision
            ? ParseInvariant<BigInteger>(this.ReadTextPayload())
            : this.ReadIntegerPayload(marker, "an integer");
        this.ValueRead();
        return value;
    }

    /// <inheritdoc/>
    public Half ReadHalf() => (Half)this.ReadSingle();

    /// <inheritdoc/>
    public float ReadSingle() => (float)this.ReadDouble();

    /// <inheritdoc/>
    public double ReadDouble()
    {
        byte marker = this.BeginValue();
        double value = marker switch
        {
            UbjsonMarkers.Float32 => BinaryPrimitives.ReadSingleBigEndian(this.TakeBytes(4)),
            UbjsonMarkers.Float64 => BinaryPrimitives.ReadDoubleBigEndian(this.TakeBytes(8)),
            UbjsonMarkers.HighPrecision => ParseInvariant<double>(this.ReadTextPayload()),
            _ => this.ReadIntegerPayload(marker, "a number"),
        };
        this.ValueRead();
        return value;
    }

    /// <inheritdoc/>
    public decimal ReadDecimal()
    {
        byte marker = this.BeginValue();
        decimal value = marker switch
        {
            UbjsonMarkers.HighPrecision => ParseInvariant<decimal>(this.ReadTextPayload()),
            UbjsonMarkers.Float32 => ToDecimal(BinaryPrimitives.ReadSingleBigEndian(this.TakeBytes(4))),
            UbjsonMarkers.Float64 => ToDecimal(BinaryPrimitives.ReadDoubleBigEndian(this.TakeBytes(8))),
            _ => this.ReadIntegerPayload(marker, "a number"),
        };
        this.ValueRead();
        return value;
    }

    /// <inheritdoc/>
    public DateTime ReadDateTime()
    {
        string text = this.ReadString();
        return DateTime.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime value)
            ? value
            : throw new DecoderException($"\"{text}\" is not an ISO 8601 date and time.");
    }

    /// <inheritdoc/>
    public TimeSpan ReadTimeSpan()
    {
        string text = this.ReadString();
        return TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out TimeSpan value)
            ? value
            : throw new DecoderException($"\"{text}\" is not a valid duration.");
    }

    /// <inheritdoc/>
    public string ReadString()
    {
        byte marker = this.BeginValue();
        string value = marker switch
        {
            UbjsonMarkers.String => this.ReadTextPayload(),
            UbjsonMarkers.Char => ((char)this.TakeBytes(1)[0]).ToString(),
            _ => throw Unexpected("a string", marker),
        };
        this.ValueRead();
        return value;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<char> ReadCharSpan() => this.ReadString();

    #region DecoderNativeChar
    /// <summary>
    /// Consumes the next value if -- and only if -- it is UBJSON's native <c>C</c> character.
    /// </summary>
    /// <param name="value">Receives the character.</param>
    /// <returns>
    /// <see langword="true" /> when a <c>C</c> value was consumed; <see langword="false" /> when the
    /// next value is anything else, in which case nothing was consumed.
    /// </returns>
    /// <exception cref="DecoderException">Thrown when a <c>C</c> marker carries a non-ASCII byte.</exception>
    /// <remarks>
    /// <para>
    /// A format-specific <em>decoder</em> method mirrors the format-specific encoder method, and takes
    /// the same <c>Try</c> shape as <see cref="TryReadNull"/>: consume on <see langword="true" />,
    /// consume nothing on <see langword="false" />.
    /// </para>
    /// <para>
    /// Reporting <see langword="false" /> rather than throwing is what lets
    /// <see cref="UbjsonCharConverter"/> fall back to <see cref="ReadCharSpan"/>, so a payload written
    /// by an implementation that used an ordinary <c>S</c> string still reads. A format-specific
    /// representation should never make the format unable to read the representation it replaced.
    /// </para>
    /// <para>
    /// Note the <see cref="NextTokenType"/> gate. Consulting the peek first is what keeps the frame
    /// states out -- an exhausted counted container, a key slot, the end of the input -- in which the
    /// raw marker byte is not the start of a value at all. A format-specific <c>Try</c> method that
    /// reads the wire directly without that gate will happily consume its neighbor's bytes.
    /// </para>
    /// </remarks>
    public bool TryReadChar(out char value)
    {
        value = default;
        if (this.NextTokenType != TokenType.String)
        {
            return false;
        }

        // NextTokenType has already skipped no-ops and proven a value begins here, so the effective
        // marker is either the frame's declared element type or the byte at the current position.
        byte marker = this.IsInTypedContainer
            ? this.frames[this.depth - 1].ElementType
            : this.source[this.position];
        if (marker != UbjsonMarkers.Char)
        {
            return false;
        }

        _ = this.BeginValue();
        byte payload = this.TakeBytes(1)[0];
        if (payload > 0x7F)
        {
            throw new DecoderException($"UBJSON's char type carries one ASCII byte, but 0x{payload:X2} was found at offset {this.position - 1}.");
        }

        value = (char)payload;
        this.ValueRead();
        return true;
    }
    #endregion

    /// <inheritdoc/>
    /// <remarks>
    /// Binary values are the conventional UBJSON <c>[$U#n</c> optimized array of unsigned bytes.
    /// </remarks>
    public byte[] ReadByteArray()
    {
        this.SkipNoOps();
        if (!this.IsBinaryHeader())
        {
            throw new DecoderException($"Expected a binary value at offset {this.position}.");
        }

        this.position += 4;
        int length = this.ReadLengthPrefix();
        byte[] value = this.TakeBytes(length).ToArray();
        this.ValueRead();
        return value;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Overriding this preserves the width the payload actually used, which is what unknown-data
    /// retention needs in order to write a value back the way it arrived.
    /// </remarks>
    public ShapeShiftNumber ReadDynamicNumber()
    {
        byte marker = this.BeginValue();
        ShapeShiftNumber value = marker switch
        {
            UbjsonMarkers.Float32 => new ShapeShiftFloat(BinaryPrimitives.ReadSingleBigEndian(this.TakeBytes(4))),
            UbjsonMarkers.Float64 => new ShapeShiftFloat(BinaryPrimitives.ReadDoubleBigEndian(this.TakeBytes(8))),
            UbjsonMarkers.HighPrecision => ParseHighPrecision(this.ReadTextPayload()),
            _ => new ShapeShiftInteger(this.ReadIntegerPayload(marker, "a number")),
        };
        this.ValueRead();
        return value;
    }

    private static DecoderException Unexpected(string expected, byte marker)
        => new($"Expected {expected} but found the UBJSON marker {UbjsonMarkers.Describe(marker)}.");

    private static T ParseInvariant<T>(string text)
        where T : IParsable<T>
        => T.TryParse(text, CultureInfo.InvariantCulture, out T? value)
            ? value
            : throw new DecoderException($"\"{text}\" is not a valid {typeof(T).Name}.");

    private static decimal ToDecimal(double value)
        => value is >= -7.9228162514264337593543950335E+28 and <= 7.9228162514264337593543950335E+28
            ? (decimal)value
            : throw new DecoderException($"The number {value.ToString(CultureInfo.InvariantCulture)} cannot be represented as a decimal.");

    private static ShapeShiftNumber ParseHighPrecision(string text)
        => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long signed) ? new ShapeShiftInteger(signed)
            : ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong unsigned) ? new ShapeShiftUnsignedInteger(unsigned)
            : BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger big) ? new ShapeShiftBigInteger(big)
            : decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal exact) ? new ShapeShiftDecimal(exact)
            : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double approximate) ? new ShapeShiftFloat(approximate)
            : throw new DecoderException($"\"{text}\" is not a number this decoder can represent.");

    private readonly TokenType TokenForMarker(byte marker) => marker switch
    {
        UbjsonMarkers.Null => TokenType.Null,
        UbjsonMarkers.True or UbjsonMarkers.False => TokenType.Boolean,
        UbjsonMarkers.Int8 or UbjsonMarkers.UInt8 or UbjsonMarkers.Int16 or UbjsonMarkers.Int32 or UbjsonMarkers.Int64
            or UbjsonMarkers.Float32 or UbjsonMarkers.Float64 or UbjsonMarkers.HighPrecision => TokenType.Number,
        UbjsonMarkers.String or UbjsonMarkers.Char => TokenType.String,
        UbjsonMarkers.ArrayStart => this.IsBinaryHeader() ? TokenType.Binary : TokenType.StartVector,
        UbjsonMarkers.ArrayEnd => TokenType.EndVector,
        UbjsonMarkers.ObjectStart => TokenType.StartMap,
        UbjsonMarkers.ObjectEnd => TokenType.EndMap,
        _ => throw new DecoderException($"Unrecognized UBJSON type marker {UbjsonMarkers.Describe(marker)} at offset {this.position}."),
    };

    private readonly bool IsBinaryHeader()
        => !this.IsInTypedContainer
        && this.position + 3 < this.source.Length
        && this.source[this.position] == UbjsonMarkers.ArrayStart
        && this.source[this.position + 1] == UbjsonMarkers.ContainerType
        && this.source[this.position + 2] == UbjsonMarkers.UInt8
        && this.source[this.position + 3] == UbjsonMarkers.ContainerCount;

    private void SkipNoOps()
    {
        if (this.IsInTypedContainer)
        {
            return;
        }

        while (this.position < this.source.Length && this.source[this.position] == UbjsonMarkers.NoOp)
        {
            this.position++;
        }
    }

    /// <summary>
    /// Positions the decoder on the next value and reports its marker.
    /// </summary>
    /// <returns>The marker of the value about to be read.</returns>
    /// <remarks>
    /// Inside a container that declared a shared element type, the elements carry no markers of their
    /// own, so the frame's type is reported and nothing is consumed.
    /// </remarks>
    private byte BeginValue()
    {
        this.SkipNoOps();
        if (this.IsInTypedContainer)
        {
            return this.frames[this.depth - 1].ElementType;
        }

        if (this.position >= this.source.Length)
        {
            throw new DecoderException("The UBJSON document ended where a value was expected.");
        }

        return this.source[this.position++];
    }

    private ReadOnlySpan<byte> TakeBytes(int count)
    {
        if (count < 0 || count > this.source.Length - this.position)
        {
            throw new DecoderException($"The UBJSON document ended before {count} bytes could be read at offset {this.position}.");
        }

        ReadOnlySpan<byte> result = this.source.Slice(this.position, count);
        this.position += count;
        return result;
    }

    private long ReadIntegerPayload(byte marker, string expected) => marker switch
    {
        UbjsonMarkers.Int8 => (sbyte)this.TakeBytes(1)[0],
        UbjsonMarkers.UInt8 => this.TakeBytes(1)[0],
        UbjsonMarkers.Int16 => BinaryPrimitives.ReadInt16BigEndian(this.TakeBytes(2)),
        UbjsonMarkers.Int32 => BinaryPrimitives.ReadInt32BigEndian(this.TakeBytes(4)),
        UbjsonMarkers.Int64 => BinaryPrimitives.ReadInt64BigEndian(this.TakeBytes(8)),
        _ => throw Unexpected(expected, marker),
    };

    private int ReadLengthPrefix()
    {
        if (this.position >= this.source.Length)
        {
            throw new DecoderException("The UBJSON document ended where a length was expected.");
        }

        byte marker = this.source[this.position++];
        long length = this.ReadIntegerPayload(marker, "a length");
        return length is >= 0 and <= int.MaxValue
            ? (int)length
            : throw new DecoderException($"{length} is not a valid UBJSON length.");
    }

    private string ReadTextPayload()
    {
        int length = this.ReadLengthPrefix();
        return length == 0 ? string.Empty : Encoding.UTF8.GetString(this.TakeBytes(length));
    }

    private (byte ElementType, int? Count) ReadContainerHeader()
    {
        byte elementType = 0;
        if (this.position < this.source.Length && this.source[this.position] == UbjsonMarkers.ContainerType)
        {
            this.position++;
            elementType = this.position < this.source.Length
                ? this.source[this.position++]
                : throw new DecoderException("The UBJSON document ended where a container element type was expected.");
            if (!UbjsonMarkers.IsScalarMarker(elementType))
            {
                throw new DecoderException($"This decoder does not support containers whose declared element type is {UbjsonMarkers.Describe(elementType)}.");
            }

            if (this.position >= this.source.Length || this.source[this.position] != UbjsonMarkers.ContainerCount)
            {
                throw new DecoderException("A UBJSON container that declares an element type must also declare a count.");
            }
        }

        if (this.position < this.source.Length && this.source[this.position] == UbjsonMarkers.ContainerCount)
        {
            this.position++;
            return (elementType, this.ReadLengthPrefix());
        }

        return (elementType, null);
    }

    private void ReadEndContainer(bool isMap)
    {
        if (this.depth == 0 || this.frames[this.depth - 1].IsMap != isMap)
        {
            throw new DecoderException($"There is no open {(isMap ? "map" : "vector")} to close.");
        }

        Frame frame = this.frames[this.depth - 1];
        if (isMap && !frame.ExpectKey)
        {
            throw new DecoderException("A map entry's value has not been read.");
        }

        if (frame.Counted)
        {
            if (frame.Remaining != 0)
            {
                throw new DecoderException($"{frame.Remaining} declared element(s) have not been read.");
            }
        }
        else
        {
            byte terminator = isMap ? UbjsonMarkers.ObjectEnd : UbjsonMarkers.ArrayEnd;
            if (this.position >= this.source.Length || this.source[this.position] != terminator)
            {
                throw new DecoderException($"Expected the end of a {(isMap ? "map" : "vector")} at offset {this.position}.");
            }

            this.position++;
        }

        this.depth--;
        this.ValueRead();
    }

    private void Push(bool isMap, byte elementType, int? count)
    {
        if (this.depth == MaxNestingDepth)
        {
            throw new DecoderException($"The UBJSON document nests containers more than {MaxNestingDepth} deep.");
        }

        if (this.depth == this.frames.Length)
        {
            Array.Resize(ref this.frames, this.frames.Length * 2);
        }

        this.frames[this.depth++] = new Frame
        {
            IsMap = isMap,
            ExpectKey = isMap,
            ElementType = elementType,
            Counted = count.HasValue,
            Remaining = count ?? 0,
        };
    }

    #region DecoderValueRead
    /// <summary>
    /// Records that one complete value has been consumed from the enclosing container.
    /// </summary>
    /// <remarks>
    /// This is what lets a counted container know when to synthesize its end token, and what lets a
    /// map alternate between key and value positions. Every read path must end here, which is why the
    /// scalar reads are written as "compute the value, then commit".
    /// </remarks>
    private void ValueRead()
    {
        if (this.depth == 0)
        {
            return;
        }

        ref Frame frame = ref this.frames[this.depth - 1];
        if (frame.IsMap)
        {
            frame.ExpectKey = true;
        }

        if (frame.Counted && frame.Remaining > 0)
        {
            frame.Remaining--;
        }
    }
    #endregion

    #region DecoderSkip
    private void SkipValue(int nesting)
    {
        if (nesting >= MaxNestingDepth)
        {
            throw new DecoderException($"The UBJSON document nests containers more than {MaxNestingDepth} deep.");
        }

        switch (this.NextTokenType)
        {
            case TokenType.StartMap:
                this.ReadStartMap();
                while (this.NextTokenType != TokenType.EndMap)
                {
                    _ = this.ReadPropertyName();
                    this.SkipValue(nesting + 1);
                }

                this.ReadEndMap();
                break;
            case TokenType.StartVector:
                this.ReadStartVector();
                while (this.NextTokenType != TokenType.EndVector)
                {
                    this.SkipValue(nesting + 1);
                }

                this.ReadEndVector();
                break;
            case TokenType.Binary:
                this.SkipBinary();
                break;
            case TokenType.EndMap:
            case TokenType.EndVector:
            case TokenType.EndDocument:
                throw new DecoderException("There is no UBJSON value here to skip.");
            default:
                this.SkipScalar();
                break;
        }
    }
    #endregion

    private void SkipBinary()
    {
        this.position += 4;
        int length = this.ReadLengthPrefix();
        _ = this.TakeBytes(length);
        this.ValueRead();
    }

    private void SkipScalar()
    {
        byte marker = this.BeginValue();
        switch (marker)
        {
            case UbjsonMarkers.Null:
            case UbjsonMarkers.True:
            case UbjsonMarkers.False:
                break;
            case UbjsonMarkers.Int8:
            case UbjsonMarkers.UInt8:
            case UbjsonMarkers.Char:
                _ = this.TakeBytes(1);
                break;
            case UbjsonMarkers.Int16:
                _ = this.TakeBytes(2);
                break;
            case UbjsonMarkers.Int32:
            case UbjsonMarkers.Float32:
                _ = this.TakeBytes(4);
                break;
            case UbjsonMarkers.Int64:
            case UbjsonMarkers.Float64:
                _ = this.TakeBytes(8);
                break;
            case UbjsonMarkers.String:
            case UbjsonMarkers.HighPrecision:
                _ = this.TakeBytes(this.ReadLengthPrefix());
                break;
            default:
                throw Unexpected("a scalar", marker);
        }

        this.ValueRead();
    }

    /// <summary>
    /// What the decoder remembers about one open container.
    /// </summary>
    private struct Frame
    {
        /// <summary>Gets or sets a value indicating whether the container is a map rather than a vector.</summary>
        internal bool IsMap { get; set; }

        /// <summary>Gets or sets a value indicating whether the next token in a map is a key.</summary>
        internal bool ExpectKey { get; set; }

        /// <summary>Gets or sets the shared marker of every element, or zero when each element carries its own.</summary>
        internal byte ElementType { get; set; }

        /// <summary>Gets or sets a value indicating whether the container declared an element count.</summary>
        internal bool Counted { get; set; }

        /// <summary>Gets or sets the number of declared elements (map entries, not tokens) still unread.</summary>
        internal int Remaining { get; set; }
    }
}
