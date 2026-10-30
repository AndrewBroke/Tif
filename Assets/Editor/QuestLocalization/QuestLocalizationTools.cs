using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

public static class QuestLocalizationTools
{
    private const string MigrationPath = "Assets/Editor/QuestLocalization/QuestMigration.json";
    public static readonly string[] ScenePaths = {
        "Assets/Scenes/Act 1.unity", "Assets/Scenes/Act 2 Main.unity",
        "Assets/Scenes/Act 2 Recover.unity", "Assets/Scenes/Act 3.unity"
    };

    [Serializable] public sealed class Binding
    {
        public string scene, componentId, property, method, ru, key, context;
        public bool IsDefault => string.IsNullOrEmpty(method);
    }
    [Serializable] private sealed class Migration { public Binding[] bindings; }

    private static void EditModeOnly()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before editing quest localization.");
    }

    public static QuestTextCatalog.Entry[] ReadSources()
    {
        var entries = JsonUtility.FromJson<QuestTextCatalog.Source>(File.ReadAllText(QuestTextCatalog.AssetPath)).entries;
        if (entries == null || entries.Length == 0 || entries.Any(e => string.IsNullOrWhiteSpace(e.key) || string.IsNullOrWhiteSpace(e.ru)))
            throw new InvalidOperationException("QuestSources requires non-empty keys and Russian sources.");
        if (entries.GroupBy(e => e.key).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Duplicate quest source keys.");
        return entries;
    }

    private static StringTableCollection Collection() => LocalizationEditorSettings.GetStringTableCollections()
        .SingleOrDefault(c => c.TableCollectionName == QuestTextCatalog.TableName);

    [MenuItem("Tools/TIF/Quest Localization/Export sources (preserve translations)")]
    public static void ExportSources()
    {
        EditModeOnly();
        var sources = ReadSources();
        var collection = Collection();
        if (collection == null)
        {
            var locales = LocalizationEditorSettings.GetLocales()
                .Where(l => l.Identifier.Code == "ru" || l.Identifier.Code == "en").ToList();
            if (locales.Count != 2) throw new InvalidOperationException("Both ru and en locales are required.");
            collection = LocalizationEditorSettings.CreateStringTableCollection(QuestTextCatalog.TableName, "Assets/Localization", locales);
        }
        var ru = collection.GetTable(new LocaleIdentifier("ru")) as StringTable;
        var en = collection.GetTable(new LocaleIdentifier("en")) as StringTable;
        if (ru == null || en == null) throw new InvalidOperationException("Quests requires ru and en tables.");
        foreach (var source in sources)
        {
            var entry = ru.GetEntry(source.key);
            if (entry == null) ru.AddEntry(source.key, source.ru);
            else if (entry.Value != source.ru)
            {
                entry.Value = source.ru;
                Debug.LogWarning($"Quest source changed: {source.key}. Review its translation.");
            }
            // Seed only absent entries. An intentionally empty translation stays empty and is reported.
            if (en.GetEntry(source.key) == null) en.AddEntry(source.key, source.en ?? "");
        }
        collection.SetPreloadTableFlag(true);
        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(collection);
        EditorUtility.SetDirty(ru);
        EditorUtility.SetDirty(en);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
        AssetDatabase.SaveAssets();
        Debug.Log(ValidateTables());
    }

    public static string ValidateTables()
    {
        var collection = Collection();
        if (collection == null) throw new InvalidOperationException("Quests collection is missing.");
        var gaps = new List<string>();
        var sources = ReadSources().ToDictionary(e => e.key);
        foreach (var source in sources.Values)
            if (collection.SharedData.GetEntry(source.key) == null) gaps.Add("Missing shared key: " + source.key);
        int count = 0;
        foreach (var key in collection.SharedData.Entries)
        {
            foreach (var locale in new[] { "ru", "en" })
            {
                count++;
                var table = collection.GetTable(new LocaleIdentifier(locale)) as StringTable;
                if (string.IsNullOrWhiteSpace(table?.GetEntry(key.Id)?.Value)) gaps.Add(locale + "/" + key.Key);
                if (locale == "ru" && sources.TryGetValue(key.Key, out var source) && table?.GetEntry(key.Id)?.Value != source.ru)
                    gaps.Add("Outdated source: " + key.Key);
            }
        }
        if (count == 0) throw new InvalidOperationException("INCONCLUSIVE: no quest entries checked.");
        if (gaps.Count > 0) throw new InvalidOperationException("Quest gaps:\n" + string.Join("\n", gaps));
        return $"COMPLETE: {count} quest values checked, no gaps.";
    }

    private static Binding[] Bindings() => JsonUtility.FromJson<Migration>(File.ReadAllText(MigrationPath)).bindings;
    private static MonoBehaviour[] Components(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();

    // Explicit component IDs and property paths are only used by this one-time migration,
    // never by the runtime key or ordinary source export.
    private static int Process(Scene scene, Binding[] bindings, bool migrate)
    {
        var components = Components(scene);
        var byId = components.GroupBy(c => GlobalObjectId.GetGlobalObjectIdSlow(c).targetObjectId.ToString())
            .ToDictionary(g => g.Key, g => g.ToArray());
        var sources = ReadSources().ToDictionary(e => e.key);
        int changes = 0;
        foreach (var group in bindings.GroupBy(b => b.componentId))
        {
            if (!byId.TryGetValue(group.Key, out var candidates) || candidates.Length != 1)
                throw new InvalidOperationException($"Missing quest component {scene.path}/{group.Key}.");
            var component = candidates[0];
            var serialized = new SerializedObject(component);
            string before = EditorJsonUtility.ToJson(component);
            var edits = new List<(string path, string before, string after)>();
            foreach (var binding in group)
            {
                if (!sources.ContainsKey(binding.key))
                    throw new InvalidOperationException("Missing migration source: " + binding.key);
                if (binding.IsDefault)
                {
                    if (!(component is QuestDisplayController) || serialized.FindProperty(binding.property)?.stringValue != binding.ru)
                        throw new InvalidOperationException("Initial quest mismatch: " + binding.context);
                    var key = serialized.FindProperty(binding.property + "Key");
                    if (key.stringValue == binding.key) continue;
                    if (!migrate || !string.IsNullOrEmpty(key.stringValue)) throw new InvalidOperationException("Initial key mismatch: " + binding.key);
                    edits.Add((key.propertyPath, key.stringValue, binding.key));
                }
                else
                {
                    var method = serialized.FindProperty(binding.property + ".m_MethodName");
                    var arg = serialized.FindProperty(binding.property + ".m_Arguments.m_StringArgument");
                    var target = serialized.FindProperty(binding.property + ".m_Target");
                    var mode = serialized.FindProperty(binding.property + ".m_Mode");
                    if (method == null || arg == null || !(target.objectReferenceValue is QuestDisplayController) || mode.intValue != (int)UnityEngine.Events.PersistentListenerMode.String)
                        throw new InvalidOperationException("Quest event binding mismatch: " + binding.context);
                    if (method.stringValue == binding.method + "Key" && arg.stringValue == binding.key) continue;
                    if (!migrate || method.stringValue != binding.method || arg.stringValue != binding.ru)
                        throw new InvalidOperationException("Quest event content mismatch: " + binding.context);
                    edits.Add((method.propertyPath, method.stringValue, binding.method + "Key"));
                    edits.Add((arg.propertyPath, arg.stringValue, binding.key));
                }
            }
            if (edits.Count == 0) continue;
            foreach (var edit in edits) serialized.FindProperty(edit.path).stringValue = edit.after;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Round-trip only our string edits and compare the entire component serialization.
            // This protects graph JSON, listeners, references, ordering and other arguments.
            serialized.Update();
            foreach (var edit in edits) serialized.FindProperty(edit.path).stringValue = edit.before;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (EditorJsonUtility.ToJson(component) != before)
                throw new InvalidOperationException("Unexpected serialized changes in " + component.name);
            serialized.Update();
            foreach (var edit in edits) serialized.FindProperty(edit.path).stringValue = edit.after;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            changes += edits.Count;
        }
        ValidateReferences(components, sources.Keys.ToHashSet());
        return changes;
    }

    private static void ValidateReferences(MonoBehaviour[] components, HashSet<string> keys)
    {
        foreach (var component in components)
        {
            var serialized = new SerializedObject(component);
            if (component is QuestDisplayController)
                foreach (var field in new[] { "defaultTitleKey", "defaultDescriptionKey" })
                    if (!keys.Contains(serialized.FindProperty(field).stringValue)) throw new InvalidOperationException("Missing initial quest key: " + field);
            var it = serialized.GetIterator();
            while (it.Next(true))
            {
                if (it.name != "m_MethodName" || it.propertyType != SerializedPropertyType.String) continue;
                string call = it.propertyPath.Substring(0, it.propertyPath.LastIndexOf('.'));
                if (!(serialized.FindProperty(call + ".m_Target")?.objectReferenceValue is QuestDisplayController)) continue;
                if (it.stringValue == "SetQuestTitle" || it.stringValue == "SetQuestDescription" || it.stringValue == "SetQuest")
                    throw new InvalidOperationException("Unmigrated quest call: " + component.name + "/" + it.propertyPath);
                if (it.stringValue != "SetQuestTitleKey" && it.stringValue != "SetQuestDescriptionKey") continue;
                var key = serialized.FindProperty(call + ".m_Arguments.m_StringArgument").stringValue;
                if (!keys.Contains(key)) throw new InvalidOperationException("Unknown event quest key: " + key);
            }
        }
    }

    [MenuItem("Tools/TIF/Quest Localization/Migrate four story scenes (one time)")]
    public static void MigrateScenes()
    {
        EditModeOnly();
        foreach (var path in ScenePaths)
        {
            var loaded = SceneManager.GetSceneByPath(path);
            if (loaded.isLoaded && loaded.isDirty) throw new InvalidOperationException("Save scene before migration: " + path);
        }
        var bindings = Bindings();
        PreflightMigration();
        ExportSources();
        foreach (var path in ScenePaths)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                int changes = Process(scene, bindings.Where(b => b.scene == path).ToArray(), true);
                if (changes > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                }
                Debug.Log($"Quest migration: {path}, {changes} string fields changed; other component data verified.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        ValidateAll();
    }

    public static string PreflightMigration()
    {
        EditModeOnly();
        var bindings = Bindings();
        int changes = 0;
        // Preview copies are discarded. This does not save or modify loaded scenes.
        foreach (var path in ScenePaths)
        {
            var preview = EditorSceneManager.OpenPreviewScene(path);
            try { changes += Process(preview, bindings.Where(b => b.scene == path).ToArray(), true); }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        return $"Preflight passed: {bindings.Count(b => !b.IsDefault)} events, {bindings.Count(b => b.IsDefault)} defaults, {changes} string edits. Component serialization round-trip verified; no scenes saved.";
    }

    [MenuItem("Tools/TIF/Quest Localization/Validate tables and story references")]
    public static void ValidateAll()
    {
        EditModeOnly();
        Debug.Log(ValidateTables());
        var bindings = Bindings();
        foreach (var path in ScenePaths)
        {
            var preview = EditorSceneManager.OpenPreviewScene(path);
            try { Process(preview, bindings.Where(b => b.scene == path).ToArray(), false); }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        string medik = File.ReadAllText("Assets/Scripts/Act 2/MedikFallController.cs");
        if (!medik.Contains("SetQuestDescriptionKey(\"act2.orderly.approach\")") || !ReadSources().Any(e => e.key == "act2.orderly.approach"))
            throw new InvalidOperationException("MedikFallController quest call is not localized.");
        Debug.Log($"Quest references verified: {bindings.Count(b => !b.IsDefault)} events, {bindings.Count(b => b.IsDefault)} defaults, MedikFallController.");
    }

    [MenuItem("Tools/TIF/Quest Localization/Verify repeat export")]
    public static void VerifyRepeatExport()
    {
        EditModeOnly();
        var beforeScenes = ScenePaths.ToDictionary(p => p, File.ReadAllBytes);
        var collection = Collection();
        if (collection == null) throw new InvalidOperationException("Export Quests first.");
        var en = collection.GetTable(new LocaleIdentifier("en")) as StringTable;
        var beforeEntries = en.Values.ToDictionary(e => e.KeyId, e => e.Value);
        var beforeKeys = collection.SharedData.Entries.ToDictionary(e => e.Id, e => e.Key);
        var probe = en.GetEntry("common.title");
        string original = probe.Value;
        const string editedTranslation = "Translator edit: preserve this value";
        try
        {
            probe.Value = editedTranslation;
            beforeEntries[probe.KeyId] = editedTranslation;
            ExportSources();
            ExportSources();
            if (beforeScenes.Any(p => !p.Value.SequenceEqual(File.ReadAllBytes(p.Key))) ||
                beforeEntries.Any(p => en.GetEntry(p.Key)?.Value != p.Value) ||
                beforeKeys.Any(p => collection.SharedData.GetEntry(p.Key)?.Key != p.Value))
                throw new InvalidOperationException("Repeat export changed scenes, existing translations or key IDs.");
            ValidateAll();
            Debug.Log("Repeat quest export preserved scene bytes, translator-edited English values and shared key IDs.");
        }
        finally
        {
            probe.Value = original;
            EditorUtility.SetDirty(en);
            AssetDatabase.SaveAssets();
        }
    }
}
