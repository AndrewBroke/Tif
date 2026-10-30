using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>Displays two independent quest fields; locale changes never advance the story.</summary>
public class QuestDisplayController : MonoBehaviour
{
    [Header("Компоненты текстов")]
    [SerializeField] private TMP_Text questTitleText;
    [SerializeField] private TMP_Text questDescriptionText;

    [Header("Текст по умолчанию")]
    [SerializeField] private string defaultTitle = "Текущее задание";
    [SerializeField] private string defaultDescription = "Заданий нет";

    [Header("Ключи начального задания из Quests (пусто — обычный текст)")]
    [SerializeField] private string defaultTitleKey;
    [SerializeField] private string defaultDescriptionKey;

    private sealed class Field
    {
        public bool Initialized;
        public LocalizedString Reference;
        public string Original;
        public int Revision;
    }

    private readonly Field title = new Field();
    private readonly Field description = new Field();

    private void Awake() => EnsureTextsAssigned();

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
        // An event from another Awake/OnEnable may already have set either field.
        if (!title.Initialized) SetDefault(title, defaultTitleKey, defaultTitle);
        if (!description.Initialized) SetDefault(description, defaultDescriptionKey, defaultDescription);
        Refresh(title);
        Refresh(description);
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
        title.Revision++;
        description.Revision++;
    }

    private void LocaleChanged(Locale _) { Refresh(title); Refresh(description); }

    public void SetQuestTitleKey(string key) => SetKey(title, key, defaultTitle);
    public void SetQuestDescriptionKey(string key) => SetKey(description, key, defaultDescription);

    // Legacy events accept literal text, including the old empty-string defaults.
    public void SetQuestTitle(string value) => SetLiteral(title, string.IsNullOrEmpty(value) ? defaultTitle : value);
    public void SetQuestDescription(string value) => SetLiteral(description, string.IsNullOrEmpty(value) ? defaultDescription : value);

    public void SetQuest(string title, string description)
    {
        SetQuestTitle(title);
        SetQuestDescription(description);
    }

    public void ResetQuestDisplay()
    {
        SetDefault(title, defaultTitleKey, defaultTitle);
        SetDefault(description, defaultDescriptionKey, defaultDescription);
        Refresh(title);
        Refresh(description);
    }

    private void SetDefault(Field field, string key, string original)
    {
        field.Initialized = true;
        field.Reference = string.IsNullOrWhiteSpace(key) ? null : new LocalizedString(QuestTextCatalog.TableName, key);
        field.Original = QuestTextCatalog.GetOriginal(key, original);
    }

    private void SetKey(Field field, string key, string fallback)
    {
        SetDefault(field, key, fallback);
        Refresh(field);
    }

    private void SetLiteral(Field field, string value)
    {
        field.Initialized = true;
        field.Reference = null;
        field.Original = value;
        Refresh(field);
    }

    private void Display(Field field, string value)
    {
        var text = field == title ? questTitleText : questDescriptionText;
        if (text != null) text.text = value;
    }

    private void Refresh(Field field)
    {
        int revision = ++field.Revision;
        Display(field, field.Original);
        if (field.Reference == null || !isActiveAndEnabled) return;
        var reference = field.Reference;
        var initialization = LocalizationSettings.InitializationOperation;
        void Load(AsyncOperationHandle<LocalizationSettings> operation)
        {
            if (!IsCurrent(field, revision) || operation.Status != AsyncOperationStatus.Succeeded) return;
            var locale = LocalizationSettings.SelectedLocale;
            if (locale == null) return;
            // Inspect the actual entry so a missing value never displays Unity's diagnostic text.
            var loading = LocalizationSettings.StringDatabase.GetTableAsync(reference.TableReference, locale);
            void Complete(AsyncOperationHandle<StringTable> table)
            {
                if (!IsCurrent(field, revision)) return;
                var entry = table.Status == AsyncOperationStatus.Succeeded && table.Result != null
                    ? table.Result.GetEntry(reference.TableEntryReference.Key) : null;
                Display(field, entry == null || string.IsNullOrWhiteSpace(entry.Value) ? field.Original : entry.Value);
            }
            if (loading.IsDone) Complete(loading);
            else loading.Completed += Complete;
            // Table handles belong to StringDatabase's cache; do not release them here.
        }
        if (initialization.IsDone) Load(initialization);
        else initialization.Completed += Load;
    }

    private bool IsCurrent(Field field, int revision) => this != null && isActiveAndEnabled && field.Revision == revision;

    [ContextMenu("Сбросить текст задания")]
    private void EnsureTextsAssigned()
    {
        if (questTitleText == null || questDescriptionText == null)
            Debug.LogWarning("QuestDisplayController: назначьте обе ссылки TMP_Text в Inspector.", this);
    }
}
