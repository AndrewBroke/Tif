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

/// <summary>Authoring and verification only; story graphs and listeners are never rewritten.</summary>
public static class Act2DialogueLocalizationTools
{
    private const string ScenePath = "Assets/Scenes/Act 2 Main.unity";
    private const string TableName = "Dialogue_Act2";
    private const string SeedPath = "Assets/Localization/DialogueAct2Translations.json";

    [Serializable] private sealed class Source { public Seed[] conversations; }
    [Serializable] private sealed class Seed
    {
        public string asset, localId, id;
        public Entry[] entries;
    }
    [Serializable] private sealed class Entry { public string key, ru, en; }

    private static Seed[] Seeds()
    {
        var seeds = JsonUtility.FromJson<Source>(File.ReadAllText(SeedPath)).conversations;
        if (seeds == null || seeds.Length == 0 || seeds.Any(s => string.IsNullOrWhiteSpace(s.id)
            || string.IsNullOrWhiteSpace(s.asset) || !ulong.TryParse(s.localId, out _)
            || s.entries == null || s.entries.Length == 0))
            throw new InvalidOperationException("Invalid Act 2 translation source.");
        if (seeds.Select(s => s.id).Distinct().Count() != seeds.Length
            || seeds.Select(s => s.asset + "/" + s.localId).Distinct().Count() != seeds.Length)
            throw new InvalidOperationException("Duplicate Act 2 dialogue identity.");
        var entries = seeds.SelectMany(s => s.entries).ToArray();
        if (entries.Any(e => string.IsNullOrWhiteSpace(e.key) || string.IsNullOrWhiteSpace(e.ru)
            || string.IsNullOrWhiteSpace(e.en)) || entries.Select(e => e.key).Distinct().Count() != entries.Length)
            throw new InvalidOperationException("Missing or duplicate Act 2 translations.");
        return seeds;
    }

    private static void EditModeOnly()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before working with dialogue localization.");
    }

    private static NPCConversation[] Conversations(IEnumerable<GameObject> roots) => roots
        .SelectMany(root => root.GetComponentsInChildren<NPCConversation>(true)).ToArray();

    private static NPCConversation Find(NPCConversation[] npcs, Seed seed) => npcs.Single(n =>
        GlobalObjectId.GetGlobalObjectIdSlow(n).targetObjectId == ulong.Parse(seed.localId));

    private static JObject Graph(NPCConversation npc) => JObject.Parse(
        new SerializedObject(npc).FindProperty("json").stringValue);

    private static Dictionary<string, string> SourceValues(NPCConversation npc, string id)
    {
        var values = new Dictionary<string, string>();
        var graph = Graph(npc);
        foreach (var node in graph["SpeechNodes"].Concat(graph["Options"]))
        {
            string prefix = $"{id}.node_{node["ID"]}";
            values.Add(prefix + ".text", (string)node["Text"]);
            if (!string.IsNullOrWhiteSpace((string)node["Name"]))
                values.Add(prefix + ".name", (string)node["Name"]);
        }
        return values;
    }

    private static void CheckBinding(NPCConversation npc, Seed seed, bool allowUnbound)
    {
        if ((npc.LocalizationId != seed.id || npc.LocalizationTable != TableName)
            && !(allowUnbound && string.IsNullOrEmpty(npc.LocalizationId) && string.IsNullOrEmpty(npc.LocalizationTable)))
            throw new InvalidOperationException("Unexpected dialogue binding: " + seed.asset + "/" + seed.id);
    }

    // Preview scenes and prefab assets keep the user's open scene and its story state untouched.
    private static void Visit(Seed[] seeds, Action<NPCConversation[], Seed[]> action)
    {
        foreach (var group in seeds.GroupBy(s => s.asset))
        {
            var expected = group.ToArray();
            if (group.Key.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                var scene = EditorSceneManager.OpenPreviewScene(group.Key);
                try { VisitRoots(scene.GetRootGameObjects(), expected, action); }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            else
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(group.Key);
                if (root == null) throw new InvalidOperationException("Missing dialogue prefab: " + group.Key);
                VisitRoots(new[] { root }, expected, action);
            }
        }
    }

    private static void VisitRoots(GameObject[] roots, Seed[] seeds, Action<NPCConversation[], Seed[]> action)
    {
        var npcs = Conversations(roots);
        if (npcs.Length != seeds.Length)
            throw new InvalidOperationException("Dialogue count changed: " + seeds[0].asset);
        foreach (var seed in seeds) Find(npcs, seed);
        action(npcs, seeds);
    }

    private static bool Bind(NPCConversation npc, Seed seed)
    {
        CheckBinding(npc, seed, true);
        if (npc.LocalizationId == seed.id && npc.LocalizationTable == TableName) return false;
        var serialized = new SerializedObject(npc);
        string before = EditorJsonUtility.ToJson(npc);
        // Round-trip the two new strings and compare every serialized field, including events/JSON.
        serialized.FindProperty("localizationId").stringValue = seed.id;
        serialized.FindProperty("localizationTable").stringValue = TableName;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        serialized.Update();
        serialized.FindProperty("localizationId").stringValue = "";
        serialized.FindProperty("localizationTable").stringValue = "";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (EditorJsonUtility.ToJson(npc) != before)
            throw new InvalidOperationException("Unexpected serialized changes: " + seed.id);
        serialized.Update();
        serialized.FindProperty("localizationId").stringValue = seed.id;
        serialized.FindProperty("localizationTable").stringValue = TableName;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    [MenuItem("Tools/TIF/Dialogue Localization/Set up Act 2 Main and spawned dialogues")]
    public static void SetUpAct2()
    {
        EditModeOnly();
        var seeds = Seeds();
        var loaded = SceneManager.GetSceneByPath(ScenePath);
        if (loaded.isLoaded && loaded.isDirty)
            throw new InvalidOperationException("Save Act 2 Main before setup; it contains unsaved changes.");
        Visit(seeds, (npcs, group) => {
            foreach (var seed in group)
            {
                var npc = Find(npcs, seed);
                CheckBinding(npc, seed, true);
                if (EditorUtility.IsDirty(npc)) throw new InvalidOperationException("Unsaved dialogue: " + seed.id);
                var values = SourceValues(npc, seed.id);
                if (values.Count != seed.entries.Length || seed.entries.Any(e =>
                    !values.TryGetValue(e.key, out var value) || value != e.ru))
                    throw new InvalidOperationException("Source changed; review translations before setup: " + seed.id);
            }
        });

        var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (collection == null)
        {
            var locales = LocalizationEditorSettings.GetLocales()
                .Where(l => l.Identifier.Code == "ru" || l.Identifier.Code == "en").ToList();
            if (locales.Count != 2) throw new InvalidOperationException("Both ru and en locales are required.");
            collection = LocalizationEditorSettings.CreateStringTableCollection(TableName, "Assets/Localization", locales);
        }
        var en = (StringTable)collection.GetTable(new LocaleIdentifier("en"));
        if (en == null) throw new InvalidOperationException("Act 2 English table is missing.");
        foreach (var entry in seeds.SelectMany(s => s.entries))
            if (en.GetEntry(entry.key) == null) en.AddEntry(entry.key, entry.en);
        EditorUtility.SetDirty(en);

        bool opened = !loaded.isLoaded;
        var scene = opened ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive) : loaded;
        try
        {
            var npcs = Conversations(scene.GetRootGameObjects());
            bool changed = false;
            foreach (var seed in seeds.Where(s => s.asset == ScenePath)) changed |= Bind(Find(npcs, seed), seed);
            if (changed && !EditorSceneManager.SaveScene(scene)) throw new IOException("Act 2 Main could not be saved.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        foreach (var group in seeds.Where(s => s.asset != ScenePath).GroupBy(s => s.asset))
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(group.Key);
            var npcs = Conversations(new[] { root });
            bool changed = false;
            foreach (var seed in group) changed |= Bind(Find(npcs, seed), seed);
            if (changed)
            {
                PrefabUtility.SavePrefabAsset(root, out bool success);
                if (!success) throw new IOException("Could not save " + group.Key);
            }
        }
        ExportAct2();
    }

    [MenuItem("Tools/TIF/Dialogue Localization/Export Act 2 Main and spawned dialogues")]
    public static void ExportAct2()
    {
        EditModeOnly();
        var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (collection == null) throw new InvalidOperationException("Set up Act 2 localization first.");
        var ru = (StringTable)collection.GetTable(new LocaleIdentifier("ru"));
        var en = (StringTable)collection.GetTable(new LocaleIdentifier("en"));
        if (ru == null || en == null) throw new InvalidOperationException("Both ru and en tables are required.");
        Visit(Seeds(), (npcs, group) => {
            foreach (var seed in group)
            {
                var npc = Find(npcs, seed);
                CheckBinding(npc, seed, false);
                foreach (var entry in SourceValues(npc, seed.id))
                {
                    var previous = ru.GetEntry(entry.Key);
                    if (previous == null) ru.AddEntry(entry.Key, entry.Value);
                    else if (previous.Value != entry.Value)
                    {
                        previous.Value = entry.Value;
                        Debug.LogWarning("Source changed; review English translation: " + entry.Key);
                    }
                }
            }
        });
        ru.AddEntry("common.continue", "Далее");
        ru.AddEntry("common.end", "Выйти");
        if (en.GetEntry("common.continue") == null) en.AddEntry("common.continue", "Continue");
        if (en.GetEntry("common.end") == null) en.AddEntry("common.end", "Leave");
        collection.SetPreloadTableFlag(true);
        EditorUtility.SetDirty(collection);
        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(ru);
        EditorUtility.SetDirty(en);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
        AssetDatabase.SaveAssets();
        Debug.Log(ValidateAct2());
    }

    [MenuItem("Tools/TIF/Dialogue Localization/Validate Act 2 Main and spawned dialogues")]
    public static void ValidateAct2Menu() => Debug.Log(ValidateAct2());

    public static string ValidateAct2()
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (collection == null) throw new InvalidOperationException("Dialogue_Act2 is missing.");
        string tables = DialogueLocalizationTools.Validate(collection);
        if (!tables.StartsWith("COMPLETE:", StringComparison.Ordinal)) throw new InvalidOperationException(tables);
        var ru = (StringTable)collection.GetTable(new LocaleIdentifier("ru"));
        int count = 0;
        Visit(Seeds(), (npcs, group) => {
            foreach (var seed in group)
            {
                var npc = Find(npcs, seed);
                CheckBinding(npc, seed, false);
                foreach (var entry in SourceValues(npc, seed.id))
                {
                    if (ru.GetEntry(entry.Key)?.Value != entry.Value)
                        throw new InvalidOperationException("Missing/outdated source: " + entry.Key);
                    count++;
                }
            }
        });
        return tables + $" {count} source references verified across scene and spawned prefabs.";
    }

    [MenuItem("Tools/TIF/Dialogue Localization/Verify Act 2 runtime graphs and repeat export")]
    public static void VerifyAct2()
    {
        EditModeOnly();
        ValidateAct2();
        var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        var en = (StringTable)collection.GetTable(new LocaleIdentifier("en"));
        var translations = en.Values.ToDictionary(e => e.KeyId, e => e.Value);
        int nodes = 0;
        // All deserialization is isolated: it may create missing event holders in its preview objects.
        foreach (var group in Seeds().GroupBy(s => s.asset))
        {
            GameObject prefab = group.Key.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                ? PrefabUtility.LoadPrefabContents(group.Key) : null;
            var scene = prefab != null ? prefab.scene : EditorSceneManager.OpenPreviewScene(group.Key);
            try
            {
                var npcs = Conversations(scene.GetRootGameObjects());
                foreach (var npc in npcs) nodes += VerifyGraph(npc, collection);
            }
            finally
            {
                if (prefab != null) PrefabUtility.UnloadPrefabContents(prefab);
                else EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        ExportAct2();
        if (translations.Count != en.Count || translations.Any(e => en.GetEntry(e.Key)?.Value != e.Value))
            throw new InvalidOperationException("Repeat export changed English translations or shared IDs.");
        Debug.Log($"VERIFIED Act 2: {Seeds().Length} graphs, {nodes} nodes in ru/en, source JSON, connections, event references and repeat export.");
    }

    private static int VerifyGraph(NPCConversation npc, StringTableCollection collection)
    {
        string json = new SerializedObject(npc).FindProperty("json").stringValue;
        var graph = Graph(npc);
        var expected = graph["SpeechNodes"].Concat(graph["Options"]).ToDictionary(n => (int)n["ID"]);
        int rootId = expected.Single(n => (bool)n.Value["EditorInfo"]["isRoot"]).Key;
        var source = npc.Deserialize();
        foreach (var locale in new[] { "ru", "en" })
        {
            var table = (StringTable)collection.GetTable(new LocaleIdentifier(locale));
            var translated = npc.Deserialize(table);
            if (translated.ContinueText != table.GetEntry("common.continue").Value
                || translated.EndText != table.GetEntry("common.end").Value)
                throw new InvalidOperationException("Unlocalized automatic dialogue buttons.");
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
                var links = expected[item.Item1]["Connections"].ToArray();
                if (b.Text != table.GetEntry(prefix + ".text")?.Value || a.GetType() != b.GetType()
                    || a.Connections.Count != b.Connections.Count || b.Connections.Count != links.Length)
                    throw new InvalidOperationException("Runtime text/type/links differ: " + prefix);
                if (a is SpeechNode sa && b is SpeechNode sb
                    && (sb.Name != table.GetEntry(prefix + ".name")?.Value || !ReferenceEquals(sa.Event, sb.Event)))
                    throw new InvalidOperationException("Runtime speaker/event differs: " + prefix);
                if (a is OptionNode oa && b is OptionNode ob && !ReferenceEquals(oa.Event, ob.Event))
                    throw new InvalidOperationException("Runtime option event differs: " + prefix);
                for (int i = 0; i < links.Length; i++)
                {
                    var ac = a.Connections[i];
                    var bc = b.Connections[i];
                    ConversationNode an = ac is SpeechConnection asc ? (ConversationNode)asc.SpeechNode : ((OptionConnection)ac).OptionNode;
                    ConversationNode bn = bc is SpeechConnection bsc ? (ConversationNode)bsc.SpeechNode : ((OptionConnection)bc).OptionNode;
                    pending.Push(Tuple.Create((int)links[i]["NodeUID"], an, bn));
                }
            }
            if (visited.Count != expected.Count) throw new InvalidOperationException("Unreachable nodes: " + npc.LocalizationId);
            if (DialogueLocalization.Resolve(table, "__missing_key__", "original") != "original")
                throw new InvalidOperationException("Missing-key fallback failed.");
        }
        if (json != new SerializedObject(npc).FindProperty("json").stringValue)
            throw new InvalidOperationException("Localization modified the original dialogue JSON.");
        return expected.Count;
    }
}
