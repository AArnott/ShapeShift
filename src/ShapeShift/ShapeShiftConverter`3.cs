// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

public abstract class ShapeShiftConverter<T, TEncoder, TDecoder> : ShapeShiftConverter<TEncoder, TDecoder>, IShapeShiftConverterInternal<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	internal override Type DataType => typeof(T);

	public abstract void Write(ref TEncoder encoder, in T? value, SerializationContext<TEncoder, TDecoder> context);

	public abstract T? Read(ref TDecoder decoder, SerializationContext<TEncoder, TDecoder> context);

	/// <inheritdoc/>
	public override sealed void WriteObject(ref TEncoder writer, object? value, SerializationContext<TEncoder, TDecoder> context) => this.Write(ref writer, (T?)value, context);

	/// <inheritdoc/>
	public override sealed object? ReadObject(ref TDecoder reader, SerializationContext<TEncoder, TDecoder> context) => this.Read(ref reader, context);

	ShapeShiftConverter<TEncoder, TDecoder> IShapeShiftConverterInternal<TEncoder, TDecoder>.WrapWithReferencePreservation() => this.WrapWithReferencePreservation();

	ShapeShiftConverter<TEncoder, TDecoder> IShapeShiftConverterInternal<TEncoder, TDecoder>.UnwrapReferencePreservation() => this.UnwrapReferencePreservation();

	/// <inheritdoc cref="IShapeShiftConverterInternal{TEncoder, TDecoder}.WrapWithReferencePreservation" />
	internal virtual ShapeShiftConverter<T, TEncoder, TDecoder> WrapWithReferencePreservation() => typeof(T).IsValueType ? this : new ReferencePreservingConverter<T, TEncoder, TDecoder>(this);

	/// <inheritdoc cref="IShapeShiftConverterInternal{TEncoder, TDecoder}.UnwrapReferencePreservation" />
	internal virtual ShapeShiftConverter<T, TEncoder, TDecoder> UnwrapReferencePreservation() => this;
}
