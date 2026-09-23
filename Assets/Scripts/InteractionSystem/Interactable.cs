using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    [SerializeField] private UnityEvent onInteract;
    private InputAction interactAction;

    public void Interact()
    {
        if (enabled && gameObject.activeInHierarchy)
            onInteract.Invoke();
    }
    void Awake()
    {
        interactAction = InputSystem.actions.FindAction("Interact");
    }

    void Update()
    {
        if(interactAction.WasPressedThisFrame())
        {
            OnInteractAction();
        }       
    }

    private void OnInteractAction()
    {
        if (enabled && gameObject.activeInHierarchy)
        {
            onInteract.Invoke();
        }
            
    }
}
