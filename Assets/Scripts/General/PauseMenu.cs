using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using StarterAssets;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using DialogueEditor;
using System.Collections.Generic;

[DefaultExecutionOrder(-200)]
public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider sensitivitySlider;
    [Tooltip("Active objects to close on Esc before opening the pause menu.")]
    [SerializeField] private List<GameObject> closeOnEscape = new List<GameObject>();

    [Header("Player")]
    [SerializeField] private FirstPersonController playerController;

    [Header("Mouse sensitivity")]
    [SerializeField] private float minSensitivity = 0.1f;
    [SerializeField] private float maxSensitivity = 5f;
    [SerializeField] private float defaultSensitivity = 1f;
    [SerializeField] private UnityEvent onPause;
    [SerializeField] private UnityEvent onContinue;

    private bool isPaused;
    private bool isClosingDialogue;
    private float timeScaleBeforePause = 1f;

    private const string VolumeKey = "MasterVolume";
    private const string SensitivityKey = "MouseSensitivity";

    private void Start()
    {
        // Загружаем сохранённые настройки
        float volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        float sensitivity = PlayerPrefs.GetFloat(
            SensitivityKey,
            defaultSensitivity
        );

        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 2f;
        volumeSlider.value = volume;

        sensitivitySlider.minValue = minSensitivity;
        sensitivitySlider.maxValue = maxSensitivity;
        sensitivitySlider.value = sensitivity;

        SetVolume(volume);
        SetSensitivity(sensitivity);

        pauseMenu.SetActive(false);

        ResumeGame();
    }

    private void Update()
    {
        ConversationManager conversationManager = ConversationManager.Instance;
        bool isDialogueActive = conversationManager != null && conversationManager.IsConversationActive;

        if (!isDialogueActive)
            isClosingDialogue = false;

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
            {
                ResumeGame();
                return;
            }

            bool closedObject = false;
            foreach (GameObject uiObject in closeOnEscape)
            {
                if (uiObject == null || !uiObject.activeInHierarchy)
                    continue;

                uiObject.SetActive(false);
                closedObject = true;
            }

            if (isDialogueActive)
            {
                // Ignore further presses while the dialogue fades out.
                if (!isClosingDialogue || DialogueController.ActiveDialogue != null)
                {
                    isClosingDialogue = true;
                    DialogueController activeDialogue = DialogueController.ActiveDialogue;
                    if (activeDialogue != null)
                        activeDialogue.InterruptDialogue();
                    else
                    {
                        conversationManager.CancelConversation();
                    }
                }

                return;
            }

            if (!closedObject)
                PauseGame();
        }
    }

    public void TogglePause()
    {
        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        if (isPaused || !isActiveAndEnabled) return;
        timeScaleBeforePause = Time.timeScale;
        isPaused = true;
        PlayerControlLocks.Acquire(this, showCursor: true);
        Time.timeScale = 0f;
        pauseMenu.SetActive(true);
        onPause?.Invoke();
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        isPaused = false;
        Time.timeScale = timeScaleBeforePause;
        PlayerControlLocks.Release(this);
        pauseMenu.SetActive(false);
        onContinue?.Invoke();
    }

    private void OnDisable()
    {
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = timeScaleBeforePause;
            if (pauseMenu != null) pauseMenu.SetActive(false);
        }
        PlayerControlLocks.Release(this);
    }

    public void SetVolume(float value)
    {
        value = Mathf.Clamp(value, 0f, 2f);
        if (audioMixer != null)
        {
            float decibels = value <= 1f
                ? (value > 0f ? Mathf.Max(-80f, Mathf.Log10(value) * 20f) : -80f)
                : (value - 1f) * 20f;
            audioMixer.SetFloat(VolumeKey, decibels);
        }

        PlayerPrefs.SetFloat(VolumeKey, value);
        PlayerPrefs.Save();
    }

    public void SetSensitivity(float value)
    {
        if (playerController != null)
        {
            playerController.RotationSpeed = value;
        }

        PlayerPrefs.SetFloat(SensitivityKey, value);
        PlayerPrefs.Save();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        PlayerPrefs.Save();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;

        PlayerPrefs.Save();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
