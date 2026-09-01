// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Equality;

/// <summary>
/// Encodes a leaf value into a canonical byte sequence suitable for keyed hashing.
/// </summary>
/// <typeparam name="T">The leaf type.</typeparam>
/// <param name="value">The value to encode.</param>
/// <param name="destination">The buffer to receive the encoded bytes. At least 16 bytes are always available.</param>
/// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
/// <remarks>
/// The encoding must be <em>canonical</em>: two values that are equal according to
/// <see cref="EqualityComparer{T}.Default"/> must produce identical byte sequences.
/// </remarks>
internal delegate int LeafEncoder<in T>(T value, Span<byte> destination);
