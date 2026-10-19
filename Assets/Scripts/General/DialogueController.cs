using UnityEngine;
using DialogueEditor;

public class DialogueController : MonoBehaviour
{
    public static DialogueController ActiveDialogue { get; private set; }

    private PlayerMovementDelay _playerMovementDelay;
    private Interactable pendingInteraction;
    private string tagBeforeDialogue;

    private void OnDestroy()
    {
        ClearActiveDialogue();
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

        CompletePendingInteraction();
        ClearActiveDialogue();
    }

    private void CompletePendingInteraction()
    {
        if (pendingInteraction != null)
            pendingInteraction.CompleteInteraction();
        pendingInteraction = null;
    }

    private void Awake()
    {
        GameObject[] objects = GameObject.FindGameObjectsWithTag("Player");
        foreach (var obj in objects)
        {
            if(obj.TryGetComponent(out _playerMovementDelay))
            {
                break;
            }
        }
    }

    /// <summary>
    /// Starts a dialogue via DialogueEditor.
    /// Stops player movement and shows the cursor.
    /// </summary>
    public void StartDialogue(NPCConversation conversation)
    {
        if (ActiveDialogue == this)
            return;

        if (ActiveDialogue != null)
            ActiveDialogue.InterruptDialogue();

        if (TryGetComponent(out Interactable interactable) && interactable.DeferCompletion())
            pendingInteraction = interactable;

        tagBeforeDialogue = gameObject.tag;
        ActiveDialogue = this;
        // Keep tracking an active conversation even if a scene event disables its source.
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
        _playerMovementDelay.LockControls();
        ShowCursor();
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
        if (_playerMovementDelay != null)
            _playerMovementDelay.UnlockControls();
        HideCursor();
    }

    private void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void HideCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
