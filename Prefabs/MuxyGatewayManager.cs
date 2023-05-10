using System;
using System.Collections.Generic;
using UnityEngine;
using MuxyGateway;
using UnityEngine.Events;


public class MuxyGatewayManager : MonoBehaviour
{
    public enum MuxyGatewayConnectionStage
    {
        Sandbox,
        Production
    }

    public MuxyGatewayConnectionStage ConnectionStage = MuxyGatewayConnectionStage.Sandbox;

    [Header("Game ID (typically Giantbomb ID)")]
    public String GameID = "";


    [SerializeField] private GameMetadata GameMetadata = new();
    [SerializeField] private List<GameAction> GameActions = new();
    [SerializeField] private List<GameText> GameTexts = new();
    [SerializeField] private PollConfiguration PollConfiguration = new();

    [Header("Events")]
    public MuxyGatewayAuthenticationEvent OnAuthentication = new();
    public MuxyGatewayGameActionUsedEvent OnGameActionUsed = new();
    public MuxyGatewayPollUpdate OnPollUpdate = new();
    public MuxyGatewayBitsUsedEvent OnBitsUsed = new();

    [Serializable]
    public class MuxyGatewayAuthenticationEvent : UnityEvent<AuthenticationResponse> { }
    [Serializable]
    public class MuxyGatewayGameActionUsedEvent : UnityEvent<GameActionUsed> { }
    [Serializable]
    public class MuxyGatewayBitsUsedEvent : UnityEvent<BitsUsed> { }
    [Serializable]
    public class MuxyGatewayPollUpdate : UnityEvent<PollUpdate> { }

    // Private //

    private const string PLAYER_PREFS_REFRESH_TOKEN = "MuxyGatewayRefreshToken";

    private SDK SDK;
    private SDK.OnAuthenticateDelegate AuthCB;
    private SDK.OnActionUsedDelegate ActionCB;
    private SDK.OnBitsUsedDelegate BitsCB;

    private bool DidOpenAndRun = false;

    private void SetupCallbacks()
    {
        ActionCB = (Used) =>
        {
            OnGameActionUsed.Invoke(Used);
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

            SDK.OnActionUsed(ActionCB);
            SDK.OnBitsUsed(BitsCB);
            SDK.SetActions(GameActions.ToArray());
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

    private void Start()
    {
        SDK = new(GameID);
        SetupCallbacks();
        CheckForRefreshToken();
    }

    public void OpenAndRunSDK()
    {
        if (DidOpenAndRun) return;

        if (ConnectionStage == MuxyGatewayConnectionStage.Production)
        {
            SDK.RunInProduction();
        }
        else
        {
            SDK.RunInSandbox();
        }
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

    public async void Deauthenticate()
    {
        PlayerPrefs.SetString(PLAYER_PREFS_REFRESH_TOKEN, "");
        await SDK.Deauthenticate();
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

    public void SetAllActionStates(GameActionState State)
    {
        foreach (var Action in GameActions)
        {
            Action.State = State;
        }
        SDK.SetActions(GameActions.ToArray());
    }
}
