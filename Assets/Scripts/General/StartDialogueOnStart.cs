using System.Collections;
using DialogueEditor;
using UnityEngine;

[RequireComponent(typeof(DialogueController))]
public class StartDialogueOnStart : MonoBehaviour
{
    [SerializeField] private NPCConversation conversation;

    private IEnumerator Start()
    {
        // Let scene startup finish before taking ownership of the dialogue UI.
        yield return null;
        if (conversation != null)
            GetComponent<DialogueController>().StartDialogue(conversation);
    }
}
