using System.Collections;
using UnityEngine;

public class BlackScreenController : MonoBehaviour
{
    [SerializeField] private CanvasGroup blackScreen;
    [SerializeField, Min(0f)] private float fadeToBlackDuration = 0.5f;
    [SerializeField, Min(0f)] private float fadeFromBlackDuration = 0.7f;
    [SerializeField] private bool useUnscaledTime = true;

    private Coroutine blinkCoroutine;

    private void Awake()
    {
        if (blackScreen == null)
            blackScreen = GetComponent<CanvasGroup>();

        if (blackScreen != null)
            blackScreen.alpha = 0f;
    }

    public void Blink(float holdDuration)
    {
        if (!isActiveAndEnabled)
            return;

        if (blackScreen == null)
        {
            Debug.LogWarning("Assign the black screen CanvasGroup before calling Blink.", this);
            return;
        }

        if (blinkCoroutine != null)
            StopCoroutine(blinkCoroutine);

        blinkCoroutine = StartCoroutine(BlinkRoutine(Mathf.Max(0f, holdDuration)));
    }

    private IEnumerator BlinkRoutine(float holdDuration)
    {
        yield return FadeTo(1f, fadeToBlackDuration);

        float elapsed = 0f;
        while (elapsed < holdDuration)
        {
            yield return null;
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        yield return FadeTo(0f, fadeFromBlackDuration);
        blinkCoroutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = blackScreen.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return null;
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            blackScreen.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
        }

        blackScreen.alpha = targetAlpha;
    }

    private void OnDisable()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        if (blackScreen != null)
            blackScreen.alpha = 0f;
    }
}
