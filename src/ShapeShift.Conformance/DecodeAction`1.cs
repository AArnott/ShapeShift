// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// Reads tokens from a decoder that a <see cref="FormatConformanceAdapter{TEncoder, TDecoder}"/> owns.
/// </summary>
/// <typeparam name="TDecoder">The format's decoder type.</typeparam>
/// <param name="decoder">
/// The decoder to read from. It is passed by reference because decoders are typically mutable
/// <see langword="ref" /> structs whose position must advance as the callback reads.
/// </param>
public delegate void DecodeAction<TDecoder>(ref TDecoder decoder)
	where TDecoder : IDecoder, allows ref struct;
