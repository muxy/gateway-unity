[Unreleased]
- Preserve reflection-loaded Mono TLS configuration types through a package Editor linker callback; fixes WSS connection failures in High-stripping Unity 6 Mono players.
- Use WSS/TLS 1.2 for production and sandbox connections, with cancellable owned TCP/TLS streams and standard WebSocket framing.
- Replace async raw threads with tracked send/receive tasks, bounded reconnect delays, fresh sockets on reopen, and main-thread native dispatch.
- Add awaitable stage/stop APIs and deterministic SDK disposal; release lifecycle subscriptions, native instances, pending authentication handles, and replaced callback handles. Dispose the prefab manager's SDK on disable/deauthentication.
- Disable native diagnostic forwarding by default and expose explicit opt-in logging plus connection status/errors.
- Expand all 13 target-typed constructors for Unity 2020.3's C# 8 compiler.
- Preserve UTF-8 marshalling attributes without relying on the LPUTF8Str enum name missing from Unity 2020.3's .NET Standard 2.0 reference assemblies.
- Declare TextMeshPro, uGUI, and image-conversion dependencies. Newer Editors resolve their built-in TMP compatibility package and uGUI implementation.
- Add the explicit UnityEngine.UI assembly reference and isolate the UnityEditor import from standalone builds.
- Replace the beta minimum-version suffix with the intended Unity 2020.3.0f1 stable target.
- Preserve the Muxy.Gateway assembly, public type ownership, and existing asset GUIDs.
- Add an isolated package-import/custom-assembly compilation harness and record scoped validation results.
- Record activated Unity 2020.3.0f1/2020.3.17f1 live-Editor compilation and production WSS PIN/refresh passes, alongside the remaining batch licensing limitation; extend clean package checks to Unity 6000.0.85f1 and 6000.4.8f1.

[0.1.0] 2022/10/04
Initial release
