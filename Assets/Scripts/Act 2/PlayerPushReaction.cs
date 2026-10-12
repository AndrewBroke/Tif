using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class PlayerPushReaction : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CharacterController characterController;
    [Tooltip("Empty child object between the player and the first-person camera. Do not assign the player root here.")]
    [SerializeField] private Transform cameraEffectPivot;
    [SerializeField] private Volume volume;

    [Header("Fall (seconds / meters)")]
    [SerializeField, Min(0.1f)] private float reactionDuration = 3.7f;
    [SerializeField, Min(0f)] private float pushDistance = 1.5f;
    [SerializeField, Min(0f)] private float cameraDrop = 1.15f;

    [Header("Camera angles (degrees)")]
    [Tooltip("Negative pitch = falling backwards and looking upwards.")]
    [SerializeField] private float fallPitchAngle = -60f;
    [SerializeField] private float fallRollAngle = 8f;

    // Curve time is normalized: 0 = beginning, 1 = end.
    [Header("Push: movement only at the beginning")]
    [SerializeField] private AnimationCurve impactPushCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.04f, 0.12f),
        new Keyframe(0.12f, 1f),
        new Keyframe(1f, 1f)
    );

    [Header("Fall: drop the camera, stay down, stand up")]
    [SerializeField] private AnimationCurve heightCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.12f, 0.08f),
        new Keyframe(0.32f, 1f),
        new Keyframe(0.58f, 1f),
        new Keyframe(0.68f, 0.92f),
        new Keyframe(0.88f, 0.12f),
        new Keyframe(1f, 0f)
    );

    [Header("Tilt: falling backwards")]
    [SerializeField] private AnimationCurve fallPitchCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.12f, 0.12f),
        new Keyframe(0.32f, 1f),
        new Keyframe(0.58f, 1f),
        new Keyframe(0.72f, 0.75f),
        new Keyframe(1f, 0f)
    );

    [SerializeField] private AnimationCurve fallRollCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.18f, 0.15f),
        new Keyframe(0.35f, 1f),
        new Keyframe(0.58f, 0.85f),
        new Keyframe(0.85f, 0.1f),
        new Keyframe(1f, 0f)
    );

    [Header("Volume: 1 = normal weight, 0 = zero weight")]
    [SerializeField] private AnimationCurve fallVolumeCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.15f, 0.75f),
        new Keyframe(0.33f, 0f),
        new Keyframe(0.58f, 0.25f),
        new Keyframe(0.85f, 0.85f),
        new Keyframe(1f, 1f)
    );

    private Coroutine pushCoroutine;
    private Vector3 startingCameraLocalPosition;
    private Quaternion startingCameraLocalRotation;
    private float startingVolumeWeight;

    public bool IsReacting { get; private set; }

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (cameraEffectPivot != null)
        {
            startingCameraLocalPosition = cameraEffectPivot.localPosition;
            startingCameraLocalRotation = cameraEffectPivot.localRotation;
        }

        if (volume != null)
            startingVolumeWeight = volume.weight;
    }

    // Call from UnityEvent or another script.
    public void Push()
    {
        if (characterController == null || cameraEffectPivot == null)
        {
            Debug.LogError("PlayerPushReaction: assign CharacterController and Camera Effect Pivot.", this);
            return;
        }

        if (pushCoroutine != null)
        {
            StopCoroutine(pushCoroutine);
            RestoreVisuals();
        }

        IsReacting = true;
        pushCoroutine = StartCoroutine(PushRoutine());
    }

    private IEnumerator PushRoutine()
    {
        float elapsed = 0f;
        float previousPush = 0f;

        // Keep push direction fixed even if mouse look changes player yaw.
        Vector3 pushDirection = -transform.forward;
        pushDirection.y = 0f;
        pushDirection.Normalize();

        while (elapsed < reactionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / reactionDuration);

            // 1. Brief backward impulse (in the first ~0.45 seconds).
            float cumulativePush = Mathf.Clamp01(impactPushCurve.Evaluate(t)) * pushDistance;
            float framePush = cumulativePush - previousPush;
            if (Mathf.Abs(framePush) > 0.0001f)
                characterController.Move(pushDirection * framePush);
            previousPush = cumulativePush;

            // 2. Lower first-person view towards the ground.
            float drop01 = Mathf.Clamp01(heightCurve.Evaluate(t));
            cameraEffectPivot.localPosition = startingCameraLocalPosition
                + Vector3.down * (cameraDrop * drop01);

            // 3. Rotate ONLY camera visual pivot, not CharacterController / player root.
            float pitch = fallPitchCurve.Evaluate(t) * fallPitchAngle;
            float roll = fallRollCurve.Evaluate(t) * fallRollAngle;
            cameraEffectPivot.localRotation = startingCameraLocalRotation
                * Quaternion.Euler(pitch, 0f, roll);

            // 4. Reduce the current Volume's initial weight to zero on impact.
            if (volume != null)
                volume.weight = Mathf.Clamp01(fallVolumeCurve.Evaluate(t));

            yield return null;
        }

        RestoreVisuals();
        IsReacting = false;
        pushCoroutine = null;
    }

    private void RestoreVisuals()
    {
        if (cameraEffectPivot != null)
        {
            cameraEffectPivot.localPosition = startingCameraLocalPosition;
            cameraEffectPivot.localRotation = startingCameraLocalRotation;
        }

        if (volume != null)
            volume.weight = startingVolumeWeight;
    }

    private void OnDisable()
    {
        if (pushCoroutine != null)
        {
            StopCoroutine(pushCoroutine);
            pushCoroutine = null;
        }

        RestoreVisuals();
        IsReacting = false;
    }
}
