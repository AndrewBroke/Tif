using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class PlayerPushReaction : MonoBehaviour
{
     [Header("Push")]
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float pushDistance = 1.5f;

    [Header("Rotation")]
    [SerializeField] private float maxPitch = -29f;
    [SerializeField] private float maxRoll = 3f;

    [Header("Curves")]

    // Движение назад: постепенно набираем смещение
    [SerializeField] private AnimationCurve pushCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Наклон назад/вниз:
    // 0 -> максимум примерно на 0.39 сек -> обратно в 0
    [SerializeField] private AnimationCurve pitchCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.78f, 1f),
            new Keyframe(1f, 0f)
        );

    // Небольшой наклон в сторону
    [SerializeField] private AnimationCurve rollCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.78f, 1f),
            new Keyframe(1f, 0f)
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

        // Запоминаем направление в момент удара
        Vector3 pushDirection = -transform.forward;

        // Запоминаем первоначальный поворот
        Quaternion startRotation = transform.rotation;

        float previousPush = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);

            // -------------------------
            // ДВИЖЕНИЕ НАЗАД
            // -------------------------

            float currentPush =
                pushCurve.Evaluate(t) * pushDistance;

            float deltaPush =
                currentPush - previousPush;

            transform.position +=
                pushDirection * deltaPush;

            previousPush = currentPush;

            // -------------------------
            // НАКЛОН
            // -------------------------

            float pitch =
                pitchCurve.Evaluate(t) * maxPitch;

            float roll =
                rollCurve.Evaluate(t) * maxRoll;

            transform.rotation =
                startRotation *
                Quaternion.Euler(pitch, 0f, roll);

            yield return null;
        }

        // Гарантированно возвращаем Rotation
        transform.rotation = startRotation;

        pushCoroutine = null;
    }

    void Update()
    {
        // if(Keyboard.current.hKey.wasPressedThisFrame)
        // {
        //     Push();
        // }
    }
}
