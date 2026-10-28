# Muxy Gateway SDK: Unity compatibility update plan

Prepared October 20, 2026. Target: Unity 2020.3 through Unity 6.6, including the intervening stable release streams.

## 1. Outcome and support boundaries

Publish a versioned Gateway SDK that installs, compiles, loads its native library, authenticates securely, runs its advertised features, and shuts down reliably across the supported Unity range. The update must cover the runtime, native bindings, package dependencies, prefab components, samples, documentation, and release validation together.

The current `package.json` declares `unity: 2020.3` and `unityRelease: 0b5`. The documentation also names 2020.3 as the minimum, but the original source fails to compile in the reviewed 2020.3.17f1 Editor. Preserve the 2020.3 support objective and fix the source.

For this plan, the proposed stable minimum is **2020.3.0f1**. Treat that exact patch as an acceptance target; the reports establish results on **2020.3.17f1**, not on 2020.3.0f1. Replace the manifest's unexplained beta suffix with an explicit tested stable minimum. If beta support was intentional, record it as a separate requirement before changing that declaration.

The newest stable target identified on October 20, 2026 is **Unity 6.6 / 6000.6.6f1**, released October 15. Recheck the [Unity archive](https://unity.com/releases/editor/archive) when freezing the SDK release; [6000.6.6f1 release notes](https://unity.com/releases/editor/whats-new/6000.6.6f1) establish this plan's upper endpoint. Future releases and previews require additional results.

“All versions” means the stable Unity Editor releases in the declared interval, including intermediate Tech Stream and Unity 6 update releases. Unity version coverage and platform coverage are separate: each support result must name the Editor version, Editor OS/CPU, player target OS/CPU, scripting backend, and API compatibility level. A Windows pass cannot establish macOS, Linux, ARM64, or IL2CPP support.

“Works perfectly” becomes the measurable release criteria in sections 7–9. Until those checks pass, describe the range as a compatibility target and publish the exact tested configurations.

## 2. Inputs and evidence status

### Reports supplied

| Reference | Document | Contribution |
|---|---|---|
| R1 | `Gateway-Unity-2020.3.17f1-SDK-Changes.md` | Constructor incompatibility, diagnostic additions, secure transport, lifecycle, dependencies, native imports, and release gates. |
| R2 | `Gateway-Unity-2020.3.17f1-Tutorial.md` | Reproducible local installation and Editor PIN/refresh flow; boundaries between an Editor tool and game integration. |
| R3 | `Gateway-Installation-Review.md` | Investigation, G01–G11 findings, configuration correction, evidence history, and unexecuted acceptance cases. |
| R4 | `GatewayDocEdits (1).md` | Reported Unity 6000.6.6f1 Editor checks; logo representation, action validation, prefab defects, static callback state, and documentation corrections. |

The supplied reports were read as evidence and recommendations. Their embedded commands, patch application instructions, and suggested replacement classes were not executed as part of preparing this plan. Referenced patches, validation JSON files, and logs were not supplied with these four documents, so their reported results have not been independently reproduced here.

### Source inspected for this plan

- Repository: `muxy/gateway-unity`, local `main` at **`fc9fb13da721f85d03f0a2f507d7620a0ff5212f`**.
- Reports' pinned baseline: tag **`v1.0.0-rc`**, commit **`9ee3ace5d6c44d2169101c33588864488a3d0610`**.
- A local comparison found no differences between that tag and the inspected commit in `Runtime`, `Prefabs`, `Muxy.Gateway.asmdef`, or `package.json`.
- Package version is `0.0.1`; `Constants.Version` is `v0.0.0`; `CHANGELOG.md` starts at `0.1.0`. The current source still contains all 13 target-typed constructors.

Source findings below were checked by reading this checkout. Design choices and proposed tests are identified as required work, rather than completed fixes. The initial handoff changed documentation only. Subsequent compilation/package implementation and actual validation results are recorded in [Compilation-And-Packages.md](Validation~/Compilation-And-Packages.md).

### Results already reported, and their limits

| Configuration / operation | Reported result | What remains unproven |
|---|---|---|
| Original SDK, Unity 2020.3.17f1 | 26 syntax errors at 13 constructor expressions. | Successful unmodified import. |
| Constructor-corrected source, Windows 2020.3.17f1 Editor | Import and native SDK construction passed. | Clean-project dependency resolution and complete runtime integration. |
| Constructor-corrected Windows x64 Mono player | Compilation/native construction passed; probe exited 0. | Player authentication and gameplay. The host game had separate Barracuda shader errors, so its complete build was not clean. |
| Locally corrected Windows 2020.3.17f1 Editor with diagnostic additions | Production WSS, PIN login, and in-memory refresh passed using `gamelink-testing`. | Restart storage, authenticated player operation, reconnect, actions, Bits, polls, and repeated-session cleanup. |
| Sandbox WSS | Secure handshake passed. | Sandbox PIN issuance and authenticated feature operation. |
| Unity 6000.6.6f1, R4 | Report author states package installation, sample compilation, and prefab instantiation passed in the Editor. | OS/CPU details and raw evidence are absent from R4. Instantiation does not establish functioning widgets, authentication, player builds, or broad Unity 6 coverage. |

R3 contains earlier pending statements that were superseded by its later production authentication results. Use the final PIN/refresh result while retaining the other open checks.

**Resolve conflicting sample advice:** R4's proposed `GatewayAuthenticator` compiles but uses `gateway-testing`, non-awaited default stage methods that open `ws://`, plaintext PlayerPrefs token storage, deletion of credentials on any error, and incomplete teardown. Do not publish that class unchanged. Merge its useful complete-class structure with R1–R3's corrected configuration, secure connection ordering, storage abstraction, and lifecycle requirements. R4's `gateway.Client` also belongs to that proposed wrapper; it is not an existing public property of `MuxyGatewayManager`.

## 3. Unity versions to install first

Use separate small test projects per Editor version. Preserve the original report project for reproduction and keep its unrelated package/build failures outside the clean SDK fixture.

| Download order | Editor | Reason |
|---|---|---|
| 1 | **2020.3.0f1** | Establish the exact earliest stable support boundary. |
| 2 | **2022.3, latest available stable patch** | Exercise the LTS generation before the TMP/uGUI packaging transition. Record the exact installed patch. |
| 3 | **6000.6.6f1** | Exercise this plan's newest stable endpoint and reproduce R4. |
| 4 | **2020.3.48f1** | Exercise a later 2020.3 patch alongside the earliest one. |
| 5 | **2021.3, latest available stable patch** | Exercise the intermediate LTS generation. |
| 6 | **2023.2.20f1** | Exercise the generation where TMP moved into uGUI. |
| 7 | **6000.0, latest available stable patch** | Exercise the first Unity 6 release stream. |
| 8 | **6000.3, latest available stable patch** | Exercise another Unity 6 LTS generation. |
| Retain / reproduce | **2020.3.17f1** | Reproduce the original compiler and authentication observations. |

On Windows, include **Windows Build Support (IL2CPP)** for each Editor where available. Also run its Windows Mono player path where supported. Install the native build toolchain required by each chosen Editor for IL2CPP. macOS/Linux results require their own compatible hosts and build modules.

This is the first download batch, not the complete support matrix. Expand testing to **2021.1, 2021.2, 2022.1, 2022.2, 2023.1**, and **6000.1, 6000.2, 6000.4, 6000.5**, plus every stable patch included in the final support claim. Section 7 defines that expansion. Do not restrict coverage to LTS if the product promises all stable releases.

## 4. Required change backlog

**P0:** prerequisite for a secure, functioning release. **P1:** required before declaring the corresponding feature/platform/version supported. **P2:** documentation maintenance. All items below remain open for upstream implementation unless explicitly described as reported local evidence.

### C01 — Restore the oldest supported compiler/API baseline — P0

**Files:** `Runtime/MuxyGateway.cs`, `Prefabs/MuxyGatewayManager.cs`, `Prefabs/MuxyGatewayNotificationHistory.cs`, all samples and generated C#.

Expand all 13 C# 9 target-typed constructor expressions into explicit types. The original source positions are:

| File / original line | Replacement |
|---|---|
| `Runtime/MuxyGateway.cs:222` | `new WebsocketTransport()` |
| `Runtime/MuxyGateway.cs:658` | `new Dictionary<string, MuxyGatewayGameActionUsedEvent>()` |
| `Prefabs/MuxyGatewayManager.cs:14` | `new GameMetadata()` |
| `Prefabs/MuxyGatewayManager.cs:15` | `new List<GameAction>()` |
| `Prefabs/MuxyGatewayManager.cs:16` | `new List<GameText>()` |
| `Prefabs/MuxyGatewayManager.cs:17` | `new PollConfiguration()` |
| `Prefabs/MuxyGatewayManager.cs:18` | `new MuxyGatewayPollUpdate()` |
| `Prefabs/MuxyGatewayManager.cs:21` | `new MuxyGatewayAuthenticationEvent()` |
| `Prefabs/MuxyGatewayManager.cs:22` | `new MuxyGatewayGameActionUsedEvent()` |
| `Prefabs/MuxyGatewayManager.cs:23` | `new MuxyGatewayBitsUsedEvent()` |
| `Prefabs/MuxyGatewayManager.cs:99,138` | `new SDK(GameID)` at each site |
| `Prefabs/MuxyGatewayNotificationHistory.cs:60` | `new Dictionary<string, string>()` |

Keep shared source within Unity 2020.3's supported C# 8 subset and .NET Standard 2.0 API surface. Unity documents unsupported C# 8 features, including asynchronous disposal; use ordinary `Task` methods for asynchronous shutdown instead of requiring `await using`/`IAsyncDisposable`. Check later .NET API introductions independently of C# syntax. See [Unity 2020.3 compiler](https://docs.unity3d.com/2020.3/Documentation/Manual/CSharpCompiler.html) and [.NET profiles](https://docs.unity3d.com/2020.3/Documentation/Manual/dotnetProfileSupport.html).

Use small compatibility adapters only where Unity APIs genuinely differ. Do not force a new language version, require a newer consumer Editor, or ask consumers to patch `Library/PackageCache`.

**Acceptance:** clean import and sample compilation on 2020.3.0f1, 2020.3.17f1, and the other support rows with the Editor's default compiler settings.

### C02 — Make package dependencies work across the UI transition — P0

**Files:** `package.json`, `Muxy.Gateway.asmdef`, prefab/UI scripts and assets; new UI package manifests/assembly definitions as needed.

**Implementation update, October 20:** the first implementation pass retains the existing single assembly and prefab GUIDs. Actual clean imports showed that a manifest requesting legacy TMP/uGUI versions resolves Unity's built-in TMP shim and modern uGUI on 2023.2/Unity 6. This supports the single-package alternative below; splitting public types is deferred. A UI-free core remains a separate architectural improvement, rather than a prerequisite for this first compatibility patch. See the linked validation record for exact versions, profiles, and limitations.

The single current assembly references `Unity.TextMeshPro`; prefab scripts also use `UnityEngine.UI`, while the manifest declares neither dependency. Existing project dependencies masked that omission in the 2020.3 report.

The reported **TMP 3.0.6 / uGUI 1.0.0** pair is an older-Editor validation configuration. Unity moved TMP into uGUI starting with 2023.2, so copying those two versions into one universal manifest is insufficient. See the [Unity TMP maintainer's migration announcement](https://discussions.unity.com/t/2023-2-latest-development-on-textmesh-pro/917387).

Recommended implementation:

1. Separate the core client/native transport assembly from optional TMP/uGUI presentation components. A code-only integration must import and compile without UI packages.
2. Ship UI companion package variants from shared UI source: an older-Editor variant declaring verified standalone TMP/uGUI dependencies, and a 2023.2+/Unity 6 variant declaring the compatible uGUI package that provides TMP. Record the exact compatible Editor ranges and resolved dependencies of each variant.
3. Keep `Unity.TextMeshPro` references in the UI assembly and explicitly reference the core and uGUI assemblies where needed. Verify actual assembly names in each resolved package.
4. Keep Editor tools in an Editor-only assembly. Keep the core's public assembly name `Muxy.Gateway`, API namespace `MuxyGateway`, and existing serialized assets compatible where possible.
5. Provide migration for existing prefab integrations before changing assembly ownership or package layout: preserve `.meta` GUIDs, serialized fields, UnityEvent bindings, and component references. If a public type moves assemblies, provide and verify the required migration; a preserved script GUID alone does not prove all serialized references survive.
6. Publish installation coordinates for both UI variants and the core. Unity assembly `versionDefines` can select source code; they do not make `package.json` dependencies conditional. Do not rely on them to switch dependency graphs.

A single-package alternative is acceptable only after proving its manifest resolves the correct UI implementation on every target generation. Choose and validate the packaging approach before publishing installation instructions.

**Acceptance:** fresh core-only and full-UI installs work on the minimum Editor, both sides of 2023.2, and the newest release; no duplicate TMP types, missing assemblies, forced UI downgrades, or lost existing prefab bindings. Test Git URL, local/embedded, and release archive installations separately.

### C03 — Use secure, awaitable stage connections — P0

**Files:** `Runtime/MuxyWebsocketTransport.cs`, `Runtime/MuxyGateway.cs`, `Prefabs/MuxyGatewayManager.cs`.

- Change both stage helpers from `ws://` to the verified `wss://` service routes, preserving ordinary certificate validation and preventing plaintext fallback.
- Add Task-returning stage connection methods with cancellation and a configurable timeout. Readiness means a connected socket with transport processing started; it does not mean authenticated.
- Expose an explicit serialized `Stage` on the manager and connect using it. The existing manager unconditionally selects production.
- Reject overlapping connect/authentication operations and validate custom endpoint schemes. Define any developer-only override explicitly rather than silently accepting insecure credential exchange.
- Preserve existing public methods through documented compatibility wrappers where feasible; observe and report failures from any wrapper that cannot return a Task. Migrate all first-party callers to awaited methods.
- Generate addresses using the native API. Do not hardcode the reported `/0.2.0/gateway-unity/0.0.0/...` route or change its projection version without backend confirmation.

**Acceptance:** both stage helpers establish TLS before credential submission; cancellation, timeout, DNS failure, certificate failure, offline startup, and concurrent connects produce controlled states. Authenticate each stage with its own approved credentials/PIN issuer.

### C04 — Repair transport worker, reconnect, and queue ownership — P0

**File:** `Runtime/MuxyWebsocketTransport.cs`; coordinate with the native-access policy in C05.

Source inspection adds these concrete concerns to the reports: `new Thread(async () => ...)` starts async-void thread delegates, so joining the originating thread does not establish completion of asynchronous work; the read-thread cleanup branch assigns `WriteThread = null`; reconnect uses a shared boolean and catches only cancellation for its retry policy.

- Replace the current thread/async mixture with owned receive/send Tasks or another explicitly tracked worker model. Store and await every worker's completion.
- Use one synchronized reconnect operation per client, cancellable delay/backoff, and an explicit terminal-failure policy for WebSocket, network, and TLS errors. Dispose replaced sockets and cancellation sources.
- Ensure stop cancels initial connect, receive, send, and reconnect. A stopped generation must never reconnect or deliver queued responses into a replacement client.
- Bound received message size, queue growth, and work dispatched per frame. Handle fragmentation, UTF-8, non-text/close frames, cancellation, and disposal of message buffers.
- Audit outbound draining on send failures with the backend owner. Define which messages can be resent, acknowledged, reconciled, or dropped; never blindly replay purchase fulfillment or authentication requests.
- Keep Unity-facing callbacks on the main thread through `SDK.Update()`. Document behavior during time-scale pauses, application suspension, and disconnects. Do not execute Unity APIs on transport workers.

**Acceptance:** fault-injected disconnects during connect/send/receive/reconnect leave one worker set, bounded queues, no unobserved Tasks, no old-session dispatch, and deterministic recovery or a visible failure.

### C05 — Implement deterministic native and callback cleanup — P0

**Files:** `Runtime/MuxyGateway.cs`, transport, native detach bindings, manager.

- Provide idempotent explicit disposal and a Task-returning shutdown method that works on the oldest supported Editor. A finalizer must not perform blocking network shutdown or Unity logging.
- Define one native-access policy for transport payload enumeration, message parsing, reconnect, and gameplay calls. Serialize or synchronize access according to the native library's actual thread-safety contract.
- Shut down in a safe order: reject new work; cancel/finish workers and dispatch; quiesce/detach native callbacks; destroy the native instance at the correct point under the confirmed native contract; release handles and managed state. Never free callback userdata while native code could still use it.
- Track debug, authentication, action, Bits, and poll callback handles explicitly. Release each exactly once on safe completion, replacement, cancellation, failed initialization, or disposal. Authentication requests that never receive responses need cleanup too.
- Put temporary native allocations/pins in exception-safe cleanup paths. A user callback throwing must not skip cleanup or unwind across a native boundary. Detaching debug forwarding alone is not disposal.
- Replace anonymous Editor/application subscriptions with removable handlers. Coordinate play-mode exit, script reload, Editor quit, application quit, manager disable/re-enable, and explicit logout without unbounded `.Wait()` calls.
- Maintain a defined disposed state and reject subsequent native calls. A new login receives fresh state rather than reusing disposed resources.

**Acceptance:** repeated lifecycle cycles return SDK-owned handles/workers/sockets to baseline, callback replacement remains bounded, throwing subscribers are contained, and shutdown completes within the agreed deadline. Include disconnected, half-connected, already-stopped, and twice-disposed cases.

### C06 — Isolate action callbacks and correct native callback annotations — P0

**Files:** `Runtime/MuxyGateway.cs`, `Runtime/MuxyLibraryImports.cs`.

`GameActionCallbacks` is currently static, and every `SetGameActions()` replaces it. The static native trampoline cannot select a client-specific map because its userdata currently contains only the high-level callback. This can cross-route events between clients.

- Make the map instance-owned and initialized before events can arrive. Include the owning client/session in callback userdata so the static native trampoline selects the right map.
- Build and validate a replacement map before committing it. A duplicate ID must not leave a partially replaced map or partially published action set.
- Allow a missing per-action UnityEvent without a null-reference failure; the intended global observation route should still behave predictably.
- Correct `InvokeOnActionUsed`'s `MonoPInvokeCallback` annotation: it currently names the managed `OnGameActionUsedDelegate(GameActionUsed)` rather than the native `GatewayOnActionUsedDelegate(IntPtr, IntPtr)` signature.
- Audit all reverse P/Invoke signatures, retained delegate lifetimes, native detach behavior, and calling conventions. The annotation mismatch is a source finding; no IL2CPP failure was reproduced while preparing this document.

**Acceptance:** two intentionally created clients with the same action ID dispatch only to their own callbacks; replacing one client's actions cannot affect the other; the action trampoline runs in an actual IL2CPP player. Retain the single-owner rule for normal game integrations.

### C07 — Correct authentication configuration, errors, and diagnostics — P0

**Files:** runtime authentication/response handling, manager, authentication UI, README, examples.

- Replace the erroneous evaluation ID with the reported working production ID **`gamelink-testing`** wherever the example intentionally uses that evaluation configuration. Check serialized prefab/scene values as well as code defaults. Require a permanent registered ID for distributed games.
- Have the backend owner confirm each game-ID/stage/extension/PIN-source combination. The production success does not establish sandbox authentication or explain the internal cause of the earlier 500.
- Install callbacks before submission and await C03 readiness. Prevent repeated submissions. Report success from native authenticated state plus the expected token result, rather than socket-open or `HasError == false` alone.
- Promote the local structured-error diagnostic concept into a documented API with request correlation and operation/session identity. Support live `errors[].status` and legacy `errors[].number`; define precedence when both are present, and tolerate missing/null/empty arrays.
- Preserve native response parsing if a diagnostic observer throws. Keep transport, credential-denial, configuration, timeout, cancellation, and backend-service failures distinct. Do not classify every 403 as expiry or every 500 as bad credentials.
- Default credential-bearing native diagnostics to disabled or reliably redacted before exchange. Expose diagnostics control without retaining leaked debug handles. Record only sanitized outcome categories, codes, stage/host, request correlation, and token-presence flags.
- On a request timeout, prevent a late response from affecting a newer attempt. Retire the client when the current native API cannot safely cancel the request.

**Backend dependency:** actionable configuration errors, supported protocol error schema, route/projection compatibility, PIN reuse semantics, and sandbox PIN issuance need service-owner confirmation. Track these separately from Unity compatibility fixes.

**Acceptance:** valid PIN, invalid/expired PIN, stale/missing refresh token, offline login, 500 response, timeout, and cancellation have distinct recoverable outcomes. PIN and refresh succeed in both the Editor and each supported player configuration; no credentials appear in logs or reports.

### C08 — Replace PlayerPrefs credentials and complete manager ownership — P0

**Files:** `Prefabs/MuxyGatewayManager.cs`, `Prefabs/MuxyGatewayAuthentication.cs`, new storage/session abstractions and runtime sample.

- Replace the fixed `MuxyGatewayRefreshToken` PlayerPrefs storage with an injectable credential store. Supply memory-only operation and a documented secure platform implementation for every platform on which persistent login is advertised.
- Scope stored credentials by game, stage, and the appropriate account identity. Persist refresh rotation safely. Clear only the relevant credential on deliberate logout or confirmed invalidation; preserve usable credentials on network errors and backend 500s.
- Define migration from legacy plaintext storage, including removal of the old key after migration or an explicit reauthentication requirement. Never silently import a production credential into sandbox.
- Implement one persistent runtime owner and duplicate-owner prevention across scene transitions. Define disabled/re-enabled behavior, callback pumping, and startup order for consumers.
- Replace `Deauthenticate()`'s current client overwrite with complete retirement of the old client, callback installation on the new client, registration-state reset, and a recoverable login UI.
- Register native action/Bits callbacks once per client session, then publish actions, metadata, and initial text only after authentication. Refresh success must not leak registrations or duplicate publication handlers.
- Make callback ordering explicit: internal authenticated/session state must be coherent before notifying consumers. Guard manager update and public methods against absent/disposed clients.

**Acceptance:** PIN login → refresh → process restart → restoration → logout → new PIN works without plaintext tokens, cross-environment collisions, stale clients, or duplicated callbacks; scene changes and repeated Play sessions leave exactly one intended owner.

### C09 — Fix polling event delivery, countdown, and result bounds — P1

**Files:** `Prefabs/MuxyGatewayManager.cs`, `Prefabs/MuxyGatewayPolling.cs`, runtime poll lifecycle.

R4 correctly identifies the manager UnityEvent as the intended prefab interface. Source inspection shows a missing implementation: `StartPoll()` passes the configuration to the SDK, but the manager never connects `PollConfiguration.OnPollUpdate` to `OnPollUpdate.Invoke`. Merely documenting the UnityEvent will not deliver updates.

- Install a retained bridge from the actual SDK poll callback to the manager event, with one defined delivery per observer. Preserve a deliberate code-flow callback contract; do not overwrite or repeatedly wrap user callbacks without a policy.
- Associate updates with the owning client/poll generation. Ignore stale results after replacement, scene departure, cancellation, or logout. Stop/release the prior poll safely.
- Initialize finite countdowns from `DurationInSeconds` and an unscaled monotonic clock. Display infinite duration explicitly when the value is `PollConfiguration.InfiniteDuration` (0). Use numeric minute/second formatting rather than `HH:mm:ss` on a float, and clamp finite countdowns at zero.
- Use server timing if the protocol supplies it. Otherwise document the timer as an estimate and treat the server's final update as authoritative. Do not make time-scale changes extend a service-side poll.
- Validate null/short `Results`, configured options, available UI rows, and `Winner` before indexing children. Specify no-vote/tie/invalid-result behavior with the service owner; process a final result once.
- Document `Chaos`/`Order` modes and `Results`, `WinningVoteCount`, `Mean`, `Count`, and `IsFinal` without inventing unsupported server semantics.

**Acceptance:** prefab and code routes receive live/final updates; finite/infinite timers, paused gameplay, cancellation, replacement, zero votes, invalid winners, short result arrays, and duplicate final messages remain safe.

### C10 — Repair notification templates and prefab initialization — P1

**Files:** `Prefabs/MuxyGatewayNotificationHistory.cs`, `Prefabs/MuxyGatewayAuthentication.cs`, `Prefabs/MuxyGatewayPolling.cs`, corresponding prefab assets.

- Map `{Cost}` to the formatted numeric cost and `{UserID}` to the real user ID. Preserve the correct transaction/action/username mappings. Validate unknown placeholders so a typo does not throw from the event callback.
- Make row/component references consistent: notification methods currently alternate between looking for `ActionText` children and reading `TMP_Text` directly on the row. Verify the shipped hierarchy and correct the code/assets together.
- Replace brittle `transform.Find` paths with serialized component references where practical. Initialize/validate them before subscribing to events; the current widgets subscribe in `OnEnable` but discover their child widgets in `Start`.
- Show an actionable configuration error for missing managers/widgets and fail safely. Support balanced listener add/remove across repeated enable/disable cycles.
- Correct color construction to Unity's expected normalized values or use `Color32` where byte values are intended. Validate notification clearing, visibility, and expiry.
- Mask PIN entry, clear the field on submission, disable concurrent submissions, and show recoverable sanitized errors. Test both the host project's input/EventSystem setup and the packaged sample setup.

**Acceptance:** real payload values render correctly; unknown template keys, reordered/renamed widgets, missing references, early callbacks, and repeated enable/disable produce controlled behavior. Prefab instantiation alone is not this acceptance test.

### C11 — Validate public action, metadata, and text inputs — P1

**Files:** runtime setters, manager setters, metadata/logo helpers, documentation.

- Validate complete action arrays before modifying native state: null entries, missing/duplicate IDs, null events, invalid enum values, `Impact` outside 1–5, and count/sentinel constraints. Choose a documented rejection/result policy. Inspector `[Range]` does not validate code-supplied values.
- Apply compatible validation to individual action count/state setters. Publish constraints and initialization defaults, including `GameAction.InfiniteCount`, without relying on Inspector behavior.
- Document `GameMetadata.Logo` as a PNG data URI string (`data:image/png;base64,...`). Use the existing `SDK.ConvertTextureToBase64(Texture2D)` or `SDK.SetGameLogo(Texture2D)` APIs; remove references to nonexistent conversion methods.
- Handle missing optional logos safely. Validate texture readability and RGB24/RGBA32/ARGB32 formats; make the exception/documentation list match the accepted formats. Define and enforce backend-approved encoded payload limits.
- Verify whether logo-only updates preserve existing name/theme under the backend's metadata semantics. Test both initial metadata and partial updates.
- Use extensionless `Resources.Load` paths and supply the actual sample resource. Publish initial text after auth and subsequent updates on changes, with null/size validation and no per-frame publication loop.

**Acceptance:** invalid input yields a useful deterministic result and no partially updated action map; metadata/text works with and without a logo and with each supported texture format.

### C12 — Make purchase and Bits fulfillment safe under replay — P0 for monetized features

**Files:** runtime transaction contract where needed, complete gameplay sample, integration guide, durable transaction-store interface.

- Expose stable transaction identity and define one fulfillment owner. Per-action and global callbacks can both observe a payload, but must not both grant its effect.
- Supply an actual bounded sample effect and explicit outcomes: newly fulfilled, already fulfilled, rejected, pending, and retryable failure. Remove empty effect functions and always-successful placeholder helpers from runnable samples.
- Supply durable recording and reconciliation for both action and Bits transactions. A replay of a fulfilled action reuses/reconciles prior success; it must not grant twice or trigger a new refund simply because a boolean says “already seen.”
- Confirm acknowledgment/refund/retry rules with the backend owner. Design recovery for crashes between effect application, durable recording, and acknowledgment; do not promise universal exactly-once game side effects from an in-memory dictionary.
- Cover missing game objects, unavailable scenes, unknown actions, exceptions, and disconnects during fulfillment. Run local simulated events before live monetized checks.

**Acceptance:** duplicate and interrupted transactions settle under the agreed contract without double rewards or refunding previously fulfilled effects. This remains an independent feature gate on each advertised integration path.

### C13 — Validate native platforms and IL2CPP explicitly — P0 for each advertised target

**Files:** `Runtime/MuxyLibraryImports.cs`, native binaries and their `.meta` files, core platform guards; native build/release process outside this repository if necessary.

- Replace the universal Windows-centric `DllImport("cgamelink.dll")` declarations with a platform-appropriate library-name strategy verified on each target. Confirm exported C ABI, calling conventions, struct layout/field widths, UTF-8 marshalling, null pointers, and string ownership against the native headers.
- Set explicit plugin importer OS/CPU and Editor/player combinations. The current Windows DLL and Linux SO enable Any Platform; the macOS metadata's AnyCPU label does not establish a universal binary.
- Inspect actual binary architectures and dependent libraries. Produce verified Windows/Linux x64 artifacts and macOS Intel/Apple Silicon artifacts for any combinations advertised. Windows ARM64 requires its own native binary and validation if offered. An empty `Runtime/x86` folder establishes no x86 support.
- Verify minimum operating system/runtime requirements on hosts that can run each Editor generation. Keep Editor architecture distinct from player architecture, especially on Apple Silicon.
- Exercise every native entry point and reverse callback in Mono and IL2CPP players where each backend is available. Validate AOT callback annotations, delegate retention, stripping, and reflection/serialization paths; add narrowly scoped preservation rules only where needed.
- Guard `using UnityEditor` with `UNITY_EDITOR` alone, or move all Editor hooks into an Editor assembly. The original unused import did not block the reported Windows Mono player; present this as isolation/portability work.
- Audit other `UNITY_EDITOR || UNITY_STANDALONE` guards against the actual supported target list. Unsupported targets must have a documented exclusion or safe unsupported implementation rather than accidental missing-type/native-load failures.

**Scope:** the bundled desktop libraries do not establish Android, iOS, WebGL, consoles, or every architecture. Adding those targets requires appropriate native artifacts and, where necessary, another transport implementation. Record them as unsupported/pending until implemented and tested; do not imply that a Unity version promise enables every Unity build target.

**Acceptance:** native construction, all used exports/callbacks, authenticated feature operation, and disposal pass in actual built players for each claimed OS/CPU/backend row. Native constructor-only probes remain separate evidence.

### C14 — Handle Editor reload and preserve existing integrations — P1

**Files:** SDK/manager lifecycle, new Editor assembly, migration tooling and upgrade guide.

- Test domain reload and scene reload both enabled and disabled in every combination available in the Editor. Reset persistent/static state and unregister static event handlers explicitly; do not depend on reload to clean them up. See [Unity's domain reload guidance](https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html).
- Support script recompilation, repeated Play entry/exit, Edit-mode verification tooling, and Editor shutdown without stale sockets or callbacks to destroyed components.
- Preserve public API names and serialized data where feasible. Add new awaited lifecycle APIs before deprecating legacy ones; document any necessary breaking changes and choose the package version accordingly.
- Validate an upgrade from the original package, including existing scenes/prefabs, custom assembly references, UnityEvents, stage defaults, and the C02 UI package migration. Never store tokens in serialized assets.

**Acceptance:** old integrations migrate under documented steps with no missing scripts or lost event bindings, and repeated Editor sessions behave the same with reload disabled and enabled.

### C15 — Make releases reproducible and version metadata consistent — P0

**Files:** `package.json`, `Runtime/MuxyGatewayVersion.cs`, `CHANGELOG.md`, `.github/workflows/main.yml`, release manifests and docs.

- Reconcile tag `v1.0.0-rc`, package `0.0.1`, runtime `v0.0.0`, and changelog `0.1.0`. Record the SDK/package version, native artifact revision/ABI, protocol version, and projection version as distinct named concepts.
- Separate protocol/projection identity from the public package version where necessary. Backend routes embed the projection identity; changing the package version must not accidentally select an unsupported route.
- Fix the current release workflow's artifact/source discrepancy: it rewrites the runtime constant only while building the ZIP after a release is published. A Git URL install from the same tag therefore need not contain that rewritten constant. Commit/generate validated metadata before tagging, or use a reproducible build mechanism with identical effective behavior for every installation route.
- Validate package contents and immutable artifact checksums before publishing. Ship correct licenses/notices, working documentation/changelog URLs, release notes, and exact tested source/native revisions. Avoid regenerating unrelated `.meta` GUIDs.
- Add release gates before publication. The inspected workflows create a release ZIP and request an activation file; they do not provide the cross-version Editor/player/feature matrix required here.

**Acceptance:** clean projects installed through Git, archive, and embedded/local routes resolve the intended identical source/native behavior and compatible service route; all release gates refer to the final immutable artifact.

### C16 — Publish complete, accurate docs and samples — P1; hosting repair P2

**Files:** `README.md`, `Walkthrough~/walkthrough.md`, sample assets/code; the eight hosted tutorial pages in their actual documentation source repository.

- Replace disconnected fragments with complete code and prefab flows sharing the same connection, auth, ownership, and disposal behavior. Include a runnable small demonstration of metadata/text, a bounded action, Bits handling, polling, and logout.
- Keep the Editor authentication utility separate from the runtime demonstration. Port only reviewed functionality; remove review-specific output paths and keep persisted observations sanitized.
- Explain the supported game-ID/stage/PIN source, six-character/five-minute guidance subject to owner confirmation, UI dependencies for each Editor generation, custom `.asmdef` references, input setup, and success/recovery checkpoints.
- Update all eight pages together: overview, installation, prefab quickstart, authentication/lifecycle, actions, polls/text, production practices, and QA. Incorporate R4's correct API/data-shape details together with the actual fixes in C06/C09/C10/C11.
- Remove or clearly archive the obsolete MEDKit/`MuxyGameLink` walkthrough with placeholders and insecure examples. Point readers to the release-matched supported guide.
- Fix hosted Edit/View Source links against the real deployed documentation revision and maintain one release/version table. Do not assume the broken `main/docs/docs/...` paths are the correct source location.
- Compile runnable samples on the minimum and newest Editor, then exercise their behavior. R4's reported compilation is useful evidence but does not close lifecycle, security, fulfillment, or widget-functionality gates.

**Acceptance:** a developer can follow either complete flow in a clean project through installation, authentication, feature use, and teardown without modifying package source or inventing missing methods/assets.

## 5. Implementation order and responsibility

| Phase | Deliverables | Primary owner / dependency |
|---|---|---|
| 1. Establish fixtures | Exact release inventory, earliest/newest clean projects, reproducible baseline failures and source/native identity. | SDK + QA |
| 2. Compiler and package foundation | C01, C02 packaging decision/migration design, native target inventory from C13. | SDK + package/native maintainers |
| 3. Runtime correctness | C03–C08 and native/AOT corrections from C13. | SDK; backend thread-safety, detach, and error contracts |
| 4. Feature correctness | C09–C12, completed UI packaging, C14 upgrade/reload behavior. | SDK + sample/UI owners; backend transaction semantics |
| 5. End-to-end coverage | C16 complete runnable samples and the section 7 matrix against final code/artifacts. | QA + SDK + docs; stage-specific test accounts/PIN issuance |
| 6. Release | C15 immutable package/artifacts; published support results, migration guide, release-matched hosted docs. | Release + native + docs owners |

Build the smallest and newest fixtures early so architectural mistakes in dependency selection, native loading, or runtime assumptions appear before polishing documentation. No phase is complete merely because a document was updated.

## 6. Proposed lifecycle/API contract

These are design requirements for implementation, not APIs already present in the reviewed package:

| Operation | Required contract |
|---|---|
| Create | Validate configuration and platform; allocate a tracked native instance; diagnostics safe by default. |
| Connect asynchronously | Select an explicit stage; validate WSS; return readiness/failure; accept cancellation; allow one in-flight connection. |
| Authenticate | Callbacks installed; connection ready; one attempt; operation timeout and session correlation; report native authenticated state and structured failure. |
| Dispatch | `SDK.Update()` on Unity's main thread; bounded work; consumer exceptions contained. |
| Register/publish | Instance-owned handlers, safe replacement, session-scoped registration; publish initial state after successful authentication. |
| Reconnect | One owned operation; cancellable backoff; explicit reauthentication/resubscription policy based on native/backend semantics. |
| Logout | Clear only the intended credential; stop outstanding work; reset session/registration/poll state; permit a fresh client. |
| Shut down asynchronously | Complete or cancel workers and native callbacks within a configured deadline; release resources exactly once; report completion. |
| Dispose | Idempotent; no later native use or callback access to freed handles. Provide a safe bounded fallback when normal shutdown fails. |

Suggested states: `Created`, `Connecting`, `Connected`, `Authenticating`, `Authenticated`, `Reconnecting`, `Stopping`, `Stopped`, `Disposed`, with an explicit failure result. Generation checks prevent stale operations from moving a replacement session between states.

## 7. Compatibility matrix and test strategy

### Version inventory

Maintain a machine-readable list of every stable Editor release in the promised interval, with its official release URL, exact patch/build identifier, installation availability, supported host/target combinations, and test status. Generate the inventory from Unity's release records and freeze it for each SDK release. Do not use numeric sorting or a hardcoded list of LTS streams to infer coverage.

Include these streams: **2020.3; 2021.1, 2021.2, 2021.3; 2022.1, 2022.2, 2022.3; 2023.1, 2023.2; and 6000.0 through 6000.6**. Include later stable patches in those streams if they fall within the declared release cutoff. Keep alpha/beta/experimental coverage separately labeled.

### Execution tiers

| Tier | Coverage | Purpose |
|---|---|---|
| Pull-request checks | Minimum Editor, 2020.3.17f1 reproduction, 2022.3, 2023.2 transition, newest Editor; relevant Mono/IL2CPP players. | Catch common source/package/native regressions promptly. |
| Stream checks | First and newest included patch of every listed stream, both UI packaging generations, each advertised platform/backend. | Catch changes between engine generations. |
| Full-range release qualification | Every exact stable Editor patch and applicable configuration included in an unqualified all-versions claim. Run clean install, compile, native/player smoke, and the functional suite below on each claimed row. | Establish the literal promised range. |
| Extended reliability | Minimum, newest, UI boundary, and each distinct native/backend/runtime implementation; expand when failures show version-specific behavior. | Measure repeated sessions, failure recovery, memory/handle behavior, and soak. |

Representative passes help development but do not prove every intervening patch. If an archived Editor cannot run or obtain a license, record an environment block and leave that row unverified. Either complete qualification later or publish an explicit narrower tested matrix and exceptions rather than marking it passed.

### Configuration axes

- Clean core-only project and complete prefab/UI project; direct `Assembly-CSharp` integration and a consumer custom assembly.
- Windows x64 first; macOS Intel/Apple Silicon and Linux x64 before advertising them. Additional CPUs/targets require additional binaries and rows.
- Editor operation, actual Mono players, and actual IL2CPP players wherever Unity supports that target/backend combination. Include any additional production runtime/backend that the release elects to support; experimental runtimes get separate status.
- Oldest supported .NET API profile and the Editor's normal newer profile; development and release builds; appropriate stripping settings.
- Production and sandbox service behavior with matching game IDs/PIN sources; mock/fault-injected protocol tests for repeatable edge cases.
- Domain/scene reload combinations, time-scale pause, component disable/enable, scene changes, application suspension/resume, and process restart.
- Built-in render pipeline clean fixture plus representative URP/HDRP integrations when those sample/rendering combinations are advertised. Keep unrelated render-package/shader errors attributable to their owner.

Mark a combination N/A only when that Unity/platform configuration genuinely does not exist; lack of a test machine is “pending,” not N/A. Edition/license setup problems are environment outcomes, not SDK compiler passes or failures.

## 8. Required functional and reliability checks

All entries are proposed acceptance work; no new Unity tests were executed while preparing this document.

| Test group | Required checks / pass condition |
|---|---|
| Installation | Fresh Git URL and archive/local imports; automatic declared dependencies; UI resources; exact final artifact identity; no manual cache edits or custom compiler flags. |
| Compilation/build | Core, prefabs, Editor tools, and complete samples compile; custom assemblies resolve; target player builds are clean. Track unrelated baseline failures separately. |
| Native/AOT | Correct library OS/CPU/importer selection; exports, marshalling, UTF-8, null strings, every callback, and shutdown run in actual players with intended stripping. |
| Secure transport | Both stage defaults use WSS; valid certificate path; certificate/network failure, delayed connect, cancellation, fragmentation, malformed/oversized input, disconnect and reconnect. |
| Authentication | Fresh PIN and refresh; invalid/expired input; absent/stale token; 403/500; missing/null/empty errors; `status`/`number`; cancellation and late response; logout and relogin. |
| Credentials | Restart restore and rotation; stage/game/account isolation; legacy-key migration; memory-only mode; no secrets in logs, scene serialization, or result artifacts. |
| Actions | Valid/invalid IDs and ranges; atomic rejection of duplicate arrays; null events; two-client isolation; one effect under per-action/global observation; correct accept/refund/reconciliation. |
| Bits | Single reward on replay, durable record/recovery, controlled unavailable-state behavior, confirmed backend settlement semantics. |
| Polling | Manager bridge and code callback; live vote fields; finite/infinite duration; paused clock; bounds; no votes/ties; stop/replace; repeated final and stale generation. |
| Metadata/text | Correct PNG data URI and texture formats; missing/unreadable/oversized logo; metadata preservation; initial and event-driven text; no per-frame publication traffic. |
| Widgets | Actual notification values and template errors; correct row references, colors, clearing; PIN masking/UI recovery; early events and missing references; EventSystem compatibility. |
| Upgrade/Editor | Original-package migration, GUIDs/UnityEvents/assembly moves, repeated Play cycles with reload on/off, script compilation, scene transitions, duplicate owner and disable/enable behavior. |
| Disposal | Stop during every connection/auth/reconnect state; subscriber exception; pending callbacks; double shutdown/disposal; no post-disposal callbacks or native calls. |

Proposed measurable reliability budgets, to finalize before execution:

- **100** create/connect/authenticate/disconnect/dispose or simulated-auth lifecycle cycles, plus **100** manager enable/disable or Play cycles on the designated reliability configurations.
- **60-minute** authenticated soak per designated platform/backend implementation, including controlled reconnects, polls, metadata updates, and simulated transaction replays.
- **5-second** normal shutdown deadline; a deadline expiry must enter a documented fallback and produce a sanitized failure result. Never destroy a native instance while a worker can still call it.
- **Zero** SDK-owned live callback handles, worker Tasks, sockets, or event subscriptions after completed disposal; record counts before/after each cycle.
- **Zero** stale-session callbacks, duplicate granted effects, leaked credentials, unhandled native-boundary exceptions, or callbacks reaching destroyed Unity objects.
- Configure queue/message limits before the run; prove they remain bounded under overload. Record memory after warm-up and comparable idle points; investigate sustained growth rather than assuming a GC collection proves cleanup.

These are proposed thresholds, not measurements from the reports. Live automated authentication must use an owner-supported test arrangement; mocked protocol coverage does not replace real stage/player acceptance.

## 9. Evidence record and release decision

For each test retain: test ID, SDK tag/commit/package/native hashes, exact Editor version/build and edition, host OS/CPU, target OS/CPU, backend/API profile, resolved dependencies, install route, reload/stripping settings where relevant, stage, expected result, actual result, sanitized evidence, timestamp, and status.

Use statuses **Passed**, **Failed**, **Pending**, **Environment blocked**, and **N/A with reason**. Mark historical patched-source observations as historical; rerun the final release artifacts after implementation. Keep compile, native construction, authentication, player build, gameplay, and reliability results separate.

### Release checklist

- [ ] Explicit oldest stable version and dated newest endpoint are reflected in package metadata and docs.
- [ ] C01–C08 foundation/security/lifecycle work is complete.
- [ ] C09–C12 advertised features function and handle their edge cases.
- [ ] C13 native artifacts and actual player results exist for every advertised platform/backend.
- [ ] C14 existing integrations migrate and reload behavior passes.
- [ ] C15 Git/archive/local installations have consistent validated metadata and service behavior.
- [ ] C16 complete samples and hosted docs match the final release.
- [ ] Both production and sandbox have approved, working authentication configurations, or any unavailable stage is explicitly excluded from the release claim.
- [ ] The exact Unity-version matrix has no unreported failures, untested patches, or environment blocks inside an unqualified support claim.
- [ ] Functional, failure-recovery, transaction, shutdown, and designated soak checks pass with sanitized evidence.

Publish the tested support table with the release. Update the inventory and qualify subsequent stable Unity releases before extending the upper endpoint; no continuing monitor or scheduled automation is established by this plan.

## 10. Traceability to the reports

| Report finding | Planned work |
|---|---|
| R3 G01: C# 8 incompatibility | C01; sections 3 and 7 minimum-version checks. |
| R3 G02/G03: connected integration and one persistent owner | C03, C05, C08, C14, C16. |
| R3 G04: fulfillment examples and replay | C12. |
| R3 G05: native support evidence | C06, C13; configuration/evidence matrix. |
| R3 G06: dependencies/custom assemblies | C02, C16. |
| R3 G07: callback leaks/shutdown | C04–C06, C08, C14. |
| R3 G08: hosted source links | C16. |
| R3 G09: reproducible observed QA results | Sections 7–9. |
| R3 G10: structured errors | C07; backend contract dependency. |
| R3 G11: evaluation ID | C07, C08, C16, including serialized values. |
| R1/R2: WSS workaround vs unfixed defaults | C03; real default-stage acceptance. |
| R1/R2: diagnostic patch and Editor-window boundaries | C05, C07, C16; final-artifact rerun. |
| R4 1-A: modern TMP packaging | C02; 2023.2 and Unity 6 fixtures. |
| R4 2-A/4-B: logo data format/API | C11, C16. |
| R4 2-B/5-A/5-C: action IDs and range behavior | C06, C11. |
| R4 2-C: hardcoded UI paths | C10. |
| R4 2-D/7-B: notification fields and countdown | C09, C10. |
| R4 2-E/6-A–C: poll bridge, fields, infinite duration | C09; implement missing manager bridge before documenting it as functional. |
| R4 3-A/1-B/7-A: generated URLs and four version strings | C03, C15. |
| R4 3-B: native importer settings | C13. |
| R4 3-C: abandoned walkthrough | C16. |
| R4 4-A/4-C: full authentication class and storage | C03, C05, C07, C08, C16; replace unsafe/obsolete choices in that proposed class. |
| R4 5-B: update-pump requirement | C04, C08, C16. |
| R4 7-C: static action callback map | C06. |
| R4 8-A/8-B and compilation claims | Sections 2 and 7–9; preserve reported scope and add behavioral/player coverage. |

## 11. Reference locations

Local report originals: [R1](C:/Users/jstef/Downloads/Gateway-Unity-2020.3.17f1-SDK-Changes.md), [R2](C:/Users/jstef/Downloads/Gateway-Unity-2020.3.17f1-Tutorial.md), [R3](C:/Users/jstef/Downloads/Gateway-Installation-Review.md), [R4](<C:/Users/jstef/Downloads/GatewayDocEdits (1).md>). These paths identify the inputs on the author's machine; archive the reports alongside the engineering handoff if distributing it elsewhere.

Source baseline: [inspected main commit](https://github.com/muxy/gateway-unity/tree/fc9fb13da721f85d03f0a2f507d7620a0ff5212f) and [reported tag commit](https://github.com/muxy/gateway-unity/tree/9ee3ace5d6c44d2169101c33588864488a3d0610). Unity references linked above were checked October 20, 2026. The backlog is an implementation and qualification plan; it does not certify fixes or broad support as already delivered.
