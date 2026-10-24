using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One idempotent lock per owner. UI requests a cursor; any lock blocks gameplay.</summary>
public static class PlayerControlLocks
{
    private struct Entry
    {
        public MonoBehaviour Owner;
        public bool ShowCursor;
    }

    private static readonly List<Entry> locks = new List<Entry>();
    private static int transitionFrame = -1;
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        locks.Clear();
        transitionFrame = -1;
        Changed = null;
    }

    public static bool IsBlocked { get { Prune(); return locks.Count != 0; } }
    // Ignore an E press from the frame in which a UI/lock was closed.
    public static bool InteractionsBlocked => IsBlocked || transitionFrame == Time.frameCount;
    public static bool WantsCursor
    {
        get
        {
            Prune();
            return locks.Exists(entry => entry.ShowCursor);
        }
    }

    public static void Acquire(MonoBehaviour owner, bool showCursor = false)
    {
        Prune();
        if (owner == null || !owner.isActiveAndEnabled)
            return;
        int index = locks.FindIndex(entry => ReferenceEquals(entry.Owner, owner));
        if (index >= 0 && locks[index].ShowCursor == showCursor)
            return;
        var value = new Entry { Owner = owner, ShowCursor = showCursor };
        if (index >= 0) locks[index] = value;
        else locks.Add(value);
        InteractionDiagnostics.Log(owner, "LOCK_ACQUIRE", $"showCursor={showCursor}; locks={locks.Count}");
        Publish();
    }

    public static void Release(MonoBehaviour owner)
    {
        if (locks.RemoveAll(entry => ReferenceEquals(entry.Owner, owner)) != 0)
        {
            InteractionDiagnostics.Log(owner, "LOCK_RELEASE", $"locks={locks.Count}; interactionsBlockedForThisFrame=true");
            Publish();
        }
        Prune();
    }

    private static void Prune()
    {
        int removed = locks.RemoveAll(entry => entry.Owner == null || !entry.Owner.isActiveAndEnabled);
        if (removed != 0)
        {
            InteractionDiagnostics.Log(InteractionDiagnostics.Source, "LOCK_PRUNE",
                $"removedDisabledOrDestroyedOwners={removed}; locks={locks.Count}");
            Publish();
        }
    }

    private static void Publish()
    {
        transitionFrame = Time.frameCount;
        ApplyCursor();
        Changed?.Invoke();
    }

    public static void ApplyCursor()
    {
        bool visible = WantsCursor;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = visible;
    }
}
