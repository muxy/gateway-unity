using TMPro;
using UnityEngine;

public class MuxyGatewayNotificationHistory : MonoBehaviour
{
    public float ClearTime = 5;

    private GameObject HistoryGroup;
    private float ClearTimer = 5;

    public void Update()
    {
        ClearTimer -= Time.deltaTime;
        if (ClearTimer <= 0)
        {
            ClearEntries();
        }
    }

    public void ShiftEntries()
    {
        for (int i = HistoryGroup.transform.childCount; i-- > 0;)
        {
            TMP_Text TmpTextRight = HistoryGroup.transform.GetChild(i).gameObject.GetComponent<TMP_Text>();

            if (i != 0)
            {
                TMP_Text TmpTextLeft = HistoryGroup.transform.GetChild(i - 1).gameObject.GetComponent<TMP_Text>();
                TmpTextRight.SetText(TmpTextLeft.text);
                TmpTextRight.color = TmpTextLeft.color;
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
        for (int i = 0; i < HistoryGroup.transform.childCount; i++)
        {
            TMP_Text TmpText = HistoryGroup.transform.GetChild(i).gameObject.GetComponent<TMP_Text>();
            TmpText.SetText("");
        }
    }
    public void AddToHistory(string ActionDesc, Color TextColor)
    {
        HistoryGroup.SetActive(true);
        this.ClearTimer = ClearTime;
        ShiftEntries();
        TMP_Text NewText = HistoryGroup.transform.GetChild(0).gameObject.GetComponent<TMP_Text>();
        NewText.SetText(ActionDesc);
        NewText.color = TextColor;
    }

    public void DeactivateHistoryGroup()
    {
        HistoryGroup.SetActive(false);
    }


}
