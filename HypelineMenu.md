# Muxy Hypeline creator menu

Add `Prefabs/MuxyHypelineMenu.prefab` to the main-menu scene. It creates a Stream Interactivity button and a creator setup panel at runtime. The prefab does not create an EventSystem; keep exactly one in your scene, configured for your project's active input system.

1. Add the existing `MuxyGatewayManager` prefab. Its Game ID defaults to `gamelink-testing`.
2. Add `MuxyHypelineMenu` to the scene, optionally under an existing Canvas, and assign the scene's manager to **Gateway Manager** before Play mode. If there is no parent Canvas, the component creates an overlay Canvas.
3. Set **Game Description** to describe the actual viewer interactions in your game. Adjust the menu button position and size, status colors, and artwork in the Inspector as needed.
4. Keep **Show TikTok Coming Soon** enabled to advertise future support, or disable it to hide the label. TikTok cannot yet be selected or connected.
5. Enter Play mode. Open **Stream Interactivity** and follow the three Twitch steps. **Open Twitch** and **Open configuration** open the Twitch extension page; in Twitch, install and activate it, then choose **Configure** to get the code. Enter it and select **Connect**. The code expires after five minutes.

The fox and adjacent label on the game menu and the larger panel status show neutral for disconnected, amber for connecting or reconnecting, green when the transport is open and authentication is reported, and red for an authentication or transport error. A failed connection can be retried with a fresh code. Open and close UnityEvents allow the game to coordinate other menu behavior. The panel restores the previously selected UI element when closed.

Unity's Console logs the transport state and whether authentication was requested, succeeded, or was rejected, without logging the PIN. If the PIN request receives no response within 30 seconds, the panel reports a timeout and disables another attempt until Play mode is restarted. This prevents overlapping requests while the earlier request may still be pending; request a fresh code before retrying.

The component retains the existing manager's refresh-token persistence and automatic authentication. `PlayerPrefs` is not encrypted; the short-lived PIN and backend do not change the security properties of local refresh-token storage. The prefab currently uses the existing native PC transport. It does not add Unity WebGL support or a browser SDK. Keep one active manager and avoid adding the old authentication UI alongside this prefab unless the game intentionally provides two ways to control the same connection.

The provided wordmark source is included as `Documentation~/HypelineWordmark.svg`; the prefab uses its rasterized PNG so no Unity SVG importer is required. The prefab exposes its layout as editable Unity UI children. Older saved menu hierarchies are rebuilt at runtime; replace the scene instance with the updated prefab to edit the new hierarchy. The previous panel-position fields remain for compatibility but do not control the redesigned panel; review any custom child overrides when replacing an instance.
