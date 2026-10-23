// Lightweight execution environment for orchestration checks, not a Unity rendering simulation.
using System;

namespace UnityEngine
{
    public class Object { }
    public class GameObject : Object { public string tag = "Untagged"; }
    public class Behaviour : Object { public bool isActiveAndEnabled = true; }
    public class MonoBehaviour : Behaviour
    {
        public GameObject gameObject = new GameObject();
        public bool TryGetComponent<T>(out T value) { value = default; return false; }
    }
    public class SerializeField : Attribute { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public static class Time { public static float unscaledTime; public static int frameCount; }
    public enum CursorLockMode { None, Locked }
    public static class Cursor { public static CursorLockMode lockState; public static bool visible; }
    public static class Debug
    {
        public static int Errors;
        public static void LogWarning(object value, Object context) { }
        public static void LogError(object value, Object context) { Errors++; }
        public static void LogException(Exception value, Object context) { throw value; }
    }
}

namespace UnityEngine.Events
{
    public class UnityEvent
    {
        private event Action listeners;
        public void AddListener(Action action) => listeners += action;
        public void Invoke() => listeners?.Invoke();
    }
}

namespace UnityEngine.Playables
{
    public enum PlayState { Paused, Playing }
    public enum DirectorWrapMode { Hold, Loop, None }
    public class PlayableAsset : UnityEngine.Object { }
    public class PlayableDirector : UnityEngine.Behaviour
    {
        public PlayState state;
        public DirectorWrapMode extrapolationMode;
        public double time;
        public double duration = 1;
        public PlayableAsset playableAsset;
        public event Action<PlayableDirector> stopped;
        public void Play() => state = PlayState.Playing;
        public void Stop() { state = PlayState.Paused; stopped?.Invoke(this); }
    }
}

namespace DialogueEditor
{
    public class NPCConversation : UnityEngine.Object { }
    public class ConversationManager : UnityEngine.MonoBehaviour
    {
        public static ConversationManager Instance;
        public static Action OnConversationEnded;
        public bool IsConversationActive;
        public void StartConversation(NPCConversation conversation)
        {
            IsConversationActive = true;
            PlayerControlLocks.Acquire(this, true);
        }
        public void EndConversation() => OnConversationEnded?.Invoke();
        public void CancelConversation() { CloseUI(); }
        public void CloseUI()
        {
            IsConversationActive = false;
            PlayerControlLocks.Release(this);
        }
    }
}

public class Interactable
{
    public bool DeferCompletion() => false;
    public void CompleteInteraction() { }
    public void CancelInteraction() { }
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
