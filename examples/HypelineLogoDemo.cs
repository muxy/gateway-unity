using System.Collections;
using System.Collections.Generic;
using MuxyHypeline;
using UnityEngine;

// A deliberately small first action: show a scene object and acknowledge delivery.
public class HypelineLogoDemo : MonoBehaviour
{
    public MuxyHypelineManager HypelineManager;
    public GameObject LogoObject;
    public string ActionID = "show-muxy-logo";
    [Min(0.1f)] public float DisplaySeconds = 5f;

    private readonly HashSet<string> handled = new HashSet<string>();
    private Coroutine hideRoutine;

    private void OnEnable()
    {
        if (HypelineManager == null)
        {
            Debug.LogError("Hypeline logo sample: assign the Hypeline Manager.", this);
            enabled = false;
            return;
        }
        if (LogoObject != null && transform.IsChildOf(LogoObject.transform))
        {
            Debug.LogError("Hypeline logo sample: the logo cannot contain this component.", this);
            enabled = false;
            return;
        }
        if (LogoObject != null && !transform.IsChildOf(LogoObject.transform)) LogoObject.SetActive(false);
        HypelineManager.OnAnyGameActionUsed.AddListener(OnAction);
    }

    private void OnDisable()
    {
        if (HypelineManager != null) HypelineManager.OnAnyGameActionUsed.RemoveListener(OnAction);
        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = null;
        if (LogoObject != null && !transform.IsChildOf(LogoObject.transform)) LogoObject.SetActive(false);
    }

    private void OnAction(GameActionUsed action)
    {
        if (action == null || action.ActionID != ActionID) return;
        if (string.IsNullOrEmpty(action.TransactionID))
        {
            Debug.LogError("Hypeline logo sample: action has no transaction ID; cannot safely acknowledge it.", this);
            return;
        }
        if (!handled.Add(action.TransactionID)) return;

        if (LogoObject == null || transform.IsChildOf(LogoObject.transform) ||
            (LogoObject.transform.parent != null && !LogoObject.transform.parent.gameObject.activeInHierarchy))
        {
            Refund(action, "Logo is unavailable.");
            return;
        }

        ShowLogo();
        Accept(action, "Logo displayed.");
    }

    // Override these in a local test harness to avoid contacting the service.
    protected virtual void Accept(GameActionUsed action, string description)
    {
        HypelineManager.AcceptGameAction(action, description);
    }

    protected virtual void Refund(GameActionUsed action, string description)
    {
        HypelineManager.RefundGameAction(action, description);
    }

    // Use this from an Inspector button or your own test UI. It has no transaction.
    public void ShowLogo()
    {
        if (LogoObject == null) return;
        if (hideRoutine != null) StopCoroutine(hideRoutine);
        LogoObject.SetActive(true);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, DisplaySeconds));
        if (LogoObject != null) LogoObject.SetActive(false);
        hideRoutine = null;
    }
}
