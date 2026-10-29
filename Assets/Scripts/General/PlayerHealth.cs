using System.Collections;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public class PlayerHealth : MonoBehaviour
{
    public const int MaxHealth = 3;

    private static readonly int HitState = Animator.StringToHash("Base Layer.hit");
    private static readonly int HitTrigger = Animator.StringToHash("Hit");

    [Tooltip("Animator on the Hit Volume post-processing object.")]
    [SerializeField] private Animator hitVolumeAnimator;
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    [Header("Death fall")]
    [Tooltip("Resolved from FirstPersonController automatically when empty.")]
    [SerializeField] private Transform deathCameraRoot;
    [SerializeField, Min(0.1f)] private float deathFallDuration = 1.8f;
    [SerializeField, Range(-85f, 85f)] private float deathRoll = 75f;
    [SerializeField, Min(0.1f)] private float groundClearance = 0.25f;
    [Tooltip("Invoked after the fall, with the cursor visible and gameplay still blocked.")]
    [SerializeField] private UnityEvent onDeathAnimationFinished = new UnityEvent();

    private int currentHealth = MaxHealth;
    private bool deathAnimationStarted;
    private bool deathPoseActive;
    private FirstPersonController movement;
    private bool movementWasEnabled;
    private Transform view;
    private CinemachineBrain brain;
    private bool brainWasEnabled;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 rootStartPosition;
    private Quaternion rootStartRotation;
    private Vector3 deathPosition;
    private Quaternion deathRotation;

    public int CurrentHealth => currentHealth;
    public bool IsDead => currentHealth == 0;

    private void Awake()
    {
        movement = GetComponent<FirstPersonController>();
        if (deathCameraRoot == null && movement != null && movement.CinemachineCameraTarget != null)
            deathCameraRoot = movement.CinemachineCameraTarget.transform;
        if (hitVolumeAnimator == null)
            TryGetComponent(out hitVolumeAnimator);

        if (hitVolumeAnimator == null)
            Debug.LogWarning("Assign the Hit Volume Animator to PlayerHealth to play the damage effect.", this);
    }

    public void TakeDamage()
    {
        TakeDamage(1);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDead)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);

        if (hitVolumeAnimator != null && hitVolumeAnimator.isActiveAndEnabled)
        {
            // The controller only has an idle -> hit trigger transition.
            // Play the state directly so another hit restarts the effect immediately.
            hitVolumeAnimator.ResetTrigger(HitTrigger);
            hitVolumeAnimator.Play(HitState, 0, 0f);
        }

        if (IsDead)
        {
            PlayDeathAnimation();
            onDeath.Invoke();
        }
    }

    public virtual void PlayDeathAnimation()
    {
        if (!isActiveAndEnabled || deathAnimationStarted)
            return;

        deathAnimationStarted = true;
        PlayerControlLocks.Acquire(this);

        // Stop reactions before capturing the starting pose. They use the same camera target.
        if (TryGetComponent(out PlayerPushReaction push))
            push.StopReaction();
        if (TryGetComponent(out PlayerStumble stumble))
            stumble.CancelFall();

        if (movement != null)
        {
            movementWasEnabled = movement.enabled;
            movement.enabled = false;
        }

        Camera mainCamera = Camera.main;
        view = mainCamera != null ? mainCamera.transform : deathCameraRoot;
        if (view == null)
        {
            Debug.LogWarning("PlayerHealth: no death camera found.", this);
            FinishDeathAnimation();
            return;
        }

        // Drive the rendered camera too: Cinemachine follow may keep the horizon upright.
        brain = view.GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            brainWasEnabled = brain.enabled;
            brain.enabled = false;
        }
        startPosition = view.position;
        startRotation = view.rotation;
        if (deathCameraRoot != null)
        {
            rootStartPosition = deathCameraRoot.position;
            rootStartRotation = deathCameraRoot.rotation;
        }

        float drop = Mathf.Max(0f, startPosition.y - transform.position.y - groundClearance);
        // Ignore our own colliders and triggers; distances are in world meters, regardless of player scale.
        foreach (RaycastHit hit in Physics.RaycastAll(startPosition, Vector3.down,
                     drop + groundClearance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform))
                drop = Mathf.Min(drop, Mathf.Max(0f, hit.distance - groundClearance));
        }

        deathPosition = startPosition;
        deathRotation = startRotation;
        deathPoseActive = true;
        StartCoroutine(DeathFall(drop));
    }

    private IEnumerator DeathFall(float drop)
    {
        float elapsed = 0f;
        while (elapsed < deathFallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, deathFallDuration));
            float height;
            float roll;
            float pitch;
            if (t < 0.2f)
            {
                float s = Mathf.SmoothStep(0f, 1f, t / 0.2f);
                height = s * 0.14f;
                roll = s * 0.06f;
                pitch = s * 7f;
            }
            else if (t < 0.3f)
            {
                height = 0.14f;
                roll = 0.06f;
                pitch = 7f;
            }
            else if (t < 0.78f)
            {
                float s = (t - 0.3f) / 0.48f;
                s *= s; // Accelerate into the ground.
                height = Mathf.Lerp(0.14f, 1f, s);
                roll = Mathf.Lerp(0.06f, 1f, s);
                pitch = Mathf.Lerp(7f, 12f, s);
            }
            else
            {
                float s = (t - 0.78f) / 0.22f;
                float recoil = Mathf.Sin(s * Mathf.PI) * (1f - s);
                height = 1f - recoil * 0.025f;
                roll = 1f - recoil * 0.04f;
                pitch = Mathf.Lerp(12f, 6f, Mathf.SmoothStep(0f, 1f, s));
            }
            deathPosition = startPosition + Vector3.down * (drop * height);
            deathRotation = startRotation * Quaternion.Euler(pitch, 0f, deathRoll * roll);
            ApplyDeathPose();
            yield return null;
        }

        FinishDeathAnimation();
    }

    private void LateUpdate()
    {
        if (deathPoseActive)
            ApplyDeathPose();
    }

    private void ApplyDeathPose()
    {
        if (view == null) return;
        if (brain != null) brain.enabled = false;
        if (deathCameraRoot != null && deathCameraRoot != view)
        {
            Quaternion delta = deathRotation * Quaternion.Inverse(startRotation);
            deathCameraRoot.SetPositionAndRotation(rootStartPosition + deathPosition - startPosition,
                delta * rootStartRotation);
        }
        view.SetPositionAndRotation(deathPosition, deathRotation);
    }

    private void FinishDeathAnimation()
    {
        PlayerControlLocks.Acquire(this, showCursor: true);
        onDeathAnimationFinished.Invoke();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        deathPoseActive = false;
        if (deathAnimationStarted)
        {
            if (movement != null) movement.enabled = movementWasEnabled;
            if (brain != null) brain.enabled = brainWasEnabled;
        }
        PlayerControlLocks.Release(this);
    }
}
