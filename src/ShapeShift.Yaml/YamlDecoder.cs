// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

namespace ShapeShift.Yaml;

/// <summary>
/// A ShapeShift-compatible YAML decoder.
/// </summary>
/// <param name="reader">The underlying text reader from which to get the YAML.</param>
public ref struct YamlDecoder(TextReader reader) : IDecoder
{
    /// <inheritdoc/>
    public TokenType NextTokenType => throw new NotImplementedException();

    /// <inheritdoc/>
    public bool TryReadNull()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public int? ReadStartMap()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void ReadEndMap()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public int? ReadStartVector()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void ReadEndVector()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public ReadOnlySpan<char> ReadPropertyName()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void Skip()
    {
        throw new NotImplementedException();
    }

    public void ReadNull()
    {
        if (!this.TryReadNull())
        {
            throw new DecoderException($"Expected a null token but instead got {this.NextTokenType}.");
        }
    }

    /// <inheritdoc/>
    public int ReadInt32()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public string ReadString()
    {
        throw new NotImplementedException();
    }
}
