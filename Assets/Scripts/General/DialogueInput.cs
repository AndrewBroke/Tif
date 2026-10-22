using DialogueEditor;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(ConversationManager))]
public class DialogueInput : MonoBehaviour
{
    private ConversationManager conversationManager;
    private InputAction dialoguePress;

    private void Awake()
    {
        conversationManager = GetComponent<ConversationManager>();
    }

    private void OnEnable()
    {
        InputAction sourceAction = InputSystem.actions?.FindAction("DialoguePress");
        if (sourceAction == null)
        {
            Debug.LogWarning("DialogueInput: DialoguePress action was not found in InputSystem.actions.", this);
            return;
        }

        // PlayerInput disables its action map while dialogue locks gameplay controls.
        // Listen independently, keeping the bindings configured on DialoguePress.
        dialoguePress = sourceAction.Clone();
        dialoguePress.Enable();
    }

    private void OnDisable()
    {
        dialoguePress?.Disable();
        dialoguePress?.Dispose();
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
