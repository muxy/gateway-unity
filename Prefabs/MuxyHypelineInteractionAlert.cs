using System.Collections.Generic;
using MuxyGateway;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays viewer action receipts without performing the game effect.</summary>
public class MuxyHypelineInteractionAlert : MonoBehaviour
{
    public MuxyGatewayManager GatewayManager;
    [Tooltip("Offset from the upper-right corner, in reference-resolution pixels.")]
    public Vector2 Position = new Vector2(-48, -220);
    public Vector2 Size = new Vector2(480, 110);
    public bool LogReceivedActions = true;
    public Color BackgroundColor = new Color(0.08f, 0.10f, 0.15f, 0.94f);
    public Color AccentColor = new Color(0.21f, 0.84f, 0.75f, 1f);
    [Header("Sounds (optional overrides)")]
    public bool PlaySounds = true;
    [Range(0f, 1f)] public float SoundVolume = 0.65f;
    public AudioClip HelpSound;
    public AudioClip HinderSound;
    public AudioClip NeutralSound;

    private const float Duration = 6f;
    private const float FadeDuration = 1f;
    private const float OlderScale = 0.6f;
    private const float Spacing = 12f;
    private readonly List<Alert> alerts = new List<Alert>();
    private AudioSource audioSource;
    private AudioClip defaultHelp, defaultHinder, defaultNeutral;

    private class Alert
    {
        public GameObject root;
        public RectTransform rect;
        public CanvasGroup group;
        public float age;
    }

    private void Awake()
    {
        if (GetComponentInParent<Canvas>() == null)
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
    }

    private void OnEnable()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
        if (GatewayManager != null) GatewayManager.OnAnyGameActionUsed.AddListener(OnActionUsed);
        else Debug.LogWarning("Hypeline interaction alert needs a Gateway Manager reference.", this);
    }

    private void OnDisable()
    {
        if (GatewayManager != null) GatewayManager.OnAnyGameActionUsed.RemoveListener(OnActionUsed);
        foreach (Alert alert in alerts) if (alert.root != null) Destroy(alert.root);
        alerts.Clear();
        if (audioSource != null) audioSource.Stop();
    }

    private void OnDestroy()
    {
        if (defaultHelp != null) Destroy(defaultHelp);
        if (defaultHinder != null) Destroy(defaultHinder);
        if (defaultNeutral != null) Destroy(defaultNeutral);
    }

    private void Update()
    {
        for (int i = alerts.Count - 1; i >= 0; i--)
        {
            Alert alert = alerts[i];
            alert.age += Time.unscaledDeltaTime;
            if (alert.age >= Duration)
            {
                Destroy(alert.root);
                alerts.RemoveAt(i);
            }
            else alert.group.alpha = Mathf.Clamp01((Duration - alert.age) / FadeDuration);
        }
        LayoutAlerts();
    }

    private void LayoutAlerts()
    {
        float y = 0f;
        float scale = 1f;
        foreach (Alert alert in alerts)
        {
            alert.rect.anchoredPosition = Position + new Vector2(0, -y);
            alert.rect.localScale = Vector3.one * scale;
            y += Size.y * scale + Spacing;
            scale *= OlderScale;
        }
    }

    private void OnActionUsed(GameActionUsed used)
    {
        if (used == null) return;
        GameAction action = GatewayManager.FindGameAction(used.ActionID);
        string actionName = action != null && !string.IsNullOrWhiteSpace(action.Name) ? action.Name : used.ActionID;
        GameActionCategory category = action != null ? action.Category : GameActionCategory.Neutral;
        if (LogReceivedActions) Debug.Log("Hypeline interaction received: " + (used.Username ?? "") + " — " + (actionName ?? ""), this);
        ShowAlert(used.Username, actionName, category);
    }

    /// <summary>Shows a preview or custom neutral alert without sending an interaction.</summary>
    public void ShowAlert(string viewerName, string actionName)
    {
        ShowAlert(viewerName, actionName, GameActionCategory.Neutral);
    }

    /// <summary>Shows a preview or custom alert without sending or fulfilling a transaction.</summary>
    public void ShowAlert(string viewerName, string actionName, GameActionCategory category)
    {
        string viewer = string.IsNullOrWhiteSpace(viewerName) ? "A viewer" : viewerName.Trim();
        string action = string.IsNullOrWhiteSpace(actionName) ? "an interaction" : actionName.Trim();
        Alert alert = MakeAlert(viewer + " sent\n" + action);
        alerts.Insert(0, alert);
        alert.rect.SetAsLastSibling();
        LayoutAlerts();
        if (PlaySounds) PlayCategorySound(category);
    }

    private Alert MakeAlert(string text)
    {
        GameObject card = new GameObject("Interaction Alert", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.sizeDelta = Size;
        Image background = card.GetComponent<Image>();
        background.color = BackgroundColor;
        background.raycastTarget = false;
        CanvasGroup group = card.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.SetParent(rect, false);
        accentRect.anchorMin = new Vector2(0, 0);
        accentRect.anchorMax = new Vector2(0, 1);
        accentRect.pivot = new Vector2(0, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(8, 0);
        Image accentImage = accent.GetComponent<Image>();
        accentImage.color = AccentColor;
        accentImage.raycastTarget = false;

        GameObject label = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(28, 12);
        labelRect.offsetMax = new Vector2(-20, -12);
        TMP_Text message = label.GetComponent<TMP_Text>();
        message.text = text;
        message.fontSize = 27;
        message.color = Color.white;
        message.alignment = TextAlignmentOptions.MidlineLeft;
        message.richText = false;
        message.raycastTarget = false;
#if UNITY_6000_0_OR_NEWER
        message.textWrappingMode = TextWrappingModes.Normal;
#else
        message.enableWordWrapping = true;
#endif
        return new Alert { root = card, rect = rect, group = group };
    }

    private void PlayCategorySound(GameActionCategory category)
    {
        AudioClip clip;
        if (category == GameActionCategory.Help)
        {
            if (HelpSound == null && defaultHelp == null) defaultHelp = MakeChime("Hypeline Help", new float[] { 523.25f, 659.25f, 783.99f });
            clip = HelpSound != null ? HelpSound : defaultHelp;
        }
        else if (category == GameActionCategory.Hinder)
        {
            if (HinderSound == null && defaultHinder == null) defaultHinder = MakeChime("Hypeline Hinder", new float[] { 392f, 329.63f, 261.63f });
            clip = HinderSound != null ? HinderSound : defaultHinder;
        }
        else
        {
            if (NeutralSound == null && defaultNeutral == null) defaultNeutral = MakeChime("Hypeline Neutral", new float[] { 440f, 587.33f, 440f });
            clip = NeutralSound != null ? NeutralSound : defaultNeutral;
        }
        if (clip != null) audioSource.PlayOneShot(clip, Mathf.Clamp01(SoundVolume));
    }

    // Three short, original synthesized note patterns; no external audio assets required.
    private static AudioClip MakeChime(string name, float[] notes)
    {
        const int sampleRate = 44100;
        const float noteDuration = 0.12f;
        const float tail = 0.16f;
        int length = Mathf.CeilToInt((notes.Length * noteDuration + tail) * sampleRate);
        float[] samples = new float[length];
        for (int n = 0; n < notes.Length; n++)
        {
            int start = Mathf.RoundToInt(n * noteDuration * sampleRate);
            int noteSamples = Mathf.RoundToInt((noteDuration + tail) * sampleRate);
            for (int i = 0; i < noteSamples && start + i < length; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 14f);
                samples[start + i] += 0.17f * envelope * (Mathf.Sin(2f * Mathf.PI * notes[n] * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * notes[n] * t));
            }
        }
        AudioClip clip = AudioClip.Create(name, length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    [ContextMenu("Preview stacked alerts")]
    private void PreviewStack()
    {
        if (!Application.isPlaying) { Debug.Log("Enter Play mode to preview interaction alerts.", this); return; }
        ShowAlert("Viewer one", "Help action", GameActionCategory.Help);
        ShowAlert("Viewer two", "Hinder action", GameActionCategory.Hinder);
        ShowAlert("Viewer three", "Neutral action", GameActionCategory.Neutral);
    }
}
