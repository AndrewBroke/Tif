using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Creates isolated scare assets without editing the source FBX or shared controllers.</summary>
public static class MedicCrawlScreamerSetup
{
    public const string ModelPath = "Assets/Models/VITALY NEWS/CHARANIM/Anim_master_medic_V0.001_APPLIED TRANSFORM.fbx";
    public const string ClipPath = "Assets/Animations/Act 2/Medic Screamer Crawl.anim";
    public const string ControllerPath = "Assets/Animations/Act 2/Medic Crawl Screamer.controller";
    public const string PrefabPath = "Assets/Prefabs/Medic Crawl Screamer.prefab";

    [MenuItem("Tools/TIF/Screamer/Create Medic Crawl Assets")]
    public static void CreateAssets()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>().FirstOrDefault(c => c.name == "Crawl");
        Animator sourceAnimator = model != null ? model.GetComponentInChildren<Animator>(true) : null;
        if (source == null || sourceAnimator == null || sourceAnimator.avatar == null
            || !sourceAnimator.avatar.isValid || !sourceAnimator.avatar.isHuman || !source.isHumanMotion)
            throw new InvalidOperationException("Medic Crawl requires a valid Humanoid clip and Avatar in " + ModelPath);

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = UnityEngine.Object.Instantiate(source);
            clip.name = "Medic Screamer Crawl";
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            settings.keepOriginalPositionXZ = true;
            settings.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, ClipPath);
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorState state = controller.layers[0].stateMachine.AddState("Crawl");
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            GameObject root = new GameObject("Medic Crawl Screamer");
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
                agent.agentTypeID = 0; // Existing Act 2 surface's agent type.
                agent.height = 0.65f;
                agent.radius = 0.3f;
                agent.speed = 4f;
                agent.acceleration = 24f;
                agent.angularSpeed = 540f;
                agent.stoppingDistance = 0.2f;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Medic crawl assets ready. Source FBX, existing characters and NavMesh were preserved.");
    }

    [MenuItem("GameObject/TIF/Medic Crawl Screamer", false, 10)]
    public static void CreateTrigger()
    {
        CreateAssets();
        GameObject root = new GameObject("Medic Crawl Screamer Trigger");
        Undo.RegisterCreatedObjectUndo(root, "Create medic crawl screamer");
        if (SceneView.lastActiveSceneView != null)
            root.transform.position = SceneView.lastActiveSceneView.pivot;
        ScreamerTrigger trigger = root.AddComponent<ScreamerTrigger>();
        root.GetComponent<BoxCollider>().size = new Vector3(2f, 2f, 2f);
        Transform spawn = new GameObject("Spawn Point").transform;
        spawn.SetParent(root.transform, false);
        spawn.localPosition = Vector3.left * 3f;
        Transform destination = new GameObject("Destination Point").transform;
        destination.SetParent(root.transform, false);
        destination.localPosition = Vector3.right * 3f;
        var serialized = new SerializedObject(trigger);
        serialized.FindProperty("medicPrefabOrCharacter").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        serialized.FindProperty("animatorController").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        serialized.FindProperty("spawnPoint").objectReferenceValue = spawn;
        serialized.FindProperty("destinationPoint").objectReferenceValue = destination;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Selection.activeGameObject = root;
    }
}
