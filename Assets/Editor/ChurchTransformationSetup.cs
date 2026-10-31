using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;
using StarterAssets;

/// <summary>Authors only the church sequence using Unity's asset serialization APIs.</summary>
public static class ChurchTransformationSetup
{
    private const string Folder = "Assets/Animations/ChurchTransformation";

    [MenuItem("Tools/TIF/Church Transformation/Build in Current Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var root = scene.GetRootGameObjects().Single(g => g.name == "Кат сцена церковь");
        if (root.transform.Find("ChurchCutsceneDirector") != null)
            throw new InvalidOperationException("Church Timeline already exists; edit its tracks directly.");
        var intact = root.transform.Find("Церковь").gameObject;
        var ruined = root.transform.Find("Разрушенная церковь").gameObject;
        var trigger = root.GetComponentInChildren<InvokeOnTrigger>(true);
        var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        var brain = Camera.main.GetComponent<CinemachineBrain>();
        var gameplayCamera = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include)
            .Single(c => c.isActiveAndEnabled && c.Follow == player.CinemachineCameraTarget.transform);
        var triggerData = new SerializedObject(trigger);
        if (triggerData.FindProperty("onTriggerEnter.m_PersistentCalls.m_Calls").arraySize != 0)
            throw new InvalidOperationException("Trigger already has events; review before connecting.");
        EnsureFolder(Folder);
        if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(Folder + "/ChurchTransformationTimeline.playable") != null)
            throw new InvalidOperationException("Timeline asset already exists.");

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Create church transformation Timeline");
        var origin = Child(root.transform, "ChurchCameraOrigin");
        origin.position = trigger.transform.position + Vector3.up * 2f;
        var direction = ruined.transform.position - origin.position;
        origin.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(direction, Vector3.up));
        var cameraTransform = Child(origin, "ChurchCutsceneCamera");
        cameraTransform.localRotation = Quaternion.Euler(-6f, 0f, 0f);
        var camera = Undo.AddComponent<CinemachineCamera>(cameraTransform.gameObject);
        camera.Priority = -100;
        camera.Lens = gameplayCamera.Lens;
        var noise = Undo.AddComponent<CinemachineBasicMultiChannelPerlin>(camera.gameObject);
        noise.NoiseProfile = MakeNoise();
        noise.AmplitudeGain = 0f;
        noise.FrequencyGain = 1f;
        var animator = Undo.AddComponent<Animator>(camera.gameObject);
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var directorObject = Child(root.transform, "ChurchCutsceneDirector").gameObject;
        var director = Undo.AddComponent<PlayableDirector>(directorObject);
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;
        director.timeUpdateMode = DirectorUpdateMode.GameTime;
        var controller = Undo.AddComponent<CutSceneController>(directorObject);
        SetReference(controller, "director", director);
        SetReference(controller, "cutsceneOrigin", origin);
        SetReference(controller, "cutsceneCamera", cameraTransform);
        SetReference(controller, "cutsceneLookTarget", ruined.transform);
        SetReference(controller, "player", player);
        // OnEnable ran before the director field was assigned.
        controller.enabled = false;
        controller.enabled = true;

        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = "ChurchTransformationTimeline";
        AssetDatabase.CreateAsset(timeline, Folder + "/ChurchTransformationTimeline.playable");
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 17;
        timeline.editorSettings.frameRate = 60;
        director.playableAsset = timeline;

        var cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "Camera • blend in 0–1.5 / out 16–17");
        director.SetGenericBinding(cameraTrack, brain);
        var shotClip = cameraTrack.CreateClip<CinemachineShot>();
        shotClip.displayName = "ChurchCutsceneCamera";
        shotClip.start = 0;
        shotClip.duration = 17;
        shotClip.easeInDuration = 1.5;
        shotClip.easeOutDuration = 1;
        var shot = (CinemachineShot)shotClip.asset;
        var cameraReference = new PropertyName("ChurchCutsceneCamera");
        shot.VirtualCamera = new ExposedReference<CinemachineVirtualCameraBase> { exposedName = cameraReference };
        director.SetReferenceValue(cameraReference, camera);

        var animation = MakeAnimation();
        var movement = timeline.CreateTrack<AnimationTrack>(null, "Camera • anomaly / fall / sky / recovery");
        movement.trackOffset = TrackOffset.ApplyTransformOffsets;
        director.SetGenericBinding(movement, animator);
        var motionClip = movement.CreateClip(animation);
        motionClip.start = 0;
        motionClip.duration = 17;
        var motion = (AnimationPlayableAsset)motionClip.asset;
        motion.removeStartOffset = false;
        motion.applyFootIK = false;

        Activation(timeline, director, intact, "Intact church • off at 12.4 s", 0, 12.4,
            ActivationTrack.PostPlaybackState.Inactive);
        Activation(timeline, director, ruined, "Ruined church • on at 12.4 s (keep)", 12.4, 4.6,
            ActivationTrack.PostPlaybackState.Active);

        var receiver = Undo.AddComponent<SignalReceiver>(directorObject);
        var handoff = ScriptableObject.CreateInstance<SignalAsset>();
        handoff.name = "ChurchCameraHandoff";
        AssetDatabase.CreateAsset(handoff, Folder + "/ChurchCameraHandoff.signal");
        var reaction = new UnityEvent();
        UnityEventTools.AddPersistentListener(reaction, controller.AlignGameplayCamera);
        receiver.AddReaction(handoff, reaction);
        var signalTrack = timeline.CreateTrack<SignalTrack>(null, "Prepare gameplay view • 15.8 s");
        director.SetGenericBinding(signalTrack, receiver);
        var marker = signalTrack.CreateMarker<SignalEmitter>(15.8);
        marker.asset = handoff;
        marker.emitOnce = true;
        marker.retroactive = true;

        string[] audioNames = { "Low rumble • 0–12 s • fade in, then silence", "Anomaly • 2–10 s • rising tension",
            "Fall • 10.2–10.9 s", "World change impact • 12.4 s" };
        var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/Music/Mixer.mixer");
        var groups = mixer != null ? mixer.FindMatchingGroups("") : null;
        foreach (var audioName in audioNames)
        {
            var audio = Undo.AddComponent<AudioSource>(Child(directorObject.transform, audioName.Split('•')[0].Trim()).gameObject);
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            if (groups != null && groups.Length > 0)
                audio.outputAudioMixerGroup = groups.FirstOrDefault(g => g.name == "Master") ?? groups[0];
            var track = timeline.CreateTrack<AudioTrack>(null, audioName);
            director.SetGenericBinding(track, audio);
        }

        Undo.RecordObject(trigger, "Connect church trigger");
        triggerData.FindProperty("oneShot").boolValue = true;
        triggerData.ApplyModifiedProperties();
        var triggerEvent = (UnityEvent)typeof(InvokeOnTrigger).GetField("onTriggerEnter",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(trigger);
        UnityEventTools.AddObjectPersistentListener<PlayableAsset>(triggerEvent, controller.StartCutScene, timeline);
        EditorUtility.SetDirty(trigger);
        intact.SetActive(true);
        ruined.SetActive(false);
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(receiver);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = directorObject;
        Debug.Log("Church transformation: 17 s Timeline created and bound to the existing trigger.", directorObject);
    }

    private static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create church cutscene object");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void EnsureFolder(string path)
    {
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedProperties();
    }

    private static void Activation(TimelineAsset timeline, PlayableDirector director, GameObject target,
        string name, double start, double duration, ActivationTrack.PostPlaybackState after)
    {
        var track = timeline.CreateTrack<ActivationTrack>(null, name);
        track.postPlaybackState = after;
        var clip = track.CreateDefaultClip();
        clip.start = start;
        clip.duration = duration;
        director.SetGenericBinding(track, target);
    }

    private static NoiseSettings MakeNoise()
    {
        var noise = ScriptableObject.CreateInstance<NoiseSettings>();
        noise.name = "ChurchAnomalyNoise";
        noise.PositionNoise = new[] { new NoiseSettings.TransformNoiseParams {
            X = new NoiseSettings.NoiseParams { Frequency = 6.93f, Amplitude = 0.018f },
            Y = new NoiseSettings.NoiseParams { Frequency = 8.91f, Amplitude = 0.025f },
            Z = new NoiseSettings.NoiseParams { Frequency = 5.94f, Amplitude = 0.012f } } };
        noise.OrientationNoise = new[] { new NoiseSettings.TransformNoiseParams {
            X = new NoiseSettings.NoiseParams { Frequency = 7.92f, Amplitude = 0.65f },
            Y = new NoiseSettings.NoiseParams { Frequency = 6.27f, Amplitude = 0.55f },
            Z = new NoiseSettings.NoiseParams { Frequency = 9.24f, Amplitude = 0.8f } } };
        AssetDatabase.CreateAsset(noise, Folder + "/ChurchAnomalyNoise.asset");
        return noise;
    }

    private static AnimationClip MakeAnimation()
    {
        var clip = new AnimationClip { name = "ChurchCameraMotion", frameRate = 60 };
        ConfigureAnimation(clip);
        AssetDatabase.CreateAsset(clip, Folder + "/ChurchCameraMotion.anim");
        return clip;
    }

    private static void ConfigureAnimation(AnimationClip clip)
    {
        Curve(clip, typeof(Transform), "localPosition.x", new float[] {0, 9.7f, 10.2f, 10.6f, 11, 13, 14.5f, 15.5f, 17},
            new float[] {0, 0, .12f, .28f, .25f, .25f, .08f, 0, 0});
        Curve(clip, typeof(Transform), "localPosition.y", new float[] {0, 10, 10.175f, 10.575f, 10.725f, 11, 13, 14.2f, 15.5f, 17},
            new float[] {0, 0, -.15f, -1.9f, -2.25f, -2.25f, -2.25f, -1.1f, 0, 0});
        Curve(clip, typeof(Transform), "localPosition.z", new float[] {0, 10, 10.75f, 13, 15.5f, 17},
            new float[] {0, 0, .18f, .18f, 0, 0});
        Curve(clip, typeof(Transform), "localEulerAnglesRaw.x", new float[] {0, 1.5f, 3.2f, 3.9f, 5.4f, 6.1f, 7.2f, 7.7f, 9, 10, 10.25f, 10.675f, 10.9f, 13, 14.2f, 15.2f, 15.5f, 17},
            new float[] {0, 0, -1, 1, -3, -2, 2, 1, -6, -6, 12, -58, -88, -88, -38, -4, -6, -6});
        // Hesitant scans stay away from the church until the one-second peak at 9–10 s.
        Curve(clip, typeof(Transform), "localEulerAnglesRaw.y", new float[] {0, 1.5f, 3.2f, 3.9f, 5.4f, 6.1f, 7.2f, 7.7f, 9, 10, 10.6f, 11, 13, 14.5f, 15.5f, 17},
            new float[] {70, 70, 115, 115, 35, 35, 95, 95, 0, 0, -8, -6, -6, 2, 0, 0});
        Curve(clip, typeof(Transform), "localEulerAnglesRaw.z", new float[] {0, 1.5f, 3.2f, 3.9f, 5.4f, 6.1f, 7.2f, 7.7f, 9, 10, 10.25f, 10.7f, 11, 13, 14, 15, 15.5f, 17},
            new float[] {0, -.4f, 1.5f, -.6f, -2, 1, 2, -1, 0, 0, 24, 72, 78, 78, 32, -3, 0, 0});
        Curve(clip, typeof(CinemachineBasicMultiChannelPerlin), "AmplitudeGain",
            new float[] {0, 1, 1.5f, 3.2f, 3.9f, 5.4f, 6.1f, 7.2f, 7.7f, 9, 10, 10.175f, 10.5f, 10.8f, 11, 13, 14, 15.5f, 17},
            new float[] {0, .2f, .35f, .75f, 1, 1.5f, 1.85f, 2.35f, 2.7f, 3.6f, 3.6f, 2.8f, 2, .5f, 0, 0, .125f, 0, 0});
        Curve(clip, typeof(CinemachineBasicMultiChannelPerlin), "FrequencyGain",
            new float[] {0, 1.5f, 3.2f, 5.4f, 7.2f, 9, 10, 10.5f, 11, 13, 15.5f, 17},
            new float[] {.75f, .9f, 1.15f, 1.5f, 1.8f, 2.2f, 2.2f, 1.8f, 1, 1, 1, 1});
    }

    private static void Curve(AnimationClip clip, Type type, string property, float[] times, float[] values)
    {
        var curve = new AnimationCurve(times.Select((time, i) => new Keyframe(time, values[i])).ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", type, property), curve);
    }
}
