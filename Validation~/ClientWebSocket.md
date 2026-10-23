# Standard ClientWebSocket transport

This implementation and its evidence are reviewed separately from the Unity
compatibility prerequisite. See [transport/lifecycle review scope](Transport-Lifecycle-Review.md).

Validation date: October 23, 2026 (America/Chicago; JSON timestamps are UTC).
Tested SDK revision: `ef29e0fd8febb2eed7948de3840d5f77cc64d4a3`.
Evidence and source/binary identities: [index](Results/ClientWebSocket/index.json).

## Decision and implementation

Use the runtime's `ClientWebSocket` for TLS, HTTP upgrade, and WebSocket protocol
handling. Remove the shipping transport's custom `TcpClient`, `SslStream`, request
construction, response parser, and `WebSocket.CreateClientWebSocket` factory.
Normal certificate validation remains enabled; no validation callback or global TLS
setting is changed. Production and sandbox helpers still require WSS.

Connection establishment has a five-second SDK deadline, including waiting for a
handshake slot. Cancellation invokes `Abort`; cleanup disposes the client. Established
send/receive work is still tracked and joined. Native dispatch and reconnect callbacks
remain on the owning thread. Initial timeout is reported as `TimeoutException` through
the awaited connection API and `LastConnectionError`.

### Accepted runtime limitation

Some tested Unity runtimes leave their internal TLS connection task pending after
cancellation, abort, and disposal. Stop/dispose returns without awaiting that internal
task. A late observer catches its eventual exception and closes an already disposed
client again if the runtime completes later. It retains the connection object, not an
SDK/native instance.

A shared semaphore allows **four in-flight handshakes per managed domain**, including
residual attempts. Successful or failed completion releases a slot. Established
connections do not consume slots. If all four operations remain pending, subsequent
attempts expire at their own deadline without opening a fifth TCP connection. This
also limits simultaneous healthy handshakes to four; callers must retry if their
connection attempt times out waiting for a slot.

This is a bounded limitation, not a guarantee that Unity releases every OS socket on
disposal. The bound does not persist across Editor domain reloads. A permanently
silent peer can consume a slot until that operation eventually completes or the
process exits. Ordinary socket closure and process exit can reclaim resources; the
SDK does not rely on an idle TCP connection automatically expiring.

## Independent cancellation reproduction

`GatewayClientWebSocketValidation` uses only `ClientWebSocket` and a loopback TCP
listener, with no Gateway SDK instance or replacement protocol implementation. It
waits for request bytes before cancelling, ensuring the peer accepted the connection
and the HTTP request or TLS ClientHello arrived. The peer deliberately sends nothing.
The probe records task completion after one second each of cancellation, abort, and
disposal, then closes the peer and waits up to five seconds.

| Runtime | Silent HTTP upgrade | Silent TLS negotiation | After peer closure |
|---|---|---|---|
| [2020.3.48f1](Results/ClientWebSocket/2020.3.48f1-standalone-client.json) | Completed after cancellation | Still pending after cancel/abort/dispose | Completed |
| [6000.6.6f1](Results/ClientWebSocket/6000.6.6f1-standalone-client.json) | Completed after cancellation | Still pending after cancel/abort/dispose | Completed |

Thus the independently reproduced defect is a **TLS-negotiation cancellation**
failure. These observations do not establish a failure rate on real networks, or an
HTTP-upgrade cancellation defect. A `passed` standalone-probe report means the
observation sequence completed; it does not mean the runtime cancelled TLS promptly.

## Current regression results

### Editor compilation and lifecycle

All six fresh fixtures passed package/consumer compilation, UTF-8 marshalling, public
type ownership, linker registration, and **46 local transport/lifecycle assertions**.

| Unity | Package import | Transport lifecycle |
|---|---|---|
| 2020.3.48f1 | [Passed](Results/ClientWebSocket/2020.3.48f1-package.json) | [Passed](Results/ClientWebSocket/2020.3.48f1-lifecycle.json) |
| 2022.3.62f3 | [Passed](Results/ClientWebSocket/2022.3.62f3-package.json) | [Passed](Results/ClientWebSocket/2022.3.62f3-lifecycle.json) |
| 2023.2.20f1 | [Passed](Results/ClientWebSocket/2023.2.20f1-package.json) | [Passed](Results/ClientWebSocket/2023.2.20f1-lifecycle.json) |
| 6000.0.85f1 | [Passed](Results/ClientWebSocket/6000.0.85f1-package.json) | [Passed](Results/ClientWebSocket/6000.0.85f1-lifecycle.json) |
| 6000.4.8f1 | [Passed](Results/ClientWebSocket/6000.4.8f1-package.json) | [Passed](Results/ClientWebSocket/6000.4.8f1-lifecycle.json) |
| 6000.6.6f1 | [Passed](Results/ClientWebSocket/6000.6.6f1-package.json) | [Passed](Results/ClientWebSocket/6000.6.6f1-lifecycle.json) |

Checks include fragmentation, reconnect, partial-message disposal, invalid upgrade
rejection, stop/reopen, pending HTTP/TLS cancellation, the five-second deadline,
four residual TLS attempts, denial of a fifth TCP connection, and slot recovery after
peer closure. All six retained four residual operations in that deliberate scenario.
Maximum observed SDK stop time was approximately 50 ms per suite, not a general
latency guarantee or proof of immediate internal socket release.

The [Unity **2020.3.0f1 bundled compiler**](Results/ClientWebSocket/minimum-compiler.json) also passed the current SDK source with C# 8
and .NET Standard 2.0 references under Editor and standalone symbols. Two existing
nullable-annotation warnings remain. This is a compiler check, not a minimum-version
Editor or player runtime pass for this revision.

### Windows x64 High-stripping players

| Unity | Backend | Build | Production WSS connect/stop/reopen | Untrusted certificate | Offline callbacks/lifecycle |
|---|---|---|---|---|---|
| 2020.3.48f1 | Mono | [Passed](Results/ClientWebSocket/2020.3.48f1-Mono-build.json) | [Passed](Results/ClientWebSocket/2020.3.48f1-Mono-connection.json) | [Rejected](Results/ClientWebSocket/2020.3.48f1-Mono-certificate.json) | [Passed](Results/ClientWebSocket/2020.3.48f1-Mono-offline.json) |
| 2020.3.48f1 | IL2CPP | [Passed](Results/ClientWebSocket/2020.3.48f1-IL2CPP-build.json) | [Passed](Results/ClientWebSocket/2020.3.48f1-IL2CPP-connection.json) | [Rejected](Results/ClientWebSocket/2020.3.48f1-IL2CPP-certificate.json) | [Passed](Results/ClientWebSocket/2020.3.48f1-IL2CPP-offline.json) |
| 6000.6.6f1 | Mono | [Passed](Results/ClientWebSocket/6000.6.6f1-Mono-build.json) | [Passed](Results/ClientWebSocket/6000.6.6f1-Mono-connection.json) | [Rejected](Results/ClientWebSocket/6000.6.6f1-Mono-certificate.json) | [Passed](Results/ClientWebSocket/6000.6.6f1-Mono-offline.json) |
| 6000.6.6f1 | IL2CPP | [Passed](Results/ClientWebSocket/6000.6.6f1-IL2CPP-build.json) | [Passed](Results/ClientWebSocket/6000.6.6f1-IL2CPP-connection.json) | [Rejected](Results/ClientWebSocket/6000.6.6f1-IL2CPP-certificate.json) | [Passed](Results/ClientWebSocket/6000.6.6f1-IL2CPP-offline.json) |

`GatewayConnectionValidation` uses public SDK production connection and stop APIs,
without submitting credentials. Both representative Editors passed the same WSS
connection check. Offline player checks exercised the bundled native callbacks,
UTF-8 metadata, synthetic authentication/poll/action/Bits fixtures, scene cleanup, and
100 disposal/component cycles each. Synthetic authentication is not live linking.

Certificate checks used a self-signed TLS 1.2 listener on loopback with matching
localhost/IPv4 SANs and unchanged trust stores. All four players reported nested
`AuthenticationException` through `WebSocketException`, disconnected, and stopped.
The server recorded four peer disconnects during TLS. Exported summaries omit bulky
stack traces; raw report hashes are retained.

Builds began from `5b59182` plus working-tree changes committed as `ef29e0f`.
Build/import source hashes match the tested transport exactly. The build identity
files retain that original base commit rather than rewriting provenance.

## Live PIN linking through the standard client

After the initial report, the user supplied a fresh PIN. The existing rebuilt
**6000.6.6f1 IL2CPP Windows x64 High-stripping player** passed
[the complete live flow](Results/ClientWebSocket/6000.6.6f1-IL2CPP-live.json) through
the SDK's normal production WSS transport:

- PIN authentication and refresh through public callbacks.
- Forced authenticated connection drop, automatic reconnect, and native
  reauthentication through normal owning-thread dispatch.
- Awaited stop, reopen, another refresh, and SDK disposal from the callback.
- All callbacks on the owning thread, plus the probe's offline native fixtures and
  100 disposal/component cycles.

PIN authentication completed at **10:37:08 CDT on October 23, 2026**. The report's
`pinRequestedAtUtc` is when the user's supplied code was published to the test
runner, not its original issuance time; `pinAgeSeconds` measures only that interval
to SDK submission. No PIN, JWT, or refresh-token value is included in the result.
The player SHA-256 matches the current evidence index. This adds one current live
authentication pass; it does not rerun the earlier 16-player authentication matrix.

## Reproduction

From the repository root, using owned disposable fixtures:

```powershell
# Standalone runtime observation (no Gateway SDK instance).
.\Tools~\Test-Transport.ps1 -UnityVersion 2020.3.48f1 -Probe Client
.\Tools~\Test-Transport.ps1 -UnityVersion 6000.6.6f1 -Probe Client

# Shipping transport's local regression suite.
.\Tools~\Test-Transport.ps1 -UnityVersion 2020.3.48f1
.\Tools~\Test-Transport.ps1 -UnityVersion 6000.6.6f1

# High-stripping players, with connection/certificate/offline probes included.
.\Tools~\Test-PlayerBuild.ps1 -UnityVersion 2020.3.48f1
.\Tools~\Test-PlayerBuild.ps1 -UnityVersion 6000.6.6f1
```

For a credential-free player connection check, launch its executable with
`-batchmode -nographics -gatewayConnectionResult "<new absolute report path>"`.
For certificate rejection, use the existing certificate probe arguments against an
untrusted local TLS listener. No trusted root or validation override is needed.

## Remaining scope

The connected Twitch browser loaded the dashboard configuration modal but not its
extension UI/PIN, so the current live check used a code supplied directly by the
user. Current PIN/refresh/reconnect authentication is established only for the
6000.6.6f1 IL2CPP player above. Earlier live results and the 16-player matrix in
[the October 20 report](Player-Compatibility.md) remain historical evidence for
their explicitly named commit. Credential-free connection tests on the other
configurations do not establish authentication there.

Other operating systems, every Unity patch, current runtime passes on 2020.3.0f1 and
2020.3.17f1, the full 16-player matrix, sandbox authentication, real purchases,
proxy/redirect behavior, domain-reload accumulation, long-duration authenticated
traffic, and immediate OS socket reclamation are not established by this revision.
