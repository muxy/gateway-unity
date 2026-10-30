# Hypeline screenshot capture brief

Capture the current Hypeline workflow rather than the retired Gateway authentication demo. Use the current package revision and document the Unity version and platform. These are requested captures, not claims that the current flow has been verified live.

| Capture | What to show |
| --- | --- |
| Package installation | Unity Package Manager with Muxy Hypeline selected and its dependencies resolved. |
| Scene setup | One `MuxyGatewayManager`, the `MuxyHypelineMenu`, its assigned manager, and one EventSystem. |
| Connection | The redesigned **Stream Interactivity** panel and its connected state; hide the code. |
| Action setup | The manager's `healthpack` action and its `HypelineActionDemo.OnHealthpack` event binding. |
| Viewer round trip | The viewer action, game health change, and updated extension value after a real transaction. |
| Poll | A viewer vote and the final result, plus a separate no-vote test if available. |

Keep PINs, tokens, account details, browser bookmarks, and private paths out of captures. A connected panel or local test button does not prove a viewer action was delivered. Use readable native-resolution PNG or JPEG images; do not use mockups as test evidence.

Place approved images in `assets/screenshots/` and add their names and accurate alt text to `assets/screenshots.json`. Use the matching `data-shot` identifiers in the guide. Historical images are retained as source material but should not be presented as captures of the redesigned UI.
