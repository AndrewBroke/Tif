using UnityEngine;
using System.Collections;

public class NPCController : MonoBehaviour
{
    [Tooltip("Скорость поворота (в секундах).")]
    [SerializeField] private float rotationDuration = 0.5f;

    void Start()
    {

    }

    public void RotateToObject(Transform objectRotateTo)
    {
        StartCoroutine(RotateToPlayerRoutine(objectRotateTo));
    }

    private IEnumerator RotateToPlayerRoutine(Transform objectTransform)
    {
        Vector3 direction = objectTransform.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            yield break;

        Quaternion initialRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        float elapsed = 0f;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, Mathf.Clamp01(elapsed / rotationDuration));
            yield return null;
        }

        transform.rotation = targetRotation;
    }
}