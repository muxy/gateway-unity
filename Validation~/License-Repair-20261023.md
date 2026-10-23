# Unity 2020.3 license repair and package rerun

Date: October 23, 2026. Tested SDK commit:
`7446772beafa11a08e2ca25704c38d248349e846`.
Shipping SDK source is unchanged from
`e2e85488d436f3ec29355c825bae71d087636532`.

## License repair

The machine had an active Unity Personal entitlement license, but no legacy
`Unity_lic.ulf` file. Both early Editors connected to their bundled licensing
client and then exited before package import with a license activation failure.

Refreshing existing entitlements and reactivating Personal through the installed
Unity CLI succeeded, but neither created the legacy file; both Editor retries
remained blocked. No SDK changes were made for those failures.

Restarting the unresponsive Unity Hub restored its interface. With the user's
explicit approval of the Personal terms, Settings > Licenses > Add license >
Get a free personal license > Agree and get personal edition license created
the legacy ULF file. Both subsequent batch imports passed. Existing license
files were not deleted or edited. No license files, account identifiers,
credentials, or raw licensing logs are included in this evidence.

## Fresh results after Hub activation

Each run used a new disposable project requesting only the local Gateway package.

| Unity | Result | Evidence |
|---|---|---|
| 2020.3.0f1 | Passed | [Report](Results/License-Repair-20261023/2020.3.0f1-package.json) |
| 2020.3.17f1 | Passed | [Report](Results/License-Repair-20261023/2020.3.17f1-package.json) |

Both checks passed SDK and separate consumer compilation, six public types
retaining `Muxy.Gateway` assembly ownership, accented/Japanese/emoji UTF-8 struct
round trips, and package linker callback registration. Both resolved TextMeshPro
3.0.6 and uGUI 1.0.0 through the package's declared dependencies. Neither created
a native SDK instance or attempted network authentication.

The [evidence index](Results/License-Repair-20261023/index.json) records result and
raw import-log SHA-256 hashes, SDK commit identity, and the earlier unsuccessful
refresh attempts. Original blocked reports remain available as historical evidence.
Each successful report also records the SDK source hashes used in its run.

## Reproduction

With the Editors activated on the test machine:

```powershell
.\Tools~\Test-PackageImport.ps1 -UnityVersion 2020.3.0f1 -OutputRoot ..\.gateway-validation\license-repair-repeat
.\Tools~\Test-PackageImport.ps1 -UnityVersion 2020.3.17f1 -OutputRoot ..\.gateway-validation\license-repair-repeat
```

## Limits

These results resolve the two package-import license blocks in PR #15. They do
not establish player build/runtime passes on these exact early patches, live PIN
linking, transport/lifecycle behavior, or other platforms. The four previously
passed stripped-player checks on 2020.3.48f1 and 6000.6.6f1 were not rerun for
this license-only follow-up. See the [combined compatibility report](Compatibility-Rerun-20261023.md).
