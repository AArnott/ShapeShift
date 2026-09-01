// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using CoreCustomization;
using ShapeShift;
using ShapeShift.Json;

namespace Samples.Tests;

/// <summary>
/// Executes the core customization walkthrough that the docs embed, so a documented sample cannot rot.
/// </summary>
public class CoreCustomizationSamplesTests
{
	/// <summary>
	/// Verifies that deriving a configuration leaves the baseline serializer untouched.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task ImmutableConfiguration()
	{
		(string baseline, string compact) = CoreCustomizationSamples.ConfigureImmutably();

		// The baseline still writes every property, under its own naming policy.
		await Assert.That(baseline).Contains("\"room\"");
		await Assert.That(baseline).Contains("\"nights\"");

		// The derived serializer inherited the naming policy and added default-value omission.
		await Assert.That(compact).IsEqualTo("""{"guestName":"Ada"}""");
	}

	/// <summary>
	/// Verifies the naming policy is applied to properties.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task NamingPolicy()
	{
		string json = CoreCustomizationSamples.ApplyNamingPolicy();
		await Assert.That(json).Contains("\"guest_name\":\"Ada\"");
		await Assert.That(json).Contains("\"room\":\"Suite\"");
	}

	/// <summary>
	/// Verifies that defaulted values are omitted while the required value survives.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task DefaultValueOmission()
	{
		await Assert.That(CoreCustomizationSamples.OmitDefaultValues()).IsEqualTo("""{"GuestName":"Ada"}""");
	}

	/// <summary>
	/// Verifies that the default policy rejects a payload missing a required value and the relaxed policy accepts it.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task Strictness()
	{
		(string rejected, Reservation accepted) = CoreCustomizationSamples.RejectIncompletePayloads();
		await Assert.That(rejected).Contains("GuestName");
		await Assert.That(accepted.Nights).IsEqualTo(2);
		await Assert.That(accepted.GuestName).IsNull();
	}

	/// <summary>
	/// Verifies that a configured limit rejects an oversized payload.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task SecurityLimits()
	{
		await Assert.That(CoreCustomizationSamples.BoundUntrustedInput()).Contains("maximum of 4");
	}

	/// <summary>
	/// Verifies that the starting context carries both limits and state.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task StartingContext()
	{
		JsonSerializer serializer = CoreCustomizationSamples.ApplyStartingContext();
		await Assert.That(serializer.StartingContext.MaxDepth).IsEqualTo(256);
		await Assert.That(serializer.StartingContext[MoneyConverter.DefaultCurrencyKey]).IsEqualTo("USD");
	}

	/// <summary>
	/// Verifies that a custom converter can read ambient state from the serialization context.
	/// </summary>
	/// <returns>A task tracking the assertion.</returns>
	[Test]
	public async Task ConverterState()
	{
		Reservation reservation = CoreCustomizationSamples.ApplyConverterState();
		await Assert.That(reservation.Deposit).IsEqualTo(new Money(25.00m, "USD"));
	}

	/// <summary>
	/// Verifies that the visitor-based factory reads a null array as an empty list.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task NullTolerantCollection()
	{
		Reservation reservation = CoreCustomizationSamples.ReadNullCollection();
		await Assert.That(reservation.Notes).IsNotNull();
		await Assert.That(reservation.Notes!.Count).IsEqualTo(0);
	}

	/// <summary>
	/// Verifies the round trip through the converter instance and both converter factories.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task CustomConverterRoundtrip()
	{
		(string payload, Reservation roundtripped) = CoreCustomizationSamples.RoundtripWithCustomConverters();

		// The converter instance wrote the money as one string.
		await Assert.That(payload).Contains("\"Deposit\":\"25.5 USD\"");

		// The generic factory wrote the preferences as a JSON string holding a JSON document.
		await Assert.That(payload).Contains("\"Preferences\":\"{");

		await Assert.That(roundtripped.GuestName).IsEqualTo("Ada");
		await Assert.That(roundtripped.Room).IsEqualTo(RoomKind.Suite);
		await Assert.That(roundtripped.Deposit).IsEqualTo(new Money(25.5m, "USD"));
		await Assert.That(roundtripped.Notes!.Count).IsEqualTo(1);
		await Assert.That(roundtripped.Preferences!.Floor).IsEqualTo(4);
		await Assert.That(roundtripped.Preferences.Quiet).IsTrue();
	}

	/// <summary>
	/// Verifies that the custom converter's contract override reaches the JSON Schema projection.
	/// </summary>
	/// <returns>A task tracking the assertions.</returns>
	[Test]
	public async Task CustomConverterContractIsProjected()
	{
		JsonSerializer serializer = CoreCustomizationSamples.CreateConfiguredSerializer();
		string schema = serializer.GetJsonSchema<Reservation>().ToJsonString();

		await Assert.That(schema).Contains("Deposit");

		// The money is described as a string, not as an object with its CLR properties.
		await Assert.That(schema).DoesNotContain("Currency");
	}
}
