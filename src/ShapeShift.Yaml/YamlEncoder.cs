// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Yaml;

/// <summary>
/// A ShapeShift-compatible YAML encoder.
/// </summary>
/// <param name="writer">The underlying text writer to which to write the YAML.</param>
public ref struct YamlEncoder(TextWriter writer) : IEncoder
{
    /// <inheritdoc/>
    public void WriteStartVector(int? itemCount)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void WriteEndVector()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void WriteStartMap(int? propertyCount)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void WriteEndMap()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void WritePropertyName(ReadOnlySpan<char> name)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void WriteNull()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void Write(int value)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void Write(string? value)
    {
        throw new NotImplementedException();
    }
}
