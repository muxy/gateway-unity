# Documentation verification

Checked October 26, 2026, against the local Unity SDK compatibility checkout.

## Passed

- Both downloadable demo scripts compiled with the current SDK runtime and
  prefab sources using Unity 2020.3.0f1's Roslyn compiler, C# 8, and .NET
  Standard 2.0 references. UI references came from the 2020.3.48f1 package
  fixture. There were no example errors; five existing SDK warnings remained.
- The local package was installed in a separate Unity 6000.6.6f1 Windows project.
  GatewayDemo compiled with the manager, authentication UI, Health game text,
  healthpack action, and dynamic OnHealthpack callback. The local button changed
  Health from 50 to 60.
- The prefab EventSystem needed InputSystemUIInputModule in this Unity 6 project
  before the PIN field would accept typing. The replacement was applied outside
  Play mode and the scene saved. An extra scene EventSystem caused a duplicate
  warning and was removed. Active Input Handling was set to Both for IMGUI input.
- The broadcaster's Twitch Gateway 1.0.0 extension was active in Overlay 1.
  Configure initially rendered an empty modal; after the broadcaster reopened
  it, Configuration PIN, Linked Games, and Actions controls appeared. The PIN
  was masked with the eye control before taking a documentation screenshot.
- The scene's Game ID was changed to gamelink-testing, an exact ID listed under
  this broadcaster's Linked Games. A fresh PIN authenticated with the live
  Gateway service; the prefab and demo both reported success. A mistyped
  character previously returned 403 Forbidden / Invalid authorization token.
- While authenticated, the demo submitted a 30-second poll. With no viewer
  votes, the final callback reported Poll ended without a usable winner.
- After stopping and restarting Play mode, the prefab authenticated from its
  saved refresh token and returned to the success state without another PIN.
- Both embedded full-script examples match their downloadable C# files.
- JavaScript syntax and registered screenshot files passed checks. The page
  has eleven image slots, nine with real captures. The local browser preview
  loaded all nine images without broken images; two placeholders remain.
- The Pages workflow uploads Documentation~ and verifies synchronized examples.

## Not established by this delivery

- A real viewer action, a submitted viewer vote, a standalone player run, and
  broader network/lifecycle behavior still need checking. The broadcaster's
  viewer page showed an offline channel, with no active overlay to use.
  The two corresponding image placeholders remain clearly marked.
- GitHub Pages configuration or deployment. The workflow and publishing
  instructions are prepared locally; no remote settings were changed.
- A wider set of viewport sizes and the code-copy control remain to be checked.

A runtime debug logging change was added after the live test exposed a PIN in
Unity Console output. The package recompiled in Unity without new errors;
future authentication logs should still be checked for redaction before shipping.
