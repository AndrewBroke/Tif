using UnityEngine;
using TMPro;

public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private string format = "Нажмите {0} чтобы взаимодействовать";

    private void Awake()
    {
        Hide();
    }

    public void Show(string binding, string label = null)
    {
        string text = string.Format(format, binding);
        if (!string.IsNullOrEmpty(label))
            text += $" {label}";
        promptText.text = text;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public void Hide()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
