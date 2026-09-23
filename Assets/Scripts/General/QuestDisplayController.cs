using TMPro;
using UnityEngine;

/// <summary>
/// Отвечает за отображение текущего задания в углу экрана.
/// Содержит публичные методы, которые удобно вызывать из UnityEvent.
/// </summary>
public class QuestDisplayController : MonoBehaviour
{
    [Header("Компоненты текстов")]
    [SerializeField] private TMP_Text questTitleText;
    [SerializeField] private TMP_Text questDescriptionText;

    [Header("Текст по умолчанию")]
    [SerializeField] private string defaultTitle = "Текущее задание";
    [SerializeField] private string defaultDescription = "Заданий нет";

    private void Awake()
    {
        EnsureTextsAssigned();
    }

    private void Start()
    {
        ResetQuestDisplay();
    }

    /// <summary>
    /// Устанавливает заголовок задания напрямую.
    /// </summary>
    public void SetQuestTitle(string title)
    {
        if (questTitleText == null) return;
        questTitleText.text = string.IsNullOrEmpty(title) ? defaultTitle : title;
    }

    /// <summary>
    /// Устанавливает описание задания напрямую.
    /// </summary>
    public void SetQuestDescription(string description)
    {
        if (questDescriptionText == null) return;
        questDescriptionText.text = string.IsNullOrEmpty(description) ? defaultDescription : description;
    }

    /// <summary>
    /// Устанавливает одновременно заголовок и описание задания.
    /// Метод удобен для UnityEvent, вызывающих метод с двумя строковыми параметрами.
    /// </summary>
    public void SetQuest(string title, string description)
    {
        SetQuestTitle(title);
        SetQuestDescription(description);
    }

    /// <summary>
    /// Сбрасывает отображение к значениям по умолчанию.
    /// </summary>
    public void ResetQuestDisplay()
    {
        SetQuestTitle(defaultTitle);
        SetQuestDescription(defaultDescription);
    }

    [ContextMenu("Сбросить текст задания")]
    private void EnsureTextsAssigned()
    {
        if (questTitleText == null || questDescriptionText == null)
        {
            Debug.LogWarning(
                "QuestDisplayController: не все ссылки на TMP_Text установлены. " +
                "Присоединяйте компонент к объекту с UI и задавайте текстовые поля вручную." ,
                this
            );
        }
    }
}