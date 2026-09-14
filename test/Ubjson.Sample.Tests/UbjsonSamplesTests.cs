// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Ubjson.Tests;

/// <summary>
/// Executes the walkthrough code the format-authoring guide embeds, so a documented sample cannot rot.
/// </summary>
public class UbjsonSamplesTests
{
	/// <summary>
	/// Verifies the round-trip walkthrough.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task RoundtripSample()
	{
		Measurement measurement = UbjsonSamples.Roundtrip();
		await Assert.That(measurement.Name).IsEqualTo("cabin-pressure");
		await Assert.That(measurement.Value).IsEqualTo(101.325m);
		await Assert.That(measurement.Samples.SequenceEqual([1, 2, 3])).IsTrue();
	}

	/// <summary>
	/// Verifies that the shared serializer policies apply to a third-party format.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task SharedPoliciesSample()
	{
		byte[] payload = UbjsonSamples.SerializeWithSharedPolicies();
		await Assert.That(Encoding.UTF8.GetString(payload)).Contains("name");
	}

	/// <summary>
	/// Verifies the asynchronous streaming walkthrough.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task StreamingSample()
	{
		IReadOnlyList<string> names = await UbjsonSamples.ReadAllAsync(CancellationToken.None);
		await Assert.That(string.Join(", ", names)).IsEqualTo("first, second");
	}

	/// <summary>
	/// Verifies the conformance walkthrough, which is the sample's own quality gate.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task ConformanceSample()
	{
		await Assert.That(UbjsonSamples.RunConformanceSuite().IsConformant).IsTrue();
	}
}
