# Install Muxy Hypeline in my Unity project

Use the open Unity project, or the project path I provide, and carry out the
Muxy Hypeline Getting Started guide:
https://muxy.github.io/gateway-unity/

Read the current guide before making changes. Follow its steps 1–7 and use its
current package URL, examples, field names, and troubleshooting advice. The
visible in-game viewer-action alert is a required part of the integration.

## Project and working method

- Inspect the project's instructions, Unity version, active scene, input system,
  package manifest/lock, existing Hypeline components, and Git changes first.
  Use the project's matching Unity Editor. Preserve unrelated work, scene
  content, and existing .meta GUIDs. Do not assume Foxfire includes the SDK.
- Use the open project by default. If I need a starter, use the guide's Foxfire
  Arena repository matching my Unity version; do not replace an existing game.
- Reuse working configuration. Keep one active SDK owner and one compatible
  EventSystem. Do not overwrite a patched or different SDK version without
  first explaining the compatibility issue and necessary change.
- Follow the prefab walkthrough by default. If I request the C# alternative,
  use the guide's complete HypelineCodeDemo.cs in a separate test scene without
  the prefab manager/menu/alert. Keep its built-in viewer-action alert. Do not
  combine both paths or register duplicate fulfillment handlers.
- Use available Unity, filesystem, and browser tools. Make persistent scene
  changes outside Play mode and save them. If a UI operation needs a file-based
  equivalent, explain it and verify Unity imports the result. If a step needs
  human interaction or missing access, finish independent work and ask for the
  specific action needed. Do not invent a successful result.

## Complete the seven steps

1. **Install.** Install the SDK from the current guide's Git URL and import
   DemoMaterials. Resolve package and script errors before continuing. Record
   the package URL and resolved commit; a moving development branch is not a
   fixed release. Add the Muxy.Hypeline assembly reference if my assembly needs
   it. Inspect the actual installed package rather than assuming types exist.

2. **Set up the scene.** Reuse or add MuxyHypelineManager. For the tutorial use
   Game ID `gamelink-testing` only if it matches Linked Games in the broadcaster's
   extension configuration; otherwise use my assigned, matching Game ID. Set
   the game name and leave Theme blank for the default. Add MuxyHypelineMenu and
   the required MuxyHypelineInteractionAlert, both referencing that manager.
   Keep the alert active and readable over the game and broadcast HUD. Ensure
   one EventSystem with a compatible input module. Add Game Text Health = 50,
   icon `fa-solid:heart`, and the imported HypelineActionDemo with the same
   manager reference. Follow the guide's Active Input Handling advice for the
   sample's IMGUI controls, including any required Editor restart.

3. **Configure the action.** Add ID `healthpack`, name `Spawn Healthpack`,
   description `Add up to 10 health to the player.`, category `Help`, state
   `Available`, impact 1, icon `fa-solid:heart`, and count 65535. Bind the dynamic
   GameActionUsed callback to HypelineActionDemo.OnHealthpack exactly once.
   The alert listens to the same manager; it must not become another health
   or fulfillment handler. Save the scene.

4. **Test locally.** In Play mode, check the sample's local health button changes
   50 to 60. Separately run the alert's Test Alert preview: the viewer name and
   Spawn Healthpack must be visible, readable, and clear after about six seconds.
   Sound is optional. Label both checks as local previews, not Twitch delivery.
   The local health button does not emit a viewer-action alert. Restart Play
   mode to reset to 50 before the live test. For the C# alternative, exercise its
   local controls and inspect its required alert drawing/received-action path;
   still verify the alert with a real action in step 6.

5. **Connect Twitch.** Use the broadcaster channel/configuration I specify.
   Confirm the extension is installed, active, and linked to the same Game ID.
   Use a fresh six-character PIN from that configuration; it expires after five
   minutes. If I authorize use of the open configuration tab, request another
   code there when needed. Enter it in the game's Stream Interactivity panel,
   connect, and verify the connected status. Keep PINs and tokens out of source,
   commits, screenshots, and reports. Close the panel for the action test.
   Authentication alone does not prove action delivery.

6. **Verify a real round trip, including the alert.** Open the correct channel
   in a viewer session and confirm the game, Health 50, and Spawn Healthpack are
   visible. Use a zero-cost action when available; review the displayed cost and
   obtain authorization before spending. Take one real viewer action, or ask me
   to click it if browser access cannot do so. Watch the Unity game immediately
   and capture the timed in-game alert showing the viewer and action. Confirm
   sample health changes exactly once from 50 to 60, the received healthpack
   event is observed, the transaction gets a successful fulfillment
   acknowledgment, and the extension updates Health to 60. If broadcasting,
   also check the alert in the delayed Twitch video and account for stream
   latency. A changed Game Info value, a status line, an alert preview, or a
   submitted request alone does not complete this step. Health changing without
   a visible in-game viewer-action alert is a failed checkpoint. If any evidence
   cannot be obtained, mark that part unverified and explain the next check.

7. **Adapt for my game.** Explain that the sample updates its own health counter;
   it does not automatically heal the Foxfire player or spawn a physical pickup.
   If my requested gameplay effect is clear, connect the handler to that effect.
   Otherwise keep the verified demo and ask which real gameplay effect I want.
   Accept only after the effect succeeds; provide a refund path on failure and
   appropriate duplicate handling/recovery. Keep one fulfillment owner, gate
   unavailable actions during loading/pause, and preserve the working viewer
   alert through scene changes and demo cleanup. Test fresh/invalid PINs, saved
   session reconnect, disconnect/reconnect, and scene reload as applicable. Build
   and repeat authentication, a real action, the visible alert, and acknowledgment
   for each intended standalone platform/backend/stripping configuration when
   available. Report Editor and standalone results separately; do not infer
   player support from package import or Editor success.

## Deliver the result

Report the scene and files changed, integration path, Unity version, package
revision, and any deviations from the guide. Give a concise result for each
step: **passed**, **failed**, or **unverified**, with the evidence supporting it.
Distinguish local previews, real Twitch delivery, visible in-game alert,
transaction acknowledgment, extension state, broadcast video, actual gameplay,
and standalone testing. Include useful screenshots or log locations with secrets
removed. Identify the specific remaining action for anything incomplete.

Keep working through the authorized setup and checks. Do not declare the full
integration complete until the required live alert and other applicable
checkpoints have been verified.
