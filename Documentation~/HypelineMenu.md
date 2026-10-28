# Muxy Hypeline creator menu

Add `Prefabs/MuxyHypelineMenu.prefab` to the main-menu scene. It creates a Stream Interactivity button and a creator setup panel at runtime. The prefab does not create an EventSystem; keep exactly one in your scene, configured for your project's active input system. Import TextMeshPro Essential Resources when prompted.

1. Add the existing `MuxyGatewayManager` prefab and set its Game ID to the exact ID linked in the Twitch extension configuration.
2. Add `MuxyHypelineMenu` to the scene, optionally under an existing Canvas, and assign the scene's manager to **Gateway Manager** before Play mode. If there is no parent Canvas, the component creates an overlay Canvas.
3. Set **Game Description** to describe the actual viewer interactions in your game. Adjust the menu button position and size, status colors, and artwork in the Inspector as needed.
4. Keep **Show TikTok Coming Soon** enabled to advertise future support, or disable it to hide the label. TikTok cannot yet be selected or connected.
5. Enter Play mode. Open **Stream Interactivity** and follow the Twitch instructions. The code comes from the existing extension's Configure page and expires after five minutes.

The fox and adjacent label show neutral for disconnected, amber for connecting or reconnecting, green when the transport is open and authentication is reported, and red for an authentication or transport error. A failed connection can be retried with a fresh code. Open and close UnityEvents allow the game to coordinate other menu behavior. The panel restores the previously selected UI element when closed.

The component retains the existing manager's refresh-token persistence and automatic authentication. `PlayerPrefs` is not encrypted; the short-lived PIN and backend do not change the security properties of local refresh-token storage. The prefab currently uses the existing native PC transport. It does not add Unity WebGL support or a browser SDK. Keep one active manager and avoid adding the old authentication UI alongside this prefab unless the game intentionally provides two ways to control the same connection.

The provided wordmark source is included as `Documentation~/HypelineWordmark.svg`; the prefab uses its rasterized PNG so no Unity SVG importer is required. The UI is constructed at runtime, and its layout and text styling can be modified in `MuxyHypelineMenu.cs`.
