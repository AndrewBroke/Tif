using UnityEngine;
using DialogueEditor;

public class DialogueController : MonoBehaviour
{
    private PlayerMovementDelay _playerMovementDelay;

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
    /// Starts a dialogue via DialogueEditor (stub).
    /// Stops player movement and shows the cursor.
    /// </summary>
    public void StartDialogue(NPCConversation conversation)
    {
        EnableDialogueMode();

        // TODO: Integrate with DialogueEditor to begin the conversation.
        ConversationManager.Instance.StartConversation(conversation);
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