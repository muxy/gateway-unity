using System;
using System.Collections.Generic;
using UnityEngine;
using MuxyGateway;
using UnityEngine.Events;


public class MuxyGatewayManager : MonoBehaviour
{

    [Header("Game ID")]
    public String GameID = "";

    [SerializeField] private GameMetadata GameMetadata = new();
    [SerializeField] private List<GameAction> GameActions = new();
    [SerializeField] private List<GameText> GameTexts = new();
    [SerializeField] private PollConfiguration PollConfiguration = new();
    public MuxyGatewayPollUpdate OnPollUpdate = new();

    [Header("Events")]
    public MuxyGatewayAuthenticationEvent OnAuthentication = new();
    public MuxyGatewayGameActionUsedEvent OnAnyGameActionUsed = new();
    public MuxyGatewayBitsUsedEvent OnBitsUsed = new();

    [Serializable]
    public class MuxyGatewayAuthenticationEvent : UnityEvent<AuthenticationResponse> { }
    [Serializable]
    public class MuxyGatewayBitsUsedEvent : UnityEvent<BitsUsed> { }
    [Serializable]
    public class MuxyGatewayPollUpdate : UnityEvent<PollUpdate> { }


    // Private //

    private const string PLAYER_PREFS_REFRESH_TOKEN = "MuxyGatewayRefreshToken";

    private SDK SDK;
    private SDK.OnAuthenticateDelegate AuthCB;
    private SDK.OnGameActionUsedDelegate ActionCB;
    private SDK.OnBitsUsedDelegate BitsCB;

    private bool DidOpenAndRun = false;

    private void SetupCallbacks()
    {
        ActionCB = (Used) =>
        {
            OnAnyGameActionUsed.Invoke(Used);
        };

        BitsCB = (Used) =>
        {
            OnBitsUsed.Invoke(Used);
        };

        AuthCB = (Response) =>
        {
            OnAuthentication.Invoke(Response);

            if (Response.HasError)
            {
                PlayerPrefs.SetString(PLAYER_PREFS_REFRESH_TOKEN, "");
                return;
            }

            PlayerPrefs.SetString(PLAYER_PREFS_REFRESH_TOKEN, Response.RefreshToken);

            SDK.OnGameActionUsed(ActionCB);
            SDK.OnBitsUsed(BitsCB);
            SDK.SetGameActions(GameActions.ToArray());
            SDK.SetGameMetadata(GameMetadata);
        };

    }

    private void CheckForRefreshToken()
    {
        String RefreshToken = PlayerPrefs.GetString(PLAYER_PREFS_REFRESH_TOKEN, "");
        if (RefreshToken != "")
        {
            OpenAndRunSDK();
            SDK.AuthenticateWithRefreshToken(RefreshToken, AuthCB);
        }
    }

    private void OnDisable()
    {
        if (SDK != null)
        {
            SDK.StopWebsocketTransport();
            SDK = null;
        }
    }

    private void OnEnable()
    {
        if (SDK == null)
        {
            SDK = new(GameID);
            DidOpenAndRun = false;
            SetupCallbacks();
            CheckForRefreshToken();
        }

        if (GameTexts.Count > 0) SubmitGameTexts();
        if (GameActions.Count > 0) SubmitGameActions();
    }


    public void OpenAndRunSDK()
    {
        if (DidOpenAndRun) return;
        SDK.RunInProduction();
        DidOpenAndRun = true;
    }

    private void Update()
    {
        SDK.Update();
    }

    public void AuthenticateWithPIN(String PIN)
    {
        OpenAndRunSDK();
        SDK.AuthenticateWithPIN(PIN, AuthCB);
    }

    public bool IsAuthenticated()
    {
        return SDK.IsAuthenticated;
    }

    public void Deauthenticate()
    {
        PlayerPrefs.SetString(PLAYER_PREFS_REFRESH_TOKEN, "");
        SDK.Deauthenticate();
        DidOpenAndRun = false;
        SDK = new(GameID);
    }

    public void AddGameText(GameText Text)
    {
        GameTexts.Add(Text);
    }

    public void ClearGameTexts()
    {
        GameTexts.Clear();
    }

    public void SubmitGameTexts()
    {
        SDK.SetGameTexts(GameTexts.ToArray());
    }

    public GameText GetGameTextAt(int Index)
    {
        return GameTexts[Index];
    }

    public void SetGameTextAt(GameText Text, int Index)
    {
        GameTexts[Index] = Text;
        SDK.SetGameTexts(GameTexts.ToArray());
    }

    public void RemoveGameTextAt(int Index)
    {
        GameTexts.RemoveAt(Index);
        SDK.SetGameTexts(GameTexts.ToArray());
    }

    public GameAction GetGameActionAt(int Index)
    {
        return GameActions[Index];
    }

    public GameAction? FindGameAction(string ActionID)
    {
        foreach (GameAction Action in GameActions)
        {
            if (Action.ID == ActionID) return Action;
        }

        return null;
    }

    public void SetGameActionAt(GameAction Action, int Index)
    {
        GameActions[Index] = Action;
    }

    public void RemoveGameActionAt(int Index)
    {
        GameActions.RemoveAt(Index);
    }

    public void SubmitGameActions()
    {
        SDK.SetGameActions(GameActions.ToArray());
    }

    public void SetPollConfiguration(PollConfiguration Config)
    {
        PollConfiguration = Config;
    }

    public PollConfiguration GetPollConfiguration()
    {
        return PollConfiguration;
    }

    public void StartPoll()
    {
        SDK.StartPoll(PollConfiguration);
    }

    public void StopPoll()
    {
        SDK.StopPoll();
    }
    public void SetGameMetadata(GameMetadata Metadata)
    {
        GameMetadata = Metadata;
        SDK.SetGameMetadata(Metadata);
    }

    public GameMetadata GetGameMetadata()
    {
        return GameMetadata;
    }

    public void AcceptGameAction(GameActionUsed Used, string Description)
    {
        SDK.AcceptGameAction(Used, Description);
    }

    public void RefundGameAction(GameActionUsed Used, string Description)
    {
        SDK.RefundGameAction(Used, Description);
    }

    public void SetAllGameActionStates(GameActionState State)
    {
        foreach (var Action in GameActions)
        {
            Action.State = State;
        }
        SDK.SetGameActions(GameActions.ToArray());
    }
}
