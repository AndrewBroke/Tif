using UnityEngine;
using System.Collections;

public class PlayerTurnLook : MonoBehaviour
{
     [SerializeField] private Transform target;
    [SerializeField] private Transform cameraTransform;
    [SerializeField, Min(0.01f)] private float turnDuration = 1f;
    [SerializeField, Min(0f)] private float minDistance = 2f;

    private Coroutine turnCoroutine;

    void Start()
    {
        if (target != null && cameraTransform != null)
            LookAtTarget();
    }

    public void LookAtTarget()
    {
        if (target == null || cameraTransform == null)
            return;

        if (turnCoroutine != null)
            StopCoroutine(turnCoroutine);

        turnCoroutine = StartCoroutine(TurnRoutine());
    }

    private IEnumerator TurnRoutine()
    {
        Vector3 horizontalDirection = target.position - transform.position;
        horizontalDirection.y = 0f;

        // Отодвигание
        float distance = horizontalDirection.magnitude;
        if (distance < minDistance && distance > 0.0001f)
        {
            Vector3 correction = -horizontalDirection.normalized * (minDistance - distance);
            transform.position += correction;

            horizontalDirection = target.position - transform.position;
            horizontalDirection.y = 0f;
        }


        if (horizontalDirection.sqrMagnitude < 0.0001f)
            yield break;

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection);
        float startPitch = NormalizeAngle(cameraTransform.localEulerAngles.x);

        Vector3 cameraToTarget = target.position - cameraTransform.position;
        float targetPitch = Mathf.Atan2(cameraToTarget.y, new Vector2(cameraToTarget.x, cameraToTarget.z).magnitude) * Mathf.Rad2Deg;

        float elapsed = 0f;
        while (elapsed < turnDuration)
        {
            float t = elapsed / turnDuration;

            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

            float pitch = Mathf.Lerp(startPitch, targetPitch, t);
            Vector3 localEuler = cameraTransform.localEulerAngles;
            cameraTransform.localEulerAngles = new Vector3(pitch, localEuler.y, localEuler.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;
        Vector3 finalLocalEuler = cameraTransform.localEulerAngles;
        cameraTransform.localEulerAngles = new Vector3(targetPitch, finalLocalEuler.y, finalLocalEuler.z);
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }
}
