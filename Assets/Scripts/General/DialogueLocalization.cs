using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>A per-conversation snapshot: changing locale affects the next conversation.</summary>
public sealed class DialogueLocalization
{
    public StringTable Table { get; private set; }

    public IEnumerator Load(string collection)
    {
        Table = null;
        if (string.IsNullOrWhiteSpace(collection))
            yield break;

        var initialization = LocalizationSettings.InitializationOperation;
        yield return initialization;
        if (initialization.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogWarning("Dialogue localization initialization failed; using original text.");
            yield break;
        }

        var locale = LocalizationSettings.SelectedLocale;
        var loading = LocalizationSettings.StringDatabase.GetTableAsync(collection, locale);
        yield return loading;
        if (loading.Status == AsyncOperationStatus.Succeeded)
            Table = loading.Result;
        else
            Debug.LogWarning($"Could not load {collection}; using original dialogue text.");
        // Table handles are owned and cached by StringDatabase, not by this snapshot.
    }

    public static string Resolve(StringTable table, string key, string original)
    {
        var entry = table != null ? table.GetEntry(key) : null;
        return entry == null || string.IsNullOrWhiteSpace(entry.Value) ? original : entry.Value;
    }
}
