# CLAUDE.md

This file gives Claude Code guidance for working in this repository. When a prompt reveals that something here is inaccurate or missing, update this file as part of your work so future sessions start with correct context.

## What this is

`InkSoft.SmbAbstraction` is a NuGet library that implements the [System.IO.Abstractions](https://github.com/TestableIO/System.IO.Abstractions) `IFileSystem` interfaces on top of [SMBLibrary](https://github.com/TalAloni/SMBLibrary). Consumers can then use `\\host\share\...` and `smb://host/share/...` paths from Linux, macOS or non-domain Windows clients, without OS-level authentication. It's a fork of `jordanlytle/SmbAbstraction`, with many breaking changes since 2.0.0 (see `CHANGELOG.md`).

## Build and test

Requires the .NET 10 SDK (the library also targets net9.0, net8.0, netstandard2.1 and netstandard2.0).

```bash
dotnet build SmbAbstraction.sln
```

```bash
dotnet test --project InkSoft.SmbAbstraction.Tests
```

- The test projects use **xunit v3 on Microsoft.Testing.Platform** (`UseMicrosoftTestingPlatformRunner` in `Directory.Build.props`). `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }` to opt into the .NET 10 SDK's new `dotnet test` experience. Without that setting, `dotnet test` fails with "Testing with VSTest target is no longer supported". Don't run `dotnet test` on the whole solution, since that would also run the integration tests. Filters use xunit v3 syntax, not VSTest `--filter`, and need no `--` separator:
  `dotnet test --project InkSoft.SmbAbstraction.Tests --filter-class "*PathExtensionsTests"` (also `--filter-method`, `--filter-trait "Category=Integration"`).
- The unit tests already produce many nullable warnings (CS86xx). Those warnings are expected; new code shouldn't add more.
- `GeneratePackageOnBuild` is on, so every build of the library also writes a `.nupkg`/`.snupkg`.
- **Integration tests** (`InkSoft.SmbAbstraction.IntegrationTests`) need a real SMB share. On first build, `appsettings.gitIgnoreTemplate.json` is copied to `appsettings.gitignore.json`, which git ignores. Fill in the credentials and host/share there, and create `Dir/` and `file.txt` on the share; the template's comments have the details. These tests leave junk files on the share. Don't run them unless the user has set this up, and never commit `appsettings.gitignore.json`.
- The integration `Dockerfile` is stale: it refers to old project names and .NET Core 3.0.

## Architecture

All library code lives in `InkSoft.SmbAbstraction/`, in a single file-scoped namespace `InkSoft.SmbAbstraction`.

- **`SmbFileSystem`** (`FileSystemBase`) is the entry point. Its constructor is `(ISmbClientFactory, ISmbCredentialProvider, SmbFileSystemOptions?, ILoggerFactory?)`. It builds every sub-factory (`File`, `Directory`, `Path`, `FileInfo`, `DirectoryInfo`, `DriveInfo`, `FileStream`, ...) with a back-reference to itself.
- **Dual-path dispatch is the core pattern.** Each Smb* wrapper derives from the matching System.IO.Abstractions `*Wrapper` class, built over `smbFileSystem.NonSmbFileSystem` (defaults to the real `FileSystem`). Every override starts with `path.IsSharePath()`: a share path goes through SMBLibrary, anything else goes to `base.*`. Keep this pattern in new overrides so local paths behave exactly like `System.IO`. Each wrapper also shadows `FileSystem` with `public new IFileSystem FileSystem => smbFileSystem;`.
- **Path helpers** live in `PathExtensions.cs`:
  - The public `PathExtensions` class has `IsSharePath`, `IsUncPath`, `IsSmbUri` and `IsValidSharePath`.
  - The internal `PathExtensionsInternal` class has `Hostname`, `ShareName`, `SharePath`, `ShareRelativePath`, `TryResolveHostnameFromPath` (DNS, IPv4 only) and separator normalization.
  - Parsing uses `System.Uri`. UNC paths use `\` and smb URIs use `/`; `SmbPath.Combine` normalizes separators to match the root path's style.
- **Credentials**: `SmbCredential.AddToProvider(domain, user, pass, path, provider, removeFromProviderWhenDisposed = true)` registers a credential and returns an `IDisposable`. `SmbCredentialProvider.GetSmbCredential(path)` first looks for a host+share match, then falls back to a host-only match. Domain, username and password must all be non-null, or `SmbConnection.Connect` throws `InvalidCredentialException`.
- **Connections** (`SmbConnection`):
  - `CreateSmbConnection` caches reference-counted connections in a static dictionary keyed by **managed thread id + IP address**. It's used for metadata and directory operations.
  - `CreateSmbConnectionForStream` creates an uncached connection that the stream owns (`SmbFile.OpenSmb` → `SmbFsStream`/`SmbStream`).
  - The class comment admits this design has stability problems. Watch for async continuations that switch threads, and for the lock/dispose path (`Dispose` waits up to 15 s for the lock).
- `SmbFile.OpenSmb(...)` is the central stream opener. It maps `FileMode`/`FileAccess`/`FileOptions` onto SMB `CreateDisposition`/`AccessMask`/`CreateOptions`. `SmbFileStreamFactory.New(path, ...)` routes share paths there.
- `SmbStream` splits reads and writes into requests no larger than `SmbFileSystemOptions.MaxBufferSize` or the negotiated `MaxReadSize`/`MaxWriteSize`. SMBLibrary charges one SMB2 credit per 64 KiB of payload, never holds more than 16 credits, and throws "Not enough credits" for a request that costs more than it has. So don't send a large buffer to `ISMBFileStore.ReadFile`/`WriteFile` in one call.
- `SmbFile` is a partial class: async methods are in `SmbFile.Async.cs`.
- `NTStatusExtensions.AssertSuccess()` turns SMBLibrary `NTStatus` failures into exceptions. Operations usually wrap failures in `SmbException`.
- `SmbFileSystemExtensions.Copy(this IDirectory, ...)` does a recursive directory copy for any `IFileSystem`.
- `RandomAccess` and `FileSystemWatcher` only delegate to the non-SMB file system. They don't support SMB paths. `IRandomAccess` works only on OS `SafeFileHandle`s, which SMBLibrary can't produce, so `SmbRandomAccess` (`#if NET`) forwards each call to `NonSmbFileSystem.RandomAccess`. It resolves that per call because `NonSmbFileSystem` is settable.

## Multi-targeting conventions

- `Directory.Build.props` defines `FEATURE_*` constants for each TFM, mirroring the System.IO.Abstractions conventions (`FEATURE_ASYNC_FILE`, `FEATURE_SPAN`, `FEATURE_FILESTREAM_OPTIONS`, ...). Guard APIs that only exist in newer frameworks with these constants, or with `#if NET`.
- netstandard2.0 gets polyfills from PolySharp and `NetStandard20Extensions.cs`. Before net9.0, `Lock` comes from `Backport.System.Threading.Lock` through a global `using` alias, so write `Lock` / `lock (x)` / `TryEnter` normally.
- `Microsoft.Extensions.Logging.Abstractions` is pinned to a different version for each TFM in the csproj `<Choose>` block. Update all three branches together.
- Assemblies are strong-name signed with `StrongName.snk`. `InternalsVisibleTo` in `AssemblyInfo.cs` includes the public key, so test assemblies must stay signed.

## Code style

`.editorconfig` is authoritative. In short:
- File-scoped namespaces, Allman braces, 4-space indentation, CRLF line endings, `using`s outside the namespace.
- `var` when the type is apparent, but explicit built-in types (`string`, `int`).
- Primary constructors, expression-bodied single-line members, collection expressions (`[]`) and target-typed `new()`.
- Naming: `_camelCase` private fields, `s_camelCase` private static fields, `c_camelCase` constants.
- Nullable reference types are enabled. Use `[NotNullWhen]` and similar attributes on path predicates.

## Releasing

When releasing, bump `<Version>` and `<PackageReleaseNotes>` in `InkSoft.SmbAbstraction/InkSoft.SmbAbstraction.csproj`, and add an entry to `CHANGELOG.md` (Keep a Changelog format). `version.json` (Nerdbank.GitVersioning) exists, but no project references the package. The csproj `<Version>` is what actually takes effect.
