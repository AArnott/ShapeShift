# Copilot instructions for this repository

## High level guidance

- Review the `CONTRIBUTING.md` file for instructions to build and test the software.
- Run the `.github/Prime-ForCopilot.ps1` script (once) before running any `dotnet` or `msbuild` commands.
  If you see any build errors about not finding git objects or a shallow clone, it may be time to run this script again.

## Software Design

- Design APIs to be highly testable, and all functionality should be tested.
- Avoid introducing binary breaking changes in public APIs of projects under `src` unless their project files have `IsPackable` set to `false`.
- Keep the `ShapeShift` core format-neutral. Format packages should be thin adapters over the shared converter and PolyType infrastructure.
- Make it straightforward for third parties to add format packages, following `ShapeShift.Taml` as the smallest reference implementation. Public extension points require documentation and conformance tests.
- Every new format package must add its own `test/ShapeShift.<Format>.Tests` project and format documentation under `docfx/docs/`, with an entry in `docfx/docs/toc.yml`. Add both projects to `ShapeShift.slnx`.
- NativeAOT coverage discovers runtime projects matching `src/ShapeShift*/ShapeShift*.csproj` and test projects matching `test/ShapeShift*.Tests/ShapeShift*.Tests.csproj`. New runtime libraries and format test projects must follow these conventions; compiler-hosted analyzer projects remain explicitly excluded. Non-`ShapeShift.*` sample test projects are intentionally listed explicitly in `test/dirs.proj`.
- All shipping libraries and default code paths must be trimming-safe and NativeAOT-ready.
- Any useful feature that cannot be NativeAOT-safe must be disabled by default and activated only by an explicit method call. Not calling that method must leave the application NativeAOT-safe.
- Do not use `InternalsVisibleTo`. Test through public APIs or move reusable test support into an appropriate public test-support package.
- Prefer immutable, instance-scoped configuration. Do not introduce mutable global serializer defaults.
- Optimize public APIs for clarity, performance, and long-term compatibility. Keep format-specific wire choices out of shared abstractions unless every format can honor them.
- New features require thorough tests, docfx documentation, and samples where a runnable example improves understanding.

## Testing

**IMPORTANT**: This repository uses TUnit with Microsoft.Testing.Platform (MTP v2). Traditional `--filter` syntax does NOT work. Use the options below instead.

- There should generally be one test project (under the `test` directory) per shipping project (under the `src` directory). Test projects are named after the project being tested with a `.Tests` suffix.
- Tests use TUnit with Microsoft.Testing.Platform (MTP v2). Traditional VSTest `--filter` syntax does NOT work.
- Some tests are known to be unstable. When running tests, you should skip the unstable ones by using `-- --filter-not-trait "FailsInCloudTest=true"`.

### Running Tests

**Run all tests**:

```bash
dotnet test --no-build -c Release
```

**Run tests for a specific test project**:

```bash
dotnet test --project test/Library.Tests/Library.Tests.csproj --no-build -c Release
```

**Run a single test method**:

```bash
dotnet test --project test/Library.Tests/Library.Tests.csproj --no-build -c Release -- --filter-method ClassName.MethodName
```

**Run all tests in a test class**:

```bash
dotnet test --project test/Library.Tests/Library.Tests.csproj --no-build -c Release -- --filter-class ClassName
```

**Run tests with wildcard matching** (supports wildcards at beginning and/or end):

```bash
dotnet test --project test/Library.Tests/Library.Tests.csproj --no-build -c Release -- --filter-method "*Pattern*"
```

**Run tests with a specific trait** (equivalent to category filtering):

```bash
dotnet test --project test/Library.Tests/Library.Tests.csproj --no-build -c Release -- --filter-trait "TraitName=value"
```

**Exclude tests with a specific trait** (skip unstable tests):

```bash
dotnet test --project test/Library.Tests/Library.Tests.csproj --no-build -c Release -- --filter-not-trait "TestCategory=FailsInCloudTest"
```

**Run tests for a specific framework only**:

```bash
dotnet test --project test/Library.Tests/Library.Tests.csproj --no-build -c Release --framework net9.0
```

**List all available tests without running them**:

```bash
cd test/Library.Tests
dotnet run --no-build -c Release --framework net9.0 -- --list-tests
```

**Key points about test filtering with MTP v2 / TUnit**:

- Options after `--` are passed to the test runner, not to `dotnet test`
- Use `--filter-method`, `--filter-class`, `--filter-namespace` for simple filtering
- Use `--filter-trait` and `--filter-not-trait` for trait-based filtering (replaces `--filter "TestCategory=..."`)
- Traditional VSTest `--filter` expressions do NOT work
- Wildcards `*` are supported at the beginning and/or end of filter values
- Multiple simple filters of the same type use OR logic, different types combine with AND
- See `--help` for query filter language for advanced scenarios

## Build documentation (takes ~19 seconds)

```bash
DocFx=true dotnet docfx docfx/docfx.json --warningsAsErrors --disableGitFeatures
```

## Coding style

- Honor StyleCop rules and fix any reported build warnings _after_ getting tests to pass.
- In C# files, use namespace _statements_ instead of namespace _blocks_ for all new files.
- Add API doc comments to all new public and internal members.
