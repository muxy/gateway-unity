# Compilation and package compatibility: first SDK update

Date: October 20, 2026. Branch: `codex/unity-compilation-package-compatibility`.

Base commit: `fc9fb13da721f85d03f0a2f507d7620a0ff5212f`. Results below concern the modified working-tree source, identified by the SHA-256 values in the result JSON files. Package version remains `0.0.1` until a release is prepared; installing the original tag does not install these fixes.

## Changes implemented

- Replaced all 13 target-typed constructors with explicit types for C# 8 compilation. Updated the three corresponding constructor examples in the README.
- Declared dependencies on TMP `3.0.6`, uGUI `1.0.0`, and the image-conversion module `1.0.0`. These are manifest requests; the Editor's built-ins and Package Manager's resolution policy determine the versions actually installed.
- Added the missing explicit `UnityEngine.UI` assembly reference alongside `Unity.TextMeshPro`.
- Restricted the `UnityEditor` import to `UNITY_EDITOR` rather than standalone-player compilation.
- Changed the package's minimum-release suffix from `0b5` to the intended stable `0f1`. Complete import validation of that exact earliest patch remains blocked as recorded below.
- Fixed another clean-project compilation blocker: Unity 2020.3's .NET Standard 2.0 reference assemblies do not expose the named `UnmanagedType.LPUTF8Str` member. All 33 existing UTF-8 marshalling annotations now use one internal constant with the same ABI value, **48**. This preserves the original UTF-8 metadata; it does not substitute ANSI strings or require changing the consumer's API compatibility level. See [Microsoft's enum definition](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedtype).
- Preserved the `Muxy.Gateway` assembly, public type ownership, serialized fields, and existing asset GUIDs. No UI package split or public-type migration was needed for the package-resolution paths tested here.

The existing root assembly still includes prefab/UI code, so even code integrations resolve the UI dependencies. An optional UI-free core is a separate architectural change.

## Actual clean-project checks

All runs used separate Windows x64 projects. Their project manifests requested **only `com.muxy.gateway` from the local working tree**; they did not preinstall TMP/uGUI or copy dependency assemblies into the projects. Unity used its default project compiler/API settings. The test helper also compiled a custom consumer assembly referencing `Muxy.Gateway`, checked that six SDK/manager/widget public types remained in that assembly, and exercised UTF-8 struct marshalling with accented text, Japanese characters, and an emoji.

| Exact Editor | Import, SDK and custom-consumer compilation | Resolved TMP / provider | Resolved uGUI | UTF-8 struct check |
|---|---|---|---|---|
| **2020.3.48f1** | **Passed** | Standalone TMP `3.0.6`, registry | `1.0.0`, built-in | **Passed** |
| **2022.3.62f3** | **Passed** | Standalone TMP `3.0.7`, registry | `1.0.0`, built-in | **Passed** |
| **2023.2.20f1** | **Passed** | Built-in TMP shim `5.0.0`; TMP types supplied by uGUI | `2.0.0`, built-in | **Passed** |
| **6000.6.6f1** | **Passed** | Built-in TMP shim `5.0.0`; TMP types supplied by uGUI | `2.6.0`, built-in | **Passed** |
| **2020.3.0f1** | **Environment blocked before import:** license activation failed | Not established by this run | Not established by this run | Not run |
| **2020.3.17f1** | **Environment blocked before import:** license activation failed | Not established by this run | Not established by this run | Not run |

The 2020.3.48f1 clean fixture first exposed `CS0117` at the 33 named UTF-8 enum references under its default .NET Standard 2.0 configuration. After the ABI-constant fix, the same fixture compiled and the Unicode round trip passed. This additional blocker was masked by the API profile used in the earlier supplied report project.

An initial validation-helper run on Unity 6.6 failed because `PackageInfo` was ambiguous between two Editor namespaces. The helper now explicitly aliases `UnityEditor.PackageManager.PackageInfo`; that helper error was not an SDK defect. The final checks above passed after this correction.

The shim resolution observed in the two modern Editors establishes a working single-manifest approach for these configurations. It supersedes the initial plan's assumption that separate UI package variants might be necessary. It does not prove every intervening Editor patch.

## Minimum-Editor managed compiler checks

Unity **2020.3.0f1's bundled Roslyn compiler** also compiled all runtime and prefab C# sources with explicit `-langversion:8.0`, its own .NET Standard 2.0 reference/shim assemblies, and its own engine assemblies:

| Compilation symbols | Result |
|---|---|
| `UNITY_EDITOR,UNITY_STANDALONE,UNITY_STANDALONE_WIN` | **Passed, compiler exit 0** |
| `UNITY_STANDALONE,UNITY_STANDALONE_WIN`, without Editor assembly references | **Passed, compiler exit 0** |

These focused checks reused TMP/uGUI assemblies produced by the clean **2020.3.48f1** fixture. They establish minimum-compiler/source compatibility, including the standalone import guard; they do **not** establish fresh dependency resolution or actual Editor/player execution in 2020.3.0f1. The Editor's activation block remains open.

Both focused compilations retained the five existing SDK warnings: two nullable-reference annotation warnings, one unawaited shutdown Task warning, and two unused exception-variable warnings. The shutdown warning belongs to the forthcoming lifecycle work. This update makes no warning-free or complete SDK-runtime claim.

## Reproduce the clean import checks

Run from the SDK repository in PowerShell, with the target Editor installed and activated:

```powershell
.\Tools~\Test-PackageImport.ps1 -UnityVersion 2020.3.48f1
.\Tools~\Test-PackageImport.ps1 -UnityVersion 2022.3.62f3
.\Tools~\Test-PackageImport.ps1 -UnityVersion 2023.2.20f1
.\Tools~\Test-PackageImport.ps1 -UnityVersion 6000.6.6f1
```

Use `-EditorPath` for a non-default installation. Use `-OutputRoot` for another validation location outside the SDK package; the default is `.gateway-validation` beside the repository. The harness creates one marked, reusable fixture per version and refuses to overwrite an unrelated project. It launches only its own hidden batch Editor, applies a timeout, requires an explicit validation success marker, and writes a JSON report with dependency resolution and SDK source hashes. It does not instantiate the SDK or submit credentials.

Raw import logs, generated projects, and lockfiles remain in those fixture directories. The minimum-compiler response files and logs are in the sibling `managed-2020.3.0f1` validation directory. Result snapshots retained in this repository are:

- [Unity 2020.3.48f1](Results/2020.3.48f1.json)
- [Unity 2022.3.62f3](Results/2022.3.62f3.json)
- [Unity 2023.2.20f1](Results/2023.2.20f1.json)
- [Unity 6000.6.6f1](Results/6000.6.6f1.json)

## Remaining acceptance work

Activate/open **2020.3.0f1 and 2020.3.17f1** through Unity Hub and rerun their clean-import checks. Preserve the current blocked results until import actually runs successfully. The focused compiler passes do not override those blocks.

This first batch does not establish Git-URL or release-archive installation, player builds, IL2CPP/reverse P/Invoke, native SDK construction, live authentication, gameplay/prefab behavior, other operating systems/architectures, or the full Unity patch matrix. Native UTF-8 parameter/callback behavior in those configurations still requires the native/player checks in the broader plan.

Follow-up SDK work includes secure transport defaults, stage/async connection APIs, authentication configuration/storage, callback isolation, disposal, and widget defects.
