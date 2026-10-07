using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class InvokeAfterTime : MonoBehaviour
{
    [SerializeField] private float delay = 1f;
    [SerializeField] private UnityEvent onTimerFinished;

    // Запускает корутину таймера с опциональной задержкой
    public void StartTimer()
    {

        StartCoroutine(InvokeAfterDelay());
    }

    private IEnumerator InvokeAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        onTimerFinished?.Invoke();
    }
}
