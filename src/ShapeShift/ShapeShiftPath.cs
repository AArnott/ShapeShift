// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Text;

namespace ShapeShift;

/// <summary>
/// Identifies the location of a value within a serialized document as a sequence of
/// <see cref="ShapeShiftPathElement"/> steps, each of which is either a map property name or a vector index.
/// </summary>
/// <remarks>
/// <para>
/// This type is format-neutral: the same path may be used to seek within a JSON document, a MessagePack document,
/// or any other ShapeShift-supported encoding, via the <c>TrySeek</c> decoder extension member declared in <see cref="DecoderExtensions"/>.
/// </para>
/// <para>
/// Construct a path with a collection expression or the constructor's <c>params</c> parameter,
/// taking advantage of the implicit conversions from <see langword="string" /> and <see langword="int" />
/// to <see cref="ShapeShiftPathElement"/>, e.g. <c>new ShapeShiftPath("items", 2, "name")</c>.
/// </para>
/// </remarks>
public readonly struct ShapeShiftPath : IEquatable<ShapeShiftPath>, IEnumerable<ShapeShiftPathElement>
{
	private readonly ImmutableArray<ShapeShiftPathElement> elements;

	/// <summary>
	/// Initializes a new instance of the <see cref="ShapeShiftPath"/> struct.
	/// </summary>
	/// <param name="elements">The ordered steps that locate a value, starting from the root of the document.</param>
	public ShapeShiftPath(params ReadOnlySpan<ShapeShiftPathElement> elements)
	{
		this.elements = [.. elements];
	}

	/// <summary>
	/// Gets the path that identifies the root of the document (i.e. an empty sequence of elements).
	/// </summary>
	public static ShapeShiftPath Root => default;

	/// <summary>
	/// Gets the number of elements (steps) in this path.
	/// </summary>
	public int Count => this.Elements.Length;

	private ImmutableArray<ShapeShiftPathElement> Elements => this.elements.IsDefault ? ImmutableArray<ShapeShiftPathElement>.Empty : this.elements;

	/// <summary>
	/// Gets the element at the given 0-based index within this path.
	/// </summary>
	/// <param name="index">The 0-based index into the path.</param>
	/// <returns>The element at that position.</returns>
	public ShapeShiftPathElement this[int index] => this.Elements[index];

	/// <summary>
	/// Checks two paths for equality.
	/// </summary>
	/// <param name="left">The first path.</param>
	/// <param name="right">The second path.</param>
	/// <returns><see langword="true" /> if the paths are equal.</returns>
	public static bool operator ==(ShapeShiftPath left, ShapeShiftPath right) => left.Equals(right);

	/// <summary>
	/// Checks two paths for inequality.
	/// </summary>
	/// <param name="left">The first path.</param>
	/// <param name="right">The second path.</param>
	/// <returns><see langword="true" /> if the paths are not equal.</returns>
	public static bool operator !=(ShapeShiftPath left, ShapeShiftPath right) => !left.Equals(right);

	/// <summary>
	/// Gets an enumerator over the elements in this path.
	/// </summary>
	/// <returns>The enumerator.</returns>
	public ImmutableArray<ShapeShiftPathElement>.Enumerator GetEnumerator() => this.Elements.GetEnumerator();

	/// <inheritdoc/>
	IEnumerator<ShapeShiftPathElement> IEnumerable<ShapeShiftPathElement>.GetEnumerator() => ((IEnumerable<ShapeShiftPathElement>)this.Elements).GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.Elements).GetEnumerator();

	/// <inheritdoc/>
	public bool Equals(ShapeShiftPath other) => this.Elements.AsSpan().SequenceEqual(other.Elements.AsSpan());

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is ShapeShiftPath other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode hash = default;
		foreach (ShapeShiftPathElement element in this.Elements)
		{
			hash.Add(element);
		}

		return hash.ToHashCode();
	}

	/// <summary>
	/// Renders this path in a JSONPath-like notation (e.g. <c>$.items[2].name</c>) for diagnostic purposes.
	/// </summary>
	/// <returns>The rendered path.</returns>
	public override string ToString()
	{
		StringBuilder builder = new("$");
		foreach (ShapeShiftPathElement element in this.Elements)
		{
			if (element.IsPropertyName)
			{
				builder.Append('.').Append(element.PropertyName);
			}
			else
			{
				builder.Append('[').Append(element.Index).Append(']');
			}
		}

		return builder.ToString();
	}
}
