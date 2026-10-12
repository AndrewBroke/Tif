using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

/// <summary>
/// First-person cutscene with a log, a camera lift, blackout and an Animator trigger.
/// Animator's transition to the final pose happens while the screen is black.
/// </summary>
public class MedicFinishCutscene : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform logPivot;
    [Tooltip("The existing PlayerCameraRoot used by Cinemachine.")]
    [SerializeField] private Transform playerCameraRoot;
    [Tooltip("Optional. Use ONLY if Cinemachine Look At is assigned to this separate target.")]
    [SerializeField] private Transform cameraLookTarget;
    [SerializeField] private CanvasGroup blackScreen;
    [SerializeField] private Animator medicAnimator;
    [SerializeField] private string deadTrigger = "dead";
    [SerializeField] private bool hideLogAfterImpact = true;

    [Header("Stage durations (seconds)")]
    [Min(0f)] [SerializeField] private float raiseTime = 1.25f;
    [Min(0f)] [SerializeField] private float raisedPause = 0.20f;
    [Min(0f)] [SerializeField] private float lowerTime = 0.35f;
    [Min(0f)] [SerializeField] private float impactDelayAfterBlack = 0.05f;
    [Tooltip("Wait for the Animator transition / death clip before showing the scene.")]
    [Min(0f)] [SerializeField] private float blackoutTime = 1.10f;
    [Min(0f)] [SerializeField] private float blinkOpenTime = 0.16f;
    [Min(0f)] [SerializeField] private float blinkCloseTime = 0.13f;
    [Min(0f)] [SerializeField] private float revealTime = 0.70f;
    [Range(0f, 1f)] [SerializeField] private float blinkVisibility = 0.35f;

    [Header("Log motion: local offsets from its starting pose")]
    [SerializeField] private Vector3 raisedLogPosition = new Vector3(0f, 0.20f, -0.06f);
    [SerializeField] private Vector3 raisedLogRotation = new Vector3(-60f, 0f, -8f);
    [SerializeField] private Vector3 loweredLogPosition = new Vector3(0f, -0.12f, 0.12f);
    [SerializeField] private Vector3 loweredLogRotation = new Vector3(35f, 0f, 5f);

    [Header("Camera motion: local offsets from its starting pose")]
    [SerializeField] private Vector3 raisedCameraPosition = new Vector3(0f, 0.12f, 0f);
    [SerializeField] private Vector3 raisedCameraRotation = new Vector3(-10f, 0f, 0f);
    [SerializeField] private Vector3 loweredCameraPosition = new Vector3(0f, -0.05f, 0f);
    [SerializeField] private Vector3 loweredCameraRotation = new Vector3(6f, 0f, 0f);
    [Tooltip("Optional Look At target: move it upward to tilt the view up, if Cinemachine ignores target rotation.")]
    [SerializeField] private Vector3 raisedLookTargetPosition = new Vector3(0f, 0.55f, 0f);
    [SerializeField] private Vector3 loweredLookTargetPosition = new Vector3(0f, -0.12f, 0f);

    [Header("Movement and fading curves")]
    [SerializeField] private AnimationCurve raiseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve lowerCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Range(0f, 1f)] [SerializeField] private float blackoutStartDuringLower = 0.15f;
    [Min(0f)] [SerializeField] private float cameraReturnTime = 0.35f;

    [Header("Audio: use 2D AudioSources")]
    [SerializeField] private AudioSource impactSource;
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private AudioSource ringingSource;
    [SerializeField] private AudioClip ringingSound;
    [Range(0f, 1f)] [SerializeField] private float ringingVolume = 0.35f;

    [Header("Events (e.g. disable and enable player movement)")]
    public UnityEvent onBegin;
    public UnityEvent onImpact;
    public UnityEvent onFinished;

    public bool IsPlaying { get; private set; }

    private Vector3 initialLogPosition;
    private Quaternion initialLogRotation;
    private bool hasInitialLogPose;

    private struct Pose
    {
        public Vector3 logPosition;
        public Quaternion logRotation;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public Vector3 lookPosition;
    }

    private void Awake()
    {
        if (logPivot != null)
        {
            initialLogPosition = logPivot.localPosition;
            initialLogRotation = logPivot.localRotation;
            hasInitialLogPose = true;
        }

        if (blackScreen != null)
        {
            blackScreen.alpha = 0f;
            blackScreen.blocksRaycasts = false;
            blackScreen.interactable = false;
        }
    }

    void Update()
    {
        if(Keyboard.current.pKey.wasPressedThisFrame)
        {
            Play();
        }
    }

    // Can be called by a UnityEvent in the existing interaction system.
    public void Play()
    {
        if (IsPlaying) return;

        if (logPivot == null || playerCameraRoot == null || blackScreen == null || medicAnimator == null)
        {
            Debug.LogError("MedicFinishCutscene: assign Log Pivot, Player Camera Root, Black Screen and Medic Animator.", this);
            return;
        }

        // Выключаем аниматор камеры и бревна
        logPivot.transform.parent.GetComponent<Animator>().enabled = false;

        IsPlaying = true;
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        if (!hasInitialLogPose)
        {
            initialLogPosition = logPivot.localPosition;
            initialLogRotation = logPivot.localRotation;
            hasInitialLogPose = true;
        }

        logPivot.localPosition = initialLogPosition;
        logPivot.localRotation = initialLogRotation;
        logPivot.gameObject.SetActive(true);
        blackScreen.gameObject.SetActive(true);
        blackScreen.alpha = 0f;

        Pose start = CapturePose();
        Pose raised = OffsetPose(start, raisedLogPosition, raisedLogRotation,
            raisedCameraPosition, raisedCameraRotation, raisedLookTargetPosition);
        Pose lowered = OffsetPose(start, loweredLogPosition, loweredLogRotation,
            loweredCameraPosition, loweredCameraRotation, loweredLookTargetPosition);

        // Disable the player's movement/look controller through onBegin if it fights the camera rig.
        onBegin?.Invoke();

        // 1. Slow lift: move both the log and camera.
        yield return AnimatePose(start, raised, raiseTime, raiseCurve, false);

        // 2. Hold at the top.
        if (raisedPause > 0f)
            yield return new WaitForSecondsRealtime(raisedPause);

        // 3. Lower the log and the camera, fading to black BEFORE the hidden moment.
        yield return AnimatePose(raised, lowered, lowerTime, lowerCurve, true);
        blackScreen.alpha = 1f;

        if (impactDelayAfterBlack > 0f)
            yield return new WaitForSecondsRealtime(impactDelayAfterBlack);

        // 4. Fully black: cue audio and trigger the medic Animator.
        if (impactSource != null && impactSound != null)
            impactSource.PlayOneShot(impactSound);

        medicAnimator.ResetTrigger(deadTrigger);
        medicAnimator.SetTrigger(deadTrigger);

        if (ringingSource != null && ringingSound != null)
        {
            ringingSource.Stop();
            ringingSource.clip = ringingSound;
            ringingSource.loop = true;
            ringingSource.volume = ringingVolume;
            ringingSource.Play();
        }

        if (hideLogAfterImpact)
            logPivot.gameObject.SetActive(false);

        onImpact?.Invoke();

        // Return the camera to its original orientation invisibly during blackout.
        Pose returnPose = lowered;
        returnPose.cameraPosition = start.cameraPosition;
        returnPose.cameraRotation = start.cameraRotation;
        returnPose.lookPosition = start.lookPosition;

        float returnDuration = Mathf.Min(cameraReturnTime, blackoutTime);
        yield return AnimatePose(lowered, returnPose, returnDuration,
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f), false);

        if (blackoutTime > returnDuration)
            yield return new WaitForSecondsRealtime(blackoutTime - returnDuration);

        // 5. A brief, half-open blink. No graphic action is shown.
        float blinkAlpha = 1f - blinkVisibility;
        yield return FadeBlack(1f, blinkAlpha, blinkOpenTime);
        yield return FadeBlack(blinkAlpha, 1f, blinkCloseTime);

        // 6. Reveal the scene, gradually fading out the ringing sound.
        float originalRingVolume = ringingSource != null ? ringingSource.volume : 0f;
        float elapsed = 0f;

        while (elapsed < revealTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = revealTime <= 0f ? 1f : Mathf.Clamp01(elapsed / revealTime);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            blackScreen.alpha = 1f - eased;

            if (ringingSource != null && ringingSource.isPlaying)
                ringingSource.volume = Mathf.Lerp(originalRingVolume, 0f, eased);

            yield return null;
        }

        blackScreen.alpha = 0f;

        if (ringingSource != null)
        {
            ringingSource.Stop();
            ringingSource.volume = ringingVolume;
        }

        // The log pivot is reset so Play() will not accumulate offsets if retriggered.
        logPivot.localPosition = initialLogPosition;
        logPivot.localRotation = initialLogRotation;
        ApplyCameraPose(start);

        IsPlaying = false;
        logPivot.gameObject.SetActive(false);
        onFinished?.Invoke();
    }

    private Pose CapturePose()
    {
        return new Pose
        {
            logPosition = logPivot.localPosition,
            logRotation = logPivot.localRotation,
            cameraPosition = playerCameraRoot.localPosition,
            cameraRotation = playerCameraRoot.localRotation,
            lookPosition = cameraLookTarget != null ? cameraLookTarget.localPosition : Vector3.zero
        };
    }

    private static Pose OffsetPose(Pose start, Vector3 logPosition, Vector3 logEuler,
        Vector3 cameraPosition, Vector3 cameraEuler, Vector3 lookPosition)
    {
        return new Pose
        {
            logPosition = start.logPosition + logPosition,
            logRotation = start.logRotation * Quaternion.Euler(logEuler),
            cameraPosition = start.cameraPosition + cameraPosition,
            cameraRotation = start.cameraRotation * Quaternion.Euler(cameraEuler),
            lookPosition = start.lookPosition + lookPosition
        };
    }

    private IEnumerator AnimatePose(Pose from, Pose to, float duration, AnimationCurve curve, bool fadeDuringLower)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.Clamp01(curve != null ? curve.Evaluate(t) : t);
            ApplyPose(from, to, eased);

            if (fadeDuringLower)
            {
                float fadeT = Mathf.InverseLerp(blackoutStartDuringLower, 1f, t);
                blackScreen.alpha = Mathf.SmoothStep(0f, 1f, fadeT);
            }

            yield return null;
        }

        ApplyPose(to);
        if (fadeDuringLower)
            blackScreen.alpha = 1f;
    }

    private void ApplyPose(Pose from, Pose to, float t)
    {
        logPivot.localPosition = Vector3.Lerp(from.logPosition, to.logPosition, t);
        logPivot.localRotation = Quaternion.Slerp(from.logRotation, to.logRotation, t);
        playerCameraRoot.localPosition = Vector3.Lerp(from.cameraPosition, to.cameraPosition, t);
        playerCameraRoot.localRotation = Quaternion.Slerp(from.cameraRotation, to.cameraRotation, t);

        if (cameraLookTarget != null)
            cameraLookTarget.localPosition = Vector3.Lerp(from.lookPosition, to.lookPosition, t);
    }

    private void ApplyPose(Pose pose)
    {
        logPivot.localPosition = pose.logPosition;
        logPivot.localRotation = pose.logRotation;
        ApplyCameraPose(pose);
    }

    private void ApplyCameraPose(Pose pose)
    {
        playerCameraRoot.localPosition = pose.cameraPosition;
        playerCameraRoot.localRotation = pose.cameraRotation;
        if (cameraLookTarget != null)
            cameraLookTarget.localPosition = pose.lookPosition;
    }

    private IEnumerator FadeBlack(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            blackScreen.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        blackScreen.alpha = to;
    }
}
