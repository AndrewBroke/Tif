using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;

public partial class ProximityChaser
{
    [Header("Grab")]
    [SerializeField] private bool enableGrab = true;
    [SerializeField, Min(0.01f)] private float grabDistance = 1.2f;
    [SerializeField] private string biteAnimationState = "Base Layer.BiteNeck";
    [SerializeField, Min(0.01f)] private float biteAnimationSpeed = 1f;
    [Tooltip("Moment of damage as a fraction of the bite clip, from 0 to 1.")]
    [SerializeField, Range(0f, 1f)] private float biteDamageTime = 0.5f;
    [SerializeField, Min(0f)] private float grabCooldown = 2f;

    [Header("Bite effect")]
    [Tooltip("X: bite animation progress (0–1). Y: Medic Bite Volume weight (0–1).")]
    [SerializeField] private AnimationCurve biteVolumeWeight = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));

    [Header("Grab camera")]
    [Tooltip("Rendered camera. If empty, uses Camera.main. Cinemachine is temporarily suspended on this camera.")]
    [SerializeField] private Camera grabCamera = null;
    [Tooltip("Optional focus point. If empty, finds MedicHead inside this medic; otherwise falls back to Focus Height.")]
    [SerializeField] private Transform grabFocusTarget = null;
    [SerializeField] private float focusHeight = 1.55f;
    [SerializeField, Min(0.01f)] private float cameraFocusTime = 0.25f;
    [SerializeField, Min(0.01f)] private float cameraReturnTime = 0.25f;

    public bool IsGrabbing { get; private set; }

    private Camera activeGrabCamera;
    private CinemachineBrain suspendedBrain;
    private FirstPersonController suspendedController;
    private bool brainWasEnabled;
    private bool controllerWasEnabled;
    private Quaternion initialCameraRotation;
    private Quaternion initialCameraLocalRotation;
    private Quaternion returnStartRotation;
    private float previousChaseTime;
    private float grabElapsed;
    private PlayerVolumeEffects activeVolumeEffects;
    private float returnElapsed;
    private float nextGrabTime;
    private bool returningCamera;
    private bool biteDamageApplied;
    private bool grabSetupWarningShown;

    private bool TryBeginGrab()
    {
        if (!enableGrab || IsGrabbing || IsRetreating || !initialized || !chasing || player == null || player.IsDead
            || Time.time < nextGrabTime || PlayerControlLocks.IsBlocked
            || (player.transform.position - transform.position).sqrMagnitude > grabDistance * grabDistance)
            return false;

        int biteStateHash = Animator.StringToHash(biteAnimationState);
        Camera camera = grabCamera != null ? grabCamera : Camera.main;
        if (camera == null || !characterAnimator.HasState(0, biteStateHash))
        {
            if (!grabSetupWarningShown)
            {
                Debug.LogWarning("ProximityChaser: grab requires a camera and Animator state '" + biteAnimationState
                    + "'. Run Tools/TIF/Medic/Create Chaser Assets to update the controller.", this);
                grabSetupWarningShown = true;
            }
            return false;
        }

        if (grabFocusTarget == null)
        {
            foreach (Transform target in GetComponentsInChildren<Transform>(true))
            {
                if (target.CompareTag("MedicHead"))
                {
                    grabFocusTarget = target;
                    break;
                }
            }
        }

        IsGrabbing = true;
        returningCamera = false;
        biteDamageApplied = false;
        grabElapsed = 0f;
        StopMoving();

        Vector3 towardPlayer = player.transform.position - transform.position;
        towardPlayer.y = 0f;
        if (towardPlayer.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(towardPlayer);

        activeGrabCamera = camera;
        initialCameraRotation = camera.transform.rotation;
        initialCameraLocalRotation = camera.transform.localRotation;
        suspendedBrain = camera.GetComponent<CinemachineBrain>();
        brainWasEnabled = suspendedBrain != null && suspendedBrain.enabled;
        suspendedController = player.GetComponent<FirstPersonController>();
        controllerWasEnabled = suspendedController != null && suspendedController.enabled;
        PlayerControlLocks.Acquire(this);
        if (!IsGrabbing)
            return false;
        if (suspendedController != null)
            suspendedController.enabled = false;
        if (suspendedBrain != null)
            suspendedBrain.enabled = false;

        previousChaseTime = Mathf.Repeat(characterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
        characterAnimator.speed = biteAnimationSpeed;
        characterAnimator.Play(biteStateHash, 0, 0f);
        characterAnimator.Update(0f);
        activeVolumeEffects = player.GetComponent<PlayerVolumeEffects>();
        UpdateBiteVolume(0f);
        return true;
    }

    private void UpdateGrab()
    {
        if (player != null && player.IsDead)
        {
            EndGrab(true);
            return;
        }
        if (activeGrabCamera == null || characterAnimator == null || player == null)
        {
            EndGrab(true);
            return;
        }

        if (returningCamera)
        {
            returnElapsed += Time.deltaTime;
            if (returnElapsed >= cameraReturnTime)
                EndGrab(true, retreatAfterGrab: true);
            return;
        }

        grabElapsed += Time.deltaTime;
        float biteProgress = Mathf.Clamp01(characterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime);
        if (!biteDamageApplied && !player.IsDead && biteProgress >= biteDamageTime)
        {
            biteDamageApplied = true;
            player.TakeDamage(contactDamage);
            // A damage/death listener may have disabled or destroyed this component.
            if (!IsGrabbing)
                return;
            if (player == null || activeGrabCamera == null || characterAnimator == null)
            {
                EndGrab(true);
                return;
            }
        }

        if (player.IsDead || biteProgress >= 1f)
        {
            if (activeVolumeEffects != null)
                activeVolumeEffects.ResetMedicBite();
            returningCamera = true;
            returnElapsed = 0f;
            returnStartRotation = activeGrabCamera.transform.rotation;
            characterAnimator.speed = 0f;
        }
    }

    private void LateUpdate()
    {
        if (!IsGrabbing || activeGrabCamera == null || (player != null && player.IsDead))
            return;

        if (returningCamera)
        {
            float t = Mathf.SmoothStep(0f, 1f, returnElapsed / Mathf.Max(0.01f, cameraReturnTime));
            activeGrabCamera.transform.rotation = Quaternion.Slerp(returnStartRotation, initialCameraRotation, t);
            return;
        }

        if (characterAnimator != null)
            UpdateBiteVolume(characterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime);

        Vector3 focus = grabFocusTarget != null ? grabFocusTarget.position : transform.position + Vector3.up * focusHeight;
        Vector3 direction = focus - activeGrabCamera.transform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            float t = Mathf.SmoothStep(0f, 1f, grabElapsed / Mathf.Max(0.01f, cameraFocusTime));
            activeGrabCamera.transform.rotation = Quaternion.Slerp(initialCameraRotation, Quaternion.LookRotation(direction), t);
        }
    }

    private void UpdateBiteVolume(float progress)
    {
        if (activeVolumeEffects != null)
            activeVolumeEffects.SetMedicBiteWeight(biteVolumeWeight == null ? 0f
                : biteVolumeWeight.Evaluate(Mathf.Clamp01(progress)));
    }

    // Hand the original component states to PlayerHealth before it captures its death snapshot.
    public void ReleaseGrabForDeath(PlayerHealth target)
    {
        if (IsGrabbing && player == target)
            EndGrab(true, restoreForDeath: true);
    }

    private void EndGrab(bool resumeChase, bool retreatAfterGrab = false, bool restoreForDeath = false)
    {
        if (!IsGrabbing)
            return;

        IsGrabbing = false;
        if (activeVolumeEffects != null)
            activeVolumeEffects.ResetMedicBite();
        activeVolumeEffects = null;
        bool playerDied = player != null && player.IsDead;
        if (activeGrabCamera != null && !playerDied)
            activeGrabCamera.transform.localRotation = initialCameraLocalRotation;
        if (suspendedBrain != null && (!playerDied || restoreForDeath))
            suspendedBrain.enabled = brainWasEnabled;
        if (suspendedController != null && (!playerDied || restoreForDeath))
            suspendedController.enabled = controllerWasEnabled;
        PlayerControlLocks.Release(this);

        activeGrabCamera = null;
        suspendedBrain = null;
        suspendedController = null;
        nextGrabTime = Time.time + grabCooldown;
        if (retreatAfterGrab && isActiveAndEnabled)
        {
            BeginRetreat();
            return;
        }
        if (resumeChase && characterAnimator != null)
        {
            characterAnimator.Play(chaseStateHash, 0, previousChaseTime);
            characterAnimator.speed = player != null && !player.IsDead ? animationSpeed : 0f;
        }
    }
}
