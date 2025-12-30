// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift;

public interface IDecoder
{
    public TokenType NextTokenType { get; }

    public bool TryReadNull();

    public int? ReadStartMap();

    public void ReadEndMap();

    public int? ReadStartVector();

    public void ReadEndVector();

    public ReadOnlySpan<char> ReadPropertyName();

    public void Skip();

    public void ReadNull()
    {
        if (!this.TryReadNull())
        {
            throw new DecoderException($"Expected a null token but instead got {this.NextTokenType}.");
        }
    }

    public int ReadInt32();

    public string ReadString();
}
