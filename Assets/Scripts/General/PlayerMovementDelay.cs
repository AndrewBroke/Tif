using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;

/// <summary>Applies shared locks to Starter Assets and owns the scene start delay.</summary>
[DefaultExecutionOrder(-300)]
public class PlayerMovementDelay : MonoBehaviour
{
    [SerializeField] private float lockDuration = 3f;
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

    private void OnEnable()
    {
        PlayerControlLocks.Changed -= ApplyState;
        PlayerControlLocks.Changed += ApplyState;
        ApplyState();
    }

    private void Start()
    {
        if (!lockOnStart) return;
        LockControls();
        if (lockDuration > 0f)
            StartCoroutine(UnlockAfterDelay(lockDuration));
    }

    // Compatibility pair: owns ONLY this component's lock.
    public void LockControls() => PlayerControlLocks.Acquire(this);
    public void UnlockControls() => PlayerControlLocks.Release(this);

    private void ApplyState()
    {
        bool blocked = PlayerControlLocks.IsBlocked;
#if ENABLE_INPUT_SYSTEM
        if (_playerInput != null && _playerInput.enabled == blocked)
            _playerInput.enabled = !blocked;
#endif
        if (_starterInputs != null)
        {
            _starterInputs.cursorLocked = !PlayerControlLocks.WantsCursor;
            _starterInputs.cursorInputForLook = !blocked;
            if (blocked)
            {
                _starterInputs.move = Vector2.zero;
                _starterInputs.look = Vector2.zero;
                _starterInputs.jump = false;
                _starterInputs.sprint = false;
            }
        }
        PlayerControlLocks.ApplyCursor();
    }

    private void Update() => ApplyState();
    private void LateUpdate() => PlayerControlLocks.ApplyCursor();

    private void OnDisable()
    {
        StopAllCoroutines();
        UnlockControls();
        // Keep applying other owners' changes even if only this component is disabled.
        ApplyState();
    }

    private void OnDestroy()
    {
        PlayerControlLocks.Changed -= ApplyState;
        UnlockControls();
        ApplyState();
    }

    private IEnumerator UnlockAfterDelay(float time)
    {
        yield return new WaitForSeconds(time);
        UnlockControls();
    }
}
