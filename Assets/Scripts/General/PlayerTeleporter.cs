
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PlayerTeleporter : MonoBehaviour
{
    [Header("Объекты")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform cameraRoot;

    [Header("Затемнение")]
    [SerializeField] private CanvasGroup blackScreen;
    [SerializeField, Min(0f)] private float fadeToBlackDuration = 0.5f;
    [SerializeField, Min(0f)] private float fadeFromBlackDuration = 0.7f;

    [Header("Позиция игрока")]
    [SerializeField] private Vector3 targetPosition;

    [Header("Вращение игрока")]
    [SerializeField] private Vector3 targetRotation;

    [Header("Вращение Camera Root")]
    [SerializeField] private Vector3 cameraRootRotation;

    [Header("Задержки")]
    [Min(0)]
    [SerializeField] private float beforeTeleportDelay = 0.3f;

    [Min(0)]
    [SerializeField] private float afterTeleportDelay = 0.3f;

    [Header("События")]
    public UnityEvent OnStart;
    public UnityEvent OnTeleported;
    public UnityEvent OnEnd;

    private bool isTeleporting;

    public void Teleport()
    {
        if (isTeleporting)
            return;

        if (player == null || cameraRoot == null)
        {
            Debug.LogError("PlayerTeleporter: Не назначены объекты!");
            return;
        }

        StartCoroutine(TeleportRoutine());
    }

    private IEnumerator TeleportRoutine()
    {
        isTeleporting = true;

        // Блокируем управление и запускаем затемнение
        OnStart?.Invoke();

        yield return FadeBlackScreen(1f, fadeToBlackDuration);

        if (beforeTeleportDelay > 0)
            yield return new WaitForSecondsRealtime(beforeTeleportDelay);

        // Перемещаем игрока
        player.SetPositionAndRotation(
            targetPosition,
            Quaternion.Euler(targetRotation)
        );

        // Поворачиваем Camera Root
        cameraRoot.localRotation =
            Quaternion.Euler(cameraRootRotation);

        Physics.SyncTransforms();

        // Игрок перемещён, экран пока остаётся чёрным
        OnTeleported?.Invoke();

        if (afterTeleportDelay > 0)
            yield return new WaitForSecondsRealtime(afterTeleportDelay);

        yield return FadeBlackScreen(0f, fadeFromBlackDuration);

        // Возвращаем управление
        OnEnd?.Invoke();

        isTeleporting = false;
    }

    private IEnumerator FadeBlackScreen(float targetAlpha, float duration)
    {
        if (blackScreen == null)
            yield break;

        float startAlpha = blackScreen.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            blackScreen.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        blackScreen.alpha = targetAlpha;
    }

    public void TeleportTo(
        Vector3 position,
        Vector3 playerEuler,
        Vector3 cameraEuler)
    {
        targetPosition = position;
        targetRotation = playerEuler;
        cameraRootRotation = cameraEuler;

        Teleport();
    }
}
