using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Сторона запускаемого приложения.
/// Интеграция минимальна: клиент НЕ управляет окнами и не захватывает фокус
/// (этим занимается лаунчер), а только
///   1) читает переданный JSON-аргумент {"lang":"..."} (при отсутствии/повреждении - en);
///   2) локализует интерфейс сцены по lang;
///   3) возвращает лаунчеру Exit Code (1 - досрочный выход, 3 - успешное прохождение).
/// Приложение при выходе вызывает методы LeaveGame/CompleteGame.
/// SimulateSystemError - для тестирования системной ошибки. Например, вылета приложения.
///
/// Тест-хук (вне спецификации): поле "auto" в JSON (1 | 3 | -1) - автоматический
/// выход с заданным Exit Code через 2 секунды после старта, для CLI-проверки.
/// </summary>
public class GameApplication : MonoBehaviour
{
    public const int ExitCodeEarlyExit = 1;
    public const int ExitCodeCompleted = 3;
    public const int ExitCodeSystemError = -1;

    [Tooltip("Только для режима редактора: применяется вместо CLI-аргумента.")]
    [SerializeField]
    private string editorOverrideLang = "ru";

    /// <summary>Применённый язык интерфейса (ru или en。</summary>
    public string AppliedLanguage { get; private set; } = "en";

    /// <summary>Сырой первый CLI-аргумент (для отладки в консоли。</summary>
    public string RawCommandLineArg { get; private set; } = "";

    private int desiredExitCode = -1;
    private int autoExitCode;
    private bool isPersistent;

    [Serializable]
    private sealed class LaunchConfig
    {
        public string lang;
        public int auto;
    }

    private void Awake()
    {
        foreach (GameObject candidate in
                     GameObject.FindGameObjectsWithTag("GameApplication"))
        {
            GameApplication other = candidate.GetComponent<GameApplication>();

            if (other != null && other != this && other.isPersistent)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }
        }

        // dont destroy onload
        DontDestroyOnLoad(gameObject);
        isPersistent = true;
        Application.wantsToQuit += OnWantsToQuit;
    }

    private void OnDestroy()
    {
        Application.wantsToQuit -= OnWantsToQuit;
    }

    private void Start()
    {
        ParseLaunchConfig();
        ApplyLocalization();

        if (autoExitCode != 0)
        {
            StartCoroutine(AutoExitRoutine(autoExitCode));
        }
    }

    // ------------------------------------------------------------------
    // Парсинг параметров (спецификация, раздел 2)
    // ------------------------------------------------------------------
    private void ParseLaunchConfig()
    {
#if UNITY_EDITOR
        // В редакторе CLI-аргументы принадлежат редактору, а не сцене -
        // берём тестовое значение из inspector.
        AppliedLanguage = NormalizeLanguage(editorOverrideLang);
        RawCommandLineArg = "(редактор)";
#else
        string[] args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            RawCommandLineArg = args[1];
            try
            {
                LaunchConfig cfg = JsonUtility.FromJson<LaunchConfig>(RawCommandLineArg);
                if (cfg != null)
                {
                    ApplyConfig(cfg);
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameApplication] Ошибка парсинга JSON-аргумента: " + e.Message);
            }

            // Аргумент есть, но повреждён - язык по умолчанию (en)
            Debug.LogWarning("[GameApplication] CLI-аргумент повреждён — применяется язык по умолчанию (en)");
            AppliedLanguage = "en";
        }
        else
        {
            // Аргумент не передан - язык по умолчанию (en)
            Debug.Log("[GameApplication] CLI-аргумент не передан — применяется язык по умолчанию (en)");
            AppliedLanguage = "en";
        }
#endif
    }

    private void ApplyConfig(LaunchConfig cfg)
    {
        if (cfg == null)
        {
            return;
        }

        string normalized = NormalizeLanguage(cfg.lang);
        AppliedLanguage = normalized;
        autoExitCode = cfg.auto;

        if (normalized == "en" && !string.IsNullOrEmpty(cfg.lang) && cfg.lang.Trim().ToLowerInvariant() != "en")
        {
            Debug.LogWarning(
                "[GameApplication] Неизвестный язык: " + cfg.lang + " — применяется язык по умолчанию (en.");
        }
    }

    private static string NormalizeLanguage(string lang)
    {
        if (string.IsNullOrEmpty(lang))
        {
            return "en";
        }

        lang = lang.Trim().ToLowerInvariant();
        return lang == "ru" ? "ru" : "en";
    }

    // ------------------------------------------------------------------
    // Гарантированный код возврата (спецификация, раздел 3)
    // ------------------------------------------------------------------

    /// <summary>Кнопка «Покинуть игру»: Exit Code 1 - игрок досрочно вышел.</summary>
    public void LeaveGame()
    {
        QuitWith(ExitCodeEarlyExit);
    }

    /// <summary>Кнопка «Завершить игру»: Exit Code 3 - успешное прохождение и выход.</summary>
    public void CompleteGame()
    {
        QuitWith(ExitCodeCompleted);
    }

    /// <summary>Имитация системной ошибки (крэша): приложение завершается с нетипичным кодом (-1).</summary>
    public void SimulateSystemError()
    {
        QuitWith(ExitCodeSystemError);
    }

    /// <summary>
    /// Единая точка выхода. Процесс завершается мгновенно через TerminateProcess —
    /// системный вызов БЕЗ managed-финализаторов и БЕЗ цикла выхода Unity, поэтому
    /// ни один вариант выхода не может зависнуть:
    ///   • Application.Quit/Environment.Exit на Windows заводят нативный цикл
    ///     завершения плеера в тупик (окно зависает, Alt+Tab не работает);
    ///   • ExitProcess дожидается DLL-detach уведомлений и дедлокируется на
    ///     потоке выхода Unity (поэтому Alt+F4 зависает).
    /// TerminateProcess не ждёт другие потоки и не выполняет DllMain — он всегда
    /// мгновенно завершает процесс с точным кодом.
    /// </summary>
    private void QuitWith(int code)
    {
        desiredExitCode = code;
#if UNITY_EDITOR
        Debug.Log("[GameApplication] Запланирован выход с кодом " + code + " (в редакторе не завершаем).");
#else
        TerminateSelf(code);
#endif
    }

    /// <summary>
    /// Точка выхода «крестиком окна»/Alt+F4: явной кнопки нажато не было —
    /// считаем досрочным выходом (1). Завершаем процесс сразу, не давая Unity
    /// войти в свой цикл выхода (именно он зависал ранее).
    /// </summary>
    private bool OnWantsToQuit()
    {
        if (desiredExitCode < 0)
        {
            desiredExitCode = ExitCodeEarlyExit; // досрочный выход по умолчанию
        }

#if !UNITY_EDITOR
        TerminateSelf(desiredExitCode);
#endif
        return true;
    }

    private void OnApplicationQuit()
    {
#if !UNITY_EDITOR
        // Страховка на случай любого пути выхода вне кнопок/крестика.
        if (desiredExitCode < 0) {
            desiredExitCode = ExitCodeEarlyExit;
        }

        TerminateSelf(desiredExitCode);
#else
        Debug.Log("[GameApplication] OnApplicationQuit: желаемый Exit Code: " + desiredExitCode);
#endif
    }

    /// <summary>
    /// Мгновенное завершение текущего процесса с точным кодом через TerminateProcess.
    /// Код -1 передаётся как 0xFFFFFFFF — лаунчер прочитает Exit Code -1.
    /// </summary>
    private static void TerminateSelf(int exitCode)
    {
        TerminateProcess(GetCurrentProcess(), unchecked((uint)exitCode));
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool TerminateProcess(IntPtr hProcess, uint exitCode);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    private System.Collections.IEnumerator AutoExitRoutine(int code)
    {
        yield return new WaitForSecondsRealtime(2f);
        switch (code)
        {
            case ExitCodeSystemError:
                SimulateSystemError();
                break;
            case ExitCodeEarlyExit:
                LeaveGame();
                break;
            case ExitCodeCompleted:
                CompleteGame();
                break;
            default:
                Debug.LogWarning("[GameApplication] Неизвестный auto-код: " + code);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Локализация интерфейса (классический UI: ссылки задаются в сцене вручную)
    // ------------------------------------------------------------------
    protected virtual void ApplyLocalization()
    {
        StartCoroutine(ApplyLocalizationRoutine());
    }

    private IEnumerator ApplyLocalizationRoutine()
{
    var initialization = LocalizationSettings.InitializationOperation;
    yield return initialization;

    if (initialization.Status != AsyncOperationStatus.Succeeded)
    {
        Debug.LogError("[GameApplication] Ошибка инициализации локализации.");
        yield break;
    }

    var locale = LocalizationSettings.AvailableLocales.Locales
        .Find(x => x.Identifier.Code == AppliedLanguage);

    if (locale == null)
    {
        Debug.LogError(
            "[GameApplication] Локаль не найдена: " + AppliedLanguage);
        yield break;
    }

    LocalizationSettings.SelectedLocale = locale;

    Debug.Log(
        "[GameApplication] Установлен язык: " + locale.Identifier.Code);
}
}
