# Shipping WSS transport and connection cleanup

Date: October 20, 2026. PR: [#15](https://github.com/muxy/gateway-unity/pull/15).

Tested SDK commit: **`0c9011fd0de204a5d006c449adee5025efd70b70`**. Each current result includes SDK source hashes. These checks supersede the earlier validation-only socket workaround in [the original compilation report](Compilation-And-Packages.md).

## Changes

- Production and sandbox stage helpers require **WSS**. TLS 1.2 is used across the supported Editor range, without overriding certificate-chain or hostname validation. Explicit custom endpoints may use WS for local tools.
- The transport owns the TCP/TLS stream throughout connection establishment. Cancellation closes that stream during TCP connect, TLS negotiation, HTTP upgrade, send, and receive cleanup. This avoids the stalled-upgrade shutdown failure observed with Unity's `ClientWebSocket` implementation during this work.
- The HTTP upgrade validates status, required headers, the server accept key, and unsolicited extensions/subprotocols. Standard .NET WebSocket framing, masking, control frames, and keepalive operate over the connected stream. Upgrade headers are limited to 16 KiB and incoming messages to 16 MiB. HTTP redirects, HTTP proxies, and WebGL are not supported by this transport.
- Tracked tasks replace async delegates inside raw threads. A single session owns its send/receive tasks, cancellation sources, connection attempts, reconnect delay, and queues. Stop cancels, aborts, joins both tasks, disposes resources, and discards stale fragments/queued connection data. Reopen creates a fresh connection. Reconnect uses cancellable delays capped at 30 seconds.
- Native payload extraction, native reconnect notification, and received-message callbacks run from `SDK.Update` on the owning/main thread. Socket tasks do not invoke native SDK or Unity APIs.
- `RunInProductionAsync`, `RunInSandboxAsync`, and `StopWebsocketTransportAsync` allow callers to await initial connection or shutdown. Existing void helpers remain available and observe connection errors. `IsConnected`, `LastConnectionError`, and `IsDisposed` expose status.
- `SDK.Dispose` permanently stops the transport, unsubscribes named lifecycle handlers, destroys the native instance, and releases managed callback handles, including pending authentication requests. Repeated callback registration releases replaced action/bits handles. Action event dictionaries belong to individual SDK instances. Disposal from an authentication callback defers native destruction until native dispatch returns.
- The prefab manager disposes on disable and deauthentication and recreates its SDK on enable. Editor play-mode exit, assembly reload, and application quit also call cleanup. Native diagnostic forwarding is disabled by default; opting in is explicit.

Call SDK/native APIs and disposal on the owning/main thread. For a reusable pause, await stop; for terminal cleanup, dispose. Cleanup aborts the connection rather than waiting for a peer close handshake. These changes do not make arbitrary concurrent SDK calls safe.

Direct `WebsocketTransport` integrations should await `Open`, call `Run(instance)`, and pump `Update(instance)`. The public `SendMessages`/`ReceiveMessage` methods remain as Update aliases; they no longer initiate independent socket reads/writes. Custom callers that managed those operations themselves must adopt the owned I/O flow.

## Clean package regression matrix

All six fixtures initially requested **only the local Gateway package**, without preinstalled UI assemblies. All passed import, SDK/custom-consumer compilation, six public-type ownership checks, and UTF-8 struct round trips using the final transport source.

| Editor | Result | Resolved TMP | Resolved uGUI |
|---|---|---|---|
| [2020.3.48f1](Results/Shipping-Transport/2020.3.48f1-package.json) | Passed | Standalone 3.0.6 | 1.0.0 |
| [2022.3.62f3](Results/Shipping-Transport/2022.3.62f3-package.json) | Passed | Standalone 3.0.7 | 1.0.0 |
| [2023.2.20f1](Results/Shipping-Transport/2023.2.20f1-package.json) | Passed | Built-in 5.0.0 shim | 2.0.0 |
| [6000.0.85f1](Results/Shipping-Transport/6000.0.85f1-package.json) | Passed | Built-in 5.0.0 shim | 2.0.0 |
| [6000.4.8f1](Results/Shipping-Transport/6000.4.8f1-package.json) | Passed | Built-in 5.0.0 shim | 2.0.0 |
| [6000.6.6f1](Results/Shipping-Transport/6000.6.6f1-package.json) | Passed | Built-in 5.0.0 shim | 2.6.0 |

The two existing activated LEGO projects also compiled the final SDK and a separate consumer assembly in **2020.3.0f1 and 2020.3.17f1**. Both retained public-type ownership and their preinstalled TMP 3.0.6/uGUI 1.0.0. Their unrelated template diagnostics remain outside this SDK result. The older Editors' separate batch licensing issue from the previous run remains unresolved; these are existing-project integration passes.

The minimum Editor's bundled C# 8 compiler also passed [Editor and standalone-symbol source checks](Results/Shipping-Transport/2020.3.0f1-managed-compiler.json) with the final SDK source and .NET Standard 2.0 references, reusing UI assemblies from the earlier 2020.3.48f1 fixture. Two existing nullable-annotation warnings remain. These focused checks are not player builds.

## PIN linking through the shipping SDK

Fresh, separate PINs came from the user's Twitch Gateway configuration page. The probe called **`RunInProductionAsync`**, awaited the SDK's connection, called `AuthenticateWithPIN`, and pumped only `SDK.Update`. It used no replacement socket, raw payload forwarding, endpoint override, private native destruction, or diagnostic detachment. Reflection only observed the connected route and final cleanup state.

All three Editors used **`wss://gamelink.muxy.io`**, game ID **`gamelink-testing`**, and normal TLS validation. All authentication callbacks reported no error, native authenticated state, JWT presence, and refresh-token presence. Credentials were retained only in memory after request-file consumption; no JWT or refresh-token values are in reports.

| Editor | PIN callback, CDT | Approx. PIN age at submission | PIN + refresh | Stop, reopen + refresh | Callback disposal / cleanup |
|---|---|---|---|---|---|
| [2020.3.0f1](Results/Shipping-Transport/2020.3.0f1-authentication.json) | 17:16:57.952 | 27.81 s | Passed | Passed | Passed |
| [2020.3.17f1](Results/Shipping-Transport/2020.3.17f1-authentication.json) | 17:17:45.778 | 17.01 s | Passed | Passed | Passed |
| [6000.6.6f1](Results/Shipping-Transport/6000.6.6f1-authentication.json) | 17:23:37.363 | 26.74 s | Passed | Passed | Passed |

PIN age uses the configuration reload request as an approximate issuance boundary. Each submission was within the five-minute window. The refresh token returned by the first refresh was used after stop/reopen; the last refresh callback called `SDK.Dispose` itself. All callbacks ran on the main thread. Subsequent observations confirmed:

- Native instance pointer cleared and SDK marked disposed.
- Pending authentication dictionary empty and stored debug/poll/action/bits handles unallocated.
- Transport session cleared and tracked socket worker completed.
- Repeated stop/dispose completed, and a disposed SDK rejected a new connection.

Measured awaited stops in these three runs took approximately **7.25 ms**, **100.27 ms**, and **9.07 ms**, respectively. These are individual Editor observations, not latency guarantees or a leak-free soak result.

The two user projects had their package manifests and lockfiles restored exactly, temporary probe assets/control files removed, and Editors left open. See [2020.3.0f1 cleanup](Results/Shipping-Transport/2020.3.0f1-cleanup.json) and [2020.3.17f1 cleanup](Results/Shipping-Transport/2020.3.17f1-cleanup.json). The Unity 6.6 test used an isolated batch fixture that exited after the result.

The first Unity 6.6 probe attempt encountered a local `IOException` while waiting for its PIN request file, before connection or authentication. Its [failed observation](Results/Shipping-Transport/6000.6.6f1-probe-input-failure.json) is retained. Atomic request publication and a fresh PIN produced the pass above; this was not a server authentication rejection. An earlier batch launch also lacked its settings after Unity cleared Temp; batch reproduction now passes the settings file outside Temp explicitly.

## Local transport/lifecycle regressions

The [2020.3.48f1](Results/Shipping-Transport/2020.3.48f1-lifecycle.json) and [6000.6.6f1](Results/Shipping-Transport/6000.6.6f1-lifecycle.json) runs both passed:

- Stop/dispose without opening, duplicate start, and repeated cleanup.
- Fragmented Unicode message assembly; discarded partial data after an abrupt disconnect.
- Automatic reconnect, with native reconnect handling dispatched by Update.
- Stop during blocked receive, reconnect/backoff, pending TCP/HTTP upgrade, and pending TLS handshake.
- Invalid upgrade/accept-key rejection; initial connection failure surfaced to the caller; retry and explicit reopen.
- Disposed transport/SDK reuse rejection.
- Prefab OnDisable/OnEnable methods invoked directly in an isolated PlayerPrefs namespace; prior SDK disposed and a fresh SDK created.
- Repeated action/bits callback registration and release of native state/handles, including a pending, locally queued authentication request that was never sent.

Maximum observed stop time in each final local regression run was below 51 ms. This is a local test bound, not a general network timing promise. The tests use a loopback WS server; the separate production PIN checks above establish the shipping WSS path.

Development checks exposed and corrected a stalled-handshake cancellation failure with `ClientWebSocket`, a test-server continuation that delayed its own handshake, and Unity 2020.3 UnityTLS rejecting `SslProtocols.None`. The final code uses owned cancellable streams and explicit TLS 1.2. Raw attempts/logs are retained in the workspace validation directories.

## Reproduction

Clean import checks use `Tools~/Test-PackageImport.ps1` as described in the original compilation report. Local transport checks use:

```powershell
.\Tools~\Test-Transport.ps1 -UnityVersion 2020.3.48f1
.\Tools~\Test-Transport.ps1 -UnityVersion 6000.6.6f1
```

The runner uses marked disposable fixtures, adds JSON serialization for the **test probe only**, launches a hidden child Editor, enforces a timeout, and requires its explicit passed JSON result. Both commands were exercised successfully. Only the runner's own child process is terminated on timeout.

For the production PIN check:

1. Use a disposable or checkpointed project with the local SDK, consumer fixture generated by the import harness, and `com.unity.modules.jsonserialize: 1.0.0` for probe output. Save baseline project/package files first.
2. Copy `Tools~/GatewayShippingTransportValidation.cs` into an Editor folder. Create `Temp/GatewayShippingTransportValidation/settings.json` in an already-running Editor, with `outputDirectory` outside the SDK package and the matching `gameId`. For batch launch, put settings **outside Temp** and pass `-executeMethod GatewayShippingTransportValidation.RunBatch -gatewayTransportSettings "<absolute settings path>"` without `-quit`.
3. Wait for `state.json` to report `waiting-for-pin`. Obtain a fresh matching extension PIN. Publish `pin-request.json` in the control folder by writing a temporary file and renaming it after closing the write handle; include `pin` and `generatedAtUtc`. Do not reuse an output directory containing a prior result.
4. Wait for `result.json`, including callback, refresh, reopen, disposal, and handle/task checks. Batch mode exits itself. Save source/probe/native identities with the result; earlier live-Editor probe copies differ from the final helper only in batch settings and request-file handling.
5. Remove owned test assets/control files and restore only test package changes, preserving any unrelated project edits. The probe never writes refresh tokens to PlayerPrefs.

The older `GatewayLiveValidation.cs` is historical validation-only tooling for commit `e1c80fb`; it is not the current transport test.

## Remaining scope

These Windows x64 Editor checks do not establish player builds, IL2CPP, other operating systems/architectures, WebGL, every Unity patch, sandbox authentication, full gameplay/widget behavior, arbitrary concurrent native API calls, certificate-failure scenarios, long-duration reconnect/authentication recovery, or complete callback exception isolation. The directly invoked prefab methods do not establish delivery of every Unity play-mode/domain-reload lifecycle event. Public disposal and the tested callback handles are now covered; server-side token revocation and a full memory/handle soak remain separate checks.
