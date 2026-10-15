using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Настройки сферы")]
    [SerializeField] private float interactRadius = 0.5f;
    [SerializeField] private float interactDistance = 1f;
    [SerializeField] private LayerMask interactableLayerMask = ~0;
    private const string InteractActionName = "Interact";
    private Interactable currentInteractable;
    private InteractionMarker focusedMarker;

    [SerializeField] private GameObject interractTip;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (interractTip != null)
            interractTip.SetActive(false);
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
            ClearCurrentInteractable();
            return;
        }

        Transform cam = Camera.main.transform;
        Vector3 origin = cam.position;
        Vector3 direction = cam.forward;

        if (Physics.SphereCast(origin, interactRadius, direction, out RaycastHit hit, interactDistance, interactableLayerMask))
        {
            Interactable hitInteractable = hit.collider.GetComponent<Interactable>();
            if (!hit.collider.CompareTag("Interact"))
            {
                ClearCurrentInteractable();
                return;
            }
            
            if (hitInteractable != null)
            {
                if (hitInteractable != currentInteractable)
                {
                    if (currentInteractable != null)
                    {
                        currentInteractable.enabled = false;
                        if (interractTip != null)
                            interractTip.SetActive(false);
                    }
                    currentInteractable = hitInteractable;
                    currentInteractable.enabled = true;
                    if (interractTip != null)
                        interractTip.SetActive(true);
                }
                return;
            }
        }

        ClearCurrentInteractable();
    }

    private void ClearCurrentInteractable()
    {
        if (currentInteractable != null)
        {
            currentInteractable.enabled = false;
            currentInteractable = null;
            if (interractTip != null)
                interractTip.SetActive(false);
        }
    }

    private void UpdateMarkerFocus()
    {
        InteractionMarker next = currentInteractable != null && currentInteractable.isActiveAndEnabled
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
