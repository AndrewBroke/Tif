using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Authored for the seven existing FBX hierarchies in Act 2 Recover, not a generic randomizer.
public static class MarketRecoveryComposition
{
    [Serializable] public class StallPlan { public float tip, yaw, x, z; }
    [Serializable] public class Layout { public List<StallPlan> stalls = new List<StallPlan>(); }
    [Serializable] public class Plans { public List<Layout> markets = new List<Layout>(); }
    [Serializable] public class Result
    {
        public string market;
        public int tippedStalls, groundProps, hiddenGoods, retainedProps;
        public float largestPropTravel, largestStallTravel;
        public List<string> ground = new List<string>();
        public List<string> obstructedProps = new List<string>();
        public List<string> intersectingStalls = new List<string>();
        public float maximumPropPenetration, maximumSupportGap;
    }
    [Serializable] public class Results { public List<Result> markets = new List<Result>(); }
    static Transform[] marketRoots;
    static readonly Dictionary<Mesh, Vector3[]> Vertices = new Dictionary<Mesh, Vector3[]>();
    static readonly Dictionary<Mesh, int[]> Triangles = new Dictionary<Mesh, int[]>();

    sealed class Piece
    {
        public Transform t;
        public Vector3 position;
        public Quaternion rotation;
        public Bounds bounds;
        public bool architecture;
    }
    sealed class Stall
    {
        public Transform t;
        public List<Piece> parts;
        public Bounds bounds;
        public Vector3 axis;
        public Quaternion delta;
        public Vector3 shift;
        public StallPlan plan;
    }

    static int Suffix(Transform t) { return int.Parse(t.name.Substring(t.name.LastIndexOf('.') + 1)); }
    static bool Architecture(int stall, int part)
    {
        switch (stall)
        {
            case 1: return part <= 9;
            case 2: return (part >= 13 && part <= 27) || (part >= 33 && part <= 38) || part == 58;
            case 3: return part == 2 || (part >= 8 && part <= 52);
            case 4: return part <= 34 || part == 37;
            case 5: return part >= 20 && part <= 64;
            case 6: return part >= 2 && part <= 46;
            case 7: return (part >= 28 && part <= 42) || (part >= 54 && part <= 59) || part == 89;
            default: throw new InvalidOperationException("Unrecognized stall.");
        }
    }
    static Vector3[] MeshVertices(Mesh mesh)
    {
        Vector3[] result;
        if (!Vertices.TryGetValue(mesh, out result)) Vertices[mesh] = result = mesh.vertices;
        return result;
    }
    static IEnumerable<Vector3> Points(Transform t)
    {
        var f = t.GetComponent<MeshFilter>();
        if (f == null || f.sharedMesh == null) yield break;
        foreach (var p in MeshVertices(f.sharedMesh)) yield return t.TransformPoint(p);
    }
    static Bounds BoundsOf(IEnumerable<Vector3> points)
    {
        var array = points.ToArray();
        if (array.Length == 0) throw new InvalidOperationException("No mesh geometry.");
        var b = new Bounds(array[0], Vector3.zero);
        foreach (var p in array) b.Encapsulate(p);
        return b;
    }
    static Vector3 LongAxis(IEnumerable<Vector3> points)
    {
        var a = points.ToArray();
        var c = a.Aggregate(Vector3.zero, (x, y) => x + y) / a.Length;
        float xx = 0, zz = 0, xz = 0;
        foreach (var p in a) { var d = p - c; xx += d.x * d.x; zz += d.z * d.z; xz += d.x * d.z; }
        var angle = 0.5f * Mathf.Atan2(2 * xz, xx - zz);
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
    }

    static float Floor(Vector3 point, float expected, out string surface)
    {
        surface = "original support plane";
        var candidates = Physics.RaycastAll(new Vector3(point.x, expected + 1.1f, point.z), Vector3.down, 5f,
            ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.normal.y > 0.65f && h.collider.gameObject.scene.path == MarketRecoveryTool.ScenePath &&
                !marketRoots.Any(r => h.transform == r || h.transform.IsChildOf(r)) && Mathf.Abs(h.point.y - expected) < 1.5f)
            .OrderBy(h => Mathf.Abs(h.point.y - expected)).ToArray();
        if (candidates.Length == 0) return expected;
        surface = candidates[0].collider.name;
        return candidates[0].point.y;
    }
    static float Floor(Vector3 point, float expected) { string surface; return Floor(point, expected, out surface); }

    static Stall ReadStall(Transform t, int number, StallPlan plan)
    {
        var parts = new List<Piece>();
        foreach (Transform child in t)
        {
            if (child.childCount != 0 || child.GetComponent<MeshFilter>() == null || !child.name.StartsWith(t.name + "."))
                throw new InvalidOperationException("Unexpected object in known FBX hierarchy: " + child.name);
            parts.Add(new Piece { t = child, position = child.position, rotation = child.rotation,
                bounds = BoundsOf(Points(child)), architecture = Architecture(number, Suffix(child)) });
        }
        var architecture = parts.Where(p => p.architecture).ToList();
        var vertices = architecture.SelectMany(p => Points(p.t)).ToArray();
        var axis = LongAxis(vertices);
        return new Stall { t = t, parts = parts, bounds = BoundsOf(vertices), axis = axis, plan = plan,
            delta = Quaternion.AngleAxis(plan.yaw, Vector3.up) * Quaternion.AngleAxis(plan.tip, axis) };
    }
    static void SetPiece(Piece p, Vector3 position, Quaternion rotation)
    {
        p.t.SetPositionAndRotation(position, rotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(p.t);
    }
    static Vector3 FitShift(Bounds b, Bounds limit, Vector3 shift)
    {
        shift.x = Mathf.Clamp(shift.x, limit.min.x - b.min.x, limit.max.x - b.max.x);
        shift.z = Mathf.Clamp(shift.z, limit.min.z - b.min.z, limit.max.z - b.max.z);
        return shift;
    }
    static float Overlap(Bounds a, Bounds b)
    {
        float x = Mathf.Max(0, Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x));
        float z = Mathf.Max(0, Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z));
        return x * z;
    }
    static void PlaceArchitecture(List<Stall> stalls, Bounds limit, Result result)
    {
        // Keep geometry centered near its original footprint; imported FBX pivots sit far off-center.
        var placed = new List<Bounds>();
        foreach (var s in stalls.OrderBy(x => Mathf.Abs(x.plan.tip) > 45 ? 1 : 0))
        {
            var c = s.bounds.center;
            float slopeX = (Floor(c + Vector3.right * 2, s.bounds.min.y) - Floor(c - Vector3.right * 2, s.bounds.min.y)) / 4;
            float slopeZ = (Floor(c + Vector3.forward * 2, s.bounds.min.y) - Floor(c - Vector3.forward * 2, s.bounds.min.y)) / 4;
            var terrainRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(-slopeX, 1, -slopeZ).normalized);
            s.delta = terrainRotation * s.delta;
            foreach (var p in s.parts.Where(p => p.architecture))
                SetPiece(p, s.bounds.center + s.delta * (p.position - s.bounds.center), s.delta * p.rotation);
            var rotated = BoundsOf(s.parts.Where(p => p.architecture).SelectMany(p => Points(p.t)));
            var desired = new Vector3(s.plan.x, 0, s.plan.z);
            Vector3 best = FitShift(rotated, limit, desired);
            float bestScore = float.PositiveInfinity;
            for (int x = -5; x <= 5; x++)
                for (int z = -5; z <= 5; z++)
                {
                    var candidate = FitShift(rotated, limit, desired + new Vector3(x * 0.24f, 0, z * 0.24f));
                    if (candidate.magnitude > 1.95f) continue;
                    var cb = new Bounds(rotated.center + candidate, rotated.size);
                    float score = (candidate - desired).sqrMagnitude * 0.6f + placed.Sum(p => Overlap(cb, p)) * 15f;
                    if (score < bestScore) { bestScore = score; best = candidate; }
                }
            if (float.IsInfinity(bestScore)) throw new InvalidOperationException("Cannot fit stall within 2m: " + s.t.name);
            var supportPoint = rotated.center + best;
            string surface;
            var floor = Floor(supportPoint, s.bounds.min.y, out surface);
            best.y = float.NegativeInfinity;
            // Contact comes from mesh vertices, not the imported origin or a rotated renderer AABB.
            foreach (var vertex in s.parts.Where(p => p.architecture).SelectMany(p => Points(p.t)))
                if (vertex.y < rotated.min.y + 0.65f)
                    best.y = Mathf.Max(best.y, Floor(vertex + best, s.bounds.min.y) + 0.008f - vertex.y);
            s.shift = best;
            foreach (var p in s.parts.Where(p => p.architecture))
                SetPiece(p, p.t.position + best, p.t.rotation);
            var final = BoundsOf(s.parts.Where(p => p.architecture).SelectMany(p => Points(p.t)));
            placed.Add(final);
            result.ground.Add(s.t.name + ": " + surface + " y=" + floor.ToString("F3"));
            result.largestStallTravel = Mathf.Max(result.largestStallTravel, new Vector2(best.x, best.z).magnitude);
            if (Mathf.Abs(s.plan.tip) > 45f) result.tippedStalls++;
        }
    }

    static bool SegmentTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e)
    {
        var direction = b - a;
        var edge1 = d - c; var edge2 = e - c;
        var h = Vector3.Cross(direction, edge2);
        var determinant = Vector3.Dot(edge1, h);
        if (Mathf.Abs(determinant) < 0.000001f) return false;
        var inverse = 1f / determinant;
        var s = a - c;
        var u = inverse * Vector3.Dot(s, h);
        if (u < 0.002f || u > 0.998f) return false;
        var q = Vector3.Cross(s, edge1);
        var v = inverse * Vector3.Dot(direction, q);
        if (v < 0.002f || u + v > 0.998f) return false;
        var t = inverse * Vector3.Dot(edge2, q);
        var margin = 0.025f / Mathf.Max(direction.magnitude, 0.025f);
        return t > margin && t < 1 - margin;
    }
    sealed class Triangle
    {
        public Vector3 a, b, c;
        public Bounds bounds;
    }
    static bool HasIntersection(IEnumerable<Triangle> moving, IEnumerable<Triangle> fixedSurface, Vector3 shift)
    {
        foreach (var t in moving)
        {
            var bounds = new Bounds(t.bounds.center + shift, t.bounds.size);
            var a = t.a + shift; var b = t.b + shift; var c = t.c + shift;
            foreach (var other in fixedSurface)
            {
                if (!bounds.Intersects(other.bounds)) continue;
                if (SegmentTriangle(a, b, other.a, other.b, other.c) || SegmentTriangle(b, c, other.a, other.b, other.c) ||
                    SegmentTriangle(c, a, other.a, other.b, other.c) || SegmentTriangle(other.a, other.b, a, b, c) ||
                    SegmentTriangle(other.b, other.c, a, b, c) || SegmentTriangle(other.c, other.a, a, b, c)) return true;
            }
        }
        return false;
    }
    static List<Triangle> Surface(Stall s) { return Surface(s.parts.Where(p => p.architecture)); }
    static List<Triangle> Surface(IEnumerable<Piece> pieces)
    {
        var result = new List<Triangle>();
        foreach (var p in pieces)
        {
            var mesh = p.t.GetComponent<MeshFilter>().sharedMesh;
            var v = MeshVertices(mesh).Select(p.t.TransformPoint).ToArray();
            var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = v[indices[i]]; var b = v[indices[i + 1]]; var c = v[indices[i + 2]];
                var bounds = new Bounds(a, Vector3.zero); bounds.Encapsulate(b); bounds.Encapsulate(c);
                result.Add(new Triangle { a = a, b = b, c = c, bounds = bounds });
            }
        }
        return result;
    }
    static void CheckStallIntersections(List<Stall> stalls, Result result)
    {
        var surfaces = stalls.Select(Surface).ToArray();
        for (int i = 0; i < stalls.Count; i++)
            for (int j = i + 1; j < stalls.Count; j++)
            {
                int contacts = 0;
                foreach (var a in surfaces[i])
                {
                    foreach (var b in surfaces[j])
                    {
                        if (!a.bounds.Intersects(b.bounds)) continue;
                        if (SegmentTriangle(a.a, a.b, b.a, b.b, b.c) || SegmentTriangle(a.b, a.c, b.a, b.b, b.c) ||
                            SegmentTriangle(a.c, a.a, b.a, b.b, b.c) || SegmentTriangle(b.a, b.b, a.a, a.b, a.c) ||
                            SegmentTriangle(b.b, b.c, a.a, a.b, a.c) || SegmentTriangle(b.c, b.a, a.a, a.b, a.c)) contacts++;
                        if (contacts >= 12) break;
                    }
                    if (contacts >= 12) break;
                }
                if (contacts > 0) result.intersectingStalls.Add(stalls[i].t.name + "/" + stalls[j].t.name + ": " + contacts);
            }
    }

    static List<Bounds> Obstacles(IEnumerable<Stall> stalls)
    {
        var obstacles = new List<Bounds>();
        foreach (var part in stalls.SelectMany(s => s.parts).Where(p => p.architecture))
        {
            var mesh = part.t.GetComponent<MeshFilter>().sharedMesh;
            var vertices = MeshVertices(mesh).Select(part.t.TransformPoint).ToArray();
            int[] triangles;
            if (!Triangles.TryGetValue(mesh, out triangles)) Triangles[mesh] = triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var b = new Bounds(vertices[triangles[i]], Vector3.zero);
                b.Encapsulate(vertices[triangles[i + 1]]); b.Encapsulate(vertices[triangles[i + 2]]);
                b.Expand(0.008f);
                obstacles.Add(b);
            }
        }
        return obstacles;
    }
    static bool Inside(Bounds b, Bounds limit)
    {
        return b.min.x >= limit.min.x - 0.015f && b.max.x <= limit.max.x + 0.015f &&
            b.min.z >= limit.min.z - 0.015f && b.max.z <= limit.max.z + 0.015f;
    }
    static float Random01(System.Random r) { return (float)r.NextDouble(); }

    static void PlaceProps(List<Stall> stalls, Bounds limit, int variant, Result result)
    {
        var obstacles = Obstacles(stalls);
        var architecture = stalls.SelectMany(Surface).ToArray();
        var settled = new List<Bounds>();
        var random = new System.Random(1919 + variant * 607);
        foreach (var s in stalls)
        {
            bool tipped = Mathf.Abs(s.plan.tip) > 45f;
            var side = Vector3.Cross(s.axis, Vector3.up).normalized;
            // The spill runs away from the counter into the aisle, retaining each item's provenance.
            if (Vector3.Dot(side, limit.center - s.bounds.center) < 0) side = -side;
            int index = 0;
            foreach (var p in s.parts.Where(p => !p.architecture).OrderByDescending(p => p.bounds.size.sqrMagnitude))
            {
                var small = Mathf.Max(p.bounds.size.x, p.bounds.size.y, p.bounds.size.z) < 0.8f;
                var choice = Random01(random);
                if (variant == 2 && small && choice < 0.74f)
                {
                    p.t.gameObject.SetActive(false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(p.t.gameObject);
                    result.hiddenGoods++;
                    continue;
                }
                bool fallen = tipped || (variant == 0 ? choice < (small ? 0.52f : 0.28f) :
                    variant == 1 ? choice < 0.86f : choice < 0.65f);
                if (!fallen)
                {
                    SetPiece(p, s.bounds.center + s.delta * (p.position - s.bounds.center) + s.shift, s.delta * p.rotation);
                    result.retainedProps++;
                    continue;
                }
                var roll = small ? Random01(random) * 160f : (index % 3 == 0 ? 90f : index % 3 == 1 ? -90f : 0f);
                if (variant == 1 && (p.t.name == "Market4.035" || p.t.name == "Market6.054")) roll = 0;
                if (variant == 2 && (p.t.name == "Market2.041" || p.t.name == "Market2.042" || p.t.name == "Market2.057")) roll = 0;
                var rotation = Quaternion.AngleAxis(Random01(random) * 100 - 50, Vector3.up) *
                    Quaternion.AngleAxis(roll, s.axis) * p.rotation;
                SetPiece(p, p.position, rotation);
                var rotated = BoundsOf(Points(p.t));
                var contactVertices = Points(p.t).Where(v => v.y < rotated.min.y + 0.65f).ToArray();
                var propSurface = Surface(new[] {p});
                var radius = small ? 0.85f : 0.65f;
                var along = ((index % 5) - 2) * (small ? 0.29f : 0.39f);
                var wanted = p.bounds.center + side * radius + s.axis * along;
                float bestScore = float.PositiveInfinity;
                Vector3 best = Vector3.zero;
                Bounds bestBounds = default(Bounds);
                // Search close to the authored spill fan; avoid actual low beams and other props.
                for (int attempt = 0; attempt < 360; attempt++)
                {
                    var angle = attempt * 2.399963f;
                    var distance = 2.94f * Mathf.Sqrt(attempt / 359f);
                    var candidate = attempt == 0 ? wanted : p.bounds.center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                    var travel = new Vector2(candidate.x - p.bounds.center.x, candidate.z - p.bounds.center.z).magnitude;
                    if (travel > 2.95f) continue;
                    var shift = new Vector3(candidate.x - rotated.center.x, 0, candidate.z - rotated.center.z);
                    shift.y = float.NegativeInfinity;
                    foreach (var vertex in contactVertices)
                        shift.y = Mathf.Max(shift.y, Floor(vertex + shift, s.bounds.min.y) + 0.009f - vertex.y);
                    var cb = new Bounds(rotated.center + shift, rotated.size);
                    if (!Inside(cb, limit)) continue;
                    var test = cb; test.Expand(-0.035f);
                    int hits = obstacles.Count(o => o.Intersects(test));
                    if (hits > 0) hits = HasIntersection(propSurface, architecture.Where(t => t.bounds.Intersects(test)).ToArray(), shift) ? 1 : 0;
                    if (hits > 0) continue;
                    float interpenetration = settled.Sum(o => o.Intersects(test) ? Overlap(o, test) : 0);
                    float score = hits * 12f + interpenetration * 30f + (candidate - wanted).sqrMagnitude;
                    if (score < bestScore) { bestScore = score; best = shift; bestBounds = cb; }
                    if (hits == 0 && interpenetration < 0.002f) break;
                }
                if (float.IsInfinity(bestScore)) throw new InvalidOperationException("No nearby grounded position: " + p.t.name);
                SetPiece(p, p.t.position + best, p.t.rotation);
                if (HasIntersection(propSurface, architecture.Where(t => t.bounds.Intersects(bestBounds)).ToArray(), best))
                    result.obstructedProps.Add(s.t.name + "/" + p.t.name);
                var lowest = Points(p.t).Where(v => v.y < bestBounds.min.y + 0.65f)
                    .Select(v => v.y - Floor(v, s.bounds.min.y)).DefaultIfEmpty(0).Min();
                result.maximumPropPenetration = Mathf.Max(result.maximumPropPenetration, -lowest);
                result.maximumSupportGap = Mathf.Max(result.maximumSupportGap, lowest);
                settled.Add(bestBounds);
                result.groundProps++;
                result.largestPropTravel = Mathf.Max(result.largestPropTravel,
                    new Vector2(bestBounds.center.x - p.bounds.center.x, bestBounds.center.z - p.bounds.center.z).magnitude);
                index++;
            }
        }
    }

    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var scene = MarketRecoveryTool.TargetScene();
        marketRoots = MarketRecoveryTool.Roots(scene);
        var plans = JsonUtility.FromJson<Plans>(File.ReadAllText(MarketRecoveryTool.WorkPath + "/Layouts.json"));
        if (plans.markets.Count != 3 || plans.markets.Any(p => p.stalls.Count != 7))
            throw new InvalidOperationException("Expected three authored layouts with seven stalls each.");
        MarketRecoveryTool.SaveOriginal(scene);
        var snapshot = JsonUtility.FromJson<MarketRecoveryTool.Snapshot>(File.ReadAllText(MarketRecoveryTool.OriginalPath));
        // Preflight all IDs and parents before recording or applying any changes.
        foreach (var n in snapshot.nodes)
        {
            var t = MarketRecoveryTool.Resolve(n.id);
            if (t == null || t.gameObject.scene != scene || (t.parent == null ? "" : MarketRecoveryTool.Id(t.parent)) != n.parent)
                throw new InvalidOperationException("Hierarchy no longer matches original layout: " + n.path);
        }
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Compose three ruined markets");
        var objects = marketRoots.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .SelectMany(t => new UnityEngine.Object[] {t, t.gameObject}).ToArray();
        Undo.RecordObjects(objects, "Compose three ruined markets");
        try
        {
            // Idempotent: every application starts from the preserved layout, not from the previous ruin.
            foreach (var n in snapshot.nodes)
            {
                var t = MarketRecoveryTool.Resolve(n.id);
                t.SetLocalPositionAndRotation(n.localPosition, n.localRotation);
                t.localScale = n.scale;
                t.gameObject.SetActive(n.active);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            }
            Physics.SyncTransforms();
            var results = new Results();
            for (int variant = 0; variant < 3; variant++)
            {
                var root = marketRoots[variant];
                var limit = MarketRecoveryTool.Geometry(root);
                var stalls = new List<Stall>();
                for (int i = 0; i < 7; i++) stalls.Add(ReadStall(root.GetChild(i), i + 1, plans.markets[variant].stalls[i]));
                var result = new Result { market = root.name };
                PlaceArchitecture(stalls, limit, result);
                PlaceProps(stalls, limit, variant, result);
                CheckStallIntersections(stalls, result);
                if (!Inside(MarketRecoveryTool.Geometry(root), limit))
                    throw new InvalidOperationException("A market exceeds its original footprint: " + root.name);
                results.markets.Add(result);
            }
            foreach (var n in snapshot.nodes)
            {
                var t = MarketRecoveryTool.Resolve(n.id);
                var v = t.localPosition;
                if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.sqrMagnitude) ||
                    t.localScale != n.scale || (t.parent == null ? "" : MarketRecoveryTool.Id(t.parent)) != n.parent)
                    throw new InvalidOperationException("Transform invariant failed: " + n.path);
                if (marketRoots.Contains(t) && (t.localPosition != n.localPosition || t.localRotation != n.localRotation))
                    throw new InvalidOperationException("Market root changed: " + t.name);
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unity could not save Act 2 Recover.");
            File.WriteAllText(MarketRecoveryTool.WorkPath + "/Result.json", JsonUtility.ToJson(results, true));
            MarketRecoveryTool.Audit();
            SceneView.RepaintAll();
            Debug.Log("Three ruined market compositions saved to " + scene.path);
        }
        catch
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }
}
