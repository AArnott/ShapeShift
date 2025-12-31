// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

/// <summary>
/// Non-generic access to internal methods of <see cref="ShapeShiftConverter{T, TEncoder, TDecoder}"/>.
/// </summary>
/// <typeparam name="TEncoder"><inheritdoc cref="SerializerBase{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
/// <typeparam name="TDecoder"><inheritdoc cref="SerializerBase{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
internal interface IShapeShiftConverterInternal<TEncoder, TDecoder>
	where TEncoder : IEncoder, allows ref struct
	where TDecoder : IDecoder, allows ref struct
{
	/// <summary>
	/// Wraps this converter with a reference preservation converter.
	/// </summary>
	/// <returns>A converter. Possibly <see langword="this"/> if this instance is already reference preserving.</returns>
	ShapeShiftConverter<TEncoder, TDecoder> WrapWithReferencePreservation();

	/// <summary>
	/// Removes the outer reference preserving converter, if present.
	/// </summary>
	/// <returns>The unwrapped converter.</returns>
	ShapeShiftConverter<TEncoder, TDecoder> UnwrapReferencePreservation();
}
