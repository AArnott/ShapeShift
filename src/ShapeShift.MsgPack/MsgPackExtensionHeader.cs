// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.MsgPack;

/// <summary>
/// Describes a MessagePack extension value that a decoder is positioned at, without reading its payload.
/// </summary>
/// <param name="TypeCode">The extension type code. See <see cref="MsgPackExtensionCodes"/> for the codes ShapeShift reserves.</param>
/// <param name="Length">The number of bytes in the extension's payload.</param>
public readonly record struct MsgPackExtensionHeader(sbyte TypeCode, int Length);
