using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public static class FinalizeLogs
{
    const string Folder="Assets/Sprites/Textures/Окровавленное полено";
    static readonly string[] Names={"BloodyLog_01_Splashes","BloodyLog_02_Smears","BloodyLog_03_Drips","BloodyLog_04_Dried","BloodyLog_05_Soaked"};
    static Texture2D Read(string path){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(path));return t;}
    static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
    public static object Main()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/1 Act/Полено (1).prefab");
        var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;
        var original=Read("Assets/Models/1 Act/MainHouse_FirstAct.fbm/Props.png");
        var originalPixels=original.GetPixels32();
        var mask=new bool[1500*1500];var uv=mesh.uv;var triangles=mesh.triangles;
        for(int i=0;i<triangles.Length;i+=3){
            Vector2 a=uv[triangles[i]]*1500,b=uv[triangles[i+1]]*1500,c=uv[triangles[i+2]]*1500;
            for(int y=Mathf.FloorToInt(Mathf.Min(a.y,b.y,c.y));y<=Mathf.CeilToInt(Mathf.Max(a.y,b.y,c.y));y++)
            for(int x=Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x));x<=Mathf.CeilToInt(Mathf.Max(a.x,b.x,c.x));x++){
                var p=new Vector2(x+.5f,y+.5f);float ca=Cross(b-a,p-a),cb=Cross(c-b,p-b),cc=Cross(a-c,p-c);
                if((ca>=0&&cb>=0&&cc>=0)||(ca<=0&&cb<=0&&cc<=0))mask[y*1500+x]=true;
            }
        }
        var padded=(bool[])mask.Clone();
        for(int y=3;y<1497;y++)for(int x=3;x<1497;x++)if(mask[y*1500+x])for(int dy=-3;dy<=3;dy++)for(int dx=-3;dx<=3;dx++)padded[(y+dy)*1500+x+dx]=true;
        Directory.CreateDirectory(Folder);
        var stats=new List<object>();
        for(int v=0;v<5;v++){
            string path=Folder+"/"+Names[v]+".png";
            if(File.Exists(path))throw new Exception("Refusing to overwrite existing texture: "+path);
            var edit=Read("tmp/imagegen/variant-"+(v+1)+".png");
            var pixels=(Color32[])originalPixels.Clone();int changed=0,blood=0;
            for(int y=0;y<1500;y++)for(int x=0;x<1500;x++){
                int index=y*1500+x;if(!padded[index])continue;
                int topY=1499-y;float sx,sy;
                if(x>=232&&x<552&&topY>=760&&topY<980){sx=(x-232+.5f)/320f;sy=(320+(y-520+.5f)*704/220f)/1024f;}
                else if(x>=760&&x<904&&topY>=1308&&topY<1452){sx=((x-760+.5f)*320/144f)/1024f;sy=((y-48+.5f)*320/144f)/1024f;}
                else throw new Exception("UV outside prepared texture regions");
                Color e=edit.GetPixelBilinear(sx,sy);Color o=originalPixels[index];
                // Keep the source atlas pixel-for-pixel wherever AI did not add blood.
                float originalRed=(o.r-o.g)/(o.r+o.g+.001f);
                float editedRed=(e.r-e.g)/(e.r+e.g+.001f);
                float stain=Mathf.Clamp01((editedRed-originalRed-.045f)/.085f);
                if(e.g>o.g*1.05f)stain=0;
                if(stain>.01f){pixels[index]=Color.Lerp(o,e,stain);changed++;if(mask[index])blood++;}
            }
            var output=new Texture2D(1500,1500,TextureFormat.RGBA32,false);output.SetPixels32(pixels);output.Apply();File.WriteAllBytes(path,output.EncodeToPNG());
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.filterMode=FilterMode.Point;importer.anisoLevel=1;importer.mipmapEnabled=true;
            importer.wrapMode=TextureWrapMode.Repeat;importer.alphaSource=TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
            int outside=0;var verify=Read(path).GetPixels32();
            for(int j=0;j<verify.Length;j++)if(!padded[j]&&!verify[j].Equals(originalPixels[j]))outside++;
            if(outside!=0)throw new Exception("Atlas outside UV changed: "+path);
            stats.Add(new {path,width=output.width,height=output.height,changedPixels=changed,stainedUvPixels=blood,outsideUvChanges=outside,guid=AssetDatabase.AssetPathToGUID(path)});
            UnityEngine.Object.DestroyImmediate(edit);UnityEngine.Object.DestroyImmediate(output);
        }
        File.WriteAllText("tmp/imagegen/generation-prompts.txt",File.ReadAllText("tmp/imagegen/prompts.txt"));
        return new {variants=stats,uvPixels=mask.Count(m=>m),paddingPixels=padded.Count(m=>m),shader=prefab.GetComponent<Renderer>().sharedMaterial.shader.name};
    }
}
