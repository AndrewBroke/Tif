using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Temporary, scene-only authoring tool. No runtime components or asset edits.
public static class MarketRecoveryTool
{
    public const string ScenePath = "Assets/Scenes/Act 2 Recover.unity";
    public const string WorkPath = "MarketRecovery";
    public const string OriginalPath = WorkPath + "/OriginalTransforms.json";

    [Serializable] public class Node
    {
        public string id, parent, path, name, mesh;
        public string[] components;
        public Vector3 position, localPosition, scale, min, max;
        public Quaternion localRotation, rotation;
        public bool active, rendered;
    }
    [Serializable] public class Market
    {
        public string name;
        public Vector3 min, max;
        public List<Node> nodes = new List<Node>();
    }
    [Serializable] public class Report
    {
        public string scene;
        public bool dirty;
        public List<Market> markets = new List<Market>();
        public List<string> loadedScenes = new List<string>();
    }
    [Serializable] public class Snapshot
    {
        public string scene;
        public List<Node> nodes = new List<Node>();
    }
    public static Scene TargetScene()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        return scene.IsValid() && scene.isLoaded ? scene : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }

    public static Transform[] Roots(Scene scene)
    {
        var roots = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name.StartsWith("Разрушенный рынок", StringComparison.Ordinal)).OrderBy(t => t.name).ToArray();
        if (roots.Length != 3)
            throw new InvalidOperationException("Expected three destroyed markets; found: " +
                string.Join(", ", scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                    .Where(t => t.name.IndexOf("рын", StringComparison.OrdinalIgnoreCase) >= 0).Select(t => t.name)));
        return roots;
    }

    public static string Id(UnityEngine.Object obj) { return GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString(); }
    public static Transform Resolve(string id)
    {
        GlobalObjectId value;
        return GlobalObjectId.TryParse(id, out value) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(value) as Transform : null;
    }
    static string PathOf(Transform t, Transform root)
    {
        return t == root ? t.name : PathOf(t.parent, root) + "/" + t.GetSiblingIndex() + ":" + t.name;
    }

    public static Bounds Geometry(Transform t, bool descendants = true)
    {
        var filters = descendants ? t.GetComponentsInChildren<MeshFilter>(true) : t.GetComponents<MeshFilter>();
        var points = new List<Vector3>();
        foreach (var f in filters)
            if (f.sharedMesh != null)
                foreach (var v in f.sharedMesh.vertices) points.Add(f.transform.TransformPoint(v));
        if (points.Count == 0) return new Bounds(t.position, Vector3.zero);
        var bounds = new Bounds(points[0], Vector3.zero);
        foreach (var p in points) bounds.Encapsulate(p);
        return bounds;
    }

    public static Node Describe(Transform t, Transform root)
    {
        var mesh = t.GetComponent<MeshFilter>();
        var b = Geometry(t, false);
        return new Node {
            id = Id(t), parent = t.parent == null ? "" : Id(t.parent), path = PathOf(t, root), name = t.name,
            mesh = mesh == null || mesh.sharedMesh == null ? "" : mesh.sharedMesh.name,
            components = t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToArray(),
            position = t.position, localPosition = t.localPosition, scale = t.localScale,
            localRotation = t.localRotation, rotation = t.rotation, active = t.gameObject.activeSelf,
            rendered = mesh != null, min = b.min, max = b.max
        };
    }

    [MenuItem("Tools/TIF/Markets/Inspect Act 2 Recover")]
    public static void Audit()
    {
        Directory.CreateDirectory(WorkPath);
        var scene = TargetScene();
        var report = new Report { scene = scene.path, dirty = scene.isDirty };
        for (int i = 0; i < SceneManager.sceneCount; ++i)
        {
            var s = SceneManager.GetSceneAt(i);
            report.loadedScenes.Add(s.path + " dirty=" + s.isDirty);
        }
        foreach (var root in Roots(scene))
        {
            var bounds = Geometry(root);
            var market = new Market { name = root.name, min = bounds.min, max = bounds.max };
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) market.nodes.Add(Describe(t, root));
            report.markets.Add(market);
        }
        File.WriteAllText(WorkPath + "/Audit.json", JsonUtility.ToJson(report, true));
        Debug.Log("Market hierarchy audit written to " + WorkPath + "/Audit.json");
    }

    public static void SaveOriginal(Scene scene)
    {
        if (File.Exists(OriginalPath)) return;
        Directory.CreateDirectory(WorkPath);
        var snapshot = new Snapshot { scene = scene.path };
        foreach (var root in Roots(scene))
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) snapshot.nodes.Add(Describe(t, root));
        File.WriteAllText(OriginalPath, JsonUtility.ToJson(snapshot, true));
        File.Copy(scene.path, WorkPath + "/Act 2 Recover.before.unity.txt", false);
    }

    [MenuItem("Tools/TIF/Markets/Restore Original Layout")]
    public static void Restore()
    {
        var scene = TargetScene();
        if (!File.Exists(OriginalPath)) throw new InvalidOperationException("No original layout snapshot exists.");
        var snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(OriginalPath));
        if (snapshot.scene != scene.path) throw new InvalidOperationException("Snapshot scene mismatch.");
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Restore original market layouts");
        foreach (var n in snapshot.nodes)
        {
            var t = Resolve(n.id);
            if (t == null || t.gameObject.scene != scene) throw new InvalidOperationException("Missing original object: " + n.path);
            Undo.RecordObject(t, "Restore market transform");
            Undo.RecordObject(t.gameObject, "Restore market visibility");
            t.SetLocalPositionAndRotation(n.localPosition, n.localRotation);
            t.localScale = n.scale;
            t.gameObject.SetActive(n.active);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/TIF/Markets/Apply Three Ruined Layouts")]
    public static void Apply()
    {
        MarketRecoveryComposition.Apply();
    }

}
