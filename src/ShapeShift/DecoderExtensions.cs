// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1101 // Prefix local calls with this

namespace ShapeShift;

/// <summary>
/// Extension methods for <see cref="IDecoder"/>.
/// </summary>
public static class DecoderExtensions
{
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
}
