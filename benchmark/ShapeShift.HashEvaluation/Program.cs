// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Running;
using ShapeShift.Benchmarks.HashEvaluation;

if (args.Length > 0 && args[0] == "--self-test")
{
	Blake3SelfTest.Run();
	Console.WriteLine("BLAKE3 self-test passed (official hash, keyed_hash, and derive_key vectors, plus the C2SP keyed example).");
	return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
