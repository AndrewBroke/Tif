using UnityEngine;
using UnityEngine.Events;



public class InvokeOnTrigger : MonoBehaviour
{
    [SerializeField] private UnityEvent onTriggerEnter;
    [SerializeField] private string tagToCompare = "Player";
    [SerializeField] private bool oneShot = true;
    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        print("Collision with: " + other.gameObject);
        if (!hasTriggered && other.CompareTag(tagToCompare))
        {
            // Destroy is deferred; multiple player colliders can enter this frame.
            if (oneShot) hasTriggered = true;
            onTriggerEnter?.Invoke();
            if(oneShot) Destroy(gameObject);
        }
    }

}
