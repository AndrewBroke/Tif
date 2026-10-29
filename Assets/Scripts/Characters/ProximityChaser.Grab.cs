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
    private float biteDuration;
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
        AnimatorStateInfo bite = characterAnimator.GetCurrentAnimatorStateInfo(0);
        biteDuration = Mathf.Max(0.01f, bite.length / Mathf.Max(0.01f,
            biteAnimationSpeed * Mathf.Abs(bite.speed * bite.speedMultiplier)));
        return true;
    }

    private void UpdateGrab()
    {
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
        if (!biteDamageApplied && !player.IsDead && grabElapsed >= biteDuration * biteDamageTime)
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

        if (player.IsDead || grabElapsed >= biteDuration)
        {
            returningCamera = true;
            returnElapsed = 0f;
            returnStartRotation = activeGrabCamera.transform.rotation;
            characterAnimator.speed = 0f;
        }
    }

    private void LateUpdate()
    {
        if (!IsGrabbing || activeGrabCamera == null)
            return;

        if (returningCamera)
        {
            float t = Mathf.SmoothStep(0f, 1f, returnElapsed / Mathf.Max(0.01f, cameraReturnTime));
            activeGrabCamera.transform.rotation = Quaternion.Slerp(returnStartRotation, initialCameraRotation, t);
            return;
        }

        Vector3 focus = grabFocusTarget != null ? grabFocusTarget.position : transform.position + Vector3.up * focusHeight;
        Vector3 direction = focus - activeGrabCamera.transform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            float t = Mathf.SmoothStep(0f, 1f, grabElapsed / Mathf.Max(0.01f, cameraFocusTime));
            activeGrabCamera.transform.rotation = Quaternion.Slerp(initialCameraRotation, Quaternion.LookRotation(direction), t);
        }
    }

    private void EndGrab(bool resumeChase, bool retreatAfterGrab = false)
    {
        if (!IsGrabbing)
            return;

        IsGrabbing = false;
        if (activeGrabCamera != null)
            activeGrabCamera.transform.localRotation = initialCameraLocalRotation;
        if (suspendedBrain != null)
            suspendedBrain.enabled = brainWasEnabled;
        if (suspendedController != null)
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
