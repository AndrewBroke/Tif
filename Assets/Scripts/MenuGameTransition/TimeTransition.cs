using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class TimeTransition : MonoBehaviour
{
    [Header("Тексты")]
    [SerializeField] private TMP_Text cityText;
    [SerializeField] private TMP_Text yearText;
    [SerializeField] private TMP_Text monthText;

    [Header("Затемнение экрана")]
    [SerializeField] private CanvasGroup fadeGroup;

    [Header("Годы")]
    [SerializeField] private int startYear = 2026;
    [SerializeField] private int targetYear = 1919;

    [Tooltip("На каком году Новосибирск сменится на Новониколаевск")]
    [SerializeField] private int cityChangeYear = 1925;

    [Tooltip("Продолжительность перемотки года")]
    [SerializeField] private float yearChangeDuration = 6f;

    [Header("Кривая перемотки")]
    [Tooltip("Медленно → быстро → медленно")]
    [SerializeField]
    private AnimationCurve yearCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.3f),
        new Keyframe(0.15f, 0.05f, 0.8f, 3f),
        new Keyframe(0.5f, 0.5f, 2.5f, 2.5f),
        new Keyframe(0.85f, 0.95f, 0.8f, 0.8f),
        new Keyframe(1f, 1f, 0f, 0f)
    );

    [Header("Смена города")]
    [SerializeField] private float cityFadeDuration = 0.5f;

    [Header("Январь")]
    [SerializeField] private float monthFadeDuration = 1f;

    [Header("Финальное затемнение")]
    [SerializeField] private float pauseBeforeFade = 2f;
    [SerializeField] private Animator fadeImageAnimator;


    private void Start()
    {
        StartCoroutine(PlayTransition());
    }


    private IEnumerator PlayTransition()
    {
        // ==========================================
        // НАЧАЛЬНОЕ СОСТОЯНИЕ
        // ==========================================

        cityText.text = "Новосибирск";
        yearText.text = startYear.ToString();
        monthText.text = "Январь";

        SetTextAlpha(cityText, 1f);
        SetTextAlpha(yearText, 1f);
        SetTextAlpha(monthText, 0f);

        fadeGroup.alpha = 0f;


        // ==========================================
        // ПЕРЕМОТКА ГОДА
        // ==========================================

        float elapsed = 0f;
        bool cityChanged = false;

        while (elapsed < yearChangeDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / yearChangeDuration
            );

            // ======================================
            // Ease In Out
            // Медленно → быстро → медленно
            // ======================================

            float easedProgress = yearCurve.Evaluate(progress);


            // ======================================
            // Вычисляем текущий год
            // ======================================

            float currentYear = Mathf.Lerp(
                startYear,
                targetYear,
                easedProgress
            );

            int displayedYear = Mathf.RoundToInt(currentYear);

            yearText.text = displayedYear.ToString();


            // ======================================
            // Смена Новосибирск → Новониколаевск
            // ======================================

            if (!cityChanged &&
                displayedYear <= cityChangeYear)
            {
                cityChanged = true;

                StartCoroutine(
                    ChangeCity()
                );
            }


            yield return null;
        }


        // ==========================================
        // ГАРАНТИРУЕМ 1919
        // ==========================================

        yearText.text = targetYear.ToString();


        // ==========================================
        // ПОЯВЛЕНИЕ "ЯНВАРЬ"
        // ==========================================

        yield return StartCoroutine(
            FadeTextIn(
                monthText,
                monthFadeDuration
            )
        );


        // ==========================================
        // НЕБОЛЬШАЯ ПАУЗА
        // ==========================================

        yield return new WaitForSeconds(
            pauseBeforeFade
        );

        fadeImageAnimator.SetTrigger("Fade");
    }


    // ==================================================
    // СМЕНА ГОРОДА
    // ==================================================

    private IEnumerator ChangeCity()
    {
        // Плавно убираем "Новосибирск"
        yield return StartCoroutine(
            FadeTextOut(
                cityText,
                cityFadeDuration
            )
        );


        // Меняем текст
        cityText.text = "Новониколаевск";


        // Плавно показываем новый текст
        yield return StartCoroutine(
            FadeTextIn(
                cityText,
                cityFadeDuration
            )
        );
    }


    // ==================================================
    // ПОЯВЛЕНИЕ ТЕКСТА
    // ==================================================

    private IEnumerator FadeTextIn(
        TMP_Text text,
        float duration
    )
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / duration
            );

            // Ease In Out
            float easedProgress = Mathf.SmoothStep(
                0f,
                1f,
                progress
            );

            SetTextAlpha(
                text,
                easedProgress
            );

            yield return null;
        }

        SetTextAlpha(text, 1f);
    }


    // ==================================================
    // ИСЧЕЗНОВЕНИЕ ТЕКСТА
    // ==================================================

    private IEnumerator FadeTextOut(
        TMP_Text text,
        float duration
    )
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / duration
            );

            // Ease In Out
            float easedProgress = Mathf.SmoothStep(
                0f,
                1f,
                progress
            );

            SetTextAlpha(
                text,
                1f - easedProgress
            );

            yield return null;
        }

        SetTextAlpha(text, 0f);
    }


    // ==================================================
    // УСТАНОВКА ПРОЗРАЧНОСТИ ТЕКСТА
    // ==================================================

    private void SetTextAlpha(
        TMP_Text text,
        float alpha
    )
    {
        Color color = text.color;

        color.a = alpha;

        text.color = color;
    }
}