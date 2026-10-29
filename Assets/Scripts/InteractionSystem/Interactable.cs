using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    [SerializeField] private UnityEvent onInteract = new UnityEvent();
    [Tooltip("Разрешено ли действие по сюжету. Наведение не меняет это значение.")]
    [SerializeField] private bool available = true;
    [Tooltip("Запретить повторное использование после завершения действия.")]
    [SerializeField] private bool oneShot;

    [Tooltip("Root of this character body. Its non-interactive colliders do not obstruct this action. Do not assign environment or shared scene roots.")]
    [SerializeField] private Transform interactionBodyRoot;

    // Explicit ownership only: never infer an owner from an arbitrary common ancestor
    // or ignore an entire layer. Other interactables remain physical obstacles.
    internal bool OwnsInteractionCollider(Collider collider)
    {
        if (collider == null) return false;
        if (collider.TryGetComponent(out Interactable owner)) return owner == this;
        return interactionBodyRoot != null && transform.IsChildOf(interactionBodyRoot) &&
            collider.transform.IsChildOf(interactionBodyRoot);
    }

    private bool isExecuting;
    private bool isCompleted;
    private bool completionPending;
    private bool availableBeforeInteraction;
    private Transform interactionRangeOrigin;
    private float interactionRangeSquared = float.PositiveInfinity;

    public bool IsExecuting => isExecuting || completionPending;
    public bool IsCompleted => isCompleted;
    public bool CanInteract => isActiveAndEnabled && available && !IsExecuting && !isCompleted;
    // Range gates input/focus, while story availability still controls the distant marker.
    // Existing objects have no range override and retain their previous behavior.
    public bool CanInteractNow => CanInteract && (interactionRangeOrigin == null ||
        (transform.position - interactionRangeOrigin.position).sqrMagnitude <= interactionRangeSquared);

    public void SetInteractionRange(Transform origin, float distance)
    {
        interactionRangeOrigin = origin;
        float radius = Mathf.Max(0f, distance);
        interactionRangeSquared = radius * radius;
    }

    public void SetAvailable(bool value)
    {
        available = value;
    }

    // Called from onInteract when the action continues beyond the UnityEvent call.
    public bool DeferCompletion()
    {
        if (!isExecuting || completionPending)
            return false;

        completionPending = true;
        isCompleted = false;
        return true;
    }

    public void CompleteInteraction()
    {
        if (!completionPending)
            return;

        completionPending = false;
        isCompleted = oneShot;
    }

    public void CancelInteraction()
    {
        if (!completionPending)
            return;

        completionPending = false;
        isCompleted = false;
        available = availableBeforeInteraction;
    }

    public void Interact()
    {
        if (PlayerControlLocks.InteractionsBlocked || !CanInteractNow)
            return;

        availableBeforeInteraction = available;
        // Set guards before callbacks, which may try to invoke this action again.
        isExecuting = true;
        if (oneShot)
            isCompleted = true;

        try
        {
            onInteract?.Invoke();
        }
        finally
        {
            isExecuting = false;
        }
    }
}
