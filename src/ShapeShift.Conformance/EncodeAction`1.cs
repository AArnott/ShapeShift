// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Conformance;

/// <summary>
/// Writes tokens to an encoder that a <see cref="FormatConformanceAdapter{TEncoder, TDecoder}"/> owns.
/// </summary>
/// <typeparam name="TEncoder">The format's encoder type.</typeparam>
/// <param name="encoder">
/// The encoder to write to. It is passed by reference because encoders are typically mutable
/// <see langword="ref" /> structs whose state must not be lost when the callback returns.
/// </param>
public delegate void EncodeAction<TEncoder>(ref TEncoder encoder)
	where TEncoder : IEncoder, allows ref struct;
