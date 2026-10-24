using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogueEditor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

public static class DialogueLocalizationTools
{
    private const string ScenePath = "Assets/Scenes/Act 1.unity";
    private const string CollectionName = "Dialogue_Act1";
    private const string SeedPath = "Assets/Localization/DialogueAct1Translations.json";

    [MenuItem("Tools/TIF/Dialogue Localization/Set up Act 1")]
    public static void SetUpAct1()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before setting up dialogue localization.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (!opened && scene.isDirty)
            throw new InvalidOperationException("Save Act 1 before setup; it contains unsaved changes.");
        if (opened)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var seeds = JObject.Parse(File.ReadAllText(SeedPath))["conversations"].ToArray();
            var conversations = Conversations(scene).ToArray();
            if (conversations.Length != seeds.Length)
                throw new InvalidOperationException("Act 1 dialogue count differs from translation source.");
            var collection = GetOrCreate(CollectionName);
            foreach (var seed in seeds)
            {
                ulong localId = ulong.Parse((string)seed["localId"]);
                var npc = conversations.Single(n => GlobalObjectId.GetGlobalObjectIdSlow(n).targetObjectId == localId);
                var serialized = new SerializedObject(npc);
                serialized.FindProperty("localizationId").stringValue = (string)seed["id"];
                serialized.FindProperty("localizationTable").stringValue = CollectionName;
                serialized.ApplyModifiedProperties();
                foreach (var entry in seed["entries"])
                    SetValue(collection, "en", (string)entry["key"], (string)entry["en"], false);
            }
            Export(conversations);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Act 1 could not be saved.");
            Debug.Log(Validate(collection));
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [MenuItem("Tools/TIF/Dialogue Localization/Export open scenes (preserve translations)")]
    public static void ExportOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before exporting.");
        var npcs = new List<NPCConversation>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            npcs.AddRange(Conversations(SceneManager.GetSceneAt(i)));
        Export(npcs.Where(n => !string.IsNullOrWhiteSpace(n.LocalizationId)).ToArray());
    }

    private static IEnumerable<NPCConversation> Conversations(Scene scene)
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NPCConversation>(true));
    }

    public static void Export(NPCConversation[] conversations)
    {
        if (conversations.Length == 0)
            throw new InvalidOperationException("No localized conversations found in the open scenes.");
        var duplicates = conversations.GroupBy(n => n.LocalizationTable + "/" + n.LocalizationId)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (duplicates.Length > 0)
            throw new InvalidOperationException("Duplicate dialogue IDs: " + string.Join(", ", duplicates));
        var collections = new HashSet<StringTableCollection>();
        foreach (var npc in conversations)
        {
            if (string.IsNullOrWhiteSpace(npc.LocalizationTable))
                throw new InvalidOperationException(npc.name + " has no localization table.");
            var collection = GetOrCreate(npc.LocalizationTable);
            collections.Add(collection);
            // Read serialized JSON directly: DeserializeForEditor can create event holder components.
            string json = new SerializedObject(npc).FindProperty("json").stringValue;
            var graph = JObject.Parse(json);
            foreach (var node in graph["SpeechNodes"].Concat(graph["Options"]))
            {
                string prefix = $"{npc.LocalizationId}.node_{node["ID"]}";
                SetValue(collection, "ru", prefix + ".text", (string)node["Text"], true);
                string speaker = (string)node["Name"];
                if (!string.IsNullOrWhiteSpace(speaker))
                    SetValue(collection, "ru", prefix + ".name", speaker, true);
            }
            SetValue(collection, "ru", "common.continue", "Далее", true);
            SetValue(collection, "en", "common.continue", "Continue", false);
            SetValue(collection, "ru", "common.end", "Выйти", true);
            SetValue(collection, "en", "common.end", "Leave", false);
        }
        foreach (var collection in collections)
        {
            collection.SetPreloadTableFlag(true);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
            foreach (var table in collection.StringTables)
                EditorUtility.SetDirty(table);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
        }
        AssetDatabase.SaveAssets();
        foreach (var collection in collections)
            Debug.Log(Validate(collection));
    }

    private static StringTableCollection GetOrCreate(string name)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollections()
            .FirstOrDefault(c => c.TableCollectionName == name);
        if (collection == null)
        {
            var locales = LocalizationEditorSettings.GetLocales()
                .Where(l => l.Identifier.Code == "ru" || l.Identifier.Code == "en").ToList();
            if (locales.Count != 2)
                throw new InvalidOperationException("Both ru and en locales are required.");
            collection = LocalizationEditorSettings.CreateStringTableCollection(name, "Assets/Localization", locales);
        }
        return collection;
    }

    private static void SetValue(StringTableCollection collection, string locale, string key, string value, bool overwrite)
    {
        var table = collection.GetTable(new LocaleIdentifier(locale)) as StringTable;
        if (table == null)
            throw new InvalidOperationException($"Missing locale {locale} in {collection.TableCollectionName}.");
        var entry = table.GetEntry(key);
        if (entry == null)
            table.AddEntry(key, value ?? "");
        else if (overwrite)
        {
            if (entry.Value != value)
                Debug.LogWarning($"Source changed: {key}. Review existing translations.");
            entry.Value = value ?? "";
        }
    }

    [MenuItem("Tools/TIF/Dialogue Localization/Validate Act 1")]
    public static void ValidateAct1()
    {
        var collection = LocalizationEditorSettings.GetStringTableCollections()
            .FirstOrDefault(c => c.TableCollectionName == CollectionName);
        if (collection == null)
            throw new InvalidOperationException("Dialogue_Act1 has not been created.");
        Debug.Log(Validate(collection));
    }

    public static string Validate(StringTableCollection collection)
    {
        var gaps = new List<string>();
        int checkedCount = 0;
        foreach (var key in collection.SharedData.Entries)
        {
            foreach (string locale in new[] { "ru", "en" })
            {
                checkedCount++;
                var table = collection.GetTable(new LocaleIdentifier(locale)) as StringTable;
                if (table == null || string.IsNullOrWhiteSpace(table.GetEntry(key.Id)?.Value))
                    gaps.Add($"{locale}/{key.Key}");
            }
        }
        if (checkedCount == 0)
            return "INCONCLUSIVE: no dialogue table entries found.";
        return gaps.Count == 0 ? $"COMPLETE: {checkedCount} entries checked, no gaps."
            : $"GAPS ({gaps.Count}/{checkedCount}):\n" + string.Join("\n", gaps);
    }

    [MenuItem("Tools/TIF/Dialogue Localization/Verify runtime graphs and repeat export")]
    public static void VerifyRuntimeGraphs()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var npcs = Conversations(scene).ToArray();
            var collection = GetOrCreate(CollectionName);
            var en = (StringTable)collection.GetTable(new LocaleIdentifier("en"));
            var before = en.Values.ToDictionary(e => e.KeyId, e => e.Value);
            int nodesChecked = 0;
            foreach (var npc in npcs)
            {
                string json = new SerializedObject(npc).FindProperty("json").stringValue;
                var graph = JObject.Parse(json);
                var expected = graph["SpeechNodes"].Concat(graph["Options"])
                    .ToDictionary(n => (int)n["ID"]);
                int rootId = expected.Single(n => (bool)n.Value["EditorInfo"]["isRoot"]).Key;
                var source = npc.Deserialize();
                var translated = npc.Deserialize(en);
                var pending = new Stack<Tuple<int, ConversationNode, ConversationNode>>();
                pending.Push(Tuple.Create(rootId, (ConversationNode)source.Root, (ConversationNode)translated.Root));
                var visited = new HashSet<int>();
                while (pending.Count > 0)
                {
                    var item = pending.Pop();
                    if (!visited.Add(item.Item1)) continue;
                    var a = item.Item2;
                    var b = item.Item3;
                    string prefix = $"{npc.LocalizationId}.node_{item.Item1}";
                    if (b.Text != en.GetEntry(prefix + ".text").Value || a.Connections.Count != b.Connections.Count)
                        throw new InvalidOperationException("Runtime text or connections differ: " + prefix);
                    if (a is SpeechNode sa && b is SpeechNode sb)
                    {
                        if (sb.Name != en.GetEntry(prefix + ".name").Value || !ReferenceEquals(sa.Event, sb.Event))
                            throw new InvalidOperationException("Runtime speaker or event differs: " + prefix);
                    }
                    if (a is OptionNode oa && b is OptionNode ob && !ReferenceEquals(oa.Event, ob.Event))
                        throw new InvalidOperationException("Runtime option event differs: " + prefix);
                    var links = expected[item.Item1]["Connections"].ToArray();
                    for (int i = 0; i < links.Length; i++)
                    {
                        var ac = a.Connections[i];
                        var bc = b.Connections[i];
                        ConversationNode an = ac is SpeechConnection asc ? (ConversationNode)asc.SpeechNode : ((OptionConnection)ac).OptionNode;
                        ConversationNode bn = bc is SpeechConnection bsc ? (ConversationNode)bsc.SpeechNode : ((OptionConnection)bc).OptionNode;
                        pending.Push(Tuple.Create((int)links[i]["NodeUID"], an, bn));
                    }
                    nodesChecked++;
                }
                if (visited.Count != expected.Count)
                    throw new InvalidOperationException("Unreachable nodes require manual review: " + npc.LocalizationId);
                if (json != new SerializedObject(npc).FindProperty("json").stringValue)
                    throw new InvalidOperationException("Localization modified the original JSON.");
            }
            if (DialogueLocalization.Resolve(en, "__missing_key__", "original") != "original")
                throw new InvalidOperationException("Missing-key fallback failed.");
            var emptyTable = ScriptableObject.CreateInstance<StringTable>();
            emptyTable.SharedData = ScriptableObject.CreateInstance<SharedTableData>();
            try
            {
                emptyTable.AddEntry("empty", "");
                if (DialogueLocalization.Resolve(emptyTable, "empty", "original") != "original")
                    throw new InvalidOperationException("Empty-entry fallback failed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(emptyTable.SharedData);
                UnityEngine.Object.DestroyImmediate(emptyTable);
            }
            Export(npcs);
            if (before.Count != en.Count || before.Any(e => en.GetEntry(e.Key)?.Value != e.Value))
                throw new InvalidOperationException("Repeat export modified English translations.");
            Debug.Log($"VERIFIED: {npcs.Length} runtime graphs, {nodesChecked} nodes, event references, original JSON, missing/empty-entry fallback and repeat export.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
