// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ShapeShift.Tests;

/// <summary>
/// Verifies the breadcrumb behavior of <see cref="ShapeShiftSerializationException"/>.
/// </summary>
public class ShapeShiftSerializationExceptionTests : TestBase
{
	[Test]
	public async Task Path_DefaultsToRoot()
	{
		ShapeShiftSerializationException ex = new("boom");

		await Assert.That(ex.Path).IsEqualTo(ShapeShiftPath.Root);
		await Assert.That(ex.Message).IsEqualTo("boom");
	}

	[Test]
	public async Task AddEnclosingPathElement_BuildsOutsideIn()
	{
		ShapeShiftSerializationException ex = new("boom");

		// Simulate an exception unwinding from $.items[2].name outward.
		await Assert.That(ex.AddEnclosingPathElement("name")).IsTrue();
		await Assert.That(ex.AddEnclosingPathElement(2)).IsTrue();
		await Assert.That(ex.AddEnclosingPathElement("items")).IsTrue();

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("items", 2, "name"));
		await Assert.That(ex.Message).IsEqualTo("boom Path: $.items[2].name.");
	}

	[Test]
	public async Task InitialPath_IsPreservedAndExtendable()
	{
		InvalidOperationException inner = new("inner");
		ShapeShiftSerializationException ex = new("boom", inner, new ShapeShiftPath("a", 1));

		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("a", 1));
		await Assert.That(ex.InnerException).IsSameReferenceAs(inner);

		ex.AddEnclosingPathElement("root");
		await Assert.That(ex.Path).IsEqualTo(new ShapeShiftPath("root", "a", 1));
	}

	[Test]
	public async Task InitialPath_MayBeRoot()
	{
		ShapeShiftSerializationException ex = new("boom", null, ShapeShiftPath.Root);

		await Assert.That(ex.Path).IsEqualTo(ShapeShiftPath.Root);
		await Assert.That(ex.Message).IsEqualTo("boom");
	}

	[Test]
	public async Task ToString_IncludesPathAndInnerException()
	{
		ShapeShiftSerializationException ex = new("boom", new InvalidOperationException("inner"));
		ex.AddEnclosingPathElement("child");

		string text = ex.ToString();

		await Assert.That(text).Contains("$.child");
		await Assert.That(text).Contains("inner");
	}

	[Test]
	public async Task ExceptionFilterUsage_RethrowsPreservingIdentity()
	{
		ShapeShiftSerializationException original = new("boom");

		ShapeShiftSerializationException Act()
		{
			try
			{
				try
				{
					throw original;
				}
				catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement("inner"))
				{
					throw;
				}
			}
			catch (ShapeShiftSerializationException ex) when (ex.AddEnclosingPathElement("outer"))
			{
				return ex;
			}
		}

		ShapeShiftSerializationException result = Act();

		await Assert.That(result).IsSameReferenceAs(original);
		await Assert.That(result.Path).IsEqualTo(new ShapeShiftPath("outer", "inner"));
	}
}
