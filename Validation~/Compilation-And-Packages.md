# Unity compilation and package compatibility

Scope split: October 23, 2026. PR: [#15](https://github.com/muxy/gateway-unity/pull/15).
Base: `fc9fb13da721f85d03f0a2f507d7620a0ff5212f`.

Fresh post-split package/compiler and stripped-player results are recorded in
[the October 23 compatibility rerun](Compatibility-Rerun-20261023.md).

## Required fixes in this PR

- Expand the 13 target-typed constructors so Unity 2020.3's C# 8 compiler can parse the SDK and prefab scripts.
- Preserve the existing UTF-8 marshalling ABI by replacing 33 references to the unavailable `UnmanagedType.LPUTF8Str` enum name with a shared `(UnmanagedType)48` constant. The native calling convention and encoding are unchanged.
- Declare TextMeshPro 3.0.6, uGUI 1.0.0 and image-conversion 1.0.0 dependencies, with the explicit `UnityEngine.UI` assembly reference. Newer Editors resolve their built-in compatible UI providers.
- Guard the `UnityEditor` import with `UNITY_EDITOR` so standalone players compile.
- Declare the stable 2020.3.0f1 minimum instead of the beta suffix.
- Preserve reflection-loaded Mono TLS configuration classes through an Editor-only `IUnityLinkerProcessor`. Package-local link.xml files are not consumed consistently across the supported versions; the callback supplies its generated descriptor to the build.
- Update the README's compiler syntax, package instructions and existing method-name example.

The runtime SDK and prefab scripts are restored to the original compatibility
revision `e1c80fb31a0b1eefa9055bd5a79f89fb33aad137`. The linker callback is retained
from the later stripping fix. The package import harness checks the Editor linker
callback's registration as well as consumer compilation and UTF-8 marshalling.

Transport and lifecycle changes are separated for their own review. Their code,
connection/PIN/reconnect results, lifecycle tests and broader reports are preserved
on `codex/gateway-transport-lifecycle`. They are not compatibility-only acceptance
results.

## Historical compatibility evidence

The original October 20 clean-package results below are retained unchanged from
`e1c80fb`. Each report records its source hashes and dependency providers. These
checks used independent Windows x64 fixture projects requesting only the local
Gateway package, a separate consumer assembly, six public type-ownership checks,
and accented/Japanese/emoji UTF-8 struct round trips. They did not construct an SDK
instance or authenticate to a server.

| Exact Editor | Package/consumer compilation | UTF-8 struct check | Evidence |
|---|---|---|---|
| 2020.3.48f1 | Passed | Passed | [Result](Results/2020.3.48f1.json) |
| 2022.3.62f3 | Passed | Passed | [Result](Results/2022.3.62f3.json) |
| 2023.2.20f1 | Passed | Passed | [Result](Results/2023.2.20f1.json) |
| 6000.6.6f1 | Passed | Passed | [Result](Results/6000.6.6f1.json) |

Earlier focused compilation also passed with the Unity 2020.3.0f1 bundled Roslyn
compiler, C# 8, its .NET Standard 2.0 references, and Editor/standalone symbol sets.
That compilation reused UI assemblies from the 2020.3.48f1 fixture. It was not a
minimum-version clean install or runtime test.

The stripping defect and its preservation fix were established during later player
work using the changed transport. That evidence stays with the transport/lifecycle
review; it is not evidence of a fresh player run of this narrowed branch.

## Reproduction

From the repository root, with the requested Editor installed and activated:

```powershell
.\Tools~\Test-PackageImport.ps1 -UnityVersion 2020.3.48f1
.\Tools~\Test-PackageImport.ps1 -UnityVersion 2022.3.62f3
.\Tools~\Test-PackageImport.ps1 -UnityVersion 2023.2.20f1
.\Tools~\Test-PackageImport.ps1 -UnityVersion 6000.6.6f1
```

The runner creates marked disposable fixtures outside the package, requests only
Gateway before dependency resolution, compiles a custom consumer, and launches a
hidden child Editor. A deadline and explicit passed result prevent stale success
reports. It does not construct an SDK instance or submit credentials.

## Scope of the split

No Unity tests or builds were rerun solely to split these PRs. Git comparisons
establish that the runtime/prefab compatibility source matches `e1c80fb`, the
Editor preservation files match the existing implementation, and the transport
branch retains the previously tested behavior changes.

The historical reports above predate the exact newly combined
compatibility-plus-linker snapshot. The subsequent requested rerun establishes
fresh package/compiler and stripped Mono/IL2CPP build/smoke results in its
[separate report](Compatibility-Rerun-20261023.md), including the two remaining
batch-license blocks. Live authentication, gameplay/network lifecycle behavior,
every patch/platform and a full current matrix remain outside this PR's scope.
