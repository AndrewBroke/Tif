using System.Collections;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public class PlayerHealth : MonoBehaviour
{
    public const int MaxHealth = 3;

    private static readonly int HitState = Animator.StringToHash("Base Layer.hit");
    private static readonly int HitTrigger = Animator.StringToHash("Hit");

    [Header("Health")]
    [Tooltip("Editable during Play Mode: 1-3 previews health effects; 0 triggers death.")]
    [SerializeField, Range(0, MaxHealth)] private int currentHealth = MaxHealth;

    [Tooltip("Animator on the Hit Volume post-processing object.")]
    [SerializeField] private Animator hitVolumeAnimator;
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    [Header("HP Volume")]
    [SerializeField] private Volume hpVolume;
    [SerializeField, Range(0f, 1f)] private float twoHpWeight = 0.4f;
    [SerializeField, Range(0f, 1f)] private float oneHpWeight = 0.8f;
    [Tooltip("Weight change per second when health changes.")]
    [SerializeField, Min(0.01f)] private float volumeTransitionSpeed = 2f;

    [Header("HP Volume pulse")]
    [SerializeField] private bool pulseEnabled = true;
    [Tooltip("Weight oscillates above and below the base weight by this amount.")]
    [SerializeField, Range(0f, 0.4f)] private float twoHpPulseAmplitude = 0.025f;
    [SerializeField, Min(0f)] private float twoHpPulseFrequency = 0.9f;
    [SerializeField, Range(0f, 0.4f)] private float oneHpPulseAmplitude = 0.06f;
    [Tooltip("Pulse frequency in cycles per second.")]
    [SerializeField, Min(0f)] private float oneHpPulseFrequency = 1.65f;

    [Header("1 HP camera sway")]
    [SerializeField] private bool lowHealthNoiseEnabled = true;
    [SerializeField] private CinemachineBasicMultiChannelPerlin lowHealthNoise;
    [Tooltip("Added to the camera's original Amplitude Gain only at 1 HP.")]
    [SerializeField, Min(0f)] private float lowHealthNoiseAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float lowHealthNoiseFrequency = 0.35f;
    [Tooltip("Blend change per second when entering or leaving 1 HP.")]
    [SerializeField, Min(0.01f)] private float noiseTransitionSpeed = 2f;

    [Header("Death fall")]
    [Tooltip("Resolved from FirstPersonController automatically when empty.")]
    [SerializeField] private Transform deathCameraRoot;
    [SerializeField, Min(0.1f)] private float deathFallDuration = 1.8f;
    [SerializeField, Range(-85f, 85f)] private float deathRoll = 75f;
    [SerializeField, Min(0.1f)] private float groundClearance = 0.25f;
    [Tooltip("Invoked after the fall, with the cursor visible and gameplay still blocked.")]
    [SerializeField] private UnityEvent onDeathAnimationFinished = new UnityEvent();

    private float hpBaseWeight;
    private float pulsePhase;
    private float noiseBlend;
    private float originalNoiseAmplitude;
    private float originalNoiseFrequency;
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

    private void OnEnable()
    {
        if (lowHealthNoise != null)
        {
            originalNoiseAmplitude = lowHealthNoise.AmplitudeGain;
            originalNoiseFrequency = lowHealthNoise.FrequencyGain;
        }
        ResetHealthEffects();
    }

    private void OnValidate()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
    }

    private void Update()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
        // Death and respawn keep ownership of health until their sequence finishes.
        if (deathAnimationStarted)
            currentHealth = 0;
        else if (IsDead)
            HandleDeath();

        UpdateHealthEffects(Time.deltaTime);
    }

    private float HealthVolumeWeight => currentHealth >= MaxHealth ? 0f
        : currentHealth == 2 ? twoHpWeight : oneHpWeight;

    private void UpdateHealthEffects(float deltaTime)
    {
        hpBaseWeight = Mathf.MoveTowards(hpBaseWeight, HealthVolumeWeight,
            Mathf.Max(0.01f, volumeTransitionSpeed) * deltaTime);
        float amplitude = 0f;
        if (pulseEnabled && !IsDead && currentHealth < MaxHealth)
        {
            float frequency = currentHealth == 1 ? oneHpPulseFrequency : twoHpPulseFrequency;
            amplitude = currentHealth == 1 ? oneHpPulseAmplitude : twoHpPulseAmplitude;
            pulsePhase = Mathf.Repeat(pulsePhase + Mathf.Max(0f, frequency) * deltaTime, 1f);
        }
        if (hpVolume != null)
            hpVolume.weight = Mathf.Clamp01(hpBaseWeight + Mathf.Sin(pulsePhase * Mathf.PI * 2f) * amplitude);

        float targetNoise = lowHealthNoiseEnabled && currentHealth == 1 && !deathAnimationStarted ? 1f : 0f;
        noiseBlend = Mathf.MoveTowards(noiseBlend, targetNoise,
            Mathf.Max(0.01f, noiseTransitionSpeed) * deltaTime);
        if (lowHealthNoise != null)
        {
            lowHealthNoise.AmplitudeGain = originalNoiseAmplitude + Mathf.Max(0f, lowHealthNoiseAmplitude) * noiseBlend;
            lowHealthNoise.FrequencyGain = Mathf.Lerp(originalNoiseFrequency,
                Mathf.Max(0f, lowHealthNoiseFrequency), noiseBlend);
        }
    }

    private void ResetHealthEffects()
    {
        hpBaseWeight = HealthVolumeWeight;
        pulsePhase = 0f;
        ResetLowHealthNoise();
        UpdateHealthEffects(0f);
    }

    private void ResetLowHealthNoise()
    {
        noiseBlend = 0f;
        if (lowHealthNoise == null) return;
        lowHealthNoise.AmplitudeGain = originalNoiseAmplitude;
        lowHealthNoise.FrequencyGain = originalNoiseFrequency;
    }

    public void TakeDamage()
    {
        TakeDamage(1);
    }

    public void RestoreFullHealth()
    {
        if (IsDead || deathAnimationStarted)
            return;

        ApplyFullHealth();
    }

    private void ApplyFullHealth()
    {
        currentHealth = MaxHealth;
        ResetHealthEffects();
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDead)
            return;
        if (TryGetComponent(out PlayerWakeUp wakeUp) && wakeUp.IsPlaying)
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
            HandleDeath();
    }

    private void HandleDeath()
    {
        if (deathAnimationStarted) return;
        PlayDeathAnimation();
        onDeath.Invoke();
    }

    public virtual void PlayDeathAnimation()
    {
        if (!isActiveAndEnabled || deathAnimationStarted)
            return;

        deathAnimationStarted = true;
        ResetLowHealthNoise();
        PlayerControlLocks.Acquire(this);

        // A lethal bite arrives while the grab has disabled movement and Cinemachine.
        // Restore their pre-grab states before recording what death must later restore.
        foreach (var chaser in FindObjectsByType<ProximityChaser>())
            chaser.ReleaseGrabForDeath(this);

        // Stop reactions before capturing the starting pose. They use the same camera target.
        if (TryGetComponent(out PlayerPushReaction push))
            push.StopReaction();
        if (TryGetComponent(out PlayerStumble stumble))
            stumble.CancelFall();
        if (TryGetComponent(out PlayerWakeUp wakeUp))
            wakeUp.CancelWakeUp();

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
        if (TryGetComponent(out PlayerWakeUp wakeUp) && wakeUp.PlayAfterDeath(this))
            return;
        PlayerControlLocks.Acquire(this, showCursor: true);
        onDeathAnimationFinished.Invoke();
    }

    // Called only while PlayerWakeUp owns the control lock and the screen is black.
    public void ReviveForRespawn()
    {
        if (!deathAnimationStarted) return;
        deathPoseActive = false;
        if (view != null) view.SetPositionAndRotation(startPosition, startRotation);
        if (deathCameraRoot != null && view != null)
            deathCameraRoot.SetPositionAndRotation(rootStartPosition, rootStartRotation);
        if (movement != null) movement.enabled = movementWasEnabled;
        if (brain != null) brain.enabled = brainWasEnabled;
        deathAnimationStarted = false;
        ApplyFullHealth();
        PlayerControlLocks.Release(this);
    }

    private void OnDisable()
    {
        if (hpVolume != null) hpVolume.weight = 0f;
        ResetLowHealthNoise();
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
