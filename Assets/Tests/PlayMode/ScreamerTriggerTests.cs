#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class ScreamerTriggerTests
{
    private const string PrefabPath = "Assets/Prefabs/Medic Crawl Screamer.prefab";
    private readonly List<Object> cleanup = new List<Object>();
    private NavMeshDataInstance navMesh;
    private Type triggerType;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Runtime scripts remain in Assembly-CSharp; tests avoid changing project assembly boundaries.
        triggerType = Type.GetType("ScreamerTrigger, Assembly-CSharp", true);
        var sources = new List<NavMeshBuildSource>
        {
            Floor(Vector3.zero), Floor(Vector3.right * 30f)
        };
        NavMeshData data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
            new Bounds(Vector3.right * 15f, new Vector3(70f, 10f, 30f)), Vector3.zero, Quaternion.identity);
        Assert.That(data, Is.Not.Null);
        cleanup.Add(data);
        navMesh = NavMesh.AddNavMeshData(data);
        yield return null;
    }

    private static NavMeshBuildSource Floor(Vector3 position) => new NavMeshBuildSource
    {
        shape = NavMeshBuildSourceShape.Box,
        size = new Vector3(20f, 0.2f, 20f),
        transform = Matrix4x4.TRS(position + Vector3.down * 0.1f, Quaternion.identity, Vector3.one),
        area = 0
    };

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // Destroy triggers first so their owned instances are cleaned up as well.
        foreach (Object item in cleanup)
            if (item != null) Object.Destroy(item);
        foreach (AudioSource sound in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (sound.name == "Screamer sound") Object.Destroy(sound.gameObject);
        yield return null;
        if (navMesh.valid) navMesh.Remove();
        cleanup.Clear();
    }

    private Component CreateTrigger(bool sceneCharacter = false, bool once = true, bool destroy = true)
    {
        GameObject root = new GameObject("Test screamer trigger");
        cleanup.Add(root);
        root.SetActive(false);
        Component trigger = root.AddComponent(triggerType);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject source = prefab;
        if (sceneCharacter)
        {
            source = Object.Instantiate(prefab, root.transform);
            source.SetActive(false);
            source.transform.SetParent(null);
            cleanup.Add(source);
        }
        Transform spawn = new GameObject("Spawn").transform;
        spawn.SetParent(root.transform);
        spawn.position = Vector3.left * 3f;
        Transform destination = new GameObject("Destination").transform;
        destination.SetParent(root.transform);
        destination.position = Vector3.right * 3f;
        Set(trigger, "medicPrefabOrCharacter", source);
        Set(trigger, "spawnPoint", spawn);
        Set(trigger, "destinationPoint", destination);
        Set(trigger, "triggerOnce", once);
        Set(trigger, "destroyOnFinish", destroy);
        root.SetActive(true);
        return trigger;
    }

    private static FieldInfo Field(Component trigger, string name) => trigger.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Set(Component trigger, string name, object value) => Field(trigger, name).SetValue(trigger, value);
    private static T Get<T>(Component trigger, string name) => (T)Field(trigger, name).GetValue(trigger);
    private static void Activate(Component trigger) => trigger.GetType().GetMethod("Activate").Invoke(trigger, null);
    private static bool Running(Component trigger) => (bool)trigger.GetType().GetProperty("IsRunning").GetValue(trigger);

    private static IEnumerator WaitForFinish(Component trigger, float timeout = 4f)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (Running(trigger) && Time.realtimeSinceStartup < end)
            yield return null;
        Assert.That(Running(trigger), Is.False, "Scare did not finish in time");
        yield return null; // Destroy is deferred until the end of the frame.
    }

    [UnityTest]
    public IEnumerator PrefabTravelsAnimatesAndDestroys_Once_WithoutAudio()
    {
        Component trigger = CreateTrigger();
        Assert.That(Get<GameObject>(trigger, "character"), Is.Null, "Prefab must not spawn before activation");
        Activate(trigger);
        Assert.That(Running(trigger), Is.True);
        GameObject character = Get<GameObject>(trigger, "character");
        Vector3 start = character.transform.position;
        Animator animator = character.GetComponentInChildren<Animator>();
        Assert.That(animator.applyRootMotion, Is.False);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Crawl"), Is.True);
        yield return new WaitForSeconds(0.25f);
        Assert.That(Vector3.Distance(character.transform.position, start), Is.GreaterThan(0.1f));
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(0f));
        Activate(trigger);
        Assert.That(Get<GameObject>(trigger, "character"), Is.SameAs(character), "Cannot spawn twice while running");
        yield return WaitForFinish(trigger);
        Assert.That(character == null, Is.True, "Instantiated character must be destroyed");
        Activate(trigger);
        Assert.That(Running(trigger), Is.False, "Trigger Once must prevent reactivation");
    }

    [UnityTest]
    public IEnumerator SceneCharacterIsHiddenThenDeactivatedAndCanRepeat()
    {
        Component trigger = CreateTrigger(sceneCharacter: true, once: false);
        GameObject source = Get<GameObject>(trigger, "medicPrefabOrCharacter");
        Assert.That(source.activeSelf, Is.False);
        Activate(trigger);
        Assert.That(Get<GameObject>(trigger, "character"), Is.SameAs(source));
        yield return WaitForFinish(trigger);
        Assert.That(source != null && !source.activeSelf, Is.True);
        Activate(trigger);
        Assert.That(Running(trigger), Is.True);
        yield return WaitForFinish(trigger);
    }

    [UnityTest]
    public IEnumerator PhysicsRecognizesTaggedParent_AndKeepsMovingAfterExit()
    {
        Component trigger = CreateTrigger();
        GameObject player = new GameObject("Test Player");
        cleanup.Add(player);
        player.tag = "Player";
        GameObject child = new GameObject("Untagged player collider");
        child.transform.SetParent(player.transform, false);
        child.AddComponent<CapsuleCollider>();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.That(Running(trigger), Is.True, "Box trigger must recognize the player parent tag");
        player.transform.position = Vector3.forward * 50f;
        yield return new WaitForFixedUpdate();
        Assert.That(Running(trigger), Is.True, "Leaving the trigger must not cancel movement");
        yield return WaitForFinish(trigger);
    }

    [UnityTest]
    public IEnumerator TimeoutCleansUpBlockedAgent()
    {
        Component trigger = CreateTrigger(destroy: false);
        Set(trigger, "movementTimeout", 0.2f);
        Activate(trigger);
        GameObject character = Get<GameObject>(trigger, "character");
        character.GetComponent<NavMeshAgent>().isStopped = true;
        LogAssert.Expect(LogType.Warning, new Regex("ScreamerTrigger: Movement timed out"));
        yield return WaitForFinish(trigger);
        Assert.That(character != null && !character.activeSelf, Is.True);
        ((Behaviour)trigger).enabled = false;
        Assert.That(character.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator DisconnectedDestinationAndOffMeshSpawnAreCleanedUp()
    {
        Component trigger = CreateTrigger(once: false);
        Get<Transform>(trigger, "destinationPoint").position = Vector3.right * 30f;
        LogAssert.Expect(LogType.Warning, new Regex("ScreamerTrigger: No complete NavMesh path"));
        Activate(trigger);
        yield return WaitForFinish(trigger);
        Get<Transform>(trigger, "spawnPoint").position = Vector3.up * 100f;
        LogAssert.Expect(LogType.Warning, new Regex("ScreamerTrigger: Spawn or destination is outside"));
        Activate(trigger);
        Assert.That(Running(trigger), Is.False);
        Assert.That(Get<GameObject>(trigger, "character"), Is.Null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator SoundOutlivesCharacterAndRetainedPrefabCanRepeat()
    {
        Component trigger = CreateTrigger(once: false, destroy: false);
        AudioClip clip = AudioClip.Create("Test scare", 44100 * 5, 1, 44100, false);
        cleanup.Add(clip);
        Set(trigger, "screamerSound", clip);
        Activate(trigger);
        GameObject character = Get<GameObject>(trigger, "character");
        yield return WaitForFinish(trigger);
        AudioSource sound = Array.Find(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None),
            s => s.clip == clip);
        Assert.That(sound != null && sound.gameObject.activeSelf, Is.True, "Sound object must survive character cleanup");
        Assert.That(sound.spatialBlend, Is.EqualTo(1f));
        Activate(trigger);
        Assert.That(Get<GameObject>(trigger, "character"), Is.SameAs(character));
        Assert.That(Running(trigger), Is.True);
        ((Behaviour)trigger).enabled = false;
        Assert.That(character.activeSelf, Is.False, "Disabling the trigger must clean up a running scare");
    }
}
#endif
