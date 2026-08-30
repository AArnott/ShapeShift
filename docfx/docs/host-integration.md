# Host integration

ShapeShift ships no ASP.NET Core MVC formatter package and no SignalR hub
protocol package. This page records that decision, what to do instead, and what
would change it.

## Decision

**Deferred, deliberately, and revisited when the criteria below are met.**

The integrations are applicable and desirable, and the JSON and MessagePack
stream APIs they would need are stable. They are nevertheless not buildable
*well* yet, because both framework extension points are driven by runtime
`Type` values while every ShapeShift entry point is statically typed. Closing
that gap is a core API design task, not an integration package task, and doing
it inside integration packages would hard-code a shape-resolution policy that
belongs in `ShapeShift` and would be duplicated per format and per host.

Deferring costs nothing that cannot be recovered: an integration package is
purely additive, so shipping one later breaks no existing API, and applications
can integrate today with the pattern below.

## What works today

Both hosts hand you a `PipeWriter` and a `PipeReader`
(`HttpResponse.BodyWriter` and `HttpRequest.BodyReader`), which is exactly what
ShapeShift's asynchronous APIs consume. When the model type is known at the call
site — which it is in a minimal API handler, in a typed MVC action, and in a
typed hub method — no `Type`-driven bridge is needed at all:

[!code-csharp[JsonAsyncStreaming](../../samples/cs/JsonAsyncStreaming.cs#JsonAsyncStreaming)]

Applied to an ASP.NET Core endpoint, that is:

```csharp
// One immutable serializer for the whole application.
static readonly JsonSerializer Serializer = new() { PropertyNamingPolicy = ShapeShiftNamingPolicy.CamelCase };

app.MapPost("/reservations", async (HttpRequest request, HttpResponse response, CancellationToken token) =>
{
    Reservation? reservation = await Serializer.DeserializeAsync<Reservation>(request.BodyReader, cancellationToken: token);
    response.ContentType = "application/json";
    await Serializer.SerializeAsync(response.BodyWriter, Confirm(reservation), token);
});
```

This is trimming-safe and NativeAOT-safe, honors every policy described in
[Customizing the core](customization.md), and streams without buffering a whole
document. What it does *not* do is participate in MVC content negotiation or
model binding, which is precisely the part that needs the missing core API.

For SignalR, a hub protocol is a wire specification rather than a serializer
setting, so a ShapeShift-based protocol is an application-level component today.

## Why not a package yet

### The extension points are `Type`-driven; ShapeShift is not

MVC formatters receive the model type at runtime — `InputFormatterContext.ModelType`
is a `Type` and the formatter returns `object?`. SignalR is the same:
`IHubProtocol.TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, out HubMessage? message)`
must deserialize each argument into `object?` using
`IInvocationBinder.GetParameterTypes(string)`, an `IReadOnlyList<Type>`.

Every public ShapeShift entry point, by contrast, is statically typed: it takes
`T : IShapeable<T>`, a `TProvider : IShapeable<T>` witness, or an
`ITypeShape<T>`. The asynchronous `Stream`/`PipeReader`/`PipeWriter` overloads
that an integration would use accept only the first two forms; they have no
`ITypeShape<T>` overload at all. `SerializationContext.GetConverter(Type, ITypeShapeProvider)`
exists, but only for converters delegating within an operation already in
progress.

A `Type`-driven facade is buildable without reflection — `ITypeShapeProvider`
resolves a `Type` to an `ITypeShape`, and `ITypeShape.Invoke` supplies the
generic type parameter the same NativeAOT-safe way the
[converter factories](customization.md#converter-factories) do. But it forces
decisions that are core policy, not host policy:

- which `ITypeShapeProvider` answers for a type, and how an application
  registers one;
- what happens when a type has no generated shape — fail, or fall back to
  reflection (and thereby to an opt-in that must not be reachable by default);
- whether the facade is `object`-typed all the way through, and what that costs
  on a per-request hot path;
- how [SHIFT004](analyzers/SHIFT004.md) and the other analyzers report a model
  type whose shape was never generated, when the model type is discovered at
  runtime.

Answering those inside `ShapeShift.AspNetCore.*` would fork the answer per
package and freeze it before the core has one.

### SignalR needs a protocol implementation, not a serializer adapter

`IHubProtocol` is not "serialize this object"; it is the full hub envelope:
invocation, stream invocation, stream item, completion (with its result/error
union), cancel invocation, ping, close, and the ack/sequence messages added for
stateful reconnect. Those message types are ASP.NET Core classes with no PolyType
shapes, so each needs a hand-written converter or surrogate, and the payloads
must be byte-compatible with the shipped JSON and MessagePack hub protocols or
the package is useless for mixed clients. ASP.NET Core publishes no reusable
conformance corpus for that compatibility, so the package would owe a test
corpus of its own — a larger commitment than the serialization work it wraps.

### The support matrix outruns the core's stability

ShapeShift is `0.1-alpha`; every package targets `net10.0` exactly and has
package validation enabled. An integration package adds a
`Microsoft.AspNetCore.App` framework reference, which pins it to ASP.NET Core
majors and obliges it to track their protocol and formatter changes, while the
serializer API underneath it is still free to change. That is the wrong order:
the stable thing should be underneath.

### "NativeAOT-safe" must stay a claim we can defend

The pattern shown above is genuinely AOT-safe. An MVC formatter would sit inside
a pipeline whose model binding and content negotiation are reflective, and a hub
protocol sits under a dispatcher that builds per-method delegates. A ShapeShift
integration can be AOT-clean in itself, but publishing it before the core's
`Type`-dispatch policy exists risks shipping a package that is AOT-safe only in
configurations we have not defined.

## Criteria that would trigger implementation

Each is objective, and all four are required:

1. **A stable core.** `ShapeShift`, `ShapeShift.Json`, and `ShapeShift.MsgPack`
   have shipped 1.0 with their serializer and stream APIs frozen under package
   validation.
2. **A `Type`-driven facade in the core.** A documented, NativeAOT-safe API that
   resolves a `Type` to a shape and dispatches to the typed path, including
   `ITypeShape<T>` overloads for the asynchronous `Stream`, `PipeReader`, and
   `PipeWriter` APIs, a defined behavior for types with no generated shape, and
   analyzer coverage for it. Both integrations then become thin adapters, which
   is the only shape in which they are worth maintaining.
3. **Demonstrated demand.** A concrete consumer requirement that the pattern
   above does not already satisfy — that is, one that genuinely needs runtime
   type dispatch through MVC content negotiation or a mixed-client SignalR
   deployment.
4. **A compatibility corpus for SignalR.** Test vectors proving byte
   compatibility with the shipped JSON and MessagePack hub protocols, including
   the stateful-reconnect messages, plus a declared support window per
   ASP.NET Core major version.

Until then, core format packages stay free of framework dependencies, which is
what keeps trimming, NativeAOT, and deployment flexibility intact.
