// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Yaml;

/// <summary>
/// Options that control how a <see cref="YamlDecoder"/> reads a YAML 1.2 stream.
/// </summary>
public sealed record YamlLoadOptions
{
	/// <summary>
	/// Gets a value indicating whether a mapping may contain the same key more than once.
	/// </summary>
	/// <value>
	/// <see langword="false" /> to reject a repeated key. That is the default.
	/// YAML 1.2 requires mapping keys to be unique. Set this to <see langword="true" /> to keep
	/// every entry in source order, which is what the official YAML test suite accepts.
	/// </value>
	public bool AllowDuplicateKeys { get; init; } = false;

	/// <summary>
	/// Gets the maximum nesting depth of collections, block scalars excluded.
	/// </summary>
	/// <value>
	/// The default is 256. The limit applies to the source and to the nesting created when an alias is
	/// expanded, including a chain of collections that each alias the previous one. Parsing recurses up to
	/// this limit, so a value far above the default can exhaust the call stack.
	/// </value>
	public int MaxDepth { get; init; } = 256;

	/// <summary>
	/// Gets the maximum number of alias substitutions permitted in one document.
	/// </summary>
	/// <value>
	/// The default is 4096. Each alias that is followed counts as one, which bounds a branching alias bomb.
	/// </value>
	public int MaxAliasExpansions { get; init; } = 4096;

	/// <summary>
	/// Gets the maximum number of extra node visits caused by reading a node through an alias again.
	/// </summary>
	/// <value>
	/// The default is 1048576. The first time a node is read it does not count. Each later read, which happens
	/// because an alias points at it, counts as one node. This stops a large anchored collection from being
	/// replayed without bound. A replayed string is the same instance, so the limit counts nodes rather than bytes.
	/// </value>
	public int MaxExpandedNodes { get; init; } = 1_048_576;
}
