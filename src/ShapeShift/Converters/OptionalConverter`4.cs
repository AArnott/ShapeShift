// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Converters;

/// <summary>
/// Converts an optional value through its element representation.
/// </summary>
/// <typeparam name="TOptional">The optional wrapper type.</typeparam>
/// <typeparam name="TElement">The wrapped element type.</typeparam>
/// <typeparam name="TEncoder">The encoder type.</typeparam>
/// <typeparam name="TDecoder">The decoder type.</typeparam>
internal sealed class OptionalConverter<TOptional, TElement, TEncoder, TDecoder>(
	ShapeShiftConverter<TElement, TEncoder, TDecoder> elementConverter,
	OptionDeconstructor<TOptional, TElement> deconstructor,
	Func<TOptional> noneConstructor,
	Func<TElement, TOptional> someConstructor) : ShapeShiftConverter<TOptional, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override TOptional? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
		=> decoder.TryReadNull() ? this.ReadNone(ref decoder) : someConstructor(elementConverter.Read(ref decoder, context)!);

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in TOptional? value, SerializationContext<TEncoder, TDecoder> context)
	{
		if (value is null || !deconstructor(value, out TElement? element))
		{
			encoder.WriteNull();
		}
		else
		{
			elementConverter.Write(ref encoder, element, context);
		}
	}

	private TOptional ReadNone(ref TDecoder decoder)
	{
		decoder.ReadNull();
		return noneConstructor();
	}
}
