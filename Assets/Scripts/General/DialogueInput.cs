using DialogueEditor;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(ConversationManager))]
public class DialogueInput : MonoBehaviour
{
    private ConversationManager conversationManager;
    private InputAction dialoguePress;
    private bool enabledAction;

    private void Awake()
    {
        conversationManager = GetComponent<ConversationManager>();
    }

    private void OnEnable()
    {
        dialoguePress = InputSystem.actions?.FindAction("DialoguePress");
        if (dialoguePress == null)
        {
            Debug.LogWarning("DialogueInput: DialoguePress action was not found in InputSystem.actions.", this);
            return;
        }

        enabledAction = !dialoguePress.enabled;
        if (enabledAction)
            dialoguePress.Enable();
    }

    private void OnDisable()
    {
        if (enabledAction)
            dialoguePress?.Disable();
        enabledAction = false;
        dialoguePress = null;
    }

    private void Update()
    {
        if (dialoguePress == null || !dialoguePress.WasPressedThisFrame() ||
            Time.timeScale == 0f || conversationManager != ConversationManager.Instance ||
            !conversationManager.isActiveAndEnabled || !conversationManager.IsConversationActive ||
            conversationManager.OptionsPanel == null)
            return;

        Transform options = conversationManager.OptionsPanel;
        for (int i = 0; i < options.childCount; i++)
        {
            if (!options.GetChild(i).TryGetComponent(out UIConversationButton button) ||
                !button.isActiveAndEnabled)
                continue;

            conversationManager.AlertHover(button);
            // The manager accepts the press only when the dialogue is ready for input.
            conversationManager.PressSelectedOption();
            return;
        }
    }
}
