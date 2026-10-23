using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InteractionIndicatorView : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image dot;
    [SerializeField] private TextMeshProUGUI interactionPrompt;

    public RectTransform Root => root;
    public CanvasGroup Group => group;
    public bool IsConfigured => root != null && root.gameObject == gameObject &&
        group != null && group.gameObject == gameObject && dot != null &&
        interactionPrompt != null && dot.transform.IsChildOf(transform) &&
        interactionPrompt.transform.IsChildOf(transform) &&
        dot.gameObject != gameObject && interactionPrompt.gameObject != gameObject &&
        dot.gameObject != interactionPrompt.gameObject;

    public void SetFocused(bool focused)
    {
        dot.gameObject.SetActive(!focused);
        interactionPrompt.gameObject.SetActive(focused);
    }
}
