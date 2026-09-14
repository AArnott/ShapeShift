// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.MsgPack;

/// <summary>
/// The length-prefixed framing that <see cref="MsgPackSerializer"/>'s frame helpers read and write.
/// </summary>
/// <remarks>
/// <para>
/// MessagePack values are self-delimiting, so a stream of concatenated values needs no framing at all; that is
/// what <see cref="MsgPackSerializer.DeserializeAllAsync{T}(System.IO.Pipelines.PipeReader, long, CancellationToken)"/>
/// reads. Framing earns its keep when a transport needs to know a message's extent <em>before</em> anything parses
/// it: to hand a whole message off to another component, to reject an implausibly large message without decoding
/// it, to skip a message whose contract the receiver does not implement, or to interleave MessagePack with other
/// content on one connection.
/// </para>
/// <para>
/// A frame is a <see cref="LengthPrefixByteCount"/>-byte big-endian unsigned length, followed by exactly that many
/// bytes, which must contain exactly one complete MessagePack value. A reader rejects a frame whose declared
/// length exceeds the caller's limit before buffering any of it, rejects a stream that ends inside a frame, and
/// rejects frame content that is not exactly one value.
/// </para>
/// </remarks>
public static class MsgPackFraming
{
	/// <summary>
	/// The number of bytes in a frame's big-endian unsigned length prefix.
	/// </summary>
	public const int LengthPrefixByteCount = 4;

	/// <summary>
	/// The default maximum frame length, in bytes, that the frame helpers accept.
	/// </summary>
	/// <remarks>
	/// A length prefix is attacker-controlled input: it is read before any of the frame's content arrives, so an
	/// unbounded reader would wait for (and buffer) as much as the prefix claims. Callers that legitimately
	/// exchange larger messages should raise this deliberately.
	/// </remarks>
	public const long DefaultMaxFrameLength = 64 * 1024 * 1024;
}
