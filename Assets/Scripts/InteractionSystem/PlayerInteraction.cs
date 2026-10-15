using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Настройки сферы")]
    [SerializeField] private float interactRadius = 0.5f;
    [SerializeField] private float interactDistance = 1f;
    [SerializeField] private LayerMask interactableLayerMask = ~0;
    [Header("Отладка")]
    [SerializeField] private bool debugInteraction = true;
    private string lastDetectionKey;
    private Interactable currentInteractable;
    private InteractionMarker focusedMarker;

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

    // Update is called once per frame
    void Update()
    {
        DetectInteractable();
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
        if (Camera.main == null)
        {
            LogDetection("no-camera", "Camera.main не найдена.");
            ClearCurrentInteractable();
            return;
        }

        Transform cam = Camera.main.transform;
        Vector3 origin = cam.position;
        Vector3 direction = cam.forward;
        int detectionLayerMask = DetectionLayerMask;

        // SphereCast does not detect colliders overlapping its starting sphere.
        Interactable nearbyInteractable = null;
        float nearestDistance = float.PositiveInfinity;
        string overlapDetails = "";
        string overlapKey = "";
        foreach (Collider nearbyCollider in Physics.OverlapSphere(origin, interactRadius, detectionLayerMask))
        {
            Interactable candidate = nearbyCollider.GetComponent<Interactable>();
            Vector3 offset = nearbyCollider.ClosestPoint(origin) - origin;
            float forwardDot = Vector3.Dot(offset, direction);
            string reason = !nearbyCollider.CompareTag("Interact") ? "неверный тег"
                : candidate == null ? "нет Interactable на коллайдере"
                : forwardDot < 0f ? "ближайшая точка позади камеры" : "подходит";
            if (debugInteraction)
            {
                overlapKey += $"{nearbyCollider.GetEntityId()}:{reason};";
                overlapDetails += $"\n  {DescribeCollider(nearbyCollider)}: {reason}, distance={offset.magnitude:F3}, forwardDot={forwardDot:F3}";
            }
            if (!nearbyCollider.CompareTag("Interact"))
                continue;

            if (candidate == null)
                continue;

            if (forwardDot < 0f)
                continue;

            float distance = offset.sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearbyInteractable = candidate;
            }
        }

        if (nearbyInteractable != null)
        {
            LogDetection($"overlap:{nearbyInteractable.GetEntityId()}:{overlapKey}",
                $"OverlapSphere выбрал '{nearbyInteractable.name}'. Пересечения:{overlapDetails}");
            SetCurrentInteractable(nearbyInteractable);
            return;
        }

        if (Physics.SphereCast(origin, interactRadius, direction, out RaycastHit hit, interactDistance, detectionLayerMask))
        {
            Interactable hitInteractable = hit.collider.GetComponent<Interactable>();
            string hitReason = !hit.collider.CompareTag("Interact") ? "неверный тег"
                : hitInteractable == null ? "нет Interactable на коллайдере" : "подходит";
            LogDetection($"cast:{hit.collider.GetEntityId()}:{hitReason}:{overlapKey}",
                $"SphereCast: {DescribeCollider(hit.collider)}, distance={hit.distance:F3}, результат: {hitReason}. Пересечения у камеры:{overlapDetails}");
            if (!hit.collider.CompareTag("Interact"))
            {
                ClearCurrentInteractable();
                return;
            }
            
            if (hitInteractable != null)
            {
                SetCurrentInteractable(hitInteractable);
                return;
            }
        }
        else
        {
            LogDetection($"miss:{overlapKey}",
                $"SphereCast ничего не нашёл. Пересечения у камеры:{overlapDetails}");
        }

        ClearCurrentInteractable();
    }

    private void LogDetection(string key, string details)
    {
        if (!debugInteraction || lastDetectionKey == key)
            return;

        lastDetectionKey = key;
        Debug.Log($"[PlayerInteraction] {details}\n" +
            $"Текущая цель: {(currentInteractable != null ? currentInteractable.name : "нет")}; " +
            $"radius={interactRadius}, distance={interactDistance}, mask={interactableLayerMask.value}, effectiveMask={DetectionLayerMask}, " +
            $"queriesHitTriggers={Physics.queriesHitTriggers}, frame={Time.frameCount}", this);
    }

    private static string DescribeCollider(Collider collider)
    {
        Interactable parentInteractable = collider.GetComponentInParent<Interactable>();
        return $"'{collider.name}' (id={collider.GetEntityId()}, tag={collider.tag}, " +
            $"layer={LayerMask.LayerToName(collider.gameObject.layer)} [{collider.gameObject.layer}], " +
            $"trigger={collider.isTrigger}, Interactable в родителях={(parentInteractable != null ? parentInteractable.name : "нет")})";
    }

    private void SetCurrentInteractable(Interactable interactable)
    {
        if (currentInteractable == interactable)
            return;

        ClearCurrentInteractable();
        currentInteractable = interactable;
        currentInteractable.enabled = true;
        if (debugInteraction)
            Debug.Log($"[PlayerInteraction] Цель включена: '{currentInteractable.name}', active={currentInteractable.isActiveAndEnabled}, frame={Time.frameCount}", currentInteractable);
    }

    private void ClearCurrentInteractable()
    {
        if (currentInteractable != null)
        {
            if (debugInteraction)
                Debug.Log($"[PlayerInteraction] Цель потеряна/сменена: '{currentInteractable.name}', enabled={currentInteractable.enabled}, activeInHierarchy={currentInteractable.gameObject.activeInHierarchy}, frame={Time.frameCount}", currentInteractable);
            currentInteractable.enabled = false;
            currentInteractable = null;
        }
    }

    private void UpdateMarkerFocus()
    {
        InteractionMarker next = currentInteractable != null && currentInteractable.isActiveAndEnabled
            ? currentInteractable.GetComponentInChildren<InteractionMarker>()
            : null;
        if (debugInteraction && focusedMarker != next)
            Debug.Log($"[PlayerInteraction] Маркер: '{(focusedMarker != null ? focusedMarker.name : "нет")}' -> '{(next != null ? next.name : "нет")}', frame={Time.frameCount}", this);
        if (focusedMarker != next && focusedMarker != null)
            focusedMarker.SetFocused(false);
        focusedMarker = next;
        if (focusedMarker != null)
            focusedMarker.SetFocused(true);
    }

    private void OnDisable()
    {
        if (debugInteraction)
            Debug.Log("[PlayerInteraction] PlayerInteraction отключён.", this);
        ClearCurrentInteractable();
        if (focusedMarker != null)
            focusedMarker.SetFocused(false);
        focusedMarker = null;
    }

}
