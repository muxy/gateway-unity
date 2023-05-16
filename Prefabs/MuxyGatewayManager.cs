using System;
using System.Collections.Generic;
using UnityEngine;
using MuxyGateway;
using UnityEngine.Events;


public class MuxyGatewayManager : MonoBehaviour
{

    [Header("Game ID (typically Giantbomb ID)")]
    public String GameID = "";

    [SerializeField] private GameMetadata GameMetadata = new();
    [SerializeField] private List<GameAction> GameActions = new();
    [SerializeField] private List<GameText> GameTexts = new();
    [SerializeField] private PollConfiguration PollConfiguration = new();

    [Header("Events")]
    public MuxyGatewayAuthenticationEvent OnAuthentication = new();
    public MuxyGatewayGameActionUsedEvent OnAnyGameActionUsed = new();
    public MuxyGatewayPollUpdate OnPollUpdate = new();
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
            if (Response.HasError)
            {
                PlayerPrefs.SetString(PLAYER_PREFS_REFRESH_TOKEN, "");
                OnAuthentication.Invoke(Response);
                return;
            }

            PlayerPrefs.SetString(PLAYER_PREFS_REFRESH_TOKEN, Response.RefreshToken);

            SDK.OnGameActionUsed(ActionCB);
            SDK.OnBitsUsed(BitsCB);
            SDK.SetGameActions(GameActions.ToArray());
            SDK.SetGameMetadata(GameMetadata);

            OnAuthentication.Invoke(Response);
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
    }

    public void AddGameText(GameText Text)
    {
        GameTexts.Add(Text);
        SDK.SetGameTexts(GameTexts.ToArray());
    }

    public void ClearGameTexts()
    {
        GameTexts.Clear();
        SDK.SetGameTexts(GameTexts.ToArray());
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

    public void SetPollConfiguration(PollConfiguration Config)
    {
        PollConfiguration = Config;
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

    public void SetAllGameActionStates(GameActionState State)
    {
        foreach (var Action in GameActions)
        {
            Action.State = State;
        }
        SDK.SetGameActions(GameActions.ToArray());
    }
}
