// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Yaml;

/// <summary>
/// Serializes objects to YAML text and reads YAML 1.2 documents back into objects.
/// </summary>
public record YamlSerializer : ShapeShiftSerializer<YamlEncoder, YamlDecoder>
{
	/// <summary>
	/// Gets the options used when reading YAML.
	/// </summary>
	/// <value>The load options. The default limits nesting and alias expansion and keeps duplicate mapping keys.</value>
	public YamlLoadOptions LoadOptions { get; init; } = new();

	/// <summary>
	/// Writes one value as a YAML document.
	/// </summary>
	/// <typeparam name="T">The value type. It must be its own shape provider.</typeparam>
	/// <param name="value">The value to write.</param>
	/// <returns>The YAML document, without a trailing document marker.</returns>
	public string Serialize<T>(in T? value)
		where T : IShapeable<T> => this.Serialize<T, T>(value);

	/// <summary>
	/// Writes one value as a YAML document.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The shape provider for <typeparamref name="T"/>.</typeparam>
	/// <param name="value">The value to write.</param>
	/// <returns>The YAML document, without a trailing document marker.</returns>
	public string Serialize<T, TProvider>(in T? value)
		where TProvider : IShapeable<T>
	{
		StringWriter stringWriter = new();
		YamlEncoder encoder = new(stringWriter);
		this.Serialize(ref encoder, value, TProvider.GetTypeShape());
		return stringWriter.ToString();
	}

	/// <summary>
	/// Reads the first YAML document in <paramref name="yaml"/>.
	/// </summary>
	/// <typeparam name="T">The value type. It must be its own shape provider.</typeparam>
	/// <param name="yaml">The YAML text. Later documents are not read.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T>(string yaml)
		where T : IShapeable<T> => this.Deserialize<T, T>(yaml);

	/// <summary>
	/// Reads the first YAML document in <paramref name="yaml"/>.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The shape provider for <typeparamref name="T"/>.</typeparam>
	/// <param name="yaml">The YAML text. Later documents are not read.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="yaml"/> is <see langword="null"/>.</exception>
	/// <exception cref="DecoderException">The text is not valid YAML 1.2.</exception>
	public T? Deserialize<T, TProvider>(string yaml)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(yaml);
		StringReader stringReader = new(yaml);
		YamlDecoder decoder = new(stringReader, this.LoadOptions);
		return this.Deserialize(ref decoder, TProvider.GetTypeShape());
	}

	/// <summary>
	/// Reads every document in <paramref name="yaml"/>.
	/// </summary>
	/// <typeparam name="T">The value type. It must be its own shape provider.</typeparam>
	/// <param name="yaml">The YAML stream.</param>
	/// <returns>
	/// One value per document, in order. An empty or comment-only stream returns an empty list,
	/// which is the case <see cref="Deserialize{T}(string)"/> reports as null.
	/// </returns>
	public IReadOnlyList<T?> DeserializeAll<T>(string yaml)
		where T : IShapeable<T> => this.DeserializeAll<T, T>(yaml);

	/// <summary>
	/// Reads every document in <paramref name="yaml"/>.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <typeparam name="TProvider">The shape provider for <typeparamref name="T"/>.</typeparam>
	/// <param name="yaml">The YAML stream.</param>
	/// <returns>
	/// One value per document, in order. An empty or comment-only stream returns an empty list.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="yaml"/> is <see langword="null"/>.</exception>
	/// <exception cref="DecoderException">The text is not valid YAML 1.2.</exception>
	public IReadOnlyList<T?> DeserializeAll<T, TProvider>(string yaml)
		where TProvider : IShapeable<T>
	{
		ArgumentNullException.ThrowIfNull(yaml);
		StringReader stringReader = new(yaml);
		YamlDecoder decoder = new(stringReader, this.LoadOptions);
		if (decoder.DocumentCount == 0)
		{
			return [];
		}

		List<T?> values = new(decoder.DocumentCount);
		do
		{
			values.Add(this.Deserialize(ref decoder, TProvider.GetTypeShape()));
		}
		while (decoder.TryAdvanceDocument());

		return values;
	}
}
