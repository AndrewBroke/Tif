using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Creates isolated hallucination assets, preserving shared models, controllers and scenes.</summary>
public static class MedicHallucinationSetup
{
    public const string ControllerPath = "Assets/Animations/Act 2/Medic Hallucination.controller";
    public const string MedicPath = "Assets/Prefabs/Hallucination Medic.prefab";
    public const string CivilianPath = "Assets/Prefabs/Hallucination Civilian.prefab";
    public const string TriggerPath = "Assets/Prefabs/Medic Hallucination Trigger.prefab";
    private const string CivilianModelPath = "Assets/Models/VITALY NEWS/CHARANIM/Anim_master_grandfather.fbx";

    [MenuItem("Tools/TIF/Screamer/Create Medic Hallucination Assets")]
    public static void CreateAssets()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(MedicCrawlScreamerSetup.ModelPath);
        GameObject civilianModel = AssetDatabase.LoadAssetAtPath<GameObject>(CivilianModelPath);
        RequireHumanoid(model);
        RequireHumanoid(civilianModel);
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(MedicCrawlScreamerSetup.ModelPath)
            .OfType<AnimationClip>().ToArray();
        AnimationClip run = clips.FirstOrDefault(c => c.name == "Run_solider_01");
        AnimationClip fall = clips.FirstOrDefault(c => c.name == "Fall_flat");
        if (run == null || fall == null || !run.isHumanMotion || !fall.isHumanMotion)
            throw new InvalidOperationException("Medic model must contain Humanoid Run_solider_01 and Fall_flat clips.");

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine states = controller.layers[0].stateMachine;
            AnimatorState running = states.AddState("Run");
            running.motion = run;
            states.defaultState = running;
            states.AddState("Fall").motion = fall;
            EditorUtility.SetDirty(controller);
        }
        CreateCharacter(MedicPath, model, controller, true);
        CreateCharacter(CivilianPath, civilianModel, controller, false);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TriggerPath) == null)
        {
            GameObject root = new GameObject("Medic Hallucination Trigger");
            root.SetActive(false);
            try
            {
                ScreamerTrigger trigger = root.AddComponent<ScreamerTrigger>();
                BoxCollider volume = root.GetComponent<BoxCollider>();
                volume.size = new Vector3(2f, 2f, 2f);
                volume.isTrigger = true;
                Rigidbody body = root.GetComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                Transform spawn = new GameObject("Spawn Point").transform;
                spawn.SetParent(root.transform, false);
                spawn.localPosition = Vector3.forward * 10f;
                spawn.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var settings = new SerializedObject(trigger);
                settings.FindProperty("mode").enumValueIndex = (int)ScreamerTrigger.ScareMode.ChaseAndFall;
                settings.FindProperty("medicPrefabOrCharacter").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(MedicPath);
                settings.FindProperty("civilianPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CivilianPath);
                settings.FindProperty("animatorController").objectReferenceValue = controller;
                settings.FindProperty("animationState").stringValue = "Base Layer.Run";
                settings.FindProperty("animationSpeed").floatValue = 1f;
                settings.FindProperty("movementSpeed").floatValue = 8f;
                settings.FindProperty("spawnPoint").objectReferenceValue = spawn;
                settings.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, TriggerPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        // Save only our assets; never save scenes or unrelated modified project assets.
        AssetDatabase.SaveAssetIfDirty(controller);
        Debug.Log("Medic hallucination assets ready: Run_solider_01, Fall_flat and Humanoid civilian. Configure the existing corpse interaction separately.");
    }

    private static void RequireHumanoid(GameObject model)
    {
        Animator animator = model != null ? model.GetComponentInChildren<Animator>(true) : null;
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            throw new InvalidOperationException("A valid Humanoid model and Avatar are required: " + model);
    }

    private static void CreateCharacter(string path, GameObject model, RuntimeAnimatorController controller, bool navigates)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        GameObject root = new GameObject(navigates ? "Hallucination Medic" : "Hallucination Civilian");
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
            if (navigates)
            {
                NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
                agent.enabled = false;
                agent.height = 1.8f;
                agent.radius = 0.3f;
                agent.speed = 8f;
            }
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [MenuItem("GameObject/TIF/Medic Hallucination Screamer", false, 11)]
    public static void CreateTrigger()
    {
        CreateAssets();
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TriggerPath));
        Undo.RegisterCreatedObjectUndo(root, "Create medic hallucination screamer");
        if (SceneView.lastActiveSceneView != null) root.transform.position = SceneView.lastActiveSceneView.pivot;
        Selection.activeGameObject = root;
    }

}
