// Executes the real dialogue state machine. These stand-ins do not simulate Unity rendering,
// raycasts, serialization, or native object destruction; verify those in Play Mode as well.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        public static Func<Object, Object> InstantiateHandler;
        public static T Instantiate<T>(T original, RectTransform parent) where T : Object =>
            (T)InstantiateHandler(original);
        public static void Destroy(Object value) { }
    }
    public class GameObject : Object
    {
        public string tag = "Untagged";
        public bool activeSelf = true;
        internal readonly List<Behaviour> Components = new();
        public void SetActive(bool active)
        {
            if (activeSelf == active) return;
            activeSelf = active;
            foreach (var component in Components.ToArray())
                component.GetType().GetMethod(active ? "OnEnable" : "OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
        }
    }
    public class Behaviour : Object
    {
        public readonly GameObject gameObject = new();
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled && gameObject.activeSelf;
        public Behaviour() => gameObject.Components.Add(this);
    }
    public class MonoBehaviour : Behaviour
    {
        public T GetComponent<T>() where T : new() => new();
        public bool TryGetComponent<T>(out T value) { value = default; return false; }
        public Coroutine StartCoroutine(IEnumerator routine) => Coroutine.Start(routine);
        public void StopCoroutine(Coroutine routine) => Coroutine.Running.Remove(routine);
    }
    public class Coroutine
    {
        public static readonly List<Coroutine> Running = new();
        private readonly Stack<IEnumerator> stack = new();
        public static Coroutine Start(IEnumerator routine)
        {
            var coroutine = new Coroutine();
            coroutine.stack.Push(routine);
            Running.Add(coroutine);
            coroutine.Step();
            return coroutine;
        }
        private void Step()
        {
            while (stack.Count > 0)
            {
                var routine = stack.Peek();
                if (!routine.MoveNext()) { stack.Pop(); continue; }
                if (routine.Current is IEnumerator child) { stack.Push(child); continue; }
                return;
            }
            Running.Remove(this);
        }
        public static void Tick()
        {
            foreach (var coroutine in Running.ToArray()) coroutine.Step();
        }
    }
    public class RectTransform : Object
    {
        public readonly GameObject gameObject = new();
        public Vector3 localScale;
    }
    public struct Vector3
    {
        public static Vector3 one => new();
        public static Vector3 operator *(Vector3 value, float amount) => value;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a;
    }
    public struct Color { public float a; }
    public class Sprite : Object { }
    public class AudioClip : Object { }
    public class AudioSource : Object
    {
        public AudioClip clip;
        public float volume;
        public int Plays;
        public void Play() => Plays++;
    }
    public static class Mathf
    {
        public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
        public static float Pow(float value, float power) => MathF.Pow(value, power);
    }
    public class SerializeField : Attribute { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public static class Time
    {
        public static float timeScale = 1f, deltaTime = 0.02f, unscaledTime;
        public static int frameCount;
    }
    public enum CursorLockMode { None, Locked }
    public static class Cursor { public static CursorLockMode lockState; public static bool visible; }
    public static class Debug
    {
        public static int Exceptions;
        public static void LogWarning(object value, Object context = null) { }
        public static void LogError(object value, Object context) => throw new Exception(value.ToString());
        public static void LogException(Exception value, Object context) => Exceptions++;
    }
}
namespace UnityEngine.UI
{
    public class MaskableGraphic : UnityEngine.Object { public UnityEngine.Color color; }
    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced }
        public Type type;
        public UnityEngine.Sprite sprite;
    }
}
namespace TMPro
{
    public class TMP_FontAsset : UnityEngine.Object { }
    public class TextMeshProUGUI : UnityEngine.UI.MaskableGraphic
    {
        public string text;
        public int maxVisibleCharacters;
        public TMP_FontAsset font;
    }
}
namespace UnityEngine.Events
{
    public class UnityEvent
    {
        private event Action listeners;
        public void AddListener(Action listener) => listeners += listener;
        public void Invoke() => listeners?.Invoke();
    }
}
namespace UnityEngine.Localization.Tables { public class StringTable { } }
namespace UnityEngine.Playables
{
    public enum PlayState { Paused, Playing }
    public enum DirectorWrapMode { Hold, Loop, None }
    public class PlayableAsset : UnityEngine.Object { }
    public class PlayableDirector : UnityEngine.Behaviour
    {
        public PlayState state;
        public DirectorWrapMode extrapolationMode;
        public double time, duration = 1;
        public PlayableAsset playableAsset;
        public event Action<PlayableDirector> stopped;
        public void Play() => state = PlayState.Playing;
        public void Stop() { state = PlayState.Paused; stopped?.Invoke(this); }
    }
}
namespace DialogueEditor
{
    // Graph construction/localization are outside these state-machine checks.
    public class NPCConversation : UnityEngine.Object
    {
        public Conversation Data;
        public string LocalizationTable;
        public Conversation Deserialize(UnityEngine.Localization.Tables.StringTable table) => Data;
    }
}
public class DialogueLocalization
{
    public UnityEngine.Localization.Tables.StringTable Table;
    public IEnumerator Load(string table) { yield break; }
}
public class Interactable
{
    public bool Completed, Cancelled;
    public bool DeferCompletion() => true;
    public void CompleteInteraction() => Completed = true;
    public void CancelInteraction() => Cancelled = true;
}
public class LogSwingController : UnityEngine.MonoBehaviour
{
    public bool IsPlaying;
    public int Starts;
    public event Action<bool> Finished;
    public void PlaySwing() { IsPlaying = true; Starts++; }
    public void Complete() { IsPlaying = false; Finished?.Invoke(true); }
    public void StopSwing() { IsPlaying = false; Finished?.Invoke(false); }
}
public class PlayerPushReaction : UnityEngine.MonoBehaviour
{
    public bool IsReacting;
    public event Action<bool> Finished;
    public void Push() => IsReacting = true;
    public void Complete() { IsReacting = false; Finished?.Invoke(true); }
    public void StopReaction() { IsReacting = false; Finished?.Invoke(false); }
}
