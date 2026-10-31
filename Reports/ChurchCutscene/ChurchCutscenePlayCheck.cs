using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;
using UnityEditor;
using StarterAssets;

public static class ChurchCutscenePlayCheck
{
    private static PlayableDirector director;
    private static FirstPersonController player;
    private static GameObject root;
    private static Vector3 playerStart;
    private static readonly List<string> samples = new List<string>();
    private static int sampleIndex;
    private static int firstFrame;
    private static float previousTime;
    private static Vector3 previousPosition;
    private static Quaternion previousRotation;
    private static float maxPositionStep;
    private static float maxRotationStep;
    private static float nextSample;
    private static bool done;

    public static string Start()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play mode required.");
        root = GameObject.Find("Кат сцена церковь");
        director = root.GetComponentInChildren<PlayableDirector>();
        player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        // Place the player on the approach side, then physically cross the existing trigger.
        var trigger = root.GetComponentInChildren<InvokeOnTrigger>();

        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = trigger.transform.position + Vector3.up * 0.1f - trigger.transform.forward * 2;
        player.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(root.transform.position-player.transform.position,Vector3.up));
        player.transform.Rotate(0,30,0);
        cc.enabled = true;
        player.GetComponent<PlayerMovementDelay>().UnlockControls();
        Time.timeScale = 1;
        Application.runInBackground = true;
        EditorApplication.update += Approach;
        return "Approaching existing collider; verification will be written to Reports/ChurchCutscene/playcheck.txt";
    }

    private static int approachFrames;
    private static void Approach()
    {
        if (Time.frameCount == firstFrame) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        firstFrame=Time.frameCount;
        var trigger=root.GetComponentInChildren<InvokeOnTrigger>();
        if (approachFrames++ < 12)
        {


            EditorApplication.QueuePlayerLoopUpdate();
            return;
        }
        if (trigger != null)
            player.GetComponent<CharacterController>().Move(trigger.transform.forward * 0.25f);
        if (director.state == PlayState.Playing)
        {
            EditorApplication.update -= Approach;
            playerStart=player.transform.position;
            previousPosition=Camera.main.transform.position;
            previousRotation=Camera.main.transform.rotation;
            previousTime=Time.time;
            firstFrame=Time.frameCount;
            samples.Add("Triggered via real CharacterController crossing. Frame="+firstFrame);
            Sample("START");
            EditorApplication.update += Observe;
        }
        if (approachFrames>100) { EditorApplication.update-=Approach; System.IO.File.WriteAllText("Reports/ChurchCutscene/playcheck.txt","FAIL: trigger not reached"); }
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private static void Observe()
    {
        if (!EditorApplication.isPlaying || done) { EditorApplication.update-=Observe; return; }
        EditorApplication.QueuePlayerLoopUpdate();
        if (Time.time==previousTime) return;
        float step=Vector3.Distance(Camera.main.transform.position,previousPosition);
        float angle=Quaternion.Angle(Camera.main.transform.rotation,previousRotation);
        maxPositionStep=Mathf.Max(maxPositionStep,step);
        maxRotationStep=Mathf.Max(maxRotationStep,angle);
        previousPosition=Camera.main.transform.position;
        previousRotation=Camera.main.transform.rotation;
        previousTime=Time.time;
        if (director.time>=nextSample && director.state==PlayState.Playing)
        {
            Sample("t="+director.time.ToString("F2"));
            nextSample+=1;
        }
        if (director.state != PlayState.Playing && Time.frameCount>firstFrame+20)
        {
            Sample("STOPPED");
            var inputs=player.GetComponent<StarterAssetsInputs>();
            var target=player.CinemachineCameraTarget.transform;
            var camera=Camera.main;
            float before=target.localEulerAngles.x;
            inputs.LookInput(new Vector2(.1f,.1f));
            typeof(FirstPersonController).GetMethod("CameraRotation",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(player,null);
            float delta=Mathf.Abs(Mathf.DeltaAngle(before,target.localEulerAngles.x));
            inputs.LookInput(Vector2.zero);
            samples.Add("First mouse pitch delta="+delta.ToString("F4")+" degrees");
            samples.Add("Frames advanced="+(Time.frameCount-firstFrame)+"; player displacement="+Vector3.Distance(playerStart,player.transform.position).ToString("F4"));
            samples.Add("Max camera step="+maxPositionStep.ToString("F4")+"; max angular step="+maxRotationStep.ToString("F3"));
            samples.Add("Trigger remaining="+(root.GetComponentInChildren<InvokeOnTrigger>()!=null));
            samples.Add("Ruined solid colliders="+root.transform.Find("Разрушенная церковь").GetComponentsInChildren<Collider>().Count(c=>c.enabled&&!c.isTrigger&&(!(c is MeshCollider)||((MeshCollider)c).sharedMesh!=null)));
            System.IO.File.WriteAllLines("Reports/ChurchCutscene/playcheck.txt",samples);
            inputs.MoveInput(Vector2.right);
            movementStart=player.transform.position;
            movementTime=Time.time;
            EditorApplication.update+=CheckMovement;
            done=true;
            EditorApplication.update-=Observe;
        }
    }

    private static Vector3 movementStart;
    private static float movementTime;
    private static void CheckMovement()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (Time.time-movementTime < 0.25f) return;
        player.GetComponent<StarterAssetsInputs>().MoveInput(Vector2.zero);
        float distance=Vector3.Distance(movementStart,player.transform.position);
        samples.Add("Movement after unlock="+distance.ToString("F3")+" m; intact="+root.transform.Find("Церковь").gameObject.activeSelf+" ruined="+root.transform.Find("Разрушенная церковь").gameObject.activeSelf);
        samples.Add("RESULT="+(distance>0.05f&&!PlayerControlLocks.IsBlocked&&!root.transform.Find("Церковь").gameObject.activeSelf&&root.transform.Find("Разрушенная церковь").gameObject.activeSelf?"PASS":"FAIL"));
        System.IO.File.WriteAllLines("Reports/ChurchCutscene/playcheck.txt",samples);
        EditorApplication.update-=CheckMovement;
    }
    private static void Sample(string label)
    {
        var brain=Camera.main.GetComponent<CinemachineBrain>();
        var camera=root.GetComponentsInChildren<CinemachineCamera>().Single();
        samples.Add(label+" frame="+Time.frameCount+" blocked="+PlayerControlLocks.IsBlocked+
            " intact="+root.transform.Find("Церковь").gameObject.activeSelf+" ruined="+root.transform.Find("Разрушенная церковь").gameObject.activeSelf+
            " camLocal="+camera.transform.localPosition+" euler="+camera.transform.localEulerAngles+
            " noise="+camera.GetComponent<CinemachineBasicMultiChannelPerlin>().AmplitudeGain.ToString("F2")+
            " active="+(brain.ActiveVirtualCamera==null?"null":brain.ActiveVirtualCamera.Name)+
            " input="+player.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled);
        if (label=="STOPPED" || (director.time>2&&director.time<3) || (director.time>7&&director.time<8) || (director.time>10&&director.time<11))
        {
            var main=Camera.main;
            var rt=RenderTexture.GetTemporary(960,540,24);
            var prev=main.targetTexture;
            var active=RenderTexture.active;
            main.targetTexture=rt;
            main.Render();
            RenderTexture.active=rt;
            var tex=new Texture2D(960,540,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,960,540),0,0);
            tex.Apply();
            System.IO.File.WriteAllBytes("Reports/ChurchCutscene/frame-"+sampleIndex+++".png",tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            main.targetTexture=prev;
            RenderTexture.active=active;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}


