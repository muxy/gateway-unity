using MuxyHypeline;
using UnityEngine;

// Tutorial scene only: use the prefab manager as the sole SDK owner.
public class HypelineActionDemo : MonoBehaviour
{
    public MuxyHypelineManager HypelineManager;
    private int health = 50;
    private string status = "Open Stream Interactivity and connect with your Twitch code.";

    private void OnEnable()
    {
        if (HypelineManager == null)
        {
            Debug.LogError("Assign Hypeline Manager before enabling the demo.");
            enabled = false;
            return;
        }
        HypelineManager.OnAuthentication.AddListener(OnAuthentication);
    }

    private void OnDisable()
    {
        if (HypelineManager != null)
            HypelineManager.OnAuthentication.RemoveListener(OnAuthentication);
    }

    private void OnAuthentication(AuthenticationResponse response)
    {
        status = response.HasError ? "Authentication failed. Request a fresh PIN."
                                  : "Authenticated. Try Spawn Healthpack in the extension.";
        if (!response.HasError) PublishHealth();
    }

    // Wire the action's Dynamic GameActionUsed event to this public method.
    public void OnHealthpack(GameActionUsed used)
    {
        if (used.ActionID != "healthpack") return;
        ApplyHealth();
        HypelineManager.AcceptGameAction(used, "Added up to 10 health.");
        status = "Viewer action received: healthpack.";
        Debug.Log("Hypeline viewer action received: healthpack.");
    }

    private void ApplyHealth()
    {
        health = Mathf.Min(100, health + 10);
        if (HypelineManager.IsAuthenticated()) PublishHealth();
    }

    private void PublishHealth()
    {
        // The guide creates Health as Game Texts element 0.
        GameText text = HypelineManager.GetGameTextAt(0);
        text.Value = health.ToString();
        HypelineManager.SetGameTextAt(text, 0);
    }

    public void StartDemoPoll()
    {
        if (!HypelineManager.IsAuthenticated()) return;
        PollConfiguration config = new PollConfiguration
        {
            Prompt = "Where should we go next?",
            Mode = PollMode.Order,
            DurationInSeconds = 30
        };
        config.Options.Add("Forest");
        config.Options.Add("Desert");
        config.Options.Add("Ice Cave");
        config.OnPollUpdate = update =>
        {
            // The manager does not forward this delegate automatically.
            HypelineManager.OnPollUpdate.Invoke(update);
            if (!update.IsFinal) return;
            status = update.Count > 0 && update.Winner >= 0 &&
                     update.Winner < config.Options.Count
                ? "Poll winner: " + config.Options[update.Winner]
                : "Poll ended without a usable winner.";
            Debug.Log(status);
        };
        HypelineManager.SetPollConfiguration(config);
        HypelineManager.StartPoll();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16, Screen.height - 206, 440, 190), GUI.skin.box);
        GUILayout.Label("Hypeline action test | Health: " + health);
        GUILayout.Label(status);
        if (GUILayout.Button("Local test: add 10 health")) ApplyHealth();
        GUI.enabled = HypelineManager != null && HypelineManager.IsAuthenticated();
        if (GUILayout.Button("Start 30-second viewer poll")) StartDemoPoll();
        GUI.enabled = true;
        GUILayout.EndArea();
    }
}
