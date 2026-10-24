using System.Collections;
using UnityEngine;
using DialogueEditor;

public class DialogueController : MonoBehaviour
{
    public static DialogueController ActiveDialogue { get; private set; }
    public bool CanCancelByPlayer { get; private set; } = true;
    // Instance-scoped notification: true = completed, false = interrupted.
    public event System.Action<bool> Finished;

    private Interactable pendingInteraction;
    private string tagBeforeDialogue;
    private Coroutine pendingStart;
    private bool completionRequested;

    private void OnDisable()
    {
        // Existing node events disable their source after requesting normal completion.
        // Keep the end subscription until the manager has actually closed the UI.
        var manager = ConversationManager.Instance;
        if (manager == null || (!manager.IsEndingConversation &&
            !(completionRequested && manager.IsConversationActive)))
            InterruptDialogue();
        PlayerControlLocks.Release(this);
    }

    private void OnDestroy()
    {
        InterruptDialogue();
        ClearActiveDialogue();
        PlayerControlLocks.Release(this);
    }

    private void ClearActiveDialogue()
    {
        CanCancelByPlayer = true;
        completionRequested = false;
        ConversationManager.OnConversationEnded -= HandleConversationEnded;
        if (ActiveDialogue == this)
            ActiveDialogue = null;
    }

    private void HandleConversationEnded()
    {
        if (ActiveDialogue != this)
            return;

        DisableDialogueMode();
    }

    private void CompletePendingInteraction()
    {
        if (pendingInteraction != null)
            pendingInteraction.CompleteInteraction();
        pendingInteraction = null;
    }

    /// <summary>
    /// Starts a dialogue via DialogueEditor.
    /// Stops player movement and shows the cursor.
    /// </summary>
    public void StartDialogue(NPCConversation conversation)
    {
        if (!isActiveAndEnabled || ActiveDialogue == this)
            return;

        if (ActiveDialogue != null)
            ActiveDialogue.InterruptDialogue();

        CanCancelByPlayer = true;
        if (TryGetComponent(out Interactable interactable) && interactable.DeferCompletion())
            pendingInteraction = interactable;

        tagBeforeDialogue = gameObject.tag;
        ActiveDialogue = this;
        ConversationManager.OnConversationEnded += HandleConversationEnded;

        try
        {
            EnableDialogueMode();
            pendingStart = StartCoroutine(PrepareAndStart(conversation));
        }
        catch
        {
            if (pendingInteraction != null)
                pendingInteraction.CancelInteraction();
            pendingInteraction = null;
            RestorePlayerControls();
            ClearActiveDialogue();
            throw;
        }
    }

    private IEnumerator PrepareAndStart(NPCConversation conversation)
    {
        // Always yield once so pendingStart is assigned before completion or cancellation.
        yield return null;
        if (conversation == null)
        {
            pendingStart = null;
            InterruptDialogue();
            yield break;
        }
        var localization = new DialogueLocalization();
        yield return localization.Load(conversation.LocalizationTable);
        if (ActiveDialogue != this || !isActiveAndEnabled)
            yield break;
        pendingStart = null;
        try
        {
            ConversationManager.Instance.StartConversation(conversation, localization.Table);
        }
        catch (System.Exception exception)
        {
            InterruptDialogue();
            Debug.LogException(exception, this);
        }
    }

    /// <summary>
    /// Allows or blocks player cancellation. Can be called from dialogue node events.
    /// </summary>
    public void SetPlayerCancellationAllowed(bool allowed)
    {
        CanCancelByPlayer = allowed;
    }

    /// <summary>
    /// Interrupts this conversation without completion events and allows interacting again.
    /// </summary>
    public void InterruptDialogue()
    {
        if (ActiveDialogue != this)
            return;

        // Drop ownership before cancellation calls back into the manager.
        ClearActiveDialogue();
        if (pendingStart != null)
        {
            StopCoroutine(pendingStart);
            pendingStart = null;
        }
        if (ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive)
            ConversationManager.Instance.CancelConversation();
        if (pendingInteraction != null)
            pendingInteraction.CancelInteraction();
        pendingInteraction = null;
        RestorePlayerControls();

        gameObject.tag = tagBeforeDialogue;

        Finished?.Invoke(false);
    }

    /// <summary>
    /// Stops player input and unlocks & shows the cursor.
    /// </summary>
    public void EnableDialogueMode()
    {
        PlayerControlLocks.Acquire(this, showCursor: true);
    }

    /// <summary>
    /// Finishes dialogue mode once the manager has closed its UI.
    /// Existing node events can request this early without skipping the remaining speech.
    /// </summary>
    public void DisableDialogueMode()
    {
        if (ActiveDialogue == this && ConversationManager.Instance != null &&
            ConversationManager.Instance.IsConversationActive)
        {
            completionRequested = true;
            return;
        }

        bool wasActive = ActiveDialogue == this;
        CompletePendingInteraction();
        ClearActiveDialogue();
        RestorePlayerControls();
        if (wasActive)
            Finished?.Invoke(true);
    }

    private void RestorePlayerControls()
    {
        PlayerControlLocks.Release(this);
    }
}
