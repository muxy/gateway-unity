# Compilation and package compatibility: first SDK update

Date: October 20, 2026. Branch: `codex/unity-compilation-package-compatibility`.

Base commit: `fc9fb13da721f85d03f0a2f507d7620a0ff5212f`. Results below concern the modified working-tree source, identified by the SHA-256 values in the result JSON files. Package version remains `0.0.1` until a release is prepared; installing the original tag does not install these fixes.

## Changes implemented

- Replaced all 13 target-typed constructors with explicit types for C# 8 compilation. Updated the three corresponding constructor examples in the README.
- Declared dependencies on TMP `3.0.6`, uGUI `1.0.0`, and the image-conversion module `1.0.0`. These are manifest requests; the Editor's built-ins and Package Manager's resolution policy determine the versions actually installed.
- Added the missing explicit `UnityEngine.UI` assembly reference alongside `Unity.TextMeshPro`.
- Restricted the `UnityEditor` import to `UNITY_EDITOR` rather than standalone-player compilation.
- Changed the package's minimum-release suffix from `0b5` to the intended stable `0f1`. Live-Editor validation of that exact earliest patch passed; its separate clean batch check remains blocked as recorded below.
- Fixed another clean-project compilation blocker: Unity 2020.3's .NET Standard 2.0 reference assemblies do not expose the named `UnmanagedType.LPUTF8Str` member. All 33 existing UTF-8 marshalling annotations now use one internal constant with the same ABI value, **48**. This preserves the original UTF-8 metadata; it does not substitute ANSI strings or require changing the consumer's API compatibility level. See [Microsoft's enum definition](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedtype).
- Preserved the `Muxy.Gateway` assembly, public type ownership, serialized fields, and existing asset GUIDs. No UI package split or public-type migration was needed for the package-resolution paths tested here.

The existing root assembly still includes prefab/UI code, so even code integrations resolve the UI dependencies. An optional UI-free core is a separate architectural change.

## Actual clean-project checks

The six batch runs below used separate Windows x64 fixture projects. Their project manifests requested **only `com.muxy.gateway` from the local working tree**; they did not preinstall TMP/uGUI or copy dependency assemblies into the projects. Unity used its default project compiler/API settings. The test helper also compiled a custom consumer assembly referencing `Muxy.Gateway`, checked that six SDK/manager/widget public types remained in that assembly, and exercised UTF-8 struct marshalling with accented text, Japanese characters, and an emoji. The original four fixtures were rerun after activation, and two additional installed versions were checked.

| Exact Editor | Import, SDK and custom-consumer compilation | Resolved TMP / provider | Resolved uGUI | UTF-8 struct check |
|---|---|---|---|---|
| **2020.3.48f1** | **Passed** | Standalone TMP `3.0.6`, registry | `1.0.0`, built-in | **Passed** |
| **2022.3.62f3** | **Passed** | Standalone TMP `3.0.7`, registry | `1.0.0`, built-in | **Passed** |
| **2023.2.20f1** | **Passed** | Built-in TMP shim `5.0.0`; TMP types supplied by uGUI | `2.0.0`, built-in | **Passed** |
| **6000.0.85f1** | **Passed** | Built-in TMP shim `5.0.0`; TMP types supplied by uGUI | `2.0.0`, built-in | **Passed** |
| **6000.4.8f1** | **Passed** | Built-in TMP shim `5.0.0`; TMP types supplied by uGUI | `2.0.0`, built-in | **Passed** |
| **6000.6.6f1** | **Passed** | Built-in TMP shim `5.0.0`; TMP types supplied by uGUI | `2.6.0`, built-in | **Passed** |

Separate batch launches of **2020.3.0f1 and 2020.3.17f1 still reported an activation failure**, despite the user activating and opening those versions successfully. Their actual live-Editor results are recorded next; a batch failure does not negate a live-Editor pass, and a live pass does not establish clean batch dependency resolution.

## Activated live-Editor checks

The user's existing Windows x64 LEGO test projects were used without closing their running Editors. Temporary local-package entries, Editor probes, and a separate `Gateway.PackageConsumer` assembly were added. Both projects already had TMP `3.0.6`; this is existing-project integration evidence, not a fresh-dependency-resolution claim.

| Exact Editor | SDK/package compilation | Custom consumer assembly | UTF-8 struct check | Native SDK construction |
|---|---|---|---|---|
| **2020.3.0f1** | **Passed** | **Passed**, six public types retain `Muxy.Gateway` ownership | **Passed** | **Passed** |
| **2020.3.17f1** | **Passed** | **Passed**, six public types retain `Muxy.Gateway` ownership | **Passed** | **Passed** |

The projects' unrelated template diagnostics, including LEGO/tutorial errors and shader diagnostics, are not a clean game-build result. The validation probes executing after compilation and their dedicated consumer assemblies establish the SDK results above.

## Fresh Twitch PIN and refresh authentication

On October 20, fresh configuration PINs were obtained by reloading the user's selected Twitch Gateway configuration tab. Each Editor received a different newly issued PIN. The same SDK source as fix commit **`e1c80fb31a0b1eefa9055bd5a79f89fb33aad137`** was used, with production game ID **`gamelink-testing`**, native-generated addresses, WSS, and normal certificate validation.

| Editor | PIN callback, CDT | Approximate time from PIN request to submission | PIN authentication | In-memory refresh | Test native instance released |
|---|---|---|---|---|---|
| **2020.3.0f1** | **16:52:17.997** | **18.47 seconds** | **Passed** | **Passed**, 16:52:18.076 | **Yes** |
| **2020.3.17f1** | **16:53:12.864** | **29.47 seconds** | **Passed** | **Passed**, 16:53:12.946 | **Yes** |

For all four callbacks: `HasError == false`, native `IsAuthenticated == true`, JWT present, refresh token present, and no protocol error was observed. Timing uses the configuration-page reload request as the approximate issuance boundary, not a server-provided issuance timestamp. Both submissions were well inside the documented five-minute PIN window. JWTs and refresh tokens stayed in memory; reports retain presence flags rather than credential values.

**Transport scope:** the validation probe uses its own short-lived `ClientWebSocket` and calls the actual SDK's `AuthenticateWithPIN`, `AuthenticateWithRefreshToken`, `ForeachPayload`, `ReceiveMessage`, and main-thread update path. It opens WSS immediately before submitting the PIN. This verifies native/managed SDK linking and refresh, not the unchanged shipping stage helpers, which still use plaintext WS. No production SDK source or native binary was changed in this rerun.

An earlier minimum-Editor probe opened its WSS connection before a fresh PIN was ready and failed before receiving an authentication callback, despite submitting the PIN approximately 37 seconds after its request. The retry with an immediate connection and a separate fresh PIN passed. The initial error was transport-related; its exact cause was not captured, and it is not evidence of PIN expiry or credential denial. The failed observation is retained separately.

The probe detached native debug forwarding before credential exchange. It used validation-only reflection for the current private SDK/native handle and idle transport cleanup because the SDK does not yet expose complete disposal. The reports confirm native destruction and validation-socket disposal, not a public disposal API, a leak-free soak, or server-side token revocation. The temporary probes and control files were removed after testing. The 2020.3.0f1 manifest was restored exactly; 2020.3.17f1 test entries were removed while preserving other template/project manifest updates. See the cleanup records.

The 2020.3.48f1 clean fixture first exposed `CS0117` at the 33 named UTF-8 enum references under its default .NET Standard 2.0 configuration. After the ABI-constant fix, the same fixture compiled and the Unicode round trip passed. This additional blocker was masked by the API profile used in the earlier supplied report project.

An initial validation-helper run on Unity 6.6 failed because `PackageInfo` was ambiguous between two Editor namespaces. The helper now explicitly aliases `UnityEditor.PackageManager.PackageInfo`; that helper error was not an SDK defect. The final checks above passed after this correction.

The shim resolution observed in four modern Editors establishes a working single-manifest approach for these configurations. It supersedes the initial plan's assumption that separate UI package variants might be necessary. It does not prove every intervening Editor patch.

## Minimum-Editor managed compiler checks

Unity **2020.3.0f1's bundled Roslyn compiler** also compiled all runtime and prefab C# sources with explicit `-langversion:8.0`, its own .NET Standard 2.0 reference/shim assemblies, and its own engine assemblies:

| Compilation symbols | Result |
|---|---|
| `UNITY_EDITOR,UNITY_STANDALONE,UNITY_STANDALONE_WIN` | **Passed, compiler exit 0** |
| `UNITY_STANDALONE,UNITY_STANDALONE_WIN`, without Editor assembly references | **Passed, compiler exit 0** |

These focused checks reused TMP/uGUI assemblies produced by the clean **2020.3.48f1** fixture. They establish minimum-compiler/source compatibility, including the standalone import guard. The separate live-Editor checks above now establish Editor execution in 2020.3.0f1; fresh batch dependency resolution and player execution remain unverified. The batch process's activation block remains open.

Both focused compilations retained the five existing SDK warnings: two nullable-reference annotation warnings, one unawaited shutdown Task warning, and two unused exception-variable warnings. The shutdown warning belongs to the forthcoming lifecycle work. This update makes no warning-free or complete SDK-runtime claim.

## Reproduce the clean import checks

Run from the SDK repository in PowerShell, with the target Editor installed and activated:

```powershell
.\Tools~\Test-PackageImport.ps1 -UnityVersion 2020.3.48f1
.\Tools~\Test-PackageImport.ps1 -UnityVersion 2022.3.62f3
.\Tools~\Test-PackageImport.ps1 -UnityVersion 2023.2.20f1
.\Tools~\Test-PackageImport.ps1 -UnityVersion 6000.0.85f1
.\Tools~\Test-PackageImport.ps1 -UnityVersion 6000.4.8f1
.\Tools~\Test-PackageImport.ps1 -UnityVersion 6000.6.6f1
```

Use `-EditorPath` for a non-default installation. Use `-OutputRoot` for another validation location outside the SDK package; the default is `.gateway-validation` beside the repository. The harness creates one marked, reusable fixture per version and refuses to overwrite an unrelated project. It launches only its own hidden batch Editor, applies a timeout, requires an explicit validation success marker, and writes a JSON report with dependency resolution and SDK source hashes. It does not instantiate the SDK or submit credentials.

Raw import logs, generated projects, and lockfiles remain in those fixture directories. The minimum-compiler response files and logs are in the sibling `managed-2020.3.0f1` validation directory. Result snapshots retained in this repository are:

- [Unity 2020.3.48f1](Results/2020.3.48f1.json)
- [Unity 2022.3.62f3](Results/2022.3.62f3.json)
- [Unity 2023.2.20f1](Results/2023.2.20f1.json)
- [Unity 6000.0.85f1](Results/6000.0.85f1.json)
- [Unity 6000.4.8f1](Results/6000.4.8f1.json)
- [Unity 6000.6.6f1](Results/6000.6.6f1.json)
- [Unity 2020.3.0f1 live import](Results/2020.3.0f1-live.json), [custom consumer](Results/2020.3.0f1-live-consumer.json), [PIN/refresh](Results/2020.3.0f1-authentication.json), [source identity](Results/2020.3.0f1-live-source-identity.json), [cleanup](Results/2020.3.0f1-cleanup.json)
- [Unity 2020.3.17f1 live import](Results/2020.3.17f1-live.json), [custom consumer](Results/2020.3.17f1-live-consumer.json), [PIN/refresh](Results/2020.3.17f1-authentication.json), [source identity](Results/2020.3.17f1-live-source-identity.json), [cleanup](Results/2020.3.17f1-cleanup.json)
- [Initial minimum-Editor transport failure](Results/2020.3.0f1-authentication-initial-failure.json)
- [Minimum-Editor batch activation result](Results/2020.3.0f1-batch-blocked.json), [2020.3.17f1 batch activation result](Results/2020.3.17f1-batch-blocked.json)

See [live-Editor validation setup](../Tools~/Live-Editor-Validation.md) for the fallback procedure and its limits.

## Remaining acceptance work

Resolve the separate batch licensing context for **2020.3.0f1 and 2020.3.17f1** before claiming their automated clean-project dependency checks pass. Their activated live-Editor SDK, custom-assembly, native construction, PIN, and refresh checks now pass.

Git-URL and release-archive installation, player builds, IL2CPP, gameplay/prefab behavior, sandbox authentication, other operating systems/architectures, and the full Unity patch matrix remain pending. The old-Editor PIN/refresh passes cover their actual native authentication callbacks; other native callbacks and player behavior still need separate checks.

Follow-up SDK work includes secure transport defaults, stage/async connection APIs, authentication configuration/storage, callback isolation, disposal, and widget defects.
