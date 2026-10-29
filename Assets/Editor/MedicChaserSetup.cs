using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static class MedicChaserSetup
{
    private const string ModelPath = "Assets/Models/VITALY NEWS/CHARANIM/Anim_master_medic_V0.001_APPLIED TRANSFORM.fbx";
    private const string Folder = "Assets/Animations/Medic Chaser";
    private const string ControllerPath = Folder + "/Medic Chaser.controller";
    private const string PrefabPath = "Assets/Prefabs/Medic Chaser.prefab";
    private static readonly string[] StateNames = { "Run", "Walk", "Crawl", "Idle", "BiteNeck" };
    private static readonly string[] ClipNames = { "Run_solider_01", "Walk_01.001", "Crawl", "Idle_01", "Neck_attack" };

    [InitializeOnLoadMethod]
    private static void UpgradeExistingController()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                return;

            if (controller.layers[0].stateMachine.states.Any(s => s.state.name == "BiteNeck"))
            {
                EnsureRetreatPointInPrefab();
                return;
            }

            UnityEngine.Object previousSelection = Selection.activeObject;
            try { CreateAssets(); }
            finally { Selection.activeObject = previousSelection; }
        };
    }

    [MenuItem("Tools/TIF/Medic/Create Chaser Assets")]
    public static void CreateAssets()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Animator modelAnimator = model != null ? model.GetComponentInChildren<Animator>(true) : null;
        if (modelAnimator == null || modelAnimator.avatar == null || !modelAnimator.avatar.isValid || !modelAnimator.avatar.isHuman)
            throw new InvalidOperationException("Medic chaser requires a valid Humanoid model and Avatar: " + ModelPath);

        AnimationClip[] sourceClips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
        AnimationClip[] sources = ClipNames.Select(name => sourceClips.FirstOrDefault(c => c.name == name)).ToArray();
        if (sources.Any(c => c == null || !c.isHumanMotion))
            throw new InvalidOperationException("Medic model must contain Humanoid clips: " + string.Join(", ", ClipNames));

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Animations", "Medic Chaser");

        var clips = new AnimationClip[StateNames.Length];
        for (int i = 0; i < StateNames.Length; i++)
        {
            string clipPath = Folder + "/" + StateNames[i] + ".anim";
            clips[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clips[i] != null)
                continue;

            clips[i] = UnityEngine.Object.Instantiate(sources[i]);
            clips[i].name = StateNames[i];
            var settings = AnimationUtility.GetAnimationClipSettings(clips[i]);
            settings.loopTime = StateNames[i] != "BiteNeck";
            settings.loopBlend = settings.loopTime;
            settings.keepOriginalPositionXZ = true;
            settings.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clips[i], settings);
            AssetDatabase.CreateAsset(clips[i], clipPath);
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        AnimatorStateMachine states = controller.layers[0].stateMachine;
        for (int i = 0; i < StateNames.Length; i++)
        {
            if (states.states.Any(s => s.state.name == StateNames[i]))
                continue;

            AnimatorState state = states.AddState(StateNames[i], new Vector3(300f, i * 80f, 0f));
            state.motion = clips[i];
            if (states.defaultState == null)
                states.defaultState = state;
            EditorUtility.SetDirty(controller);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            GameObject root = new GameObject("Medic Chaser");
            root.SetActive(false);
            try
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                Animator animator = visual.GetComponentInChildren<Animator>(true);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
                agent.agentTypeID = 0; // Matches the existing Act 2 NavMeshSurface.
                agent.radius = 0.3f;
                agent.height = 1.8f;
                agent.speed = 3.5f;
                agent.acceleration = 12f;
                agent.angularSpeed = 360f;
                agent.stoppingDistance = 0f;

                CapsuleCollider contact = root.AddComponent<CapsuleCollider>();
                contact.isTrigger = true;
                contact.center = Vector3.up * 0.9f;
                contact.radius = 0.4f;
                contact.height = 1.8f;
                Rigidbody body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                ProximityChaser chaser = root.AddComponent<ProximityChaser>();
                var serialized = new SerializedObject(chaser);
                serialized.FindProperty("characterAnimator").objectReferenceValue = animator;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        EnsureRetreatPointInPrefab();
        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log("Medic Chaser ready: place the prefab on the baked NavMesh. PlayerHealth is found automatically, or assign it explicitly. Choose Initial Animation in ProximityChaser.");
    }

    private static void EnsureRetreatPointInPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ProximityChaser chaser = root.GetComponent<ProximityChaser>();
            if (chaser == null)
                return;

            var serialized = new SerializedObject(chaser);
            SerializedProperty pointProperty = serialized.FindProperty("retreatPoint");
            if (pointProperty.objectReferenceValue != null)
                return;

            Transform point = root.transform.Find("Retreat Point");
            if (point == null)
            {
                point = new GameObject("Retreat Point").transform;
                point.SetParent(root.transform, false);
                point.localPosition = new Vector3(0f, 0f, -8f);
            }
            pointProperty.objectReferenceValue = point;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
