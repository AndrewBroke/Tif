using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using StarterAssets;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider sensitivitySlider;

    [Header("Player")]
    [SerializeField] private FirstPersonController playerController;

    [Header("Mouse sensitivity")]
    [SerializeField] private float minSensitivity = 0.1f;
    [SerializeField] private float maxSensitivity = 5f;
    [SerializeField] private float defaultSensitivity = 1f;
    [SerializeField] private UnityEvent onPause;
    [SerializeField] private UnityEvent onContinue;

    private bool isPaused;

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
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
            
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
        onPause?.Invoke();
        isPaused = true;

        pauseMenu.SetActive(true);

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
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
