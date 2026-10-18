using UnityEngine;
using DialogueEditor;

public class DialogueController : MonoBehaviour
{
    public static DialogueController ActiveDialogue { get; private set; }

    private PlayerMovementDelay _playerMovementDelay;

    private void OnEnable()
    {
        ConversationManager.OnConversationEnded += ClearActiveDialogue;
    }

    private void OnDisable()
    {
        ConversationManager.OnConversationEnded -= ClearActiveDialogue;
        ClearActiveDialogue();
    }

    private void ClearActiveDialogue()
    {
        if (ActiveDialogue == this)
            ActiveDialogue = null;
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
        ActiveDialogue = this;
        EnableDialogueMode();

        ConversationManager.Instance.StartConversation(conversation);
    }

    /// <summary>
    /// Interrupts this conversation without completion events and allows interacting again.
    /// </summary>
    public void InterruptDialogue()
    {
        if (ActiveDialogue != this)
            return;

        ConversationManager.Instance.CancelConversation();
        DisableDialogueMode();

        if (TryGetComponent(out SetTagObject tagObject))
            tagObject.SetTag();

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
