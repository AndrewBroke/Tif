using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayerPushReaction : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Volume volume;

    [Header("Push")]
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float pushDistance = 1.5f;

    [Header("Rotation")]
    [SerializeField] private float maxPitch = -29f;
    [SerializeField] private float maxRoll = 3f;

    [Header("Push Curve")]
    [SerializeField] private AnimationCurve pushCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Rotation Curves")]
    [SerializeField] private AnimationCurve pitchCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.78f, 1f),
            new Keyframe(1f, 0f)
        );

    [SerializeField] private AnimationCurve rollCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.78f, 1f),
            new Keyframe(1f, 0f)
        );

    [Header("Post Processing")]
    [SerializeField] private AnimationCurve volumeCurve =
        new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.78f, 0f),
            new Keyframe(1f, 1f)
        );

    private Coroutine pushCoroutine;

    public void Push()
    {
        if (pushCoroutine != null)
        {
            StopCoroutine(pushCoroutine);
        }

        pushCoroutine = StartCoroutine(PushRoutine());
    }

    private IEnumerator PushRoutine()
    {
        float timer = 0f;

        // Направление толчка фиксируем в момент получения урона
        Vector3 pushDirection = -transform.forward;

        // Запоминаем обычный поворот игрока
        Quaternion startRotation = transform.rotation;

        float previousPush = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);

            // =========================
            // ТОЛЧОК
            // =========================

            float currentPush =
                pushCurve.Evaluate(t) * pushDistance;

            float deltaPush =
                currentPush - previousPush;

            characterController.Move(
                pushDirection * deltaPush
            );

            previousPush = currentPush;


            // =========================
            // НАКЛОН ИГРОКА
            // =========================

            float pitch =
                pitchCurve.Evaluate(t) * maxPitch;

            float roll =
                rollCurve.Evaluate(t) * maxRoll;

            transform.rotation =
                startRotation *
                Quaternion.Euler(pitch, 0f, roll);


            // =========================
            // POST PROCESSING
            // =========================

            if (volume != null)
            {
                volume.weight =
                    Mathf.Clamp01(volumeCurve.Evaluate(t));
            }


            yield return null;
        }

        // Возвращаем всё точно в исходное состояние
        transform.rotation = startRotation;

        if (volume != null)
        {
            volume.weight = 0f;
        }

        pushCoroutine = null;
    }
}