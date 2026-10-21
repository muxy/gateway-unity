# Windows player compatibility

Validation date: October 20, 2026 (America/Chicago; JSON timestamps use UTC).
SDK commit: `b0df7c86f4e3be8bb994ae1acf876370882c73d0`.

## Shipping issue found and fixed

The first Unity 6000.6.6f1 Mono player built successfully but could not establish
WSS with **High** managed stripping. TLS initialized `ServicePointManager`, which
failed because stripping removed `System.Configuration.ExeConfigurationHost`'s
default constructor. This was a player defect that Editor compilation did not expose.

The package now has an Editor-only `IUnityLinkerProcessor` that supplies a generated
descriptor from `Library/MuxyGateway/link.xml`. It preserves `System.Configuration`
and the reflection-loaded `System.Net.Configuration` types. The reproduced Mono
connection failure passed after this change, with normal certificate validation.
In that Unity 6.6 fixture, player build size increased by about 0.9 MB. The runtime
assembly and existing asset GUIDs remain unchanged.

The build callback is used because a package must supply preservation through a
mechanism supported across the minimum and current Editors. See Unity's
[linker callback API](https://docs.unity3d.com/2020.3/Documentation/ScriptReference/Build.IUnityLinkerProcessor.GenerateAdditionalLinkXmlFile.html).

## Build and offline runtime matrix

All builds are Windows x64, nondevelopment, High managed stripping, using the
fixture's .NET Standard API profile. Each available combination passed native DLL
loading, Unicode metadata marshalling, native PIN/refresh response fixtures,
poll/action/Bits callbacks, the action UnityEvent, main-thread callback dispatch,
at least 100 idempotent disposal cycles, actual manager disable/re-enable, and scene unload.

| Unity | Mono build/runtime | IL2CPP build/runtime |
|---|---|---|
| 2020.3.0f1 | Passed | Module missing |
| 2020.3.17f1 | Passed | Passed |
| 2020.3.48f1 | Passed | Passed |
| 2022.3.62f3 | Passed | Passed |
| 2023.2.20f1 | Passed | Passed |
| 6000.0.85f1 | Passed | Passed |
| 6000.4.8f1 | Passed | Module missing |
| 6000.6.6f1 | Passed | Passed |

The two oldest versions were built through their already-activated, running Editors.
The other versions used isolated batch fixtures. The two unavailable IL2CPP modules
were downloaded by Unity's installer, but installation stopped when administrator
approval timed out. They remain untested; this is not a compiler failure.

Offline authentication and transaction messages are **synthetic protocol fixtures**
passed through the actual native SDK and managed delegates. No socket is started
for these messages, and no viewer purchase, charge, refund, or live Bits transaction
is performed. Those callback results establish native ABI/stripping compatibility.

## Live PIN and authenticated reconnect

**All 14 available combinations in the table passed the complete live sequence**
against the final SDK commit. Each run used a separate fresh configuration PIN.

The live probe uses a separate new SDK instance and its normal production WSS flow.
It awaits connection, submits a fresh Twitch configuration PIN, checks the public
authentication callback and native authenticated state, refreshes, forces an
authenticated socket drop, waits for automatic reconnect/native reauthentication,
then stops, reopens, refreshes again, and disposes from the final callback.

Fault injection calls the existing connection's `Abort` through reflection. A
read-only queue observation checks only response action/error metadata before
the original response is dispatched by `SDK.Update`. No replacement connection,
fabricated live response, private token extraction, or private native cleanup is used.
JWTs and refresh tokens are held in memory and excluded from reports. PIN age is
measured from the browser reload request, rather than an asserted server issue time.

Final per-player results are recorded in `Results/Players/`. Codes are generated
separately for each live run: reusing a code after another successful link produced
an authentication rejection even before the visible five-minute expiry.

## Bounded lifecycle stress

Four endpoint configurations run two batches of 1,000 native disposal and actual
manager enable/disable cycles, with garbage collection and external Windows process
measurements at baseline, midpoint, and completion. The manager's SDK must be
disposed on disable and recreated on enable; scene unload must dispose its SDK.

| Player | Private bytes: baseline / midpoint / end | Windows handles |
|---|---|---|
| 2020.3.0f1 Mono | 141,242,368 / 141,242,368 / 141,320,192 | 390 / 390 / 390 |
| 2020.3.17f1 IL2CPP | 144,084,992 / 144,084,992 / 144,084,992 | 393 / 393 / 393 |
| 6000.6.6f1 Mono | 158,334,976 / 158,605,312 / 158,642,176 | 398 / 398 / 398 |
| 6000.6.6f1 IL2CPP | 147,243,008 / 147,148,800 / 147,148,800 | 361 / 361 / 361 |

The external measurements are authoritative. Unity Mono's in-player Process API
returned zero metrics; IL2CPP threw `NotSupportedException`. The helper now handles
that limitation. These rapid allocation/lifecycle checks do not establish the
absence of leaks during hours of authenticated gameplay or sustained event traffic.

## Transport and certificate checks

The final SDK also repeats the 29 local transport/lifecycle regressions on Unity
2020.3.48f1 and 6000.6.6f1. These cover fragmentation, Unicode, dropped partial
messages, reconnect notification, blocked receive, connect/TLS/backoff cancellation,
invalid upgrade responses, retry/reopen, pending authentication handles, and cleanup.

An ephemeral self-signed localhost certificate, with matching IPv4 SAN, is served
over TLS 1.2 by a loopback listener. No certificate is installed in a trust store.
Both Editors reject it with `AuthenticationException`, disconnect, and join stop.
Dedicated High-stripping **Mono and IL2CPP players in both versions also passed**.
They distinguish certificate rejection from a configuration initialization failure,
and verify disconnect and awaited stop. Server traces confirm the test certificate
was presented; this was not merely a connection-refused result.

## Distribution

Clean installation is checked on Unity 2020.3.48f1 and 6000.6.6f1 through both:

- Git URL pinned to the SDK commit above.
- A candidate source ZIP, extracted and installed through its `package.json`.

The checks cover consumer assembly compilation, dependency resolution and Unicode
struct marshalling. Additional High-stripping Mono players are built from the clean
Unity 2020 ZIP install and Unity 6.6 Git install, exercising package-delivered linker
preservation. **Both additional players passed the complete live PIN/refresh,
authenticated reconnect, reopen and disposal sequence**, for 16 live player runs
in total. The ZIP is a release candidate; the existing `v1.0.0-rc` asset has not
been replaced and no new release has been published.

## Reproduction

From the repository, in PowerShell:

```powershell
& './Tools~/Test-PlayerBuild.ps1' -UnityVersion 6000.6.6f1 -OutputRoot D:/GatewayValidation
& './Tools~/Test-PlayerRuntime.ps1' -Player D:/GatewayValidation/builds-6000.6.6f1/Mono/GatewayValidation.exe -OutputDirectory D:/GatewayValidation/mono-offline
& './Tools~/Test-PlayerRuntime.ps1' -Player D:/GatewayValidation/builds-6000.6.6f1/IL2CPP/GatewayValidation.exe -OutputDirectory D:/GatewayValidation/il2cpp-soak -Mode soak -Cycles 2000
```

Use a fresh output directory for each runtime run. For `-Mode live`, atomically
publish `pin-request.json` into the output directory while `state.json` reports
`waiting-for-pin`: fields are `pin` and UTC `generatedAtUtc`. Alternatively provide
`-PinRequestPath` pointing to a fresh request file. Never put real JWTs or refresh
tokens into fixture files. Build/source/probe identities and executable/native
hashes accompany the retained JSON snapshots.

For certificate reproduction, generate a one-day self-signed TLS certificate with
`subjectAltName=DNS:localhost,IP:127.0.0.1` and serve it using OpenSSL `s_server`,
bound to an unused `127.0.0.1` port, with `-tls1_2 -quiet -state -ign_eof`.
The player build runner includes `GatewayCertificatePlayerValidation.cs`.
Launch that player with `-batchmode -nographics`,
`-gatewayCertificateEndpoint wss://127.0.0.1:PORT/`, and
`-gatewayCertificateResult ABSOLUTE_RESULT_JSON_PATH` instead of
`-gatewayPlayerSettings`. Require a passed result, an initialized configuration
system, rejected authentication, awaited stop, and a disconnected transport.
Stop the owned OpenSSL process afterward. The Editor equivalent is
`GatewayCertificateValidation.Run` in an owned fixture with the same endpoint flag.

## Cleanup and initial attempts

Both user projects were left open on their original clean LEGO Tutorial scenes.
Package manifests, lockfiles, PlayerSettings, GraphicsSettings and EditorBuildSettings
were restored byte-for-byte from backups, with hashes checked again after Editor
refresh. The four temporary cached PlayerSettings values were restored too, and
only the owned probe assets/job files were removed.

The initial IL2CPP process-metrics exception and the initial empty-scene build
helper exception were harness failures, resolved before the final matrix. An initial
IPv6 `localhost`/IPv4-only TLS-listener mismatch was rerun using matching IPv4 SAN
and endpoint. The reproduced High-stripping Mono TLS failure was the SDK issue
fixed in this PR; its connection diagnostic is retained separately from final passes.

## Limits

This matrix covers these eight installed releases on Windows x64. Other Unity
patches, macOS/Linux, WebGL, sandbox authentication, authenticated long-duration
traffic and real viewer-originated poll/action/Bits transactions are not established
by these results. Direct connections still have no HTTP proxy or redirect support.
The two missing IL2CPP modules must be installed before their player runs can pass.
