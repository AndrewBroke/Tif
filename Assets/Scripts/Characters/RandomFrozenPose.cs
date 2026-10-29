using UnityEngine;

/// <summary>Chooses a random heading, samples one animation frame and holds the pose.</summary>
[DisallowMultipleComponent]
public class RandomFrozenPose : MonoBehaviour
{
    [SerializeField] private RuntimeAnimatorController animationController;
    [SerializeField] private AnimationClip poseClip;

    private void Start()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator == null || animationController == null || poseClip == null)
        {
            Debug.LogWarning("RandomFrozenPose: assign a controller and pose clip on a model with an Animator.", this);
            return;
        }

        if (poseClip.isHumanMotion && (animator.avatar == null || !animator.avatar.isValid || !animator.isHuman))
        {
            Debug.LogWarning("RandomFrozenPose: a valid Humanoid Avatar is required for this clip.", this);
            return;
        }

        transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);

        Vector3 position = animator.transform.localPosition;
        Quaternion rotation = animator.transform.localRotation;
        animator.runtimeAnimatorController = animationController;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.keepAnimatorStateOnDisable = true;
        animator.speed = 0f;

        int frameCount = Mathf.Max(1, Mathf.CeilToInt(poseClip.length * poseClip.frameRate));
        float frameTime = Random.Range(0, frameCount) / Mathf.Max(1f, poseClip.frameRate);
        float normalizedTime = poseClip.length > 0f ? frameTime / poseClip.length : 0f;
        animator.Play("Base Layer.Walk", 0, normalizedTime);
        animator.Update(0f);

        // Hold a walking pose without changing the prefab's placement.
        animator.transform.SetLocalPositionAndRotation(position, rotation);
    }
}
