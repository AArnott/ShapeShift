// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// Reads a value from a decoder that a <see cref="FormatConformanceAdapter{TEncoder, TDecoder}"/> owns.
/// </summary>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
/// <typeparam name="TResult">
/// The type of the value produced. It may not itself be a <see langword="ref" /> struct
/// because the value outlives the decoder that produced it.
/// </typeparam>
/// <param name="decoder">
/// The decoder to read from. It is passed by reference because decoders are typically mutable
/// <see langword="ref" /> structs whose position must advance as the callback reads.
/// </param>
/// <returns>The value that was read.</returns>
public delegate TResult DecodeFunc<TDecoder, TResult>(ref TDecoder decoder)
	where TDecoder : IDecoder, allows ref struct;
