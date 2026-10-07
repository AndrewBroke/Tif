using UnityEngine;
using UnityEngine.Events;



public class InvokeOnTrigger : MonoBehaviour
{
    [SerializeField] private UnityEvent onTriggerEnter;
    [SerializeField] private string tagToCompare = "Player";

    private void OnTriggerEnter(Collider other)
    {
        print("Collision with: " + other.gameObject);
        if (other.CompareTag(tagToCompare))
        {
            onTriggerEnter?.Invoke();
        }
    }

}
