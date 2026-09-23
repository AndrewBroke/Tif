using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;

/// <summary>
/// Блокирует управление игроком на старте сцены и автоматически
/// снимает блокировку через заданное время.
/// Навесьте на тот же объект, где находятся PlayerInput и StarterAssetsInputs
/// (префаб FirstPersonController из Starter Assets).
/// </summary>
public class PlayerMovementDelay : MonoBehaviour
{
    [Tooltip("Сколько секунд после загрузки сцены управление будет заблокировано")]
    [SerializeField] private float lockDuration = 3f;

    [Tooltip("Блокировать ли управление сразу при старте")]
    [SerializeField] private bool lockOnStart = true;

#if ENABLE_INPUT_SYSTEM
    private PlayerInput _playerInput;
#endif
    private StarterAssetsInputs _starterInputs;

    private void Awake()
    {
#if ENABLE_INPUT_SYSTEM
        _playerInput = GetComponent<PlayerInput>();
#endif
        _starterInputs = GetComponent<StarterAssetsInputs>();
    }

    private void Start()
    {
        if (!lockOnStart) return;

        LockControls();

        if (lockDuration > 0f)
        {
            StartCoroutine(UnlockAfterDelay(lockDuration));
        }
    }

    /// <summary>
    /// Полная блокировка ввода: PlayerInput отключается, значения обнуляются.
    /// </summary>
    public void LockControls()
    {
#if ENABLE_INPUT_SYSTEM
        if (_playerInput != null)
        {
            _playerInput.enabled = false;
        }
#endif

        if (_starterInputs != null)
        {
            _starterInputs.move = Vector2.zero;
            _starterInputs.look = Vector2.zero;
            _starterInputs.jump = false;
            _starterInputs.sprint = false;
        }
    }

    /// <summary>
    /// Разблокировка ввода.
    /// </summary>
    public void UnlockControls()
    {
#if ENABLE_INPUT_SYSTEM
        if (_playerInput != null)
        {
            _playerInput.enabled = true;
        }
#endif
    }

    private IEnumerator UnlockAfterDelay(float time)
    {
        yield return new WaitForSeconds(time);
        UnlockControls();
    }
}
