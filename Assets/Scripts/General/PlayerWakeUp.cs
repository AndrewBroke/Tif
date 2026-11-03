using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using StarterAssets;

/// <summary>Death blackout, teleport and first-person wake-up with one continuous control lock.</summary>
[DisallowMultipleComponent]
public class PlayerWakeUp : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera pivot used by PlayerStumble / PlayerPushReaction, not the player root.")]
    [SerializeField] private Transform playerCameraRoot;
    [SerializeField] private CanvasGroup fadeImage;

    [Header("Playback")]
    [SerializeField] private bool playOnStart;
    [SerializeField] private bool onlyOnce = true;

    [Header("Death and respawn (world coordinates)")]
    [SerializeField] private bool respawnOnDeath = true;
    [SerializeField] private Vector3 respawnPosition;
    [SerializeField] private Vector3 respawnRotation;
    [SerializeField, Min(0f)] private float fadeToBlackTime = 0.8f;

    [Header("Stage durations (seconds)")]
    [SerializeField, Min(0f)] private float blackPause = 0.5f;
    [SerializeField, Min(0f)] private float blinkOpenTime = 0.6f;
    [SerializeField, Min(0f)] private float blinkCloseTime = 0.2f;
    [SerializeField, Min(0f)] private float revealTime = 1.2f;
    [SerializeField, Min(0f)] private float headLiftTime = 0.8f;
    [SerializeField, Min(0f)] private float lookPause = 0.5f;
    [SerializeField, Min(0f)] private float standUpTime = 1.8f;
    [SerializeField, Range(0f, 1f)] private float blinkVisibility = 0.45f;

    [Header("Camera offsets from the standing pose")]
    [SerializeField] private Vector3 lyingPosition = new Vector3(0f, -1.15f, 0f);
    [SerializeField] private Vector3 lyingRotation = new Vector3(-35f, 0f, -12f);
    [SerializeField] private Vector3 sittingPosition = new Vector3(0f, -0.65f, 0.1f);
    [SerializeField] private Vector3 sittingRotation = new Vector3(10f, 0f, -3f);

    [Header("Events")]
    public UnityEvent onWakeStart;
    public UnityEvent onWakeEnd;
    public UnityEvent onTeleported;

    public bool IsPlaying { get; private set; }

    private bool wasPlayed;
    private bool hasStandingPose;
    private Vector3 standingPosition;
    private Quaternion standingRotation;
    private Animator fadeAnimator;
    private bool restoreFadeAnimator;
    private bool initialBlocksRaycasts;
    private bool initialInteractable;
    private static readonly int ClearState = Animator.StringToHash("Base Layer.Clear");

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    // Can be called by an existing trigger, UnityEvent, or another script.
    public void Play()
    {
        if (!isActiveAndEnabled || IsPlaying || (onlyOnce && wasPlayed)) return;
        if (TryGetComponent(out PlayerHealth health) && health.IsDead) return;
        if (!ValidateReferences()) return;
        Begin(false, null);
    }

    // Called by PlayerHealth after the death fall. Death recovery can repeat regardless of Only Once.
    public bool PlayAfterDeath(PlayerHealth health)
    {
        if (!respawnOnDeath || !isActiveAndEnabled || IsPlaying || health == null
            || health.gameObject != gameObject || !ValidateReferences()) return false;
        Begin(true, health);
        return true;
    }

    private bool ValidateReferences()
    {
        if (playerCameraRoot == null || fadeImage == null || !fadeImage.gameObject.activeInHierarchy)
        {
            Debug.LogError("PlayerWakeUp: assign Player Camera Root and an active Fade Image CanvasGroup.", this);
            return false;
        }
        if (playerCameraRoot == transform || !playerCameraRoot.IsChildOf(transform))
        {
            Debug.LogError("PlayerWakeUp: add this component to the player and assign its child camera pivot.", this);
            return false;
        }
        return true;
    }

    private void Begin(bool afterDeath, PlayerHealth health)
    {
        hasStandingPose = false;
        if (!afterDeath)
            CaptureStandingPose();
        initialBlocksRaycasts = fadeImage.blocksRaycasts;
        initialInteractable = fadeImage.interactable;
        fadeAnimator = fadeImage.GetComponent<Animator>();
        restoreFadeAnimator = fadeAnimator != null && fadeAnimator.enabled;
        if (restoreFadeAnimator)
            fadeAnimator.enabled = false;

        IsPlaying = true;
        wasPlayed = true;
        PlayerControlLocks.Acquire(this);
        fadeImage.blocksRaycasts = true;
        fadeImage.interactable = false;
        if (!afterDeath)
        {
            fadeImage.alpha = 1f;
            ApplyPose(lyingPosition, Quaternion.Euler(lyingRotation));
        }
        StartCoroutine(Sequence(afterDeath, health));
    }

    private void CaptureStandingPose()
    {
        standingPosition = playerCameraRoot.localPosition;
        standingRotation = playerCameraRoot.localRotation;
        hasStandingPose = true;
    }

    private IEnumerator Sequence(bool afterDeath, PlayerHealth health)
    {
        bool completed = false;
        try
        {
            onWakeStart?.Invoke();
            if (!IsPlaying) yield break;
            if (afterDeath)
            {
                yield return FadeTo(1f, fadeToBlackTime);
                // Restore the standing camera BEFORE moving the root; everything is hidden now.
                health.ReviveForRespawn();
                TeleportPlayer();
                CaptureStandingPose();
                ApplyPose(lyingPosition, Quaternion.Euler(lyingRotation));
                onTeleported?.Invoke();
                if (!IsPlaying) yield break;
                // Let Cinemachine settle at the new position under the black screen.
                yield return null;
            }
            yield return new WaitForSeconds(blackPause);
            yield return FadeTo(1f - blinkVisibility, blinkOpenTime);
            yield return FadeTo(1f, blinkCloseTime);
            yield return FadeTo(0f, revealTime);
            yield return MoveCamera(lyingPosition, sittingPosition, lyingRotation, sittingRotation, headLiftTime);
            yield return new WaitForSeconds(lookPause);
            yield return MoveCamera(sittingPosition, Vector3.zero, sittingRotation, Vector3.zero, standUpTime);
            completed = true;
        }
        finally
        {
            Restore();
        }

        if (completed)
            onWakeEnd?.Invoke();
    }

    private void TeleportPlayer()
    {
        CharacterController controller = GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled) controller.enabled = false;
        try
        {
            // Prevent Cinemachine from damping all the way from the death location.
            var cameras = FindObjectsByType<Unity.Cinemachine.CinemachineCamera>();
            transform.SetPositionAndRotation(respawnPosition, Quaternion.Euler(0f, respawnRotation.y, 0f));
            if (TryGetComponent(out FirstPersonController movement))
                movement.SetLookRotation(Quaternion.Euler(respawnRotation.x, respawnRotation.y, 0f));
            else
                playerCameraRoot.localRotation = Quaternion.Euler(respawnRotation.x, 0f, 0f);
            foreach (var camera in cameras)
                if (camera.Follow != null && (camera.Follow == transform || camera.Follow.IsChildOf(transform)))
                    camera.PreviousStateIsValid = false;
            Physics.SyncTransforms();
        }
        finally
        {
            if (controllerWasEnabled && controller != null) controller.enabled = true;
        }
    }

    private IEnumerator FadeTo(float alpha, float duration)
    {
        float from = fadeImage.alpha;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            fadeImage.alpha = Mathf.Lerp(from, alpha, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        fadeImage.alpha = alpha;
    }

    private IEnumerator MoveCamera(Vector3 from, Vector3 to, Vector3 fromAngles, Vector3 toAngles, float duration)
    {
        Quaternion fromRotation = Quaternion.Euler(fromAngles);
        Quaternion toRotation = Quaternion.Euler(toAngles);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            ApplyPose(Vector3.Lerp(from, to, t), Quaternion.Slerp(fromRotation, toRotation, t));
            yield return null;
        }
        ApplyPose(to, toRotation);
    }

    private void ApplyPose(Vector3 offset, Quaternion rotation)
    {
        if (playerCameraRoot == null) return;
        playerCameraRoot.localPosition = standingPosition + offset;
        playerCameraRoot.localRotation = standingRotation * rotation;
    }

    private void Restore()
    {
        if (!IsPlaying) return;
        if (hasStandingPose)
            ApplyPose(Vector3.zero, Quaternion.identity);
        if (restoreFadeAnimator && fadeAnimator != null)
        {
            fadeAnimator.enabled = true;
            // Existing FadeImage controller must resume in Clear, not restart its opening fade.
            if (fadeAnimator.isActiveAndEnabled && fadeAnimator.runtimeAnimatorController != null
                && fadeAnimator.HasState(0, ClearState))
            {
                fadeAnimator.Play(ClearState, 0, 0f);
                fadeAnimator.Update(0f);
            }
        }
        if (fadeImage != null)
        {
            fadeImage.alpha = 0f;
            fadeImage.blocksRaycasts = initialBlocksRaycasts;
            fadeImage.interactable = initialInteractable;
        }
        IsPlaying = false;
        PlayerControlLocks.Release(this);
    }

    public void CancelWakeUp()
    {
        StopAllCoroutines();
        Restore();
    }

    private void OnDisable() => CancelWakeUp();
}
