# Transport/lifecycle PR #16: post-split validation

Date: October 23, 2026 (America/Chicago; JSON timestamps are UTC).
Tested shipping source: `a35baf6dc34deaba74a686d8e4f9630fdd630ae3`.
No shipping SDK code changed during this run. See
[source and binary identities](Results/Transport-Rerun-20261023/index.json).

## Fresh stripped player builds

| Unity | Backend | Windows x64 High-stripping build |
|---|---|---|
| 2020.3.48f1 | Mono | [Passed](Results/Transport-Rerun-20261023/2020.3.48f1-Mono-build.json) |
| 2020.3.48f1 | IL2CPP | [Passed](Results/Transport-Rerun-20261023/2020.3.48f1-IL2CPP-build.json) |
| 6000.6.6f1 | Mono | [Passed](Results/Transport-Rerun-20261023/6000.6.6f1-Mono-build.json) |
| 6000.6.6f1 | IL2CPP | [Passed](Results/Transport-Rerun-20261023/6000.6.6f1-IL2CPP-build.json) |

## Fresh live authentication

The older Mono and newer IL2CPP players were selected for live regression checks.
Both used the public SDK production WSS flow and a separate fresh PIN.

| Unity/backend | PIN and refresh | Forced reconnect and reauthentication | Stop/reopen, refresh, callback disposal |
|---|---|---|---|
| [2020.3.48f1 Mono](Results/Transport-Rerun-20261023/2020.3.48f1-Mono-live.json) | Passed | Passed | Passed |
| [6000.6.6f1 IL2CPP](Results/Transport-Rerun-20261023/6000.6.6f1-IL2CPP-live.json) | Passed | Passed | Passed |

The user supplied both PINs after the Mac Codex in-app browser reference was
reported unavailable by the remote PC's browser tools. The tools exposed only PC
Chrome, where the configuration UI did not render. These were user-supplied live
codes, not codes automatically retrieved by the agent from the Mac browser.
The original issuance time is unknown: `pinRequestedAtUtc` records publication to
the player runner, and `pinAgeSeconds` measures that interval to submission.
No PIN, JWT or refresh-token value is included in reports.

All authentication callbacks ran on the owning thread. Each live player also passed
native callback fixtures, UTF-8 metadata, scene cleanup, and 100 disposal/component
cycles. The forced drop invokes the real connection's Abort method; reconnect and
reauthentication then use normal SDK work and owning-thread Update dispatch. There
is no replacement validation transport, synthetic live authentication reply, or
private native destruction. Process records identify the executed player binaries.

## Fresh local transport/lifecycle checks

Both [2020.3.48f1](Results/Transport-Rerun-20261023/2020.3.48f1-lifecycle.json) and
[6000.6.6f1](Results/Transport-Rerun-20261023/6000.6.6f1-lifecycle.json) passed package
imports and **46 assertions** each, including fragmentation, reconnect, discarded
partial data, cancellation during HTTP/TLS setup, deadline/handshake-budget
enforcement, preventing a fifth TCP connection, recovery of slots after peer
closure, stop/reopen, prefab recreation, and native/callback-handle cleanup.
Maximum observed SDK stop time was approximately 50 ms in each local suite, not
a general latency guarantee or proof of immediate runtime socket release.

## Reproduction and remaining scope

Use `Tools~/Test-PlayerBuild.ps1`, `Tools~/Test-PlayerRuntime.ps1 -Mode live` with a
fresh PIN request, and `Tools~/Test-Transport.ps1` for the two listed versions.
The detailed command contracts remain in the existing player and standard-client
reports. Fixtures and logs are retained outside the package.

Only two of the four fresh players received live PINs. This rerun does not repeat
the full 16-player matrix, certificate rejection, 2,000-cycle stress checks, all
platforms/patches, sandbox authentication, real purchases, proxy/redirect checks,
or a long-duration soak. Earlier results retain their original commit identities.
The existing four-handshake cap is per managed domain; the accepted residual TLS
limitation and lack of a bound across domain reloads remain unchanged.
