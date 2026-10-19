using UnityEngine;
using DialogueEditor;

public class DialogueController : MonoBehaviour
{
    public static DialogueController ActiveDialogue { get; private set; }

    private Interactable pendingInteraction;
    private string tagBeforeDialogue;

    private void OnDisable()
    {
        InterruptDialogue();
        PlayerControlLocks.Release(this);
    }

    private void OnDestroy()
    {
        ClearActiveDialogue();
        PlayerControlLocks.Release(this);
    }

    private void ClearActiveDialogue()
    {
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

        if (TryGetComponent(out Interactable interactable) && interactable.DeferCompletion())
            pendingInteraction = interactable;

        tagBeforeDialogue = gameObject.tag;
        ActiveDialogue = this;
        ConversationManager.OnConversationEnded += HandleConversationEnded;

        try
        {
            EnableDialogueMode();
            ConversationManager.Instance.StartConversation(conversation);
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

    /// <summary>
    /// Interrupts this conversation without completion events and allows interacting again.
    /// </summary>
    public void InterruptDialogue()
    {
        if (ActiveDialogue != this)
            return;

        if (ConversationManager.Instance != null)
            ConversationManager.Instance.CancelConversation();
        if (pendingInteraction != null)
            pendingInteraction.CancelInteraction();
        pendingInteraction = null;
        RestorePlayerControls();

        gameObject.tag = tagBeforeDialogue;

        ClearActiveDialogue();
    }

    /// <summary>
    /// Stops player input and unlocks & shows the cursor.
    /// </summary>
    public void EnableDialogueMode()
    {
        PlayerControlLocks.Acquire(this, showCursor: true);
    }

    /// <summary>
    /// Resumes player input and locks & hides the cursor.
    /// </summary>
    public void DisableDialogueMode()
    {
        CompletePendingInteraction();
        ClearActiveDialogue();
        RestorePlayerControls();
    }

    private void RestorePlayerControls()
    {
        PlayerControlLocks.Release(this);
    }
}
