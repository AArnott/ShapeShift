// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Converters;

/// <summary>
/// Converts a value through its PolyType surrogate.
/// </summary>
/// <typeparam name="T">The represented type.</typeparam>
/// <typeparam name="TSurrogate">The surrogate type.</typeparam>
/// <typeparam name="TEncoder">The encoder type.</typeparam>
/// <typeparam name="TDecoder">The decoder type.</typeparam>
internal sealed class SurrogateConverter<T, TSurrogate, TEncoder, TDecoder>(
	ISurrogateTypeShape<T, TSurrogate> shape,
	ShapeShiftConverter<TSurrogate, TEncoder, TDecoder> surrogateConverter) : ShapeShiftConverter<T, TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <inheritdoc/>
	public override T? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context)
		=> shape.Marshaler.Unmarshal(surrogateConverter.Read(ref decoder, context));

	/// <inheritdoc/>
	public override void Write(ref TEncoder encoder, in T? value, SerializationContext<TEncoder, TDecoder> context)
		=> surrogateConverter.Write(ref encoder, shape.Marshaler.Marshal(value), context);
}
