// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Taml;

public record TamlSerializer : ShapeShiftSerializer<TamlEncoder, TamlDecoder>
{
	public string Serialize<T>(in T? value)
		where T : IShapeable<T> => this.Serialize<T, T>(value);

	public string Serialize<T, TProvider>(in T? value)
		where TProvider : IShapeable<T>
	{
		StringWriter stringWriter = new();
		TamlEncoder encoder = new(stringWriter);
		this.Serialize(ref encoder, value, TProvider.GetTypeShape());
		return stringWriter.ToString();
	}

	public T? Deserialize<T>(string yaml)
		where T : IShapeable<T> => this.Deserialize<T, T>(yaml);

	public T? Deserialize<T, TProvider>(string yaml)
		where TProvider : IShapeable<T>
	{
		StringReader stringReader = new(yaml);
		TamlDecoder decoder = new(stringReader);
		return this.Deserialize(ref decoder, TProvider.GetTypeShape());
	}
}
