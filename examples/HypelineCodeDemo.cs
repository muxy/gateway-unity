using MuxyHypeline;
using UnityEngine;

// Alternative to the prefab manager. Do not run both integrations together.
public class HypelineCodeDemo : MonoBehaviour
{
    [SerializeField] private string gameID = "gamelink-testing";
    private SDK sdk;
    private string pin = "";
    private string status = "Request a code from the Twitch extension configuration.";
    private int health = 50;
    private bool authenticationPending;

    private void OnEnable()
    {
        sdk = new SDK(gameID);
        sdk.RunInProduction();
    }

    private void Update()
    {
        if (sdk != null) sdk.Update();
    }

    public void Authenticate(string enteredPIN)
    {
        if (sdk == null || authenticationPending || sdk.IsAuthenticated) return;
        enteredPIN = enteredPIN.Trim();
        if (enteredPIN.Length != 6)
        {
            status = "Enter the six-character PIN from the extension.";
            return;
        }
        authenticationPending = true;
        status = "Authenticating...";
        sdk.AuthenticateWithPIN(enteredPIN, response =>
        {
            authenticationPending = false;
            pin = "";
            if (response.HasError)
            {
                status = "Authentication failed. Request a fresh PIN and retry.";
                return;
            }
            PublishSetup();
            status = "Authenticated. Try Spawn Healthpack in the extension.";
        });
    }

    private void PublishSetup()
    {
        sdk.SetGameMetadata(new GameMetadata
        {
            Name = "Hypeline Unity Demo", Logo = "", Theme = "default"
        });
        GameAction action = new GameAction
        {
            ID = "healthpack",
            Name = "Spawn Healthpack",
            Description = "Add up to 10 health to the player.",
            Icon = "fa-solid:heart",
            Category = GameActionCategory.Help,
            State = GameActionState.Available,
            Impact = 1,
            Count = GameAction.InfiniteCount,
            OnGameActionUsed = new MuxyHypelineGameActionUsedEvent()
        };
        action.OnGameActionUsed.AddListener(OnHealthpack);
        sdk.SetGameActions(new[] { action });
        // Register the global callback too: it enables native action delivery
        // and dispatch to the per-action UnityEvent in this SDK revision.
        sdk.OnGameActionUsed(used =>
            Debug.Log("Hypeline action callback received."));
        PublishHealth();
    }

    private void OnHealthpack(GameActionUsed used)
    {
        health = Mathf.Min(100, health + 10);
        PublishHealth();
        sdk.AcceptGameAction(used, "Added up to 10 health.");
        status = "Viewer action received: healthpack.";
    }

    private void PublishHealth()
    {
        sdk.SetGameTexts(new[]
        {
            new GameText { Label = "Health", Value = health.ToString(),
                           Icon = "fa-solid:heart" }
        });
    }

    public void StartDemoPoll()
    {
        if (sdk == null || !sdk.IsAuthenticated) return;
        PollConfiguration config = new PollConfiguration
        {
            Prompt = "Where should we go next?", Mode = PollMode.Order,
            DurationInSeconds = 30
        };
        config.Options.Add("Forest");
        config.Options.Add("Desert");
        config.Options.Add("Ice Cave");
        config.OnPollUpdate = update =>
        {
            if (!update.IsFinal) return;
            status = update.Count > 0 && update.Winner >= 0 &&
                     update.Winner < config.Options.Count
                ? "Poll winner: " + config.Options[update.Winner]
                : "Poll ended without a usable winner.";
        };
        sdk.StartPoll(config);
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16, 16, 440, 230), GUI.skin.box);
        GUILayout.Label("Hypeline code demo | Health: " + health);
        GUILayout.Label(status);
        if (sdk != null && !sdk.IsAuthenticated)
        {
            pin = GUILayout.TextField(pin, 6);
            GUI.enabled = !authenticationPending;
            if (GUILayout.Button("Authenticate")) Authenticate(pin);
        }
        GUI.enabled = sdk != null && sdk.IsAuthenticated;
        if (GUILayout.Button("Start 30-second viewer poll")) StartDemoPoll();
        GUI.enabled = true;
        GUILayout.EndArea();
    }

    private void OnDisable()
    {
        if (sdk != null) sdk.StopWebsocketTransport();
        sdk = null;
        authenticationPending = false;
        pin = "";
    }
}
