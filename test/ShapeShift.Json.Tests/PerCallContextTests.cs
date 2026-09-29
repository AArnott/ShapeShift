// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using ShapeShift.Tests;

namespace ShapeShift.Json.Tests;

/// <summary>
/// Verifies the core serializer overloads that accept a per-call <see cref="SerializationContext{TEncoder, TDecoder}"/>.
/// </summary>
public partial class PerCallContextTests : TestBase
{
	private static readonly object LabelKey = new();

	private readonly JsonSerializer serializer = new() { Converters = [new LabelConverter()] };

	[Test]
	public async Task Serialize_HonorsPerCallLimits()
	{
		SerializationContext<JsonEncoder, JsonDecoder> context = new() { MaxStringLength = 3 };

		Action act = () => this.SerializeWith("long", context);

		await Assert.That(act).Throws<ShapeShiftSerializationException>();
		await Assert.That(this.SerializeWith("abc", context)).IsEqualTo("\"abc\"");
		await Assert.That(this.serializer.StartingContext.MaxStringLength).IsNotEqualTo(3);
	}

	[Test]
	public async Task Deserialize_HonorsPerCallLimits()
	{
		SerializationContext<JsonEncoder, JsonDecoder> context = new() { MaxStringLength = 3 };

		Func<string?> act = () => this.DeserializeWith<string>("\"long\"", context);

		await Assert.That(act).Throws<ShapeShiftSerializationException>();
		await Assert.That(this.serializer.Deserialize<string, Witness>("\"long\"")).IsEqualTo("long");
	}

	[Test]
	public async Task PerCallState_IsVisibleToConverters()
	{
		SerializationContext<JsonEncoder, JsonDecoder> context = this.serializer.StartingContext;
		context[LabelKey] = "per-call";

		string json = this.SerializeWith(new Label(), context);
		Label? label = this.DeserializeWith<Label>("\"ignored\"", context);

		await Assert.That(json).IsEqualTo("\"per-call\"");
		await Assert.That(label?.Text).IsEqualTo("per-call");
		await Assert.That(this.serializer.Serialize(new Label())).IsEqualTo("\"(none)\"");
	}

	[Test]
	public async Task PerCallCancellationToken_IsHonored()
	{
		SerializationContext<JsonEncoder, JsonDecoder> context = new() { CancellationToken = new CancellationToken(canceled: true) };

		Action serialize = () => this.SerializeWith("a", context);
		Func<string?> deserialize = () => this.DeserializeWith<string>("\"a\"", context);

		await Assert.That(serialize).Throws<OperationCanceledException>();
		await Assert.That(deserialize).Throws<OperationCanceledException>();
	}

	[Test]
	public async Task InUseContext_IsRejectedByEveryOverload()
	{
		SerializationContext<JsonEncoder, JsonDecoder> inUse = this.CaptureInUseContext();
		ShapeShiftPath path = new("Value");
		ITypeShape<string> shape = Witness.GetTypeShape<string>();

		List<Action> actions =
		[
			() => this.SerializeWith("a", inUse),
			() => this.DeserializeWith<string>("\"a\"", inUse),
			() =>
			{
				JsonDecoder decoder = new(Encoding.UTF8.GetBytes("""{"Value":"a"}"""));
				this.serializer.TryDeserializeFragment(ref decoder, path, shape, out _, inUse);
			},
			() =>
			{
				JsonDecoder decoder = new(Encoding.UTF8.GetBytes("""{"Value":"a"}"""));
				this.serializer.DeserializeFragment(ref decoder, path, shape, inUse);
			},
			() => this.serializer.CreateSequenceReader(shape, inUse).Dispose(),
			() => this.serializer.CreateDocumentReader(shape, inUse).Dispose(),
			() => new ContextExposingSerializer().Create(shape.Provider, inUse),
		];

		foreach (Action action in actions)
		{
			await Assert.That(action).Throws<ArgumentException>().WithParameterName("startingContext");
		}
	}

	[Test]
	public async Task Fragments_HonorPerCallContext()
	{
		SerializationContext<JsonEncoder, JsonDecoder> context = new() { MaxStringLength = 3 };
		ShapeShiftPath path = new("Value");
		ITypeShape<string> shape = Witness.GetTypeShape<string>();
		byte[] json = Encoding.UTF8.GetBytes("""{"Value":"abc","Other":"long"}""");
		byte[] longJson = Encoding.UTF8.GetBytes("""{"Value":"long"}""");

		JsonDecoder decoder = new(json);
		bool found = this.serializer.TryDeserializeFragment(ref decoder, path, shape, out string? value, context);
		decoder = new(json);
		string? value2 = this.serializer.DeserializeFragment(ref decoder, path, shape, context);
		Action tooLong = () =>
		{
			JsonDecoder d = new(longJson);
			this.serializer.DeserializeFragment(ref d, path, shape, context);
		};

		await Assert.That(found).IsTrue();
		await Assert.That(value).IsEqualTo("abc");
		await Assert.That(value2).IsEqualTo("abc");
		await Assert.That(tooLong).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task Readers_HonorPerCallContext()
	{
		SerializationContext<JsonEncoder, JsonDecoder> context = new() { MaxStringLength = 3 };
		ITypeShape<string> shape = Witness.GetTypeShape<string>();

		List<string?> elements = [];
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes("""["a","bc","def"]"""));
		using (ShapeShiftSequenceReader<string, JsonEncoder, JsonDecoder> reader = this.serializer.CreateSequenceReader(shape, context))
		{
			while (reader.MoveNext(ref decoder))
			{
				elements.Add(reader.Current);
			}
		}

		Action tooLong = () =>
		{
			JsonDecoder d = new(Encoding.UTF8.GetBytes("\"abcd\""));
			using ShapeShiftDocumentReader<string, JsonEncoder, JsonDecoder> reader = this.serializer.CreateDocumentReader(shape, context);
			reader.MoveNext(ref d);
		};

		await Assert.That(string.Join(",", elements)).IsEqualTo("a,bc,def");
		await Assert.That(tooLong).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task DefaultArgument_BindsToCancellationTokenOverload()
	{
		// A 'default' final argument would be ambiguous without OverloadResolutionPriority.
		// It must bind to the CancellationToken overload, which uses StartingContext.
		JsonSerializer limited = this.serializer with { StartingContext = new() { MaxStringLength = 3 } };

		Action act = () =>
		{
			ArrayBufferWriter<byte> buffer = new();
			using Utf8JsonWriter writer = new(buffer);
			JsonEncoder encoder = new(writer);
			limited.Serialize(ref encoder, "long", Witness.GetTypeShape<string>(), default);
		};

		await Assert.That(act).Throws<ShapeShiftSerializationException>();
	}

	[Test]
	public async Task DerivedSerializer_CanCreateContextFromPerCallContext()
	{
		SerializationContext<JsonEncoder, JsonDecoder> context = new() { MaxDepth = 7 };
		context[LabelKey] = "derived";

		(int maxDepth, object? label, bool hasProvider) = new ContextExposingSerializer().Create(Witness.GetTypeShape<string>().Provider, context);

		await Assert.That(maxDepth).IsEqualTo(7);
		await Assert.That(label).IsEqualTo("derived");
		await Assert.That(hasProvider).IsTrue();
	}

	private SerializationContext<JsonEncoder, JsonDecoder> CaptureInUseContext()
	{
		CapturingConverter capturing = new();
		JsonSerializer capturingSerializer = new() { Converters = [capturing] };
		capturingSerializer.Serialize(new Label());
		return capturing.Captured;
	}

	private string SerializeWith<T>(T value, SerializationContext<JsonEncoder, JsonDecoder> context)
	{
		ArrayBufferWriter<byte> buffer = new();
		using (Utf8JsonWriter writer = new(buffer))
		{
			JsonEncoder encoder = new(writer);
			this.serializer.Serialize(ref encoder, value, Witness.GetTypeShape<T>(), context);
		}

		return Encoding.UTF8.GetString(buffer.WrittenSpan);
	}

	private T? DeserializeWith<T>(string json, SerializationContext<JsonEncoder, JsonDecoder> context)
	{
		JsonDecoder decoder = new(Encoding.UTF8.GetBytes(json));
		return this.serializer.Deserialize(ref decoder, Witness.GetTypeShape<T>(), context);
	}

	[GenerateShape]
	internal partial class Label
	{
		public string? Text { get; set; }
	}

	internal sealed class LabelConverter : ShapeShiftConverter<Label, JsonEncoder, JsonDecoder>
	{
		public override Label? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
		{
			decoder.Skip();
			return new Label { Text = context[LabelKey] as string };
		}

		public override void Write(ref JsonEncoder encoder, in Label? value, SerializationContext<JsonEncoder, JsonDecoder> context)
			=> encoder.WriteValue(context[LabelKey] as string ?? "(none)");
	}

	internal sealed class CapturingConverter : ShapeShiftConverter<Label, JsonEncoder, JsonDecoder>
	{
		internal SerializationContext<JsonEncoder, JsonDecoder> Captured { get; private set; }

		public override Label? Read(ref JsonDecoder decoder, SerializationContext<JsonEncoder, JsonDecoder> context)
			=> throw new NotSupportedException();

		public override void Write(ref JsonEncoder encoder, in Label? value, SerializationContext<JsonEncoder, JsonDecoder> context)
		{
			this.Captured = context;
			encoder.WriteNull();
		}
	}

	internal sealed record ContextExposingSerializer : ShapeShiftSerializer<JsonEncoder, JsonDecoder>
	{
		internal (int MaxDepth, object? Label, bool HasProvider) Create(ITypeShapeProvider provider, SerializationContext<JsonEncoder, JsonDecoder> startingContext)
		{
			using DisposableSerializationContext context = this.CreateSerializationContext(provider, startingContext);
			return (context.Value.MaxDepth, context.Value[LabelKey], context.Value.TypeShapeProvider is not null);
		}
	}

	[GenerateShapeFor<string>]
	[GenerateShapeFor<Label>]
	private partial class Witness
	{
		internal static ITypeShape<T> GetTypeShape<T>() => (ITypeShape<T>)GeneratedTypeShapeProvider.GetTypeShape(typeof(T))!;
	}
}
