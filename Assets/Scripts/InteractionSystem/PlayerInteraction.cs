using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Настройки сферы")]
    [SerializeField] private float interactRadius = 0.5f;
    [SerializeField] private float interactDistance = 1f;
    [SerializeField] private LayerMask interactableLayerMask = ~0;
    [Header("Диагностика взаимодействия (Editor / Development Build)")]
    [SerializeField] internal bool logInteractionDiagnostics = true;
    [Tooltip("Часть имени объекта или его родителя. Пусто — все взаимодействия.")]
    [SerializeField] internal string diagnosticNameFilter = "FireInteract";
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

    private struct EvaluationDiagnostic
    {
        public string failure;
        public Vector3 point;
        public bool hasPoint;
        public bool raycastHit;
        public Collider blocker;
        public float score;
    }

    private sealed class TargetDiagnostic
    {
        public Interactable target;
        public EvaluationDiagnostic evaluation;
        public int seenFrame = -1;
        public bool logged;
        public string lastFailure;
        public bool lastSelected;
        public bool lastRaycastHit;
        public Collider lastBlocker;
    }

    private readonly Dictionary<Collider, TargetDiagnostic> diagnosticTargets =
        new Dictionary<Collider, TargetDiagnostic>();
    private string cachedDiagnosticFilter;
    private bool diagnosticsPrepared;

    private void OnEnable()
    {
        InteractionDiagnostics.Source = this;
        InteractionDiagnostics.Log(this, "CONFIG",
            $"filter={diagnosticNameFilter}; distance={interactDistance:F3}; radius={interactRadius:F3}");
    }

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
            ClearCurrentInteractable("InteractionsBlocked");
            UpdateMarkerFocus();
            return;
        }

        DetectInteractable();

        if (interactAction != null && interactAction.WasPressedThisFrame())
            TryInteract();

        if (PlayerControlLocks.InteractionsBlocked ||
            (currentInteractable != null && !currentInteractable.CanInteract))
            ClearCurrentInteractable(PlayerControlLocks.InteractionsBlocked
                ? "InteractionsBlockedAfterInput" : "TargetUnavailableAfterInput");

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
            ClearCurrentInteractable("MainCameraMissing");
            return;
        }

        Vector3 origin = camera.transform.position;
        Vector3 forward = camera.transform.forward;
        PrepareDiagnostics();
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
                ClearCurrentInteractable("CandidateQueryCapacityExceeded");
                return;
            }
        }

        int overlapCount = QueryOriginOverlaps(origin);
        if (overlapCount < 0)
        {
            ClearCurrentInteractable("OriginOverlapQueryCapacityExceeded");
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
            bool accepted = TryEvaluate(candidate, collider, origin, forward, overlapCount,
                out float score, out EvaluationDiagnostic evaluation);
            RecordEvaluation(candidate, collider, evaluation);
            if (!accepted)
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
        if (currentInteractable != best && InteractionDiagnostics.Matches(currentInteractable))
            InteractionDiagnostics.Log(currentInteractable, "TARGET_CHANGE",
                $"next={InteractionDiagnostics.Path(best)}; nextScore={bestScore:F3}; " +
                $"previousScore={retainedScore:F3}; switchMargin={FocusSwitchMargin:F3}; " +
                $"previousCollider={InteractionDiagnostics.Path(currentCollider)}");
        currentInteractable = best;
        currentCollider = bestCollider;
        LogDetectionChanges(origin, forward, count);
    }

    private bool TryEvaluate(Interactable candidate, Collider collider, Vector3 origin,
        Vector3 forward, int overlapCount, out float score, out EvaluationDiagnostic diagnostic)
    {
        diagnostic = default;
        score = float.PositiveInfinity;
        if (candidate == null || collider == null) return Reject(ref diagnostic, "MissingTargetOrCollider");
        if (!candidate.CanInteract) return Reject(ref diagnostic, "TargetUnavailable");
        if (!collider.enabled || !collider.gameObject.activeInHierarchy)
            return Reject(ref diagnostic, "ColliderDisabledOrInactive");
        if (!collider.CompareTag("Interact")) return Reject(ref diagnostic, "TagIsNotInteract");
        if (IsPlayerCollider(collider)) return Reject(ref diagnostic, "PlayerCollider");
        if ((DetectionLayerMask & (1 << collider.gameObject.layer)) == 0)
            return Reject(ref diagnostic, "LayerExcluded");

        float reach = Mathf.Max(0f, interactDistance) + Mathf.Max(PointEpsilon, interactRadius);
        Vector3 point;
        if (collider.Raycast(new Ray(origin, forward), out RaycastHit hit, reach) &&
            (hit.point - origin).sqrMagnitude >= PointEpsilon * PointEpsilon)
        {
            point = hit.point;
            diagnostic.raycastHit = true;
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

        diagnostic.point = point;
        diagnostic.hasPoint = true;
        Vector3 offset = point - origin;
        float distance = offset.magnitude;
        float forwardDistance = Vector3.Dot(offset, forward);
        if (distance < PointEpsilon) return Reject(ref diagnostic, "PointTooCloseToCamera");
        if (forwardDistance <= 0f) return Reject(ref diagnostic, "PointBehindCamera");
        if (distance > reach) return Reject(ref diagnostic, "BeyondReach");

        Vector3 axisPoint = origin + forward * Mathf.Clamp(forwardDistance, 0f,
            Mathf.Max(0f, interactDistance));
        float radius = Mathf.Max(PointEpsilon, interactRadius);
        if ((point - axisPoint).sqrMagnitude > radius * radius + PointEpsilon * PointEpsilon)
            return Reject(ref diagnostic, "PointOutsideSearchCapsule");

        float angle = Mathf.Atan2(Vector3.Cross(offset, forward).magnitude, forwardDistance) * Mathf.Rad2Deg;
        if (angle > MaxAimAngle)
            return Reject(ref diagnostic, "AimAngleExceeded");

        if (!IsPathClear(candidate, origin, point, overlapCount,
            out diagnostic.blocker, out diagnostic.failure))
            return false;

        // Angle dominates; distance provides a small secondary preference.
        score = angle + 0.2f * distance / Mathf.Max(PointEpsilon, reach);
        diagnostic.score = score;
        return true;
    }

    private static bool Reject(ref EvaluationDiagnostic diagnostic, string reason)
    {
        diagnostic.failure = reason;
        return false;
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

    private bool IsPathClear(Interactable target, Vector3 origin, Vector3 point, int overlapCount,
        out Collider blocker, out string failure)
    {
        blocker = null;
        failure = null;
        // Raycasts do not report a collider containing their origin. Do not allow
        // a camera clipped into a solid wall to interact through that wall.
        for (int i = 0; i < overlapCount; i++)
        {
            Collider collider = originOverlaps[i];
            if (BlocksInteraction(collider, target))
            {
                blocker = collider;
                failure = "CameraInsideSolid";
                return false;
            }
        }

        Vector3 offset = point - origin;
        float distance = offset.magnitude;
        Vector3 direction = offset / distance;
        // Check both directions: single-sided mesh walls must also block when
        // viewed from their back face, without changing global physics settings.
        if (!IsSegmentClear(target, origin, direction, distance, out blocker, out failure))
        {
            if (blocker != null) failure = "ObstructedCameraToTarget";
            return false;
        }
        if (!IsSegmentClear(target, point, -direction, distance, out blocker, out failure))
        {
            if (blocker != null) failure = "ObstructedTargetToCamera";
            return false;
        }
        return true;
    }

    private bool IsSegmentClear(Interactable target, Vector3 origin, Vector3 direction, float distance,
        out Collider blocker, out string failure)
    {
        blocker = null;
        failure = null;
        while (true)
        {
            // Visibility uses a thin segment to the selected point, not the thick
            // candidate search volume, so a nearby wall edge does not mask it.
            int count = Physics.RaycastNonAlloc(origin, direction, obstacleHits,
                distance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            if (count == obstacleHits.Length)
            {
                if (!GrowBuffer(ref obstacleHits))
                {
                    failure = "ObstacleQueryCapacityExceeded";
                    return false;
                }
                continue;
            }
            for (int i = 0; i < count; i++)
                if (BlocksInteraction(obstacleHits[i].collider, target))
                {
                    blocker = obstacleHits[i].collider;
                    return false;
                }
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
        if (InteractionDiagnostics.Enabled &&
            (currentInteractable == null || InteractionDiagnostics.Matches(currentInteractable)))
            InteractionDiagnostics.Log(currentInteractable != null ? currentInteractable : this,
                "E_PRESSED", $"target={InteractionDiagnostics.Path(currentInteractable)}; " +
                $"blocked={PlayerControlLocks.InteractionsBlocked}; camera={InteractionDiagnostics.Path(camera)}");
        if (PlayerControlLocks.InteractionsBlocked || camera == null || currentInteractable == null)
        {
            ClearCurrentInteractable("InputBlockedOrCameraOrTargetMissing");
            return;
        }

        Vector3 origin = camera.transform.position;
        int count = QueryOriginOverlaps(origin);
        if (count < 0)
        {
            ClearCurrentInteractable("InputOriginOverlapQueryCapacityExceeded");
            return;
        }
        if (!TryEvaluate(currentInteractable, currentCollider, origin,
            camera.transform.forward, count, out _, out EvaluationDiagnostic evaluation))
        {
            if (InteractionDiagnostics.Matches(currentInteractable))
                InteractionDiagnostics.Log(currentInteractable, "INPUT_REJECTED",
                    EvaluationSummary(evaluation, currentCollider, origin, camera.transform.forward));
            ClearCurrentInteractable("InputRevalidationFailed");
            return;
        }
        currentInteractable.Interact();
    }

    private void ClearCurrentInteractable(string reason,
        [System.Runtime.CompilerServices.CallerMemberName] string caller = null)
    {
        if (InteractionDiagnostics.Matches(currentInteractable))
            InteractionDiagnostics.Log(currentInteractable, "TARGET_CLEAR",
                $"caller={caller}; reason={reason}; {InteractionDiagnostics.TargetState(currentInteractable)}; " +
                $"collider={InteractionDiagnostics.Path(currentCollider)}");
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
        ClearCurrentInteractable("PlayerInteractionDisabled");
        if (focusedMarker != null)
            focusedMarker.SetFocused(false);
        focusedMarker = null;
        if (InteractionDiagnostics.Source == this)
            InteractionDiagnostics.Source = null;
    }

    private void PrepareDiagnostics()
    {
        if (!InteractionDiagnostics.Enabled) return;
        if (diagnosticsPrepared && cachedDiagnosticFilter == diagnosticNameFilter) return;
        diagnosticTargets.Clear();
        cachedDiagnosticFilter = diagnosticNameFilter;
        diagnosticsPrepared = true;
        // Discover once, including unavailable stove actions, so a marker that never
        // gains focus still produces a reason. No additional physics queries.
        foreach (Interactable target in UnityEngine.Object.FindObjectsByType<Interactable>(
            FindObjectsInactive.Include))
        {
            if (!InteractionDiagnostics.Matches(target)) continue;
            foreach (Collider collider in target.GetComponents<Collider>())
                diagnosticTargets[collider] = new TargetDiagnostic { target = target };
        }
    }

    private void RecordEvaluation(Interactable target, Collider collider, EvaluationDiagnostic evaluation)
    {
        if (!InteractionDiagnostics.Matches(target)) return;
        if (!diagnosticTargets.TryGetValue(collider, out TargetDiagnostic state))
        {
            state = new TargetDiagnostic { target = target };
            diagnosticTargets.Add(collider, state);
        }
        state.seenFrame = Time.frameCount;
        state.evaluation = evaluation;
    }

    private void LogDetectionChanges(Vector3 origin, Vector3 forward, int candidateCount)
    {
        if (!InteractionDiagnostics.Enabled) return;
        foreach (var pair in diagnosticTargets)
        {
            Collider collider = pair.Key;
            TargetDiagnostic state = pair.Value;
            if (state.target == null || collider == null) continue;
            EvaluationDiagnostic evaluation = state.evaluation;
            if (state.seenFrame != Time.frameCount)
            {
                evaluation = default;
                evaluation.failure = !state.target.CanInteract ? "TargetUnavailable" :
                    !collider.enabled || !collider.gameObject.activeInHierarchy ? "ColliderDisabledOrInactive" :
                    !collider.CompareTag("Interact") ? "TagIsNotInteract" :
                    (DetectionLayerMask & (1 << collider.gameObject.layer)) == 0 ? "LayerExcluded" :
                    "ColliderOutsideSearchCapsule";
            }
            bool selected = currentInteractable == state.target && currentCollider == collider;
            if (state.logged && state.lastFailure == evaluation.failure &&
                state.lastSelected == selected && state.lastRaycastHit == evaluation.raycastHit &&
                state.lastBlocker == evaluation.blocker)
                continue;
            state.logged = true;
            state.lastFailure = evaluation.failure;
            state.lastSelected = selected;
            state.lastRaycastHit = evaluation.raycastHit;
            state.lastBlocker = evaluation.blocker;
            InteractionDiagnostics.Log(state.target, "DETECT",
                $"selected={selected}; chosen={InteractionDiagnostics.Path(currentInteractable)}; " +
                $"candidates={candidateCount}; {InteractionDiagnostics.TargetState(state.target)}; " +
                EvaluationSummary(evaluation, collider, origin, forward));
        }
    }

    private string EvaluationSummary(EvaluationDiagnostic evaluation, Collider collider,
        Vector3 origin, Vector3 forward)
    {
        string summary = $"reason={evaluation.failure ?? "Accepted"}; camera={origin.ToString("F3")}; " +
            $"forward={forward.ToString("F3")}; reach={Mathf.Max(0f, interactDistance) + Mathf.Max(PointEpsilon, interactRadius):F3}; " +
            $"radius={Mathf.Max(PointEpsilon, interactRadius):F3}; mask=0x{DetectionLayerMask:X}; " +
            $"collider={InteractionDiagnostics.Path(collider)}; blocker={InteractionDiagnostics.Path(evaluation.blocker)}";
        if (evaluation.blocker != null)
            summary += $"; blockerType={evaluation.blocker.GetType().Name}; blockerLayer={evaluation.blocker.gameObject.layer}";
        if (evaluation.hasPoint)
        {
            Vector3 offset = evaluation.point - origin;
            Vector3 axis = origin + forward * Mathf.Clamp(Vector3.Dot(offset, forward), 0f, Mathf.Max(0f, interactDistance));
            summary += $"; pointMethod={(evaluation.raycastHit ? "Raycast" : "ClosestPoint")}; " +
                $"point={evaluation.point.ToString("F3")}; pointDistance={offset.magnitude:F3}; " +
                $"axisDistance={Vector3.Distance(evaluation.point, axis):F3}; " +
                $"angle={Vector3.Angle(offset, forward):F2}/{MaxAimAngle:F1}";
        }
        if (evaluation.failure == null)
            summary += $"; score={evaluation.score:F3}; switchMargin={FocusSwitchMargin:F3}";
        Interactable target = collider != null ? collider.GetComponent<Interactable>() : null;
        InteractionMarker marker = target != null ? target.GetComponentInChildren<InteractionMarker>(true) : null;
        if (marker != null)
        {
            Vector3 offset = marker.MarkerPosition - origin;
            summary += $"; marker={marker.MarkerPosition.ToString("F3")}; markerDistance={offset.magnitude:F3}; " +
                $"markerAngle={Vector3.Angle(offset, forward):F2}; markerCanDisplay={marker.CanDisplay}";
        }
        return summary;
    }

}

// Shared configuration for detection, marker and visibility logs. Messages are
// compiled out of release builds; the Inspector switch controls Editor/dev runs.
internal static class InteractionDiagnostics
{
    internal static PlayerInteraction Source;

    internal static bool Enabled
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return Source != null && Source.logInteractionDiagnostics;
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Source = null;

    internal static bool Matches(Component target)
    {
        if (!Enabled || target == null) return false;
        string filter = Source.diagnosticNameFilter;
        if (string.IsNullOrEmpty(filter)) return true;
        for (Transform node = target.transform; node != null; node = node.parent)
            if (node.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        return false;
    }

    internal static string Path(Component target)
    {
        if (target == null) return "<none>";
        string path = target.name;
        for (Transform node = target.transform.parent; node != null; node = node.parent)
            path = node.name + "/" + path;
        return path + $"#{target.GetEntityId()}";
    }

    internal static string TargetState(Interactable target) => target == null ? "target=<none>" :
        $"active={target.gameObject.activeInHierarchy}; enabled={target.enabled}; " +
        $"canInteract={target.CanInteract}; executing={target.IsExecuting}; completed={target.IsCompleted}; " +
        $"tag={target.tag}; layer={target.gameObject.layer}";

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    internal static void Log(Component target, string stage, string detail)
    {
        if (!Enabled) return;
        Debug.Log($"[InteractionDiag][{stage}] frame={Time.frameCount} time={Time.unscaledTime:F3} " +
            $"object={Path(target)}; {detail}", target);
    }
}
