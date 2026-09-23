using UnityEngine;
using UnityEngine.Events;



public class InvokeOnTriggerPlayer : MonoBehaviour
{
    [SerializeField] private UnityEvent onTriggerEnter;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            onTriggerEnter?.Invoke();
        }
    }

}
