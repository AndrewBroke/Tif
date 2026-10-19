using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Настройки сферы")]
    [SerializeField] private float interactRadius = 0.5f;
    [SerializeField] private float interactDistance = 1f;
    [SerializeField] private LayerMask interactableLayerMask = ~0;
    private Interactable currentInteractable;
    private InteractionMarker focusedMarker;
    private InputAction interactAction;
    private Transform playerRoot;
    private Collider currentCollider;

    // Buffers grow only on saturation and are then reused. At the safety limit an
    // incomplete query rejects interaction rather than dropping a wall or a target.
    private const int MaxQueryCapacity = 4096;
    private const float PointEpsilon = 0.001f;
    private const float MaxAimAngle = 45f;
    private const float FocusSwitchMargin = 1.5f;
    private Collider[] candidates = new Collider[32];
    private Collider[] originOverlaps = new Collider[16];
    private RaycastHit[] obstacleHits = new RaycastHit[32];

    private int DetectionLayerMask
    {
        get
        {
            int ignoredLayer = LayerMask.NameToLayer("IgnoreInteract");
            return ignoredLayer >= 0
                ? interactableLayerMask.value & ~(1 << ignoredLayer)
                : interactableLayerMask.value;
        }
    }

    private void Awake()
    {
        interactAction = InputSystem.actions?.FindAction("Player/Interact");
        // In both gameplay scenes this component is on the PlayerCapsule root.
        CharacterController controller = GetComponentInParent<CharacterController>();
        playerRoot = controller != null ? controller.transform : transform;
    }

    void Update()
    {
        if (PlayerControlLocks.InteractionsBlocked)
        {
            ClearCurrentInteractable();
            UpdateMarkerFocus();
            return;
        }

        DetectInteractable();

        if (interactAction != null && interactAction.WasPressedThisFrame())
            TryInteract();

        if (PlayerControlLocks.InteractionsBlocked ||
            (currentInteractable != null && !currentInteractable.CanInteract))
            ClearCurrentInteractable();

        UpdateMarkerFocus();
    }

    private void OnDrawGizmos()
    {
        if (Camera.main == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Camera.main.transform.position + Camera.main.transform.forward * interactDistance, interactRadius);
    }

    private void DetectInteractable()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            ClearCurrentInteractable();
            return;
        }

        Vector3 origin = camera.transform.position;
        Vector3 forward = camera.transform.forward;
        float radius = Mathf.Max(PointEpsilon, interactRadius);
        Vector3 end = origin + forward * Mathf.Max(0f, interactDistance);
        int count;
        while (true)
        {
            // A capsule includes both the swept volume and its starting overlap.
            // Trigger policy must not depend on Physics.queriesHitTriggers.
            count = Physics.OverlapCapsuleNonAlloc(origin, end, radius, candidates,
                DetectionLayerMask, QueryTriggerInteraction.Collide);
            if (count < candidates.Length) break;
            if (!GrowBuffer(ref candidates))
            {
                ClearCurrentInteractable();
                return;
            }
        }

        int overlapCount = QueryOriginOverlaps(origin);
        if (overlapCount < 0)
        {
            ClearCurrentInteractable();
            return;
        }

        Interactable best = null;
        Collider bestCollider = null;
        float bestScore = float.PositiveInfinity;
        Collider retainedCollider = null;
        float retainedScore = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider collider = candidates[i];
            // Keep the existing same-GameObject ownership contract.
            collider.TryGetComponent(out Interactable candidate);
            if (!TryEvaluate(candidate, collider, origin, forward, overlapCount, out float score))
                continue;

            if (candidate == currentInteractable && score < retainedScore)
            {
                retainedScore = score;
                retainedCollider = collider;
            }

            if (score < bestScore || (score == bestScore &&
                (best == null || candidate.GetEntityId().CompareTo(best.GetEntityId()) < 0)))
            {
                best = candidate;
                bestCollider = collider;
                bestScore = score;
            }
        }

        // Hysteresis only retains a target that passed the same checks this frame.
        if (retainedCollider != null && retainedScore <= bestScore + FocusSwitchMargin)
        {
            best = currentInteractable;
            bestCollider = retainedCollider;
        }
        currentInteractable = best;
        currentCollider = bestCollider;

    }

    private bool TryEvaluate(Interactable candidate, Collider collider, Vector3 origin,
        Vector3 forward, int overlapCount, out float score)
    {
        score = float.PositiveInfinity;
        if (candidate == null || collider == null) return false;
        if (!candidate.CanInteract) return false;
        if (!collider.enabled || !collider.gameObject.activeInHierarchy) return false;
        if (!collider.CompareTag("Interact")) return false;
        if (IsPlayerCollider(collider)) return false;
        if ((DetectionLayerMask & (1 << collider.gameObject.layer)) == 0) return false;

        float reach = Mathf.Max(0f, interactDistance) + Mathf.Max(PointEpsilon, interactRadius);
        Vector3 point;
        if (collider.Raycast(new Ray(origin, forward), out RaycastHit hit, reach) &&
            (hit.point - origin).sqrMagnitude >= PointEpsilon * PointEpsilon)
        {
            point = hit.point;

        }
        else
        {
            // ClosestPoint(origin) becomes zero inside a collider and favours side
            // objects up close. Probe the gaze axis instead; an enclosing collider
            // gets a real forward point, never a zero direction.
            float depth = Mathf.Clamp(Vector3.Dot(collider.bounds.center - origin, forward),
                PointEpsilon * 2f, Mathf.Max(PointEpsilon * 2f, reach));
            point = collider.ClosestPoint(origin + forward * depth);

        }

        Vector3 offset = point - origin;
        float distance = offset.magnitude;
        float forwardDistance = Vector3.Dot(offset, forward);
        if (distance < PointEpsilon) return false;
        if (forwardDistance <= 0f) return false;
        if (distance > reach) return false;

        Vector3 axisPoint = origin + forward * Mathf.Clamp(forwardDistance, 0f,
            Mathf.Max(0f, interactDistance));
        float radius = Mathf.Max(PointEpsilon, interactRadius);
        if ((point - axisPoint).sqrMagnitude > radius * radius + PointEpsilon * PointEpsilon)
            return false;

        float angle = Mathf.Atan2(Vector3.Cross(offset, forward).magnitude, forwardDistance) * Mathf.Rad2Deg;
        if (angle > MaxAimAngle)
            return false;

        if (!IsPathClear(candidate, origin, point, overlapCount))
            return false;

        // Angle dominates; distance provides a small secondary preference.
        score = angle + 0.2f * distance / Mathf.Max(PointEpsilon, reach);
        return true;
    }

    private int QueryOriginOverlaps(Vector3 origin)
    {
        while (true)
        {
            int count = Physics.OverlapSphereNonAlloc(origin, PointEpsilon, originOverlaps,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            if (count < originOverlaps.Length) return count;
            if (!GrowBuffer(ref originOverlaps)) return -1;
        }
    }

    private bool IsPathClear(Interactable target, Vector3 origin, Vector3 point, int overlapCount)
    {
        // Raycasts do not report a collider containing their origin. Do not allow
        // a camera clipped into a solid wall to interact through that wall.
        for (int i = 0; i < overlapCount; i++)
        {
            Collider collider = originOverlaps[i];
            if (BlocksInteraction(collider, target))
                return false;
        }

        Vector3 offset = point - origin;
        float distance = offset.magnitude;
        Vector3 direction = offset / distance;
        // Check both directions: single-sided mesh walls must also block when
        // viewed from their back face, without changing global physics settings.
        return IsSegmentClear(target, origin, direction, distance) &&
            IsSegmentClear(target, point, -direction, distance);
    }

    private bool IsSegmentClear(Interactable target, Vector3 origin, Vector3 direction, float distance)
    {
        while (true)
        {
            // Visibility uses a thin segment to the selected point, not the thick
            // candidate search volume, so a nearby wall edge does not mask it.
            int count = Physics.RaycastNonAlloc(origin, direction, obstacleHits,
                distance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            if (count == obstacleHits.Length)
            {
                if (!GrowBuffer(ref obstacleHits)) return false;
                continue;
            }
            for (int i = 0; i < count; i++)
                if (BlocksInteraction(obstacleHits[i].collider, target))
                    return false;
            return true;
        }
    }

    private bool BlocksInteraction(Collider collider, Interactable target)
    {
        // Availability, tags and candidate masks never exempt a solid obstacle.
        return collider != null && !collider.isTrigger && !IsPlayerCollider(collider) &&
            !target.OwnsInteractionCollider(collider);
    }

    private bool IsPlayerCollider(Collider collider)
    {
        return playerRoot != null && collider.transform.IsChildOf(playerRoot);
    }

    private static bool GrowBuffer<T>(ref T[] buffer)
    {
        if (buffer.Length >= MaxQueryCapacity) return false;
        Array.Resize(ref buffer, Mathf.Min(buffer.Length * 2, MaxQueryCapacity));
        return true;
    }

    private void TryInteract()
    {
        Camera camera = Camera.main;
        if (PlayerControlLocks.InteractionsBlocked || camera == null || currentInteractable == null)
        {
            ClearCurrentInteractable();
            return;
        }

        Vector3 origin = camera.transform.position;
        int count = QueryOriginOverlaps(origin);
        if (count < 0 || !TryEvaluate(currentInteractable, currentCollider, origin,
            camera.transform.forward, count, out _))
        {
            ClearCurrentInteractable();
            return;
        }
        currentInteractable.Interact();
    }

    private void ClearCurrentInteractable()
    {
        currentInteractable = null;
        currentCollider = null;
    }

    private void UpdateMarkerFocus()
    {
        InteractionMarker next = currentInteractable != null && currentInteractable.CanInteract
            ? currentInteractable.GetComponentInChildren<InteractionMarker>()
            : null;
        if (focusedMarker != next && focusedMarker != null)
            focusedMarker.SetFocused(false);
        focusedMarker = next;
        if (focusedMarker != null)
            focusedMarker.SetFocused(true);
    }

    private void OnDisable()
    {
        ClearCurrentInteractable();
        if (focusedMarker != null)
            focusedMarker.SetFocused(false);
        focusedMarker = null;
    }

}
