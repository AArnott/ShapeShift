// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Enumerates the ways a <see cref="MapContract"/> may be encoded.
/// </summary>
public enum MapEncoding
{
	/// <summary>
	/// The map is encoded as a map of string keys to values.
	/// </summary>
	StringKeyedMap,

	/// <summary>
	/// The map is encoded as a vector of two-element key/value vectors.
	/// </summary>
	/// <remarks>
	/// This encoding is used when the key type is not <see cref="string"/>,
	/// so that arbitrary key types are preserved with full fidelity.
	/// </remarks>
	KeyValuePairSequence,
}
