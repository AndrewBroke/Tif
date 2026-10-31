using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class PreviewLogs
{
    public static object Main()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/1 Act/Полено (1).prefab");
        var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;var original=prefab.GetComponent<Renderer>().sharedMaterial;
        var paths=new[]{"", "BloodyLog_01_Splashes","BloodyLog_02_Smears","BloodyLog_03_Drips","BloodyLog_04_Dried","BloodyLog_05_Soaked"};
        for(int i=0;i<6;i++)for(int angle=0;angle<2;angle++){
            var preview=new PreviewRenderUtility();
            var mat=new Material(original);
            try {
                if(i>0){var tex=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Textures/Окровавленное полено/"+paths[i]+".png");mat.SetTexture("_BaseMap",tex);mat.SetTexture("_MainTex",tex);}
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.075f,.07f,.065f,1);
                preview.camera.fieldOfView=30;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=20;
                preview.camera.transform.position=new Vector3(.4f,.5f,-3.6f);preview.camera.transform.LookAt(Vector3.zero);
                preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(40,35,0);
                preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=Quaternion.Euler(340,210,0);
                preview.ambientColor=new Color(.5f,.5f,.5f,1);
                float scale=1.65f/Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.y,mesh.bounds.size.z);
                Quaternion rot=Quaternion.Euler(angle==0?10:190,65,angle==0?-8:8);
                var matrix=Matrix4x4.TRS(Vector3.zero,rot,Vector3.one*scale)*Matrix4x4.Translate(-mesh.bounds.center);
                preview.BeginStaticPreview(new Rect(0,0,512,384));preview.DrawMesh(mesh,matrix,mat,0);
                preview.Render(true);var image=preview.EndStaticPreview();
                File.WriteAllBytes("tmp/imagegen/preview-"+i+"-"+angle+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }finally{preview.Cleanup();UnityEngine.Object.DestroyImmediate(mat);}
        }
        return new {views=12,location="tmp/imagegen",sceneModified=false};
    }
}
