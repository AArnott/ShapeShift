// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1101 // Prefix local calls with this

namespace ShapeShift;

/// <summary>
/// Extension methods for <see cref="IDecoder"/>.
/// </summary>
public static class DecoderExtensions
{
#if !DOCFX
	extension<TDecoder>(TDecoder decoder)
		where TDecoder : IDecoder, allows ref struct
	{
		public byte ReadByte() => checked((byte)decoder.ReadUInt64());
		public ushort ReadUInt16() => checked((ushort)decoder.ReadUInt64());
		public uint ReadUInt32() => checked((ushort)decoder.ReadUInt64());

		public sbyte ReadSByte() => checked((sbyte)decoder.ReadInt64());
		public short ReadInt16() => checked((short)decoder.ReadInt64());
		public int ReadInt32() => checked((int)decoder.ReadInt64());
	}

	// This overload set is intentionally separate from the one above: mutating methods like TrySeek
	// must receive the decoder *by reference* so that the caller's decoder is actually advanced.
	// A `ref` receiver on an extension block requires the type parameter to carry a `struct` constraint,
	// which every real IDecoder implementation satisfies, so we add it only here to avoid widening the
	// constraint on every other generic type in this library that merely reads through a decoder.
	extension<TDecoder>(ref TDecoder decoder)
		where TDecoder : struct, IDecoder, allows ref struct
	{
		/// <summary>
		/// Advances the decoder to the value located at a given <see cref="ShapeShiftPath"/>, skipping over
		/// everything else, without buffering or fully parsing any value that is not on the path.
		/// </summary>
		/// <param name="path">The location, relative to the decoder's current position, of the value to seek to.</param>
		/// <returns>
		/// <see langword="true" /> if the decoder was successfully positioned at the start of the value identified by <paramref name="path"/>;
		/// <see langword="false" /> if some step along the path could not be found (e.g. a map lacked a named property,
		/// a vector was shorter than a requested index, or a <see langword="null" /> was found where a map or vector was expected).
		/// </returns>
		/// <exception cref="DecoderException">
		/// Thrown when a step along the path expects a map or vector, but the decoder finds some other, non-<see langword="null" /> token.
		/// </exception>
		/// <remarks>
		/// <para>
		/// On a successful (<see langword="true" />) return, the decoder is positioned exactly as if the caller had
		/// manually stepped into each map/vector and skipped every sibling that precedes the sought value:
		/// the value at <paramref name="path"/> is ready to be read (or further sought into) next, but any
		/// remaining, unread siblings and ancestor closing tokens are left unconsumed.
		/// </para>
		/// <para>
		/// On a failed (<see langword="false" />) return, every map or vector that this method opened while searching
		/// has been fully consumed (including its closing token), leaving the decoder positioned immediately
		/// after the last container it searched.
		/// </para>
		/// </remarks>
		public bool TrySeek(ShapeShiftPath path) => TrySeekCore(ref decoder, path);
	}
#endif

	/// <summary>
	/// The implementation behind the public <c>TrySeek</c> extension member, callable from generic code
	/// (such as <see cref="ShapeShiftSerializer{TEncoder, TDecoder}"/>) whose own <typeparamref name="TDecoder"/>
	/// type parameter is not (and need not be) constrained to <see langword="struct"/>.
	/// </summary>
	/// <typeparam name="TDecoder">The type of decoder.</typeparam>
	/// <param name="decoder">The decoder to seek within.</param>
	/// <param name="path">The location, relative to the decoder's current position, of the value to seek to.</param>
	/// <returns><see langword="true" /> if <paramref name="path"/> was found; otherwise <see langword="false" />.</returns>
	internal static bool TrySeekCore<TDecoder>(ref TDecoder decoder, ShapeShiftPath path)
		where TDecoder : IDecoder, allows ref struct
	{
		foreach (ShapeShiftPathElement element in path)
		{
			if (decoder.NextTokenType == TokenType.Null)
			{
				decoder.ReadNull();
				return false;
			}

			if (element.IsPropertyName)
			{
				decoder.ReadStartMap();
				bool found = false;
				while (decoder.NextTokenType != TokenType.EndMap)
				{
					ReadOnlySpan<char> propertyName = decoder.ReadPropertyName();
					if (propertyName.SequenceEqual(element.PropertyName))
					{
						found = true;
						break;
					}

					decoder.Skip();
				}

				if (!found)
				{
					decoder.ReadEndMap();
					return false;
				}
			}
			else
			{
				decoder.ReadStartVector();
				int i = 0;
				bool found = false;
				while (decoder.NextTokenType != TokenType.EndVector)
				{
					if (i == element.Index)
					{
						found = true;
						break;
					}

					decoder.Skip();
					i++;
				}

				if (!found)
				{
					decoder.ReadEndVector();
					return false;
				}
			}
		}

		return true;
	}
}
