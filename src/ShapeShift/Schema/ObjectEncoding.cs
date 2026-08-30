// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Schema;

/// <summary>
/// Enumerates the ways an <see cref="ObjectContract"/> may be encoded.
/// </summary>
public enum ObjectEncoding
{
	/// <summary>
	/// The object is encoded as a map whose keys are the property names.
	/// </summary>
	/// <remarks>
	/// This is the default for every ShapeShift format because unrecognized properties can be ignored or retained,
	/// and properties can be added, removed, or reordered without invalidating existing payloads.
	/// </remarks>
	Map,

	/// <summary>
	/// The object is encoded as a vector whose elements are identified by position rather than by name.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Positional encoding is more compact but strictly more version sensitive: each member's position is part of
	/// the contract forever, so positions may be appended and retired but never reused or reordered. A retired
	/// position is written as a null placeholder when a later position is still occupied.
	/// </para>
	/// <para>
	/// Only positions after the last one a writer needs may be omitted (by writing a shorter vector), because a
	/// vector offers no way to say that an interior element is absent rather than null. See
	/// <see cref="PropertyContract.Position"/>.
	/// </para>
	/// </remarks>
	Positional,
}
