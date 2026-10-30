using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Editable key/source catalog, also available when a localized table is missing.</summary>
public static class QuestTextCatalog
{
    public const string TableName = "Quests";
    public const string AssetPath = "Assets/Resources/QuestSources.json";

    [Serializable]
    public sealed class Entry
    {
        public string key;
        public string ru;
        public string en;
        public string context;
    }

    [Serializable]
    public sealed class Source { public Entry[] entries; }

    private static Dictionary<string, string> originals;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearCache() => originals = null;

    public static string GetOriginal(string key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key)) return fallback;
        if (originals == null)
        {
            originals = new Dictionary<string, string>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("QuestSources");
            if (asset != null)
                foreach (var entry in JsonUtility.FromJson<Source>(asset.text).entries)
                    originals[entry.key] = entry.ru;
        }
        return originals.TryGetValue(key, out var original) && !string.IsNullOrWhiteSpace(original) ? original : fallback;
    }
}
