using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Localization;

public class TimeTransition : MonoBehaviour
{
    [Header("Тексты")]
    [SerializeField] private TMP_Text cityText;
    [SerializeField] private TMP_Text yearText;
    [SerializeField] private TMP_Text monthText;

    private readonly LocalizedString cityName = new LocalizedString("UI_Text", "transition_city_modern");
    private readonly LocalizedString monthName = new LocalizedString("UI_Text", "transition_month_january");
    private bool cityNameReady;
    private bool monthNameReady;

    private void OnEnable()
    {
        cityNameReady = false;
        monthNameReady = false;
        cityName.StringChanged += UpdateCityName;
        monthName.StringChanged += UpdateMonthName;
    }

    private void OnDisable()
    {
        cityName.StringChanged -= UpdateCityName;
        monthName.StringChanged -= UpdateMonthName;
    }

    private void UpdateCityName(string value)
    {
        cityText.text = value;
        cityNameReady = true;
    }

    private void UpdateMonthName(string value)
    {
        monthText.text = value;
        monthNameReady = true;
    }

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

        // Lock and hide cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }


    private IEnumerator PlayTransition()
    {
        // ==========================================
        // НАЧАЛЬНОЕ СОСТОЯНИЕ
        // ==========================================

        yearText.text = startYear.ToString();

        SetTextAlpha(cityText, 0f);
        SetTextAlpha(yearText, 1f);
        SetTextAlpha(monthText, 0f);

        fadeGroup.alpha = 0f;

        yield return new WaitUntil(() => cityNameReady && monthNameReady);
        SetTextAlpha(cityText, 1f);


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
        cityNameReady = false;
        cityName.TableEntryReference = "transition_city_historical";
        yield return new WaitUntil(() => cityNameReady);


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
