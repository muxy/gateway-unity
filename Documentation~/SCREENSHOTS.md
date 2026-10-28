# Screenshot capture brief

The guide has eleven image slots. Seven Unity captures and two Twitch captures
were taken on October 26, 2026. The two remaining captures need a live Twitch
viewer action and a submitted poll vote.
Use `Gateway Unity Demo`, action ID `healthpack`, action name `Spawn Healthpack`,
and a `Health` game text starting at `50`. Use the supplied prefab demo script.

## Essential captures

| File | Frame and exact state | What it teaches |
| --- | --- | --- |
| `01-package-manager.jpg` · captured | Package Manager, Muxy Gateway selected; package name, version, local source indicator, and dependencies visible. | Confirms the package is installed and dependencies resolved. |
| `02-scene-wiring.jpg` · captured | Unity Hierarchy with `MuxyGatewayManager`, `MuxyGatewayAuthenticateUI`, and `GatewayDemoController`; authentication Inspector has its Gateway Manager reference assigned. The prefab contains the EventSystem. | Shows the scene objects and where to wire the manager. |
| `02-input-module.png` · captured | The authentication prefab's EventSystem selected outside Play mode; Inspector shows Input System UI Input Module. | Shows the Unity 6 input fix that made the PIN field clickable. |
| `03-extension-active.png` · captured | Twitch My Extensions showing Gateway active in Overlay 1. The frame excludes account and other extension details. | Shows the broadcaster-side activation state. |
| `03-extension-pin.png` · captured | Twitch Gateway configuration with Get New Code and Linked Games visible; PIN masked using the eye control. | Shows where to obtain a PIN and verify the manager's exact Game ID. |
| `04-authenticated.png` · captured | Unity Game view after a successful live PIN callback, with the auth prefab success group and demo authenticated message. | Defines what successful authentication looks like. |
| `04-local-test.jpg` · captured | Unity Game view after clicking `Local test: add 10 health`, with Health: 60 and an empty Console. The PIN form remains unauthenticated. | Verifies only the local demo behavior and input setup. |
| `05-action-inspector.jpg` · captured | Manager Inspector with the healthpack action expanded: ID, category Help, state Available, impact 1, name, description, icon, count 65535, and the `OnHealthpack` event binding. | Shows the complete action setup and callback wiring. |
| `06-action-roundtrip.png` | Viewer extension alongside Unity Game view. Show Spawn Healthpack and Health = 60, plus the game's `Health: 60` and viewer-action status. Ideally provide a before frame at 50 and an after frame at 60, taken after a real viewer action. | Proves both directions of the integration. |
| `07-poll-no-votes.png` · captured | Authenticated Unity scene after a 30-second poll, showing `Poll ended without a usable winner.` | Proves final no-vote callback handling; it does not prove viewer voting. |
| `07-poll-result.png` | Viewer poll with Forest / Desert / Ice Cave and a submitted vote, alongside Unity's final `Poll winner: ...` message. Provide separate during/after originals if needed. | Shows the viewer voting flow and completed result. |

## Optional captures

- Invalid or expired PIN error and the retry UI; cover the entire input.
- The code-only demo's initial PIN field/status panel.
- A standalone desktop player after receiving a real viewer action; include the
  Unity build target/backend settings separately if documenting platform setup.
- Notifications only after validating/customizing the bundled notification widget.
- A polished poll widget only after validating its countdown and no-vote behavior.

No stock art, hero illustration, or rendered fake product screenshot is needed.
The page already includes a small HTML connection diagram.

## Capture requirements

- PNG or JPEG, native display resolution, ideally at least 1600 pixels wide for a full
  application frame. Narrow Inspector crops may be smaller if the text is sharp.
- Use the same Unity theme, test scene, account display convention, and game labels.
- Keep UI text legible at a normal browser zoom. Expand panels rather than fitting
  an entire desktop into the shot. Provide originals without arrows or annotations
  so any final callouts can be added consistently.
- Remove unrelated windows, notifications, browser bookmarks, private paths,
  account/email details, PINs, JWTs, and refresh tokens. Cover secrets completely;
  do not rely on a blur or the fact that a PIN may have expired.
- A short capture or two source frames is welcome for 06 and 07; the published
  page uses a static proof image so readers can inspect it at their own pace.

## Add approved images to the site

Place images in `Documentation~/assets/screenshots/` using the filenames above.
Register only images that actually exist in `assets/screenshots.json`:

```json
{
  "01-package-manager": {
    "file": "01-package-manager.jpg",
    "alt": "Unity Package Manager showing Muxy Gateway installed from disk with its dependencies resolved."
  },
  "02-scene-wiring": {
    "file": "02-scene-wiring.jpg",
    "alt": "Gateway demo scene Hierarchy and authentication Inspector with the scene manager assigned."
  }
}
```

Use meaningful alt text describing the actual state shown. The JavaScript replaces
each matching placeholder after its image loads and keeps the explanatory caption.
Unregistered or failed images leave the text placeholder intact. No build is needed.
