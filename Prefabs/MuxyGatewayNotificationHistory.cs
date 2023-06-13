using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MuxyGateway;

public class MuxyGatewayNotificationHistory : MonoBehaviour
{
    [Header("Muxy Gateway Manager")]
    public MuxyGatewayManager GatewayManager;

    [Header("Settings")]
    public float ClearTime = 8;
    public Texture2D HelpActionIcon;
    public Texture2D NeutralActionIcon;
    public Texture2D HinderActionIcon;
    public Color Impact1Color = new Color(255, 255, 255);
    public Color Impact2Color = new Color(255, 255, 255);
    public Color Impact3Color = new Color(255, 255, 255);
    public Color Impact4Color = new Color(255, 255, 255);
    public Color Impact5Color = new Color(255, 255, 255);
    [Header("Messaging")]
    public bool AutoAddActionMessages = true;
    public string MessageTemplate = @"{Username} just bought {ActionID}";

    private GameObject Elements;
    private GameObject Canvas;
    private float ClearTimer = 8;

    private void OnEnable()
    {
        GatewayManager.OnAnyGameActionUsed.AddListener(OnGameActionUsed);
    }

    private void OnDisable()
    {
        GatewayManager.OnAnyGameActionUsed.RemoveListener(OnGameActionUsed);
    }

    private Color GetColorForImpact(int Impact)
    {
        switch (Impact)
        {
            case 1: return Impact1Color;
            case 2: return Impact2Color;
            case 3: return Impact3Color;
            case 4: return Impact4Color;
            case 5: return Impact5Color;
            default: return Color.white;
        }
    }

    private void OnGameActionUsed(GameActionUsed UsedAction)
    {
        if (!AutoAddActionMessages)
        {
            return;
        }

        Dictionary<string, string> parameters = new Dictionary<string, string>();

        parameters.Add("TransactionID", UsedAction.TransactionID);
        parameters.Add("ActionID", UsedAction.ActionID);
        parameters.Add("Cost", UsedAction.ActionID);
        parameters.Add("UserID", UsedAction.ActionID);
        parameters.Add("Username", UsedAction.Username);

        string Text = Regex.Replace(MessageTemplate, @"\{(.+?)\}", m => parameters[m.Groups[1].Value]);
        Color Col = Color.white;
        GameAction? Action = GatewayManager.FindGameAction(UsedAction.ActionID);
        if (Action != null)
        {
            Col = GetColorForImpact(Action.Impact);
        }

        AddToHistory(Text, Col);
    }

    void Start()
    {
        Elements = this.transform.Find("Canvas/Notifications/Elements").gameObject;
        Canvas = this.transform.Find("Canvas").gameObject;
    }

    void Update()
    {
        ClearTimer -= Time.deltaTime;
        if (ClearTimer <= 0)
        {
            ClearEntries();
            Canvas.SetActive(false);
        }
    }

    public void ShiftEntries()
    {
        for (int i = Elements.transform.childCount; i-- > 0;)
        {
            TMP_Text TmpTextRight = Elements.transform.GetChild(i).transform.Find("ActionText").gameObject.GetComponent<TMP_Text>();
            RawImage TmpImgRight = Elements.transform.GetChild(i).transform.Find("StatusIcon").gameObject.GetComponent<RawImage>();

            if (i != 0)
            {
                TMP_Text TmpTextLeft = Elements.transform.GetChild(i-1).transform.Find("ActionText").gameObject.GetComponent<TMP_Text>();
                RawImage TmpImgLeft = Elements.transform.GetChild(i-1).transform.Find("StatusIcon").gameObject.GetComponent<RawImage>();
                TmpTextRight.SetText(TmpTextLeft.text);
                TmpTextRight.color = new Color(TmpTextLeft.color.r, TmpTextLeft.color.g, TmpTextLeft.color.b, TmpTextRight.color.a);
                TmpImgRight.texture = TmpImgLeft.texture;
                TmpImgRight.color = new Color(255, 255, 255, TmpTextRight.color.a);
            }
            else
            {
                TmpTextRight.SetText("");
                TmpTextRight.color = Color.white;
            }
        }
    }

    private void ClearEntries()
    {
        for (int i = 0; i < Elements.transform.childCount; i++)
        {
            TMP_Text TmpText = Elements.transform.GetChild(i).gameObject.GetComponent<TMP_Text>();
            TmpText.SetText("");
        }

    }

    public void AddToHistory(string ActionDesc, Color TextColor)
    {
        this.ClearTimer = ClearTime;
        ShiftEntries();
        TMP_Text NewText = Elements.transform.GetChild(0).transform.Find("ActionText").gameObject.GetComponent<TMP_Text>();
        NewText.SetText(ActionDesc);
        NewText.color = new Color(TextColor.r, TextColor.g, TextColor.b, NewText.color.a);
        Canvas.SetActive(true);
    }




}
