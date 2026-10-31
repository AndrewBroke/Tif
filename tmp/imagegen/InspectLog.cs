using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class InspectLog
{
    [Serializable] public class MeshData { public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
    public static object Main()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/1 Act/Полено (1).prefab");
        var mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
        var source = prefab.GetComponent<Renderer>().sharedMaterial.mainTexture;
        Directory.CreateDirectory("tmp/imagegen");
        var uv = mesh.uv;
        File.WriteAllText("tmp/imagegen/log-mesh.json", JsonUtility.ToJson(new MeshData { vertices=mesh.vertices, uv=uv, triangles=mesh.triangles }));
        var tex=new Texture2D(2,2); tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source)));
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/1 Act/MainHouse_FirstAct.fbx");
        return new { prefabMesh=mesh.name, width=tex.width, height=tex.height, objects=model.GetComponentsInChildren<MeshFilter>(true).Select(m=>new { obj=m.name, mesh=m.sharedMesh.name, vertices=m.sharedMesh.vertexCount, uvMin=new[]{m.sharedMesh.uv.Min(v=>v.x),m.sharedMesh.uv.Min(v=>v.y)}, uvMax=new[]{m.sharedMesh.uv.Max(v=>v.x),m.sharedMesh.uv.Max(v=>v.y)} }) };
    }
}
