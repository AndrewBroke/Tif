using UnityEngine;

/// <summary>Prefab-local visual swap called by the existing dialogue's final node.</summary>
[DisallowMultipleComponent]
public sealed class MedicVisualSwap : MonoBehaviour
{
    [SerializeField] private GameObject medicModel;
    [SerializeField] private GameObject civilianModel;
    [SerializeField] private Interactable corpseInteraction;

    private bool fallen;
    public bool HasSwapped { get; private set; }
    public event System.Action Swapped;

    public void MarkFallen() => fallen = true;

    public void SwapToCivilian()
    {
        if (!fallen || HasSwapped) return;
        if (medicModel == null || civilianModel == null || medicModel == civilianModel
            || medicModel == gameObject || civilianModel == gameObject)
        {
            Debug.LogWarning("MedicVisualSwap: assign separate medic and civilian child models.", this);
            return;
        }
        Animator source = medicModel.GetComponentInChildren<Animator>(true);
        Animator target = civilianModel.GetComponentInChildren<Animator>(true);
        if (source == null || target == null || source.runtimeAnimatorController == null)
        {
            Debug.LogWarning("MedicVisualSwap: both models need compatible Animators.", this);
            return;
        }

        AnimatorStateInfo pose = source.GetCurrentAnimatorStateInfo(0);
        civilianModel.transform.SetPositionAndRotation(medicModel.transform.position, medicModel.transform.rotation);
        civilianModel.transform.localScale = medicModel.transform.localScale;
        target.runtimeAnimatorController = source.runtimeAnimatorController;
        target.applyRootMotion = false;
        target.enabled = true;
        target.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        target.speed = 1f;
        civilianModel.SetActive(true);
        target.Rebind();
        if (!target.HasState(0, pose.fullPathHash))
        {
            civilianModel.SetActive(false);
            Debug.LogWarning("MedicVisualSwap: the civilian controller cannot reproduce the fallen pose.", this);
            return;
        }
        target.Play(pose.fullPathHash, 0, Mathf.Repeat(pose.normalizedTime, 1f));
        target.Update(0f);
        target.speed = source.speed;
        if (source.isHuman && target.isHuman)
        {
            Transform sourceHips = source.GetBoneTransform(HumanBodyBones.Hips);
            Transform targetHips = target.GetBoneTransform(HumanBodyBones.Hips);
            if (sourceHips != null && targetHips != null)
                civilianModel.transform.position += sourceHips.position - targetHips.position;
        }
        // Matching the medic's current pose also retains its temporary vertical animation correction.
        medicModel.SetActive(false);
        if (corpseInteraction != null) corpseInteraction.SetAvailable(false);
        HasSwapped = true;
        Swapped?.Invoke();
    }
}
