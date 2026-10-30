using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>Runs only the quest HUD in a temporary empty scene; never invokes story events.</summary>
[InitializeOnLoad]
public static class QuestLocalizationPlayChecks
{
    private const string SessionKey = "TIF.QuestChecks";
    [Serializable] private sealed class SavedSetup { public SceneSetup[] scenes; public bool background; }
    private static IEnumerator checks;
    private static readonly List<string> passed = new List<string>();
    private static double deadline;
    public static string ResultPath => Path.Combine(Path.GetTempPath(), "tif-quest-play-checks.txt");

    static QuestLocalizationPlayChecks() { EditorApplication.playModeStateChanged += StateChanged; }

    [MenuItem("Tools/TIF/Quest Localization/Run isolated Play Mode checks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !string.IsNullOrEmpty(SessionState.GetString(SessionKey, "")))
            throw new InvalidOperationException("Quest checks are already running or Editor is playing.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        if (setup.Any(s => string.IsNullOrEmpty(s.path) || UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
            throw new InvalidOperationException("Save open scenes before isolated quest checks.");
        SessionState.SetString(SessionKey, JsonUtility.ToJson(new SavedSetup { scenes = setup, background = Application.runInBackground }));
        File.WriteAllText(ResultPath, "RUNNING");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Application.runInBackground = true;
        EditorApplication.isPlaying = true;
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (string.IsNullOrEmpty(SessionState.GetString(SessionKey, ""))) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            passed.Clear();
            deadline = EditorApplication.timeSinceStartup + 90;
            checks = Verify();
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            var setup = JsonUtility.FromJson<SavedSetup>(SessionState.GetString(SessionKey, ""));
            SessionState.EraseString(SessionKey);
            Application.runInBackground = setup.background;
            EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
        }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Quest checks timed out.");
            if (checks.MoveNext()) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            File.WriteAllText(ResultPath, "PASS\n" + string.Join("\n", passed));
            Debug.Log("Isolated quest Play Mode checks passed:\n" + string.Join("\n", passed));
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, "FAIL\n" + exception + "\nPassed:\n" + string.Join("\n", passed));
            Debug.LogException(exception);
        }
        EditorApplication.update -= Tick;
        (checks as IDisposable)?.Dispose();
        checks = null;
        EditorApplication.isPlaying = false;
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException(name);
        passed.Add(name);
    }

    private static void Assign(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static (QuestDisplayController controller, TMP_Text title, TMP_Text description) Create(string titleKey, string descriptionKey)
    {
        var root = new GameObject("Quest test");
        root.SetActive(false);
        var titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(root.transform);
        var descriptionObject = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
        descriptionObject.transform.SetParent(root.transform);
        var title = titleObject.GetComponent<TMP_Text>();
        var description = descriptionObject.GetComponent<TMP_Text>();
        var controller = root.AddComponent<QuestDisplayController>();
        Assign(controller, "questTitleText", title);
        Assign(controller, "questDescriptionText", description);
        Assign(controller, "defaultTitleKey", titleKey);
        Assign(controller, "defaultDescriptionKey", descriptionKey);
        Assign(controller, "defaultTitle", QuestTextCatalog.GetOriginal(titleKey, ""));
        Assign(controller, "defaultDescription", QuestTextCatalog.GetOriginal(descriptionKey, ""));
        return (controller, title, description);
    }

    private static IEnumerator Verify()
    {
        int firstFrame = Time.frameCount;
        var db = LocalizationSettings.StringDatabase;
        var early = Create("act1.home.title", "act1.home.talk_wife");
        early.controller.SetQuestTitleKey("act1.stove.title");
        early.controller.gameObject.SetActive(true);
        early.controller.SetQuestDescriptionKey("act1.stove.find_wood");
        early.controller.SetQuestDescription("literal during initialization");
        while (!LocalizationSettings.InitializationOperation.IsDone) yield return null;
        var ru = LocalizationSettings.AvailableLocales.GetLocale("ru");
        var en = LocalizationSettings.AvailableLocales.GetLocale("en");
        Check(ru != null && en != null, "ru/en locales available");
        LocalizationSettings.SelectedLocale = en;
        var enLoad = db.GetTableAsync(QuestTextCatalog.TableName, en);
        while (!enLoad.IsDone) yield return null;
        yield return null;
        Check(early.title.text == "The stove" && early.description.text == "literal during initialization", "Event before OnEnable survives initialization; legacy call cancels pending localization");

        var fields = new[] {
            Create("act1.home.title", "act1.home.talk_wife"),
            Create("act2.defense.title", "act2.defense.find_soldier"),
            Create("act2.defense.title", "act2.defense.find_soldier"),
            Create("act3.home.title", "act3.home.talk_wife")
        };
        foreach (var field in fields) field.controller.gameObject.SetActive(true);
        foreach (var locale in new[] { ru, en })
        {
            LocalizationSettings.SelectedLocale = locale;
            var loading = db.GetTableAsync(QuestTextCatalog.TableName, locale);
            while (!loading.IsDone) yield return null;
            yield return null;
            string[] expectedTitles = locale == ru ? new[] { "Дом, милый дом", "Защита дома", "Защита дома", "Дом, милый дом?" }
                : new[] { "Home, sweet home", "Defending your home", "Defending your home", "Home, sweet home?" };
            string[] expectedDescriptions = locale == ru ? new[] { "Поговорить с женой", "Найти солдата у часовни", "Найти солдата у часовни", "Поговорить с женой" }
                : new[] { "Talk to your wife", "Find the soldier by the chapel", "Find the soldier by the chapel", "Talk to your wife" };
            Check(fields.Select((f, i) => f.title.text == expectedTitles[i] && f.description.text == expectedDescriptions[i]).All(v => v), "Four scene defaults in " + locale.Identifier.Code);
        }

        var test = fields[0];
        test.controller.SetQuestDescriptionKey("act1.stove.find_wood");
        Check(test.title.text == "Home, sweet home" && test.description.text == "Find firewood", "Description changes independently");
        test.controller.SetQuestTitleKey("act1.stove.title");
        Check(test.title.text == "The stove" && test.description.text == "Find firewood", "Title changes independently");
        LocalizationSettings.SelectedLocale = ru;
        for (int i = 0; i < 100 && (test.title.text != "Печь" || test.description.text != "Найти дрова"); i++) yield return null;
        Check(test.title.text == "Печь" && test.description.text == "Найти дрова", "Locale switch preserves current quest stage");

        foreach (var f in fields) f.controller.enabled = false;
        early.controller.enabled = false;
        db.ReleaseTable(QuestTextCatalog.TableName, en);
        db.ReleaseTable(QuestTextCatalog.TableName, ru);
        test.controller.enabled = true;
        var cold = db.GetTableAsync(QuestTextCatalog.TableName, ru);
        bool coldPending = !cold.IsDone;
        test.controller.SetQuestDescriptionKey("act1.stove.add_wood");
        LocalizationSettings.SelectedLocale = en;
        LocalizationSettings.SelectedLocale = ru;
        test.controller.SetQuestDescriptionKey("act1.before_dark.leave_home");
        LocalizationSettings.SelectedLocale = en;
        var current = db.GetTableAsync(QuestTextCatalog.TableName, en);
        while (!current.IsDone || !cold.IsDone) yield return null;
        yield return null;
        Check(test.title.text == "The stove" && test.description.text == "Leave the house", "Rapid locale/quest changes keep the latest request (cold load pending: " + coldPending + ")");
        Check(coldPending, "Race exercised during an unfinished table load");

        for (int i = 0; i < 5; i++) { test.controller.enabled = false; test.controller.enabled = true; }
        LocalizationSettings.SelectedLocale = ru;
        for (int i = 0; i < 100 && test.description.text != "Выйти из дома"; i++) yield return null;
        Check(test.title.text == "Печь" && test.description.text == "Выйти из дома", "Disable/re-enable preserves state across repeated subscriptions");
        test.controller.SetQuestTitle("literal title");
        test.controller.SetQuestDescription("literal description");
        LocalizationSettings.SelectedLocale = en;
        yield return null;
        Check(test.title.text == "literal title" && test.description.text == "literal description", "Legacy text cannot be overwritten by locale changes");
        test.controller.SetQuest("pair title", "pair description");
        Check(test.title.text == "pair title" && test.description.text == "pair description", "Legacy SetQuest pair");
        test.controller.SetQuestTitle(""); test.controller.SetQuestDescription(null);
        Check(test.title.text == "Дом, милый дом" && test.description.text == "Поговорить с женой", "Legacy empty/null uses serialized defaults");
        test.controller.ResetQuestDisplay();
        for (int i = 0; i < 100 && (test.title.text != "Home, sweet home" || test.description.text != "Talk to your wife"); i++) yield return null;
        Check(test.title.text == "Home, sweet home" && test.description.text == "Talk to your wife", "Reset restores localized scene defaults");

        // Locale switches can release the old cached table handle. Reacquire the current one.
        var fallbackTable = db.GetTableAsync(QuestTextCatalog.TableName, en);
        while (!fallbackTable.IsDone) yield return null;
        var table = fallbackTable.Result;
        string key = "act1.stove.find_wood";
        var entry = table.GetEntry(key);
        string translation = entry.Value;
        try
        {
            entry.Value = "   ";
            test.controller.SetQuestDescriptionKey(key);
            Check(test.description.text == "Найти дрова", "Whitespace translation uses Russian source");
            table.RemoveEntry(key);
            test.controller.SetQuestDescriptionKey(key);
            Check(test.description.text == "Найти дрова", "Missing translation uses Russian source");
        }
        finally { table.AddEntry(key, translation); }
        Check(Time.frameCount > firstFrame, "Play Mode frames advanced");
    }
}
