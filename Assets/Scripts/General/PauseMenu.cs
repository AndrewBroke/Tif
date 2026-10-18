using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using StarterAssets;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using DialogueEditor;
using System.Collections.Generic;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Slider volumeSlider;
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
    private PlayerMovementDelay playerMovementDelay;

    private const string VolumeKey = "MasterVolume";
    private const string SensitivityKey = "MouseSensitivity";

    private void Awake()
    {
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (player.TryGetComponent(out playerMovementDelay))
                break;
        }
    }

    private void Start()
    {
        // Загружаем сохранённые настройки
        float volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        float sensitivity = PlayerPrefs.GetFloat(
            SensitivityKey,
            defaultSensitivity
        );

        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
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
                        RestorePlayerControls();
                    }
                }

                return;
            }

            if (closedObject)
                RestorePlayerControls();
            else
                PauseGame();
        }
    }

    private void RestorePlayerControls()
    {
        if (playerMovementDelay != null)
            playerMovementDelay.UnlockControls();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        if (playerMovementDelay != null)
            playerMovementDelay.LockControls();

        onPause?.Invoke();
        isPaused = true;

        pauseMenu.SetActive(true);

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        // Start also calls this method; preserve the player's initial movement delay.
        if (isPaused && playerMovementDelay != null)
            playerMovementDelay.UnlockControls();

        onContinue?.Invoke();
        isPaused = false;

        pauseMenu.SetActive(false);

        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetVolume(float value)
    {
        AudioListener.volume = value;

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
