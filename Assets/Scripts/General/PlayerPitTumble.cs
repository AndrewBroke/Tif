using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

/// <summary>First-person pit fall with an optional teleport halfway through the tumble.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(FirstPersonController))]
[DefaultExecutionOrder(9999)]
public class PlayerPitTumble : MonoBehaviour, ISerializationCallbackReceiver
{
    [Header("Test")]
    [SerializeField] private bool testOnP = true;

    [Header("Stage durations (seconds)")]
    [SerializeField, Min(0f)] private float fallDuration = 0.896f;
    [SerializeField, Min(0f)] private float tumbleDuration = 1.408f;
    [SerializeField, Min(0f)] private float faceForwardDuration = 0.384f;
    [SerializeField, Min(0f)] private float standUpDuration = 0.512f;

    // Migrate the previously serialized total once, preserving the existing pacing.
    [FormerlySerializedAs("duration")]
    [SerializeField, HideInInspector] private float legacyDuration = -1f;
    [SerializeField, HideInInspector] private float forwardFallDuration = -1f;
    [SerializeField, HideInInspector] private float sideFallDuration = -1f;

    [Header("Tumble")]
    [FormerlySerializedAs("forwardTurns")]
    [SerializeField, Range(1, 8)] private int sidewaysTurns = 3;
    [SerializeField] private bool reverseRotation;
    [SerializeField, Range(0f, 75f)] private float sidewaysTilt = 32f;
    [SerializeField, Range(0f, 85f)] private float forwardTilt = 55f;
    [SerializeField, Min(0f)] private float cameraDrop = 0.75f;

    [Header("Tumble randomness")]
    [SerializeField, Range(0f, 45f)] private float rotationRandomness = 12f;
    [SerializeField, Min(0f)] private float randomnessFrequency = 2.5f;

    [Header("Post-processing")]
    [SerializeField] private Volume fallRollVolume;

    [Header("Teleport")]
    [Tooltip("Teleport once, halfway through the tumble stage.")]
    [SerializeField] private bool teleportDuringTumble;
    [Tooltip("Destination of the player transform in world coordinates, not the camera position.")]
    [SerializeField] private Vector3 teleportPosition;
    [Tooltip("Player rotation around the world Y axis after teleporting, in degrees.")]
    [SerializeField] private float teleportYaw;

    private FirstPersonController movement;
    private Camera view;
    private CinemachineBrain brain;
    private bool brainWasEnabled;
    private bool movementWasEnabled;
    private Vector3 startingPosition;
    private Quaternion startingRotation;
    private float elapsed;
    private Vector3 rotationNoiseSeed;
    private bool hasTeleported;

    public bool IsTumbling { get; private set; }
    public float TotalDuration => Mathf.Max(0f, fallDuration) + Mathf.Max(0f, tumbleDuration)
        + Mathf.Max(0f, faceForwardDuration) + Mathf.Max(0f, standUpDuration);

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        if (legacyDuration >= 0f)
        {
            float total = Mathf.Max(0.1f, legacyDuration);
            fallDuration = total * 0.28f;
            tumbleDuration = total * 0.44f;
            faceForwardDuration = total * 0.12f;
            standUpDuration = total * 0.16f;
        }
        else if (forwardFallDuration >= 0f || sideFallDuration >= 0f)
        {
            fallDuration = Mathf.Max(0f, forwardFallDuration) + Mathf.Max(0f, sideFallDuration);
        }
        legacyDuration = -1f;
        forwardFallDuration = -1f;
        sideFallDuration = -1f;
    }

    private void Awake() => movement = GetComponent<FirstPersonController>();

    private void Update()
    {
        if (testOnP && Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            StartTumble();

        if (!IsTumbling) return;

        if (TryGetComponent(out PlayerHealth health) && health.IsDead)
        {
            CancelTumble();
            return;
        }

        elapsed += Time.deltaTime;
        float teleportTime = Mathf.Max(0f, fallDuration) + Mathf.Max(0f, tumbleDuration) * 0.5f;
        if (teleportDuringTumble && !hasTeleported && view != null && elapsed >= teleportTime)
            TeleportPlayer();
        if (elapsed >= TotalDuration)
            CancelTumble();
    }

    // Can later be called by a pit trigger instead of the test key.
    public void StartTumble()
    {
        if (!isActiveAndEnabled || IsTumbling || PlayerControlLocks.IsBlocked || Time.timeScale <= 0f)
            return;
        if (TryGetComponent(out PlayerHealth health) && health.IsDead) return;
        if (TryGetComponent(out PlayerPushReaction push) && push.IsReacting) return;

        view = Camera.main;
        if (view == null || movement == null || !movement.enabled) return;

        startingPosition = view.transform.position;
        startingRotation = view.transform.rotation;
        brain = view.GetComponent<CinemachineBrain>();
        brainWasEnabled = brain != null && brain.enabled;
        movementWasEnabled = movement.enabled;
        elapsed = 0f;
        hasTeleported = false;
        rotationNoiseSeed = new Vector3(Random.Range(0f, 1000f),
            Random.Range(0f, 1000f), Random.Range(0f, 1000f));
        IsTumbling = true;
        if (fallRollVolume != null) fallRollVolume.weight = 1f;
        PlayerControlLocks.Acquire(this);
        movement.enabled = false;
        if (brain != null) brain.enabled = false;
    }

    private void LateUpdate()
    {
        if (!IsTumbling) return;
        if (view == null)
        {
            CancelTumble();
            return;
        }

        float fallEnd = Mathf.Max(0f, fallDuration);
        float tumbleEnd = fallEnd + Mathf.Max(0f, tumbleDuration);
        float faceForwardEnd = tumbleEnd + Mathf.Max(0f, faceForwardDuration);
        float fullTurns = 360f * Mathf.Clamp(sidewaysTurns, 1, 8);
        // Share a non-zero angular speed at the join instead of easing to a stop twice.
        float joinSpeed = Mathf.Min(180f / Mathf.Max(0.001f, fallEnd),
            fullTurns / Mathf.Max(0.001f, tumbleDuration));
        float pitch;
        float roll;
        float yaw = 0f;
        float drop;

        if (elapsed < fallEnd)
        {
            // Tip forwards and sideways together, carrying rotation into the tumble.
            float s = Phase(elapsed, 0f, fallEnd);
            pitch = 20f * s + Mathf.Max(0f, forwardTilt - 10f) * Mathf.Sin(Mathf.PI * s);
            float rollProgress = Mathf.InverseLerp(0f, fallEnd, elapsed);
            roll = 90f * RotationProgress(rollProgress, 0f, joinSpeed * fallEnd / 90f);
            drop = cameraDrop * s;
        }
        else if (elapsed < tumbleEnd)
        {
            float s = Phase(elapsed, fallEnd, tumbleEnd);
            // Unwrapped angles preserve every complete sideways turn.
            pitch = 20f + Mathf.Sin(s * Mathf.PI * 2f) * sidewaysTilt * 0.25f;
            float rollProgress = Mathf.InverseLerp(fallEnd, tumbleEnd, elapsed);
            roll = 90f + fullTurns * RotationProgress(rollProgress,
                joinSpeed * Mathf.Max(0f, tumbleDuration) / fullTurns, 0f);
            // Fade both the offsets and their velocity at the stage boundaries.
            float noiseEnvelope = Mathf.Sin(rollProgress * Mathf.PI);
            noiseEnvelope *= noiseEnvelope;
            float noiseTime = (elapsed - fallEnd) * Mathf.Max(0f, randomnessFrequency);
            float noiseStrength = rotationRandomness * noiseEnvelope;
            pitch += RotationNoise(rotationNoiseSeed.x, noiseTime) * noiseStrength;
            yaw = RotationNoise(rotationNoiseSeed.y, noiseTime) * noiseStrength;
            roll += RotationNoise(rotationNoiseSeed.z, noiseTime) * noiseStrength;
            drop = cameraDrop;
        }
        else if (elapsed < faceForwardEnd)
        {
            // Turn face-forward while still down, then stand up separately.
            float s = Phase(elapsed, tumbleEnd, faceForwardEnd);
            pitch = Mathf.Lerp(20f, forwardTilt, s);
            roll = fullTurns + 90f * (1f - s);
            drop = cameraDrop;
        }
        else
        {
            float s = Phase(elapsed, faceForwardEnd, TotalDuration);
            pitch = forwardTilt * (1f - s);
            roll = fullTurns;
            drop = cameraDrop * (1f - s);
        }

        view.transform.SetPositionAndRotation(startingPosition + Vector3.down * drop,
            startingRotation * Quaternion.Euler(pitch, yaw, reverseRotation ? -roll : roll));
    }

    private static float RotationNoise(float seed, float time)
    {
        return (Mathf.PerlinNoise(seed, time) - 0.5f) * 2f;
    }

    private void TeleportPlayer()
    {
        Vector3 playerPosition = transform.position;
        Vector3 cameraPosition = view.transform.position;
        Quaternion cameraRotation = view.transform.rotation;
        Quaternion destinationPlayerRotation = Quaternion.Euler(0f, teleportYaw, 0f);
        Quaternion rotationDelta = destinationPlayerRotation
            * Quaternion.Inverse(transform.rotation);
        CharacterController controller = GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled) controller.enabled = false;
        try
        {
            transform.SetPositionAndRotation(teleportPosition, destinationPlayerRotation);
            // Keep the current animation pose and its final camera position at the destination.
            startingPosition = teleportPosition + rotationDelta * (startingPosition - playerPosition);
            startingRotation = rotationDelta * startingRotation;
            view.transform.SetPositionAndRotation(
                teleportPosition + rotationDelta * (cameraPosition - playerPosition),
                rotationDelta * cameraRotation);
            Physics.SyncTransforms();
            hasTeleported = true;
        }
        finally
        {
            if (controllerWasEnabled && controller != null) controller.enabled = true;
        }
    }

    private static float Phase(float time, float start, float end)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));
    }

    private static float RotationProgress(float t, float startSpeed, float endSpeed)
    {
        // Cubic Hermite: preserve the angle and speed on both sides of a stage boundary.
        float squared = t * t;
        float cubed = squared * t;
        return 3f * squared - 2f * cubed
            + (cubed - 2f * squared + t) * startSpeed
            + (cubed - squared) * endSpeed;
    }

    public void CancelTumble()
    {
        if (!IsTumbling) return;

        IsTumbling = false;
        if (fallRollVolume != null) fallRollVolume.weight = 0f;
        if (view != null)
            view.transform.SetPositionAndRotation(startingPosition, startingRotation);
        if (hasTeleported)
        {
            // Resume Cinemachine at the destination without damping from the old location.
            foreach (var camera in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
                if (camera.Follow != null && (camera.Follow == transform || camera.Follow.IsChildOf(transform)))
                    camera.PreviousStateIsValid = false;
        }
        if (brain != null) brain.enabled = brainWasEnabled;
        if (movement != null) movement.enabled = movementWasEnabled;
        PlayerControlLocks.Release(this);
    }

    private void OnDisable() => CancelTumble();
}
