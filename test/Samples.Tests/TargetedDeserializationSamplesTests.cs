// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TargetedDeserialization;

namespace Samples.Tests;

/// <summary>
/// Executes the targeted deserialization walkthrough that the docs embed, so a documented sample cannot rot.
/// </summary>
public class TargetedDeserializationSamplesTests
{
	/// <summary>
	/// Verifies that the typed expression paths and the raw path all locate the intended values.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task ExpressionAndRawPathsLocateTheSameDocument()
	{
		(bool found, string? city, string? tag, string? zip, string? someTag) = TargetedDeserializationSample.Run();

		await Assert.That(found).IsTrue();
		await Assert.That(city).IsEqualTo("London");
		await Assert.That(tag).IsEqualTo("programmer");
		await Assert.That(zip).IsEqualTo("E1");
		await Assert.That(someTag).IsEqualTo("mathematician");
	}
}
