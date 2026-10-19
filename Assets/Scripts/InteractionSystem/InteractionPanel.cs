using UnityEngine;

[DisallowMultipleComponent]
public sealed class InteractionPanel : MonoBehaviour
{
    [Tooltip("Действие, открывающее эту панель. При закрытии оно снова становится доступным.")]
    [SerializeField] private Interactable source;

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // Component reloads/disabling alone are not a panel close.
        if (source == null || gameObject.activeInHierarchy)
            return;

        if (source.TryGetComponent(out DialogueController dialogue))
            dialogue.DisableDialogueMode();

        source.SetAvailable(true);
        if (source.TryGetComponent(out SetTagObject tagObject))
            tagObject.SetTag();
    }
}
