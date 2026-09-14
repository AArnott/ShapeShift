// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;
using ShapeShift.Json;
using ShapeShift.Schema;

namespace CoreCustomization;

// Customizing the format-neutral core: immutable serializer configuration, the naming, default-value,
// strictness, and security policies, and custom converters supplied as instances or built by a factory.
// JsonSerializer appears throughout only because its output is readable. Every option and every converter
// concept shown here is declared by the format-neutral core and behaves the same way in MessagePack,
// YAML, TAML, and third-party formats.
public static class CoreCustomizationSamples
{
    #region ImmutableConfiguration
    // Derives a second configuration from a shared baseline without mutating the baseline.
    public static (string Baseline, string Compact) ConfigureImmutably()
    {
        // A serializer is an immutable record, so one instance can be a static field of an
        // application and used concurrently. Configure it once, at construction.
        JsonSerializer baseline = new()
        {
            PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase,
            StartingContext = new() { MaxDepth = 16 },
        };

        // `with` derives a second configuration. `baseline` is unchanged and still usable,
        // and each serializer keeps its own converter cache.
        JsonSerializer compact = baseline with
        {
            SerializeDefaultValues = SerializeDefaultValuesPolicy.Required,
        };

        Reservation reservation = new("Ada");
        return (baseline.Serialize(reservation), compact.Serialize(reservation));
    }
    #endregion

    #region NamingPolicy
    // Renames properties and enum values on the wire without touching the CLR model.
    public static string ApplyNamingPolicy()
    {
        JsonSerializer serializer = new()
        {
            // Applies to every property that does not declare its own [PropertyShape(Name = "...")].
            PropertyNamingPolicy = ShapeShiftNamingPolicy.SnakeLowerCase,

            // Enum values are written by name by default; set this to false to write ordinals.
            SerializeEnumValuesByName = true,
        };

        return serializer.Serialize(new Reservation("Ada", RoomKind.Suite, Nights: 3));
    }
    #endregion

    #region DefaultValues
    // Omits properties whose values match their declared defaults, keeping the required ones.
    public static string OmitDefaultValues()
    {
        JsonSerializer serializer = new()
        {
            // Never omits every defaulted property; Required keeps the ones needed to reconstruct
            // the object. Both are safe for map-shaped formats such as JSON.
            SerializeDefaultValues = SerializeDefaultValuesPolicy.Required,
        };

        // Room, Nights, Deposit, Notes, and Preferences all still hold their declared defaults,
        // so only the constructor parameter that has no default is written.
        return serializer.Serialize(new Reservation("Ada"));
    }
    #endregion

    #region Strictness
    // Contrasts the strict default deserialization policy with an explicitly relaxed one.
    public static (string Rejected, Reservation Accepted) RejectIncompletePayloads()
    {
        const string MissingRequiredValue = """{"Nights":2}""";

        // The default policy rejects a payload that omits a required value or assigns null to a
        // non-nullable member, and reports where the offending value belonged.
        JsonSerializer strict = new();
        string rejected;
        try
        {
            strict.Deserialize<Reservation>(MissingRequiredValue);
            throw new InvalidOperationException("The strict policy was expected to reject this payload.");
        }
        catch (ShapeShiftSerializationException ex)
        {
            rejected = ex.Message;
        }

        // Relaxing the policy is explicit and per-serializer; it leaves the declared default in place.
        JsonSerializer lenient = strict with
        {
            DeserializeDefaultValues = DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties,
        };

        return (rejected, lenient.Deserialize<Reservation>(MissingRequiredValue)!);
    }
    #endregion

    #region SecurityLimits
    // Bounds the work an untrusted payload can ask for.
    public static string BoundUntrustedInput()
    {
        JsonSerializer serializer = new()
        {
            StartingContext = new()
            {
                MaxDepth = 8,
                MaxCollectionLength = 4,
                MaxStringLength = 1024,
                MaxBinaryLength = 4096,
            },
        };

        try
        {
            serializer.Deserialize<Reservation>("""{"GuestName":"Ada","Notes":["a","b","c","d","e"]}""");
            throw new InvalidOperationException("The configured limit was expected to reject this payload.");
        }
        catch (ShapeShiftSerializationException ex)
        {
            return ex.Message;
        }
    }
    #endregion

    #region RegisterConverters
    // Registers a converter instance and two converter factories. None of them uses reflection,
    // so the result is trimming-safe and NativeAOT-safe.
    public static JsonSerializer CreateConfiguredSerializer()
    {
        JsonSerializer serializer = new();

        // Append to the converters the format already installed rather than replacing them,
        // so JsonElement, JsonNode, and binary support survive.
        return serializer with
        {
            Converters = [.. serializer.Converters, new MoneyConverter()],
            ConverterFactories =
            [
                new EmbeddedDocumentConverterFactory(typeof(GuestPreferences)),
                new NullTolerantListConverterFactory(),
            ],
        };
    }
    #endregion

    // Supplies the context that every operation starts from.
    public static JsonSerializer ApplyStartingContext()
    {
        #region ApplyingStartingContext
        JsonSerializer serializer = new()
        {
            StartingContext = new()
            {
                MaxDepth = 128,
            },
        };
        #endregion

        #region ModifyingStartingContext
        serializer = serializer with
        {
            StartingContext = serializer.StartingContext with
            {
                MaxDepth = 256,
            },
        };
        #endregion

        #region ModifyingStartingContextState
        // The context is a struct, so change a local copy and reassign it to the serializer.
        SerializationContext<JsonEncoder, JsonDecoder> context = serializer.StartingContext;
        context[MoneyConverter.DefaultCurrencyKey] = "USD";
        serializer = serializer with
        {
            StartingContext = context,
        };
        #endregion

        return serializer;
    }

    #region ConverterState
    // Reads a payload whose money value omits its currency, which the converter takes from the
    // ambient state the caller placed in the starting context.
    public static Reservation ApplyConverterState()
    {
        JsonSerializer serializer = CreateConfiguredSerializer();
        SerializationContext<JsonEncoder, JsonDecoder> context = serializer.StartingContext;
        context[MoneyConverter.DefaultCurrencyKey] = "USD";
        serializer = serializer with { StartingContext = context };

        return serializer.Deserialize<Reservation>("""{"GuestName":"Ada","Deposit":"25.00"}""")!;
    }
    #endregion

    #region NullTolerantList
    // The registered factory turns a null JSON array into an empty list instead of a null property.
    public static Reservation ReadNullCollection()
    {
        JsonSerializer serializer = CreateConfiguredSerializer();
        return serializer.Deserialize<Reservation>("""{"GuestName":"Ada","Notes":null}""")!;
    }
    #endregion

    #region Roundtrip
    // Round-trips a reservation through the fully customized serializer.
    public static (string Payload, Reservation Roundtripped) RoundtripWithCustomConverters()
    {
        JsonSerializer serializer = CreateConfiguredSerializer();
        Reservation original = new(
            "Ada",
            RoomKind.Suite,
            Nights: 2,
            Deposit: new Money(25.5m, "USD"),
            Notes: ["late arrival"],
            Preferences: new GuestPreferences { Quiet = true, Floor = 4 });

        string payload = serializer.Serialize(original);
        return (payload, serializer.Deserialize<Reservation>(payload)!);
    }
    #endregion
}

#region ConverterInstance
// Writes a Money as "25.5 USD" instead of as an object with two properties.
// A converter instance is the most direct customization: no reflection and no activation,
// so there is nothing for trimming or NativeAOT to preserve.
public sealed class MoneyConverter : ShapeShiftConverter<Money, JsonEncoder, JsonDecoder>
{
    // The context state key whose value supplies the currency for a payload that omits one.
    // An object reference is used rather than a string so the key cannot collide with another
    // component's key.
    public static readonly object DefaultCurrencyKey = new();

    public override Money Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
    {
        string text = decoder.ReadString();
        int separator = text.LastIndexOf(' ');
        if (separator < 0)
        {
            return context[DefaultCurrencyKey] is string currency
                ? new Money(ParseAmount(text), currency)
                : throw new ShapeShiftSerializationException($"'{text}' has no currency and no default currency was supplied.");
        }

        return new Money(ParseAmount(text.AsSpan(0, separator)), text[(separator + 1)..]);
    }

    public override void Write(ref JsonEncoder encoder, in Money value, SerializationContext<JsonEncoder, JsonDecoder> context)
        => encoder.WriteValue(FormattableString.Invariant($"{value.Amount} {value.Currency}"));

    // Describes what this converter really writes, so schema consumers see a string rather than
    // the "undocumented" contract an unannotated converter produces.
    public override DataContract GetContract(ContractContext<JsonEncoder, JsonDecoder> context)
        => new PrimitiveContract(typeof(Money), PrimitiveDataType.String);

    private static decimal ParseAmount(ReadOnlySpan<char> text)
        => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount)
            ? amount
            : throw new ShapeShiftSerializationException($"'{text}' is not a valid amount.");
}
#endregion

#region FactoryNonGeneric
// A special purpose factory knows exactly what it supports, so no generic type parameter is needed.
public sealed class MoneyConverterFactory : IShapeShiftConverterFactory<JsonEncoder, JsonDecoder>
{
    public ShapeShiftConverter<JsonEncoder, JsonDecoder>? CreateConverter(Type type, ITypeShape? shape, in ConverterContext<JsonEncoder, JsonDecoder> context)
        => type == typeof(Money) ? new MoneyConverter() : null;
}
#endregion

#region FactoryGeneric
// Writes the listed types as a JSON *string* holding their own JSON document, the shape some HTTP
// APIs require of an embedded payload.
public sealed class EmbeddedDocumentConverterFactory : IShapeShiftConverterFactory<JsonEncoder, JsonDecoder>, ITypeShapeFunc
{
    private readonly Type[] embeddedTypes;

    public EmbeddedDocumentConverterFactory(params Type[] embeddedTypes) => this.embeddedTypes = embeddedTypes;

    // The type check needs no generic type parameter, so it happens here; the converter does need
    // one, so this method hands the shape back to the generic method below.
    public ShapeShiftConverter<JsonEncoder, JsonDecoder>? CreateConverter(Type type, ITypeShape? shape, in ConverterContext<JsonEncoder, JsonDecoder> context)
        => shape is not null && Array.IndexOf(this.embeddedTypes, type) >= 0
            ? (ShapeShiftConverter<JsonEncoder, JsonDecoder>?)shape.Invoke(this)
            : null;

    // The type check is already done, so just create the converter. ITypeShape<T>.Invoke supplies
    // the generic type parameter without reflection, which keeps this factory NativeAOT-safe.
    object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state)
        => new EmbeddedDocumentConverter<T>(typeShape);
}

public sealed class EmbeddedDocumentConverter<T> : ShapeShiftConverter<T, JsonEncoder, JsonDecoder>
{
    // The embedded document is written by an ordinary serializer of its own, which is how it gets
    // its own policies (and why it never recurses back into the outer serializer's factories).
    private static readonly JsonSerializer Embedded = new();

    private readonly ITypeShape<T> shape;

    public EmbeddedDocumentConverter(ITypeShape<T> shape) => this.shape = shape;

    public override T? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
    {
        if (decoder.TryReadNull())
        {
            return default;
        }

        context.DepthStep();
        byte[] utf8 = Encoding.UTF8.GetBytes(decoder.ReadString());
        JsonDecoder embedded = new(utf8);
        return Embedded.Deserialize(ref embedded, this.shape, context.CancellationToken);
    }

    public override void Write(ref JsonEncoder encoder, in T? value, SerializationContext<JsonEncoder, JsonDecoder> context)
    {
        context.DepthStep();
        ArrayBufferWriter<byte> buffer = new();
        using (System.Text.Json.Utf8JsonWriter writer = new(buffer))
        {
            JsonEncoder embedded = new(writer);
            Embedded.Serialize(ref embedded, value, this.shape, context.CancellationToken);
        }

        encoder.WriteValue(Encoding.UTF8.GetString(buffer.WrittenSpan));
    }
}
#endregion

#region FactoryVisitor
// Reads a null JSON array as an empty list, which lets a service accept peers that write null for
// an absent collection without making every model property nullable.
public sealed class NullTolerantListConverterFactory : IShapeShiftConverterFactory<JsonEncoder, JsonDecoder>
{
    // The converter needs the *element* type as a generic type parameter, which a TypeShapeVisitor
    // supplies. Perform the type check, then defer to the visitor.
    public ShapeShiftConverter<JsonEncoder, JsonDecoder>? CreateConverter(Type type, ITypeShape? shape, in ConverterContext<JsonEncoder, JsonDecoder> context)
        => shape is IEnumerableTypeShape && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)
            ? (ShapeShiftConverter<JsonEncoder, JsonDecoder>?)shape.Accept(Visitor.Instance, context)
            : null;

    private sealed class Visitor : TypeShapeVisitor
    {
        internal static readonly Visitor Instance = new();

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null)
        {
            var context = (ConverterContext<JsonEncoder, JsonDecoder>)state!;
            return new NullTolerantListConverter<TElement>(context.GetConverter(enumerableShape.ElementType));
        }
    }
}

public sealed class NullTolerantListConverter<TElement> : ShapeShiftConverter<List<TElement>, JsonEncoder, JsonDecoder>
{
    private readonly ShapeShiftConverter<TElement, JsonEncoder, JsonDecoder> elementConverter;

    public NullTolerantListConverter(ShapeShiftConverter<TElement, JsonEncoder, JsonDecoder> elementConverter)
        => this.elementConverter = elementConverter;

    public override List<TElement> Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
    {
        if (decoder.TryReadNull())
        {
            return [];
        }

        context.DepthStep();
        List<TElement> elements = decoder.ReadStartVector() is int count ? new(count) : [];
        while (decoder.NextTokenType != TokenType.EndVector)
        {
            // A custom converter is responsible for honoring the context's security limits.
            if (elements.Count == context.MaxCollectionLength)
            {
                throw new ShapeShiftSerializationException($"Collection length exceeds the configured maximum of {context.MaxCollectionLength}.");
            }

            elements.Add(this.elementConverter.Read(ref decoder, context)!);
        }

        decoder.ReadEndVector();
        return elements;
    }

    public override void Write(ref JsonEncoder encoder, in List<TElement>? value, SerializationContext<JsonEncoder, JsonDecoder> context)
    {
        context.DepthStep();
        List<TElement> elements = value ?? [];
        encoder.WriteStartVector(elements.Count);
        foreach (TElement element in elements)
        {
            this.elementConverter.Write(ref encoder, element, context);
        }

        encoder.WriteEndVector();
    }
}
#endregion

#region Model
// Optional constructor parameters declare the defaults that SerializeDefaultValuesPolicy omits, and
// GuestName is the required value that the default deserialization policy insists on.
[GenerateShape]
public partial record Reservation(
    string GuestName,
    RoomKind Room = RoomKind.Standard,
    int Nights = 1,
    Money? Deposit = null,
    List<string>? Notes = null,
    GuestPreferences? Preferences = null);

public enum RoomKind
{
    /// <summary>A standard room.</summary>
    Standard,

    /// <summary>A suite.</summary>
    Suite,
}

// An amount in a currency, written as a single string by MoneyConverter.
public readonly struct Money : IEquatable<Money>
{
    public Money(decimal amount, string currency)
    {
        this.Amount = amount;
        this.Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public bool Equals(Money other) => this.Amount == other.Amount && this.Currency == other.Currency;

    public override bool Equals(object? obj) => obj is Money other && this.Equals(other);

    public override int GetHashCode() => HashCode.Combine(this.Amount, this.Currency);

    public override string ToString() => FormattableString.Invariant($"{this.Amount} {this.Currency}");
}

// Preferences carried as an embedded JSON document.
public record GuestPreferences
{
    public bool Quiet { get; init; }

    public int Floor { get; init; }
}
#endregion

// Member-level customizations that need no serializer configuration at all.
public static class MemberCustomization
{
    #region IncludingExcludingMembers
    public class Registration
    {
        [PropertyShape(Ignore = true)] // exclude this property from serialization
        public string? ScratchPad { get; set; }

        [PropertyShape] // include this non-public property in serialization
        internal string? InternalNote { get; set; }
    }
    #endregion

    #region ChangingPropertyNames
    public class Guest
    {
        [PropertyShape(Name = "name")] // serialize this property as "name"
        public string? GuestName { get; set; }
    }
    #endregion

    #region ChangingEnumNames
    public enum Floor
    {
        /// <summary>The first floor.</summary>
        [EnumMemberShape(Name = "1st")] // serialize this enum value as "1st"
        First,

        /// <summary>The second floor.</summary>
        [EnumMemberShape(Name = "2nd")] // serialize this enum value as "2nd"
        Second,
    }
    #endregion

    #region DeserializingConstructors
    public class ImmutableGuest
    {
        // The parameter is matched to the property it initializes, which is written as "person_name".
        public ImmutableGuest(string? name) => this.Name = name;

        [PropertyShape(Name = "person_name")]
        public string? Name { get; }
    }
    #endregion

    #region CustomComparerOnMember
    public class Directory
    {
        // The attribute governs the dictionary ShapeShift creates while deserializing; the property
        // initializer governs the one user code creates. Specify both so they agree.
        [UseComparer(typeof(StringComparer), nameof(StringComparer.OrdinalIgnoreCase))]
        public Dictionary<string, string> EntriesByName { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
    #endregion
}
