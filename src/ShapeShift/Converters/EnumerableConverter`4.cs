// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Converters;

internal class EnumerableConverter<TEnumerable, TElement, TEncoder, TDecoder> : ShapeShiftConverter<TEnumerable, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	private readonly Func<TEnumerable, IEnumerable<TElement>> getEnumerable;
	private readonly ShapeShiftConverter<TElement, TEncoder, TDecoder> elementConverter;
	private readonly MutableCollectionConstructor<TElement, TEnumerable> ctor;
	private readonly EnumerableAppender<TEnumerable, TElement> appender;

	public EnumerableConverter(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, ShapeShiftConverter<TElement, TEncoder, TDecoder> elementConverter)
	{
		this.getEnumerable = enumerableShape.GetGetEnumerable();
		this.elementConverter = elementConverter;
		this.ctor = enumerableShape.GetDefaultConstructor() ?? throw new NotSupportedException();
		this.appender = enumerableShape.GetAppender();
	}

	public override TEnumerable? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
	{
		if (decoder.TryReadNull())
		{
			return default;
		}

		context.DepthStep();
		int? length = decoder.ReadStartVector();
		var options = new CollectionConstructionOptions<TElement> { Capacity = length };
		var result = this.ctor(options);

		while (decoder.NextTokenType != TokenType.EndVector)
		{
			TElement element = this.elementConverter.Read(ref decoder, context)!;
			this.appender(ref result, element);
		}

		decoder.ReadEndVector();
		return result;
	}

	public override void Write(ref TEncoder encoder, in TEnumerable? value, SerializationContext<TEncoder, TDecoder> context)
	{
		if (value is null)
		{
			encoder.WriteNull();
			return;
		}

		context.DepthStep();
		IEnumerable<TElement> enumerable = this.getEnumerable(value);
		bool success = enumerable.TryGetNonEnumeratedCount(out int count);
		encoder.WriteStartVector(success ? count : null);

		foreach (TElement element in enumerable)
		{
			this.elementConverter.Write(ref encoder, element, context);
		}

		encoder.WriteEndVector();
	}
}
