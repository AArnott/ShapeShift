# Keyed BLAKE3 vs SipHash-2-4

Recommendation: **do not switch** ShapeShift's collision-resistant leaf hash from SipHash-2-4 to keyed BLAKE3.

A scalar C# keyed BLAKE3, validated against the official vectors, is slower at every size this code hashes, including 64 KiB. It allocates nothing, and a correct port is feasible. BLAKE3's extra cryptographic strength does not improve a secret-keyed hash that is folded to 32 bits. The published BLAKE3 speeds come from SIMD across many chunks, which does not apply to the 4–64 byte leaves on the hot path. On this machine an upstream AVX-512 build only pulls ahead of SipHash past 1 KiB, and only by hashing large buffers.

This project is a scratch benchmark. It is not a shipping library and it does not change `src/ShapeShift`.

## What is being hashed

`CollisionResistantHashingPolicy` draws one key per process from `RandomNumberGenerator` and, on every leaf, runs SipHash-2-4 and folds the 64-bit result to 32 bits (`SipHash.Fold`). Call shapes:

- Opaque leaves and the post-process of an existing hash code: 4 bytes.
- Numeric, temporal, and `Guid` leaves: 1–16 bytes (`LeafEncoders.MaxEncodedLength` is 16).
- Strings: UTF-16 bytes, so a short string of 4–32 characters is 8–64 bytes.
- `byte[]` and binary `ShapeShiftValue`s: whatever the payload is, occasionally 1 KiB–64 KiB.

The benchmark times one full keyed hash plus the 32-bit fold. Key bytes are parsed once, outside the timed loop, matching the static constructor. Per-call key mixing and finalization are inside the measurement.

## Benchmark

BenchmarkDotNet 0.15.8, job `Medium` (launch 2, warmup 10, iteration 15), .NET 10.0.15, RyuJIT x86-64-v4. Host: Linux VM, Intel Xeon at 2.40 GHz, 4 cores, AVX2 and AVX-512 available. `MemoryDiagnoser` reports **0 B** allocated for both methods at every length (the markdown report prints that as `-`). SipHash is the production `src/ShapeShift/Equality/SipHash.cs`, compiled into this assembly.

| Bytes | SipHash-2-4 | Scalar keyed BLAKE3 | BLAKE3 / SipHash |
|------:|------------:|--------------------:|-----------------:|
| 4 | 23.07 ns | 249.4 ns | 10.8× |
| 8 | 9.23 ns | 250.1 ns | 27.1× |
| 16 | 12.05 ns | 253.5 ns | 21.0× |
| 32 | 17.80 ns | 250.9 ns | 14.1× |
| 64 | 29.15 ns | 249.9 ns | 8.6× |
| 128 | 52.39 ns | 434.2 ns | 8.3× |
| 512 | 192.9 ns | 1.498 µs | 7.8× |
| 1024 | 377.6 ns | 2.890 µs | 7.7× |
| 4096 | 1.614 µs | 16.88 µs | 10.5× |
| 16384 | 5.962 µs | 67.80 µs | 11.4× |
| 65536 | 24.44 µs | 273.4 µs | 11.2× |

Standard deviation is under 1% of the mean in every row. The 4-byte SipHash point is slower than the 8-byte point because a length that is not a multiple of 8 takes the byte-at-a-time tail in `SipHash.Compute`.

Rough throughput of the scalar prototype stays around 0.24–0.35 GB/s. SipHash stays around 2.5–2.7 GB/s from 128 bytes upward. The scalar tree (inputs above one 1024-byte chunk) is slower per byte than the single-chunk path, because parent nodes are more serial compressions and nothing runs in parallel.

Reproduce:

```bash
dotnet run --project benchmark/ShapeShift.HashEvaluation/ShapeShift.HashEvaluation.csproj -c Release -- --self-test
dotnet run --project benchmark/ShapeShift.HashEvaluation/ShapeShift.HashEvaluation.csproj --no-build -c Release -- --filter '*KeyedHash*' --job medium
```

## Upstream SIMD, same machine

`blake3` crate 1.5.5, release build, with its AVX2 and AVX-512 kernels linked (crate 1.8 needs a newer Rust than this image). Three runs of `keyed_hash`, median shown. This harness is a timed loop, not BenchmarkDotNet, and it returns 32 bytes rather than a folded `int`. Treat the column as a ceiling for a mature SIMD implementation, not as a third BenchmarkDotNet row.

| Bytes | Upstream keyed BLAKE3 | vs SipHash on this machine |
|------:|-----------------------:|---------------------------:|
| 4 | 58 ns | slower (~2.5×) |
| 8 | 59 ns | slower (~6×) |
| 16 | 42 ns | slower (~3.5×) |
| 64 | 49 ns | slower (~1.7×) |
| 1024 | 740 ns | slower (~2.0×) |
| 4096 | 875 ns | faster (~1.8×) |
| 65536 | 8.19 µs | faster (~3.0×) |

Single-chunk throughput is about 1.4 GB/s. At 4 KiB (four chunks) it jumps to about 4.7 GB/s, and at 64 KiB it is about 8.0 GB/s. That jump is the parallel tree. It never arrives for a 4–64 byte leaf, which is one compression.

## Correctness, size, and NativeAOT

The prototype follows the reference algorithm: 7-round compression, 1024-byte chunks, the chaining-value stack, and keyed mode (the 32-byte key is the chaining value, and `KEYED_HASH` is set on every compression, including parents). `dotnet run -- --self-test` checks:

- All 35 official cases in `hash`, `keyed_hash`, and `derive_key`, each a 131-byte extended output, from [BLAKE3 `test_vectors.json` at `ae3e8e6`](https://github.com/BLAKE3-team/BLAKE3/blob/ae3e8e6b3a5ae3190ca5d62820789b17886a0038/test_vectors/test_vectors.json). Input byte `i` is `i % 251`. The keyed vector key is the 32-byte ASCII string in that file.
- The first 32 bytes match a default-length hash, and `KeyedHash32` matches the XOR fold of those eight words.
- The worked two-chunk keyed example in the [C2SP BLAKE3 spec](https://c2sp.org/BLAKE3) (key `0xCC` repeated, 1024 bytes of `0xAA` then 1024 of `0xBB`, digest `34afab3d…`).

SipHash is about 100 lines. The scalar prototype is about 620 lines, because the permutation, the message schedule, chunking, the parent stack, and extended output are all part of a vector-clean implementation. A `Hash32`-only subset could drop `derive_key` and long XOF outputs, and would still be several times SipHash: inputs past 1 KiB need the tree. IL for `SipHash.Compute` is 313 bytes plus 123 for `Round`. IL for `Compress` is 752 bytes plus 249 for `G`.

A NativeAOT publish of a program that only calls the prototype succeeded on `linux-x64` and ran. The native image was about 16 KB larger than an empty NativeAOT exe. The hash path is spans, `stackalloc`, and inline arrays: no reflection, no static mutable configuration, and the 54-deep chaining-value stack (1728 bytes) is created only for inputs longer than one chunk. That is trim- and NativeAOT-friendly. SIMD intrinsics with `IsSupported` fallbacks would be too. The thing that is not friendly is the size and review cost of a faithful AVX2/AVX-512 port; those kernels are separate C translations upstream, not a few dozen intrinsics calls.

## Security

The property this code needs is a secret-keyed pseudorandom function, so an attacker who does not know the per-process key cannot build colliding inputs offline. SipHash-2-4 was designed for that, for short inputs ([Aumasson and Bernstein, 2012](https://eprint.iacr.org/2012/351)). Keyed BLAKE3 is also specified as a PRF, aimed at replacing HMAC, with its speed coming from parallelism on large messages ([BLAKE3 spec](https://github.com/BLAKE3-team/BLAKE3-specs/blob/master/blake3.pdf), [C2SP writeup](https://c2sp.org/BLAKE3)).

Both outputs are then folded to 32 bits for `GetHashCode`. Birthday collisions in a 32-bit value show up after on the order of 2^16 queries. BLAKE3's 256-bit output and 256-bit key do not move that bound. The comparer exposes `GetHashCode`, so the truncated output is not a secret MAC either; ShapeShift's own equality docs already say this is hash-flooding mitigation, not a MAC. A stronger compression function does not fix a 32-bit public hash code.

Other ecosystems, for this same problem:

- .NET strings use Marvin32 with a per-process seed. `Dictionary` and `HashSet` start non-randomized and switch after a collision chain passes `HashHelpers.HashCollisionThreshold` (100). [Marvin.cs](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Marvin.cs), [HashHelpers.cs](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Collections/HashHelpers.cs).
- Rust's `HashMap` uses SipHash-1-3, fewer rounds than SipHash-2-4, and the docs say faster hashes usually give up HashDoS resistance. [HashMap](https://doc.rust-lang.org/std/collections/hash_map/struct.HashMap.html).
- Python adopted SipHash in [PEP 456](https://peps.python.org/pep-0456/) and then moved to SipHash-1-3 for speed ([bpo-29410](https://bugs.python.org/issue29410)). Ruby did the same.
- Go's map hash is AES-NI when the CPU has it, otherwise a keyed non-cryptographic hash. [`hash/maphash`](https://pkg.go.dev/hash/maphash) says the functions are not cryptographically secure.
- Java's `HashMap` treeifies long bins instead of using a cryptographic hash.

Nobody in that set moved from SipHash to BLAKE3 for hash tables. The ones who changed SipHash reduced the round count.

## Why not "only for large inputs"

A split would still need a second implementation, and the scalar one is the wrong second implementation: it is an order of magnitude slower at 64 KiB. The upstream SIMD kernel is faster there (about 3× at 64 KiB on this VM) and still slower at every size the leaf encoders and short strings produce. Structural equality of a typical object is many small leaves, not one multi-kilobyte buffer. Paying AVX-512 complexity on the rare large `byte[]` does not pay back the regression on every `int`, `Guid`, and short string.
