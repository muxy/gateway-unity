# Compatibility-only PR #15: post-split validation

Date: October 23, 2026 (America/Chicago; result timestamps are UTC).
Tested shipping source: `e2e85488d436f3ec29355c825bae71d087636532`.
No shipping SDK code changed during this run. Added files are validation tooling
and evidence. See [the source/binary index](Results/Compatibility-Rerun-20261023/index.json).

## Fresh clean-package checks

Each fixture requested only Gateway before package resolution. Checks include
SDK/custom-consumer compilation, six public types retaining `Muxy.Gateway`
ownership, UTF-8 struct round trips, and Editor linker callback registration.

| Unity | Result | Evidence |
|---|---|---|
| 2020.3.0f1 | Passed after Hub license reactivation | [Report](Results/License-Repair-20261023/2020.3.0f1-package.json) |
| 2020.3.17f1 | Passed after Hub license reactivation | [Report](Results/License-Repair-20261023/2020.3.17f1-package.json) |
| 2020.3.48f1 | Passed | [Report](Results/Compatibility-Rerun-20261023/2020.3.48f1-package.json) |
| 2022.3.62f3 | Passed | [Report](Results/Compatibility-Rerun-20261023/2022.3.62f3-package.json) |
| 2023.2.20f1 | Passed | [Report](Results/Compatibility-Rerun-20261023/2023.2.20f1-package.json) |
| 6000.0.85f1 | Passed | [Report](Results/Compatibility-Rerun-20261023/6000.0.85f1-package.json) |
| 6000.4.8f1 | Passed | [Report](Results/Compatibility-Rerun-20261023/6000.4.8f1-package.json) |
| 6000.6.6f1 | Passed | [Report](Results/Compatibility-Rerun-20261023/6000.6.6f1-package.json) |

Both earliest Editors initially exited with Unity license activation failures
before package import. A subsequent Hub Personal reactivation created the missing
legacy ULF license file, and fresh imports passed on both releases without SDK
changes. See the [license repair and rerun](License-Repair-20261023.md). The original
blocked reports are retained as historical evidence.

The 2020.3.0f1 bundled compiler separately passed the current SDK/runtime and prefab
source with C# 8 and .NET Standard 2.0 references under Editor and standalone
symbols. It reused the established UI assembly references. Five existing warnings
remain: two nullable annotations, one unawaited shutdown task, and two unused
exception variables. The subsequent early-Editor package runs now establish
clean-package compilation in those actual Editors as well.

## High-stripping Windows x64 players

All four nondevelopment builds used the compatibility-only SDK, its actual package
linker callback, and a dedicated smoke probe without any transport/lifecycle PR API.

| Unity | Backend | Build | Runtime smoke |
|---|---|---|---|
| 2020.3.48f1 | Mono | [Passed](Results/Compatibility-Rerun-20261023/2020.3.48f1-Mono-build.json) | [Passed](Results/Compatibility-Rerun-20261023/2020.3.48f1-Mono-runtime.json) |
| 2020.3.48f1 | IL2CPP | [Passed](Results/Compatibility-Rerun-20261023/2020.3.48f1-IL2CPP-build.json) | [Passed](Results/Compatibility-Rerun-20261023/2020.3.48f1-IL2CPP-runtime.json) |
| 6000.6.6f1 | Mono | [Passed](Results/Compatibility-Rerun-20261023/6000.6.6f1-Mono-build.json) | [Passed](Results/Compatibility-Rerun-20261023/6000.6.6f1-Mono-runtime.json) |
| 6000.6.6f1 | IL2CPP | [Passed](Results/Compatibility-Rerun-20261023/6000.6.6f1-IL2CPP-build.json) | [Passed](Results/Compatibility-Rerun-20261023/6000.6.6f1-IL2CPP-runtime.json) |

Smoke checks establish public type ownership, actual UTF-8 struct bytes, bundled
native library construction, Unicode metadata passing through the C ABI and back
through its payload callback, and `ServicePointManager` configuration initialization
in the stripped player. The native instance is explicitly created/destroyed through
the public C ABI. The original managed SDK transport is not instantiated.

The generated linker descriptors are retained for
[2020.3.48f1](Results/Compatibility-Rerun-20261023/2020.3.48f1-link.xml) and
[6000.6.6f1](Results/Compatibility-Rerun-20261023/6000.6.6f1-link.xml). Source,
probe and executable SHA-256 identities are recorded alongside the results.

## Reproduction

```powershell
.\Tools~\Test-PackageImport.ps1 -UnityVersion 2020.3.48f1
.\Tools~\Test-PackageImport.ps1 -UnityVersion 6000.6.6f1
.\Tools~\Test-CompatibilityPlayers.ps1 -UnityVersion 2020.3.48f1
.\Tools~\Test-CompatibilityPlayers.ps1 -UnityVersion 6000.6.6f1
```

The compatibility runner was exercised for both versions and both backends. It
creates marked fixtures outside the package, builds at High stripping, launches
owned hidden players, checks explicit result files, and verifies source hashes
were unchanged. Its generic build helper is shared with the transport tooling;
its runtime probe contains no dependency on the new disposal/status/async APIs.

## Limits

This run validates the exact compatibility-plus-linker source combination for the
listed successful configurations. It does not establish live authentication,
the original managed SDK's network/lifecycle behavior, actual TLS certificate
negotiation, every Unity patch/backend combination, other platforms, or runtime
passes on 2020.3.0f1 and 2020.3.17f1. Fresh PIN/reconnect checks belong to PR #16.
