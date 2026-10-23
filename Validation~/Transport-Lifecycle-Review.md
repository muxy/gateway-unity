# Transport and SDK lifecycle review

Split date: October 23, 2026. This branch is stacked on the compatibility-only
[PR #15](https://github.com/muxy/gateway-unity/pull/15). Review and merge the
compatibility prerequisite first, then retarget this PR to `main` if necessary.

The subsequently requested fresh builds, local lifecycle checks and live
authentication results are in [the post-split rerun report](Transport-Rerun-20261023.md).

## Changes for this review

The runtime difference from the compatibility branch is limited to:

- `Runtime/MuxyWebsocketTransport.cs`: WSS stage helpers, tracked connection/send/
  receive tasks, owned reconnect flow, main-thread payload extraction and reconnect
  notification, standard ClientWebSocket, cancellation/deadline handling and limits.
- `Runtime/MuxyGateway.cs`: awaitable connection/stop APIs, status properties,
  deterministic native/callback disposal, callback-handle ownership, per-instance
  action callbacks, disposed-use checks and opt-in diagnostics.
- `Prefabs/MuxyGatewayManager.cs`: disposing the SDK on disable/deauthentication,
  creating a fresh instance on enable, and guarding authentication-event reentrancy.

The compiler syntax, marshalling declarations, UI dependencies, assembly references,
Editor import guard and TLS linker preservation are inherited from PR #15 rather
than proposed again in this diff.

## Behavior decisions to review explicitly

| Area | Proposed behavior |
|---|---|
| Connection security | Production/sandbox require WSS with normal runtime TLS and certificate validation. |
| Dispatch | `SDK.Update()` on the owning thread handles payload extraction, incoming dispatch and reconnect notification. |
| Stop and disposal | Abort/dispose the connection, join SDK tasks, allow reopening after stop, and reject reuse after disposal. A graceful close exchange is not awaited. |
| Prefab lifetime | Disable releases the SDK/native instance; enable creates another instance. |
| Direct transport APIs | `SendMessages`/`ReceiveMessage` become update-pump aliases; endpoint/SDK changes require stopping first. |
| Timeouts and reconnect | Enforce the existing five-second connection intent even when runtime cancellation stalls; use cancellable reconnect delays capped at 30 seconds. The delay schedule changes. |
| Resource limits | Add a 16 MiB incoming-message limit and four in-flight handshakes per managed domain, including residual TLS operations. |
| Callbacks and logging | Catch authentication callback exceptions at the native boundary; release replaced handles; disable native diagnostics by default. |

## Validation provenance

The runtime, prefab, Editor, manifest and assembly-definition source is identical
to the previously tested pre-split snapshot
`e50a213acae8eb307b5d1e0db7d6568d93e862a3`. The split changes branch organization
and review documentation. Tests were not rerun solely for the split.

See [the standard-client report](ClientWebSocket.md) and its
[evidence index](Results/ClientWebSocket/index.json):

- Six Editor versions passed package compilation and 46 local lifecycle assertions.
- Four High-stripping Windows players (2020.3.48f1 and 6000.6.6f1, Mono/IL2CPP)
  passed builds, production WSS connect/stop/reopen, untrusted certificate rejection,
  native callback fixtures and 100 disposal/component cycles each.
- The 6000.6.6f1 IL2CPP player passed fresh PIN linking, refresh, authenticated
  disconnect/reconnect/reauthentication, stop/reopen and callback disposal.
- A standalone runtime probe reproduced stalled TLS cancellation on 2020.3.48f1
  and 6000.6.6f1. The SDK deadline returns while the internal operation may remain;
  the shared handshake budget bounds attempts within a managed domain. It does not
  establish immediate OS socket release or a bound across domain reloads.

The [October 20 player matrix](Player-Compatibility.md) and
[earlier transport report](Shipping-Transport.md) identify their own historical
commits and transport implementations. They are retained as historical evidence,
not current-head passes of every configuration. Platform coverage and other
remaining limits are recorded in those reports.
