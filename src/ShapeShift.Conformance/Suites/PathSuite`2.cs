// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance.Suites;

/// <summary>
/// Verifies <see cref="ShapeShiftPath"/> traversal, which powers targeted (fragment) reads.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
/// <remarks>
/// Traversal is implemented once, format-neutrally, on top of <see cref="IDecoder.Skip"/>,
/// <see cref="IDecoder.ReadPropertyName"/>, and <see cref="IDecoder.NextTokenType"/>. These cases
/// therefore assert that those three primitives compose correctly, rather than that the format
/// implements seeking itself.
/// </remarks>
internal sealed class PathSuite<TEncoder, TDecoder> : IConformanceSuite<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public ConformanceCategory Category => ConformanceCategory.Path;

	/// <inheritdoc/>
	public void AddTests(ConformanceTestCollector<TEncoder, TDecoder> collector)
	{
		Requires.NotNull(collector);

		string? skipReason = collector.Options.SupportsPathSeek && collector.Options.SupportsSkip
			? null
			: "The format does not support path traversal.";

		collector.Add("SeekToProperty", skipReason, adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			byte[] payload = EncodeDocument(adapter);

			string? found = adapter.Decode(payload, (ref TDecoder decoder) =>
				serializer.DeserializeFragment(ref decoder, new ShapeShiftPath("title"), Shapes.Of<string, ConformanceWitness>()));
			ConformanceAssert.Equal("hello", found, "the value at $.title");
		});

		collector.Add("SeekToNestedProperty", skipReason, adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			byte[] payload = EncodeDocument(adapter);

			long found = adapter.Decode(payload, (ref TDecoder decoder) =>
				serializer.DeserializeFragment(ref decoder, new ShapeShiftPath("nested", "deep"), Shapes.Of<long, ConformanceWitness>()));
			ConformanceAssert.Equal(99L, found, "the value at $.nested.deep");
		});

		collector.Add("SeekToVectorIndex", skipReason, adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			byte[] payload = EncodeDocument(adapter);

			string? found = adapter.Decode(payload, (ref TDecoder decoder) =>
				serializer.DeserializeFragment(ref decoder, new ShapeShiftPath("items", 1), Shapes.Of<string, ConformanceWitness>()));
			ConformanceAssert.Equal("second", found, "the value at $.items[1]");
		});

		collector.Add("SeekToMissingPropertyReportsFailure", skipReason, adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			byte[] payload = EncodeDocument(adapter);

			bool found = adapter.Decode(payload, (ref TDecoder decoder) =>
				serializer.TryDeserializeFragment(ref decoder, new ShapeShiftPath("absent"), Shapes.Of<string, ConformanceWitness>(), out _));
			ConformanceAssert.False(found, "Seeking a property the map does not contain should report failure rather than throw.");
		});

		collector.Add("SeekBeyondVectorEndReportsFailure", skipReason, adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			byte[] payload = EncodeDocument(adapter);

			bool found = adapter.Decode(payload, (ref TDecoder decoder) =>
				serializer.TryDeserializeFragment(ref decoder, new ShapeShiftPath("items", 7), Shapes.Of<string, ConformanceWitness>(), out _));
			ConformanceAssert.False(found, "Seeking past the end of a vector should report failure rather than throw.");
		});

		collector.Add("SeekThroughNullReportsFailure", collector.Options.SupportsNull ? skipReason : "The format has no null representation.", adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			byte[] payload = EncodeDocument(adapter);

			bool found = adapter.Decode(payload, (ref TDecoder decoder) =>
				serializer.TryDeserializeFragment(ref decoder, new ShapeShiftPath("nothing", "deeper"), Shapes.Of<string, ConformanceWitness>(), out _));
			ConformanceAssert.False(found, "Seeking through a null should report failure rather than throw.");
		});

		collector.Add("SeekLeavesSuccessorReadable", skipReason, adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			byte[] payload = EncodeDocument(adapter);

			string? next = adapter.Decode(payload, (ref TDecoder decoder) =>
			{
				string? title = serializer.DeserializeFragment(ref decoder, new ShapeShiftPath("title"), Shapes.Of<string, ConformanceWitness>());
				ConformanceAssert.Equal("hello", title, "the value at $.title");
				return decoder.ReadPropertyName().ToString();
			});

			ConformanceAssert.Equal("items", next, "the key immediately following the sought value");
		});

		collector.Add("SeekToRootReadsWholeDocument", collector.Options.SupportsRootVectors ? skipReason : "The format cannot carry a vector at the root of a document.", adapter =>
		{
			ShapeShiftSerializer<TEncoder, TDecoder> serializer = adapter.CreateSerializer();
			byte[] payload = adapter.Encode(static (ref TEncoder encoder) =>
			{
				encoder.WriteStartVector(2);
				encoder.WriteValue(1L);
				encoder.WriteValue(2L);
				encoder.WriteEndVector();
			});

			List<int>? all = adapter.Decode(payload, (ref TDecoder decoder) =>
				serializer.DeserializeFragment(ref decoder, ShapeShiftPath.Root, Shapes.Of<List<int>, ConformanceWitness>()));
			ConformanceAssert.Equal(2, all?.Count ?? -1, "the element count of the document read at the root path");
		});
	}

	private static byte[] EncodeDocument(FormatConformanceAdapter<TEncoder, TDecoder> adapter)
		=> adapter.Encode(static (ref TEncoder encoder) =>
		{
			encoder.WriteStartMap(4);

			encoder.WritePropertyName("title");
			encoder.WriteValue("hello");

			encoder.WritePropertyName("items");
			encoder.WriteStartVector(3);
			encoder.WriteValue("first");
			encoder.WriteValue("second");
			encoder.WriteValue("third");
			encoder.WriteEndVector();

			encoder.WritePropertyName("nested");
			encoder.WriteStartMap(2);
			encoder.WritePropertyName("shallow");
			encoder.WriteValue("x");
			encoder.WritePropertyName("deep");
			encoder.WriteValue(99L);
			encoder.WriteEndMap();

			encoder.WritePropertyName("nothing");
			encoder.WriteNull();

			encoder.WriteEndMap();
		});
}
