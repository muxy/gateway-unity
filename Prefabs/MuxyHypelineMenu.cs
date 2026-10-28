using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MuxyGateway;

/// <summary>Creator-facing Hypeline menu and Twitch connection panel.</summary>
public class MuxyHypelineMenu : MonoBehaviour
{
    public MuxyGatewayManager GatewayManager;
    public Texture2D FoxArtwork;
    public Texture2D WordmarkArtwork;
    [TextArea] public string GameDescription = "Let your live audience interact with your game.";
    public bool ShowTikTokComingSoon = true;
    public string TwitchSetupUrl = "https://dashboard.twitch.tv/extensions/i575hs2x9lb3u8hqujtezit03w1740";
    public Color DisconnectedColor = new Color(0.67f, 0.70f, 0.76f);
    public Color ConnectingColor = new Color(1f, 0.72f, 0.22f);
    public Color ConnectedColor = new Color(0.30f, 0.86f, 0.52f);
    public Color ErrorColor = new Color(1f, 0.36f, 0.34f);
    public Vector2 MenuButtonPosition = new Vector2(24, -24);
    public Vector2 MenuButtonSize = new Vector2(300, 76);
    public UnityEvent OnPanelOpened = new UnityEvent();
    public UnityEvent OnPanelClosed = new UnityEvent();

    private Image fox;
    private TMP_Text menuStatus, panelStatus, message;
    private TMP_InputField code;
    private GameObject modal;
    private Button menuButton, connectButton, disconnectButton;
    private bool requested, authError;
    private string lastStatus;
    private GameObject previousSelection;

    private void Awake()
    {
        if (GetComponentInParent<Canvas>() == null)
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();
        }
        BuildUI();
    }

    private void OnEnable()
    {
        if (GatewayManager != null) GatewayManager.OnAuthentication.AddListener(AuthenticationFinished);
    }

    private void OnDisable()
    {
        if (GatewayManager != null) GatewayManager.OnAuthentication.RemoveListener(AuthenticationFinished);
    }

    private void Update()
    {
        string status;
        Color color;
        if (GatewayManager == null) { status = "Setup required"; color = ErrorColor; }
        else if (GatewayManager.TransportState == GatewayTransportState.Error || authError)
        { status = "Connection issue"; color = ErrorColor; }
        else if (GatewayManager.TransportState == GatewayTransportState.Reconnecting)
        { status = "Reconnecting..."; color = ConnectingColor; }
        else if (GatewayManager.TransportState == GatewayTransportState.Connected && GatewayManager.IsAuthenticated())
        { status = "Connected"; color = ConnectedColor; requested = false; }
        else if (requested || GatewayManager.TransportState == GatewayTransportState.Connecting || GatewayManager.TransportState == GatewayTransportState.Connected)
        { status = "Connecting..."; color = ConnectingColor; }
        else { status = "Not connected"; color = DisconnectedColor; }

        fox.color = color;
        menuStatus.text = status;
        panelStatus.text = status;
        if (status != lastStatus)
        {
            lastStatus = status;
            if (status == "Connected") message.text = "Your Twitch connection is ready.";
            else if (status == "Connection issue") message.text = "Could not connect. Check your internet connection or get a new code and try again.";
            else if (status == "Reconnecting...") message.text = "Trying to restore your connection.";
            else if (status == "Setup required") message.text = "Assign the Gateway Manager in the Inspector.";
        }
        connectButton.interactable = GatewayManager != null && status != "Connected" && status != "Connecting..." && status != "Reconnecting...";
        disconnectButton.interactable = GatewayManager != null && (status == "Connected" || status == "Reconnecting...");
    }

    private void AuthenticationFinished(AuthenticationResponse response)
    {
        requested = false;
        authError = response.HasError;
        if (code != null) code.text = "";
    }

    public void OpenPanel()
    {
        if (EventSystem.current != null) previousSelection = EventSystem.current.currentSelectedGameObject;
        modal.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(code.gameObject);
        OnPanelOpened.Invoke();
    }

    public void ClosePanel()
    {
        modal.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection != null ? previousSelection : menuButton.gameObject);
        OnPanelClosed.Invoke();
    }

    private void Connect()
    {
        string pin = code.text.Trim();
        if (pin.Length != 6)
        {
            message.text = "Enter the six-character code from Twitch.";
            return;
        }
        authError = false;
        requested = true;
        message.text = "Connecting to Twitch...";
        GatewayManager.AuthenticateWithPIN(pin);
        code.text = "";
    }

    private void Disconnect()
    {
        GatewayManager.Deauthenticate();
        requested = false;
        authError = false;
        message.text = "Disconnected. Enter a new Twitch code to connect again.";
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    private static Image Box(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, Color color)
    {
        Image image = Rect(name, parent, min, max, pos, size).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text Text(string name, Transform parent, string value, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        TMP_Text text = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value; text.fontSize = fontSize; text.alignment = alignment; text.color = Color.white;
#if UNITY_6000_0_OR_NEWER
        text.textWrappingMode = TextWrappingModes.Normal;
#else
        text.enableWordWrapping = true;
#endif
        text.raycastTarget = false;
        return text;
    }

    private static Button MakeButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, Color color, UnityAction action)
    {
        Image image = Box(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, color);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image; button.onClick.AddListener(action);
        Text("Label", image.transform, label, Vector2.zero, size - new Vector2(12, 6), 21, TextAlignmentOptions.Center);
        return button;
    }

    private static Sprite MakeSprite(Texture2D texture)
    {
        return texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private void BuildUI()
    {
        Color surface = new Color(0.10f, 0.11f, 0.16f, 0.98f);
        Color buttonColor = new Color(0.23f, 0.25f, 0.37f, 1);
        Image menu = Box("Stream Interactivity", transform, new Vector2(0, 1), new Vector2(0, 1), MenuButtonPosition + new Vector2(MenuButtonSize.x / 2, -MenuButtonSize.y / 2), MenuButtonSize, surface);
        menuButton = menu.gameObject.AddComponent<Button>(); menuButton.targetGraphic = menu; menuButton.onClick.AddListener(OpenPanel);
        fox = Box("Fox status", menu.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-112, 0), new Vector2(48, 48), DisconnectedColor);
        fox.sprite = MakeSprite(FoxArtwork); fox.preserveAspect = true; fox.raycastTarget = false;
        Text("Title", menu.transform, "Stream Interactivity", new Vector2(20, 13), new Vector2(224, 27), 21, TextAlignmentOptions.Left);
        menuStatus = Text("Status", menu.transform, "Not connected", new Vector2(20, -14), new Vector2(224, 22), 16, TextAlignmentOptions.Left);

        modal = Box("Hypeline Panel", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.72f)).gameObject;
        Image card = Box("Card", modal.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 760), surface);
        Image mark = Box("Hypeline wordmark", card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-190, 320), new Vector2(310, 83), Color.white);
        mark.sprite = MakeSprite(WordmarkArtwork); mark.preserveAspect = true; mark.raycastTarget = false;
        MakeButton("Close", card.transform, "Close", new Vector2(315, 320), new Vector2(100, 44), buttonColor, ClosePanel);
        Text("Description", card.transform, GameDescription, new Vector2(0, 245), new Vector2(660, 60), 23, TextAlignmentOptions.Left);
        Text("Platform", card.transform, ShowTikTokComingSoon ? "TWITCH    |    TikTok - Coming soon" : "TWITCH", new Vector2(0, 183), new Vector2(660, 38), 20, TextAlignmentOptions.Left);
        Text("Instructions", card.transform, "1. Install and activate the extension in your Twitch Creator Dashboard.\n2. Open Configure and get a code.\n3. Enter the code below within five minutes.", new Vector2(0, 95), new Vector2(660, 130), 21, TextAlignmentOptions.TopLeft);
        Image inputBg = Box("Code", card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-140, -12), new Vector2(360, 55), new Color(0.17f, 0.18f, 0.24f));
        code = inputBg.gameObject.AddComponent<TMP_InputField>();
        RectTransform viewport = Rect("Text Area", inputBg.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-24, -8));
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text inputText = Text("Text", viewport, "", Vector2.zero, Vector2.zero, 22, TextAlignmentOptions.Left);
        RectTransform textRect = inputText.rectTransform; textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.sizeDelta = Vector2.zero;
        TMP_Text placeholder = Text("Placeholder", viewport, "Six-character code", Vector2.zero, Vector2.zero, 22, TextAlignmentOptions.Left);
        placeholder.rectTransform.anchorMin = Vector2.zero; placeholder.rectTransform.anchorMax = Vector2.one; placeholder.rectTransform.sizeDelta = Vector2.zero; placeholder.color = new Color(0.7f, 0.7f, 0.7f);
        code.textViewport = viewport; code.textComponent = inputText; code.placeholder = placeholder; code.characterLimit = 6; code.lineType = TMP_InputField.LineType.SingleLine;
        connectButton = MakeButton("Connect", card.transform, "Connect", new Vector2(208, -12), new Vector2(220, 55), buttonColor, Connect);
        panelStatus = Text("Connection status", card.transform, "Not connected", new Vector2(0, -85), new Vector2(660, 30), 22, TextAlignmentOptions.Left);
        message = Text("Help message", card.transform, "A code expires after five minutes. Get a new one if needed.", new Vector2(0, -140), new Vector2(660, 65), 18, TextAlignmentOptions.Left);
        MakeButton("Open Twitch setup", card.transform, "Open Twitch setup", new Vector2(-184, -230), new Vector2(290, 55), buttonColor, delegate { Application.OpenURL(TwitchSetupUrl); });
        disconnectButton = MakeButton("Disconnect", card.transform, "Disconnect", new Vector2(184, -230), new Vector2(290, 55), buttonColor, Disconnect);
        modal.SetActive(false);
    }
}
