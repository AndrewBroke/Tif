using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class PrepareLog
{
    public static object Main()
    {
        var src=new Texture2D(2,2); src.LoadImage(File.ReadAllBytes("Assets/Models/1 Act/MainHouse_FirstAct.fbm/Props.png"));
        var sheet=new Texture2D(1024,1024,TextureFormat.RGB24,false);
        for(int y=0;y<1024;y++) for(int x=0;x<1024;x++)
        {
            Color c=new Color32(35,30,25,255);
            if(y>=320) c=src.GetPixel(232+x*320/1024,1500-760-220+(y-320)*220/704);
            else if(x<320) c=src.GetPixel(760+x*144/320,1500-1308-144+y*144/320);
            sheet.SetPixel(x,y,c);
        }
        sheet.Apply(); File.WriteAllBytes("tmp/imagegen/log-edit-target.png",sheet.EncodeToPNG());
        var p=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/1 Act/Полено (1).prefab");
        var m=p.GetComponent<MeshFilter>().sharedMesh;
        var uv=m.uv;var tri=m.triangles;
        var overlay=new Texture2D(1500,1500,TextureFormat.RGB24,false);overlay.SetPixels(src.GetPixels());
        for(int i=0;i<tri.Length;i+=3)for(int e=0;e<3;e++) {
            var a=uv[tri[i+e]]*1500;var b=uv[tri[i+(e+1)%3]]*1500;
            int steps=(int)Vector2.Distance(a,b)+1;
            for(int t=0;t<=steps;t++){var v=Vector2.Lerp(a,b,(float)t/steps);overlay.SetPixel((int)v.x,(int)v.y,Color.cyan);}
        }
        overlay.Apply();File.WriteAllBytes("tmp/imagegen/log-uv.png",overlay.EncodeToPNG());
        return new {sheet="tmp/imagegen/log-edit-target.png",uv="tmp/imagegen/log-uv.png", side="x 232..551; top y 760..979", end="x760..903; top y1308..1451"};
    }
}
