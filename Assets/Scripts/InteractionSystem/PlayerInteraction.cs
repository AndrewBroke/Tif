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
    }

    void Update()
    {
        DetectInteractable();

        if (interactAction != null && interactAction.WasPressedThisFrame())
            currentInteractable?.Interact();

        if (currentInteractable != null && !currentInteractable.CanInteract)
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
        if (Camera.main == null)
        {
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
        foreach (Collider nearbyCollider in Physics.OverlapSphere(origin, interactRadius, detectionLayerMask))
        {
            Interactable candidate = nearbyCollider.GetComponent<Interactable>();
            Vector3 offset = nearbyCollider.ClosestPoint(origin) - origin;
            float forwardDot = Vector3.Dot(offset, direction);
            if (!nearbyCollider.CompareTag("Interact"))
                continue;

            if (candidate == null || !candidate.CanInteract)
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
            SetCurrentInteractable(nearbyInteractable);
            return;
        }

        if (Physics.SphereCast(origin, interactRadius, direction, out RaycastHit hit, interactDistance, detectionLayerMask))
        {
            Interactable hitInteractable = hit.collider.GetComponent<Interactable>();
            if (!hit.collider.CompareTag("Interact"))
            {
                ClearCurrentInteractable();
                return;
            }
            
            if (hitInteractable != null && hitInteractable.CanInteract)
            {
                SetCurrentInteractable(hitInteractable);
                return;
            }
        }

        ClearCurrentInteractable();
    }

    private void SetCurrentInteractable(Interactable interactable)
    {
        currentInteractable = interactable;
    }

    private void ClearCurrentInteractable()
    {
        currentInteractable = null;
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
