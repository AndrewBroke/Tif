
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PlayerStumble : MonoBehaviour
{
    [Header("Объекты")]
    public Transform player;
    public Transform playerCameraRoot;
    public Animator volumeAnimator;

    [Header("События")]
    public UnityEvent onFallStart;
    public UnityEvent onFallEnd;

    [Header("Время анимации")]
    public float stumbleTime = 0.2f;
    public float fallTime = 0.45f;
    public float groundPause = 0.3f;
    public float headLiftTime = 0.6f;
    public float lookPause = 0.5f;
    public float standUpTime = 1.2f;

    [Header("Спотыкание")]
    public Vector3 stumblePosition = new Vector3(0, -0.1f, 0.15f);
    public Vector3 stumbleRotation = new Vector3(20, 0, -10);

    [Header("Падение")]
    public Vector3 fallPosition = new Vector3(0, -1.2f, 0.4f);
    public Vector3 fallRotation = new Vector3(70, 0, -15);

    [Header("Поднятие головы")]
    public Vector3 lookPosition = new Vector3(0, -1.1f, 0.4f);
    public Vector3 lookRotation = new Vector3(-5, 0, -5);

    [Header("Настройки")]
    public bool onlyOnce = true;

    private Vector3 originalPosition;
    private Quaternion originalRotation;

    private bool isFalling;
    private bool wasActivated;

    private void OnTriggerEnter(Collider other)
    {
        if (player == null) return;

        if (other.transform != player &&
            !other.transform.IsChildOf(player))
            return;

        StartFall();
    }

    public void StartFall()
    {
        if (player != null && player.TryGetComponent(out PlayerHealth health) && health.IsDead) return;
        if (!isActiveAndEnabled || isFalling) return;
        if (onlyOnce && wasActivated) return;
        if (playerCameraRoot == null) return;

        StartCoroutine(FallSequence());
    }

    private IEnumerator FallSequence()
    {
        try
        {
            isFalling = true;
            PlayerControlLocks.Acquire(this);
            wasActivated = true;

            // Блокируем управление
            onFallStart?.Invoke();

            // Запоминаем исходное положение камеры
            originalPosition = playerCameraRoot.localPosition;
            originalRotation = playerCameraRoot.localRotation;

            // 1. Спотыкание
            yield return AnimateCamera(
                Vector3.zero,
                stumblePosition,
                Vector3.zero,
                stumbleRotation,
                stumbleTime
            );

            // 2. Падение
            yield return AnimateCamera(
                stumblePosition,
                fallPosition,
                stumbleRotation,
                fallRotation,
                fallTime
            );

            // 3. Эффект удара о землю
            if (volumeAnimator != null)
            {
                volumeAnimator.SetTrigger("Hit");
            }

            // 4. Лежим на земле
            yield return new WaitForSeconds(groundPause);

            // 5. Поднимаем голову
            yield return AnimateCamera(
                fallPosition,
                lookPosition,
                fallRotation,
                lookRotation,
                headLiftTime
            );

            // 6. Смотрим вперёд
            yield return new WaitForSeconds(lookPause);

            // 7. Поднимаемся
            yield return AnimateCamera(
                lookPosition,
                Vector3.zero,
                lookRotation,
                Vector3.zero,
                standUpTime
            );

            // Восстанавливаем исходное положение
            playerCameraRoot.localPosition = originalPosition;
            playerCameraRoot.localRotation = originalRotation;


            // Возвращаем управление
            onFallEnd?.Invoke();
        }
        finally
        {
            isFalling = false;
            PlayerControlLocks.Release(this);
        }
    }

    private IEnumerator AnimateCamera(
        Vector3 fromPosition,
        Vector3 toPosition,
        Vector3 fromRotation,
        Vector3 toRotation,
        float duration)
    {
        if (duration <= 0f)
        {
            ApplyCameraPose(
                toPosition,
                Quaternion.Euler(toRotation)
            );
            yield break;
        }

        float timer = 0f;

        Quaternion startRot = Quaternion.Euler(fromRotation);
        Quaternion endRot = Quaternion.Euler(toRotation);

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);

            // Плавное начало и конец движения
            t = Mathf.SmoothStep(0f, 1f, t);

            Vector3 position = Vector3.Lerp(
                fromPosition,
                toPosition,
                t
            );

            Quaternion rotation = Quaternion.Slerp(
                startRot,
                endRot,
                t
            );

            ApplyCameraPose(position, rotation);

            yield return null;
        }

        ApplyCameraPose(toPosition, endRot);
    }

    private void ApplyCameraPose(
        Vector3 offset,
        Quaternion rotation)
    {
        playerCameraRoot.localPosition =
            originalPosition + offset;

        playerCameraRoot.localRotation =
            originalRotation * rotation;
    }
    public void CancelFall()
    {
        StopAllCoroutines();
        isFalling = false;
        PlayerControlLocks.Release(this);
    }

    private void OnDisable() => CancelFall();

}
