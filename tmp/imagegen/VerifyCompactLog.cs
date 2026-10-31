using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;

public static class VerifyCompactLog
{
    const string Root="Assets/Sprites/Textures/Окровавленное полено";
    static readonly string[] Names={"BloodyLog_01_Splashes","BloodyLog_02_Smears","BloodyLog_03_Drips","BloodyLog_04_Dried","BloodyLog_05_Soaked"};
    [Serializable] public class State{public string[] paths;public string[] hashes;}
    static Texture2D Read(string path){var t=new Texture2D(2,2);t.LoadImage(File.ReadAllBytes(path));return t;}
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
    static int ComparePatch(Texture2D old,Texture2D compact,RectInt src,Vector2Int dst)
    {
        int compared=0;
        for(int y=0;y<src.height;y++)for(int x=0;x<src.width;x++){
            Color32 a=old.GetPixel(src.x+x,src.y+y),b=compact.GetPixel(dst.x+x,dst.y+y);
            Require(a.Equals(b),"Texture pixel mismatch");compared++;
        }
        return compared;
    }
    static void Render(Mesh mesh,Material material,int variant,int angle)
    {
        var preview=new PreviewRenderUtility();
        try{
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.075f,.07f,.065f,1);
            preview.camera.fieldOfView=30;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=20;
            preview.camera.transform.position=new Vector3(.4f,.5f,-3.6f);preview.camera.transform.LookAt(Vector3.zero);
            preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(40,35,0);
            preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=Quaternion.Euler(340,210,0);
            preview.ambientColor=new Color(.5f,.5f,.5f,1);
            float scale=1.65f/Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.y,mesh.bounds.size.z);
            var rotation=Quaternion.Euler(angle==0?10:190,65,angle==0?-8:8);
            var matrix=Matrix4x4.TRS(Vector3.zero,rotation,Vector3.one*scale)*Matrix4x4.Translate(-mesh.bounds.center);
            preview.BeginStaticPreview(new Rect(0,0,512,384));preview.DrawMesh(mesh,matrix,material,0);preview.Render(true);
            var result=preview.EndStaticPreview();File.WriteAllBytes("tmp/imagegen/compact-preview-"+variant+"-"+angle+".png",result.EncodeToPNG());UnityEngine.Object.DestroyImmediate(result);
        }finally{preview.Cleanup();}
    }
    public static object Main()
    {
        var state=JsonUtility.FromJson<State>(File.ReadAllText("tmp/imagegen/compact-original-state.json"));
        for(int i=0;i<state.paths.Length;i++)Require(Hash(state.paths[i])==state.hashes[i],"Original changed: "+state.paths[i]);
        string prefabPath=Root+"/Полено_Окровавленное.prefab";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/1 Act/Полено (1).prefab");
        var oldMesh=source.GetComponent<MeshFilter>().sharedMesh;var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;
        Require(mesh!=oldMesh,"Expected an independent mesh");
        Require(mesh.vertices.SequenceEqual(oldMesh.vertices)&&mesh.normals.SequenceEqual(oldMesh.normals)&&mesh.tangents.SequenceEqual(oldMesh.tangents)&&mesh.triangles.SequenceEqual(oldMesh.triangles)&&mesh.uv2.SequenceEqual(oldMesh.uv2),"Geometry or other mesh data changed");
        Require(prefab.transform.localScale==source.transform.localScale&&prefab.transform.localRotation==source.transform.localRotation&&prefab.transform.localPosition==Vector3.zero,"Prefab transform mismatch");
        float error=0;var uv=mesh.uv;var oldUv=oldMesh.uv;
        for(int i=0;i<uv.Length;i++){
            var pixel=oldUv[i]*1500;Vector2 recovered;
            if(pixel.x<600)recovered=new Vector2(uv[i].x*512-8+232,uv[i].y*256-16+520);
            else recovered=new Vector2(uv[i].x*512-352+760,uv[i].y*256-56+48);
            error=Mathf.Max(error,Vector2.Distance(pixel,recovered));
        }
        Require(error<.001f,"UV remapping lost texel alignment");
        var records=new List<object>();int compared=0;
        for(int i=0;i<Names.Length;i++){
            string texturePath=Root+"/Textures/"+Names[i]+".png";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            Require(texture.width==512&&texture.height==256&&importer.filterMode==FilterMode.Point&&importer.mipmapEnabled&&importer.sRGBTexture&&importer.textureCompression==TextureImporterCompression.Uncompressed,"Import settings mismatch");
            var old=Read("tmp/imagegen/compact-source/"+Names[i]+".png");var compact=Read(texturePath);
            compared+=ComparePatch(old,compact,new RectInt(232,520,320,220),new Vector2Int(8,16));
            compared+=ComparePatch(old,compact,new RectInt(760,48,144,144),new Vector2Int(352,56));
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/"+Names[i]+".mat");
            Require(material.GetTexture("_BaseMap")==texture&&material.GetTexture("_MainTex")==texture,"Material texture mismatch");
            Require(material.GetTextureScale("_BaseMap")==Vector2.one&&material.GetTextureOffset("_BaseMap")==Vector2.zero,"Material UV transform mismatch");
            if(i==0)Require(prefab.GetComponent<Renderer>().sharedMaterial==material,"Default material mismatch");
            Render(mesh,material,i+1,0);Render(mesh,material,i+1,1);
            records.Add(new{variant=Names[i],width=texture.width,height=texture.height,bytes=new FileInfo(texturePath).Length});
            UnityEngine.Object.DestroyImmediate(old);UnityEngine.Object.DestroyImmediate(compact);
        }
        var dependencies=AssetDatabase.GetDependencies(prefabPath,true);
        Require(!dependencies.Any(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)||p.EndsWith("/Props.mat")||p.EndsWith("/Props.png")),"Prefab still depends on original atlas or FBX");
        Require(!Directory.GetFiles(Root,"*.png").Any(),"Old full atlas still in asset root");
        return new{success=true,variants=records,comparedPixels=compared,sourcePixelMismatches=0,maxUvErrorPixels=error,originalFilesUnchanged=state.paths.Length,prefabDependencies=dependencies};
    }
}
