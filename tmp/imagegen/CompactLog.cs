using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;

public static class CompactLog
{
    const string Root="Assets/Sprites/Textures/Окровавленное полено";
    const string SourcePrefab="Assets/Models/1 Act/Полено (1).prefab";
    public static readonly string[] Names={"BloodyLog_01_Splashes","BloodyLog_02_Smears","BloodyLog_03_Drips","BloodyLog_04_Dried","BloodyLog_05_Soaked"};
    static readonly RectInt SideSource=new RectInt(232,520,320,220);
    static readonly RectInt EndSource=new RectInt(760,48,144,144);
    static readonly Vector2Int SideDestination=new Vector2Int(8,16);
    static readonly Vector2Int EndDestination=new Vector2Int(352,56);
    static readonly string[] Originals={"Assets/Models/1 Act/MainHouse_FirstAct.fbx","Assets/Models/1 Act/MainHouse_FirstAct.fbx.meta",SourcePrefab,SourcePrefab+".meta","Assets/Models/1 Act/Props.mat","Assets/Models/1 Act/Props.mat.meta","Assets/Models/1 Act/MainHouse_FirstAct.fbm/Props.png","Assets/Models/1 Act/MainHouse_FirstAct.fbm/Props.png.meta"};
    static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
    [Serializable] public class OriginalState{public string[] paths; public string[] hashes;}
    static Texture2D Read(string path){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(path));return t;}
    static void Paste(Texture2D source,Texture2D target,RectInt rect,Vector2Int dest)
    {
        // Extrude eight texels around each island to protect mipmap filtering.
        for(int y=-8;y<rect.height+8;y++)for(int x=-8;x<rect.width+8;x++)
            target.SetPixel(dest.x+x,dest.y+y,source.GetPixel(rect.x+Mathf.Clamp(x,0,rect.width-1),rect.y+Mathf.Clamp(y,0,rect.height-1)));
    }
    static Vector2 Remap(Vector2 uv)
    {
        var pixel=uv*1500;
        RectInt source;Vector2Int destination;
        if(pixel.x>=SideSource.xMin&&pixel.x<SideSource.xMax&&pixel.y>=SideSource.yMin&&pixel.y<SideSource.yMax){source=SideSource;destination=SideDestination;}
        else if(pixel.x>=EndSource.xMin&&pixel.x<EndSource.xMax&&pixel.y>=EndSource.yMin&&pixel.y<EndSource.yMax){source=EndSource;destination=EndDestination;}
        else throw new Exception("Unexpected UV: "+uv);
        return new Vector2((pixel.x-source.x+destination.x)/512f,(pixel.y-source.y+destination.y)/256f);
    }
    public static object Main()
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
        var originalMesh=source.GetComponent<MeshFilter>().sharedMesh;
        var uv=originalMesh.uv.Select(Remap).ToArray();
        string prefabPath=Root+"/Полено_Окровавленное.prefab",meshPath=Root+"/Meshes/BloodyLog_Compact.asset";
        if(File.Exists(prefabPath)||File.Exists(meshPath))throw new Exception("Compact assets already exist");
        foreach(var name in Names){if(!File.Exists(Root+"/"+name+".png")||File.Exists(Root+"/"+name+".mat"))throw new Exception("Unexpected asset state: "+name);}
        var state=new OriginalState{paths=Originals,hashes=Originals.Select(Hash).ToArray()};
        Directory.CreateDirectory("tmp/imagegen/compact-source");
        File.WriteAllText("tmp/imagegen/compact-original-state.json",JsonUtility.ToJson(state,true));
        if(!AssetDatabase.IsValidFolder(Root+"/Textures"))AssetDatabase.CreateFolder(Root,"Textures");
        if(!AssetDatabase.IsValidFolder(Root+"/Meshes"))AssetDatabase.CreateFolder(Root,"Meshes");
        var mesh=UnityEngine.Object.Instantiate(originalMesh);mesh.name="BloodyLog_Compact";mesh.uv=uv;
        AssetDatabase.CreateAsset(mesh,meshPath);
        var materials=new List<Material>();
        foreach(string name in Names){
            string oldPath=Root+"/"+name+".png",path=Root+"/Textures/"+name+".png";
            var sourceTexture=Read(oldPath);
            if(sourceTexture.width!=1500||sourceTexture.height!=1500)throw new Exception("Expected original atlas");
            File.Copy(oldPath,"tmp/imagegen/compact-source/"+name+".png",false);
            var compact=new Texture2D(512,256,TextureFormat.RGB24,false);
            compact.SetPixels(Enumerable.Repeat(new Color(.12f,.10f,.075f,1),512*256).ToArray());
            Paste(sourceTexture,compact,SideSource,SideDestination);Paste(sourceTexture,compact,EndSource,EndDestination);compact.Apply();
            string moveError=AssetDatabase.MoveAsset(oldPath,path);if(!string.IsNullOrEmpty(moveError))throw new Exception(moveError);
            File.WriteAllBytes(path,compact.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=true;importer.anisoLevel=1;
            importer.wrapMode=TextureWrapMode.Clamp;importer.alphaSource=TextureImporterAlphaSource.None;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var material=new Material(source.GetComponent<Renderer>().sharedMaterial){name=name};
            material.SetTexture("_BaseMap",texture);material.SetTexture("_MainTex",texture);
            material.SetTextureScale("_BaseMap",Vector2.one);material.SetTextureOffset("_BaseMap",Vector2.zero);
            material.SetTextureScale("_MainTex",Vector2.one);material.SetTextureOffset("_MainTex",Vector2.zero);
            AssetDatabase.CreateAsset(material,Root+"/"+name+".mat");materials.Add(material);
            UnityEngine.Object.DestroyImmediate(sourceTexture);UnityEngine.Object.DestroyImmediate(compact);
        }
        var contents=PrefabUtility.LoadPrefabContents(SourcePrefab);
        try{
            contents.name="Полено_Окровавленное";contents.transform.localPosition=Vector3.zero;
            contents.GetComponent<MeshFilter>().sharedMesh=mesh;contents.GetComponent<MeshRenderer>().sharedMaterials=new[]{materials[0]};
            var saved=PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);if(saved==null)throw new Exception("Prefab save failed");
        }finally{PrefabUtility.UnloadPrefabContents(contents);}
        foreach(var pair in state.paths.Select((p,i)=>new{path=p,hash=state.hashes[i]}))if(Hash(pair.path)!=pair.hash)throw new Exception("Original was changed: "+pair.path);
        return new {prefab=prefabPath,mesh=meshPath,materials=materials.Select(AssetDatabase.GetAssetPath).ToArray(),textures="512x256, five variants",originalsUnchanged=true};
    }
}
