// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using static ShapeShift.ConverterResult;

namespace ShapeShift;

internal static class ConverterResult
{
	/// <summary>
	/// Wraps a converter as a successful result.
	/// </summary>
	/// <typeparam name="TEncoder"><inheritdoc cref="SerializerBase{TEncoder, TEncoder}" path="/typeparam[@name='TEncoder']"/></typeparam>
	/// <typeparam name="TDecoder"><inheritdoc cref="SerializerBase{TEncoder, TDecoder}" path="/typeparam[@name='TDecoder']"/></typeparam>
	/// <param name="value">The converter.</param>
	/// <returns>A successful result.</returns>
	internal static ConverterResult<TEncoder, TDecoder> Ok<TEncoder, TDecoder>(ShapeShiftConverter<TEncoder, TDecoder> value)
		where TEncoder : IEncoder, allows ref struct
		where TDecoder : IDecoder, allows ref struct
		=> new(value);
}
