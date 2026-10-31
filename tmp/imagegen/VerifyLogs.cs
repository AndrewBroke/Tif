using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class VerifyLogs
{
    public static object Main()
    {
        var files=Directory.GetFiles("Assets/Sprites/Textures/Окровавленное полено","BloodyLog_*.png");
        if(files.Length!=5)throw new Exception("Expected exactly five textures");
        var result=files.Select(p=>{
            p=p.Replace('\\','/');var t=AssetDatabase.LoadAssetAtPath<Texture2D>(p);var i=(TextureImporter)AssetImporter.GetAtPath(p);
            if(t.width!=1500||t.height!=1500||i.filterMode!=FilterMode.Point||i.npotScale!=TextureImporterNPOTScale.None||i.textureCompression!=TextureImporterCompression.Uncompressed||!i.sRGBTexture||!i.mipmapEnabled)throw new Exception("Unexpected texture import settings: "+p);
            return new {file=Path.GetFileName(p),width=t.width,height=t.height,filter=i.filterMode.ToString(),compression=i.textureCompression.ToString(),mipmaps=i.mipmapEnabled};
        }).ToArray();
        return new {success=true,textures=result};
    }
}
