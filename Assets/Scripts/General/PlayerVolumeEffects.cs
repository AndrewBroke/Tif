using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class PlayerVolumeEffects : MonoBehaviour
{
    [Header("Medic bite")]
    [SerializeField] private Volume medicBiteVolume;

    private void Awake() => ResetMedicBite();

    public void SetMedicBiteWeight(float weight)
    {
        if (medicBiteVolume != null && isActiveAndEnabled)
            medicBiteVolume.weight = Mathf.Clamp01(weight);
    }

    public void ResetMedicBite()
    {
        if (medicBiteVolume != null)
            medicBiteVolume.weight = 0f;
    }

    private void OnDisable() => ResetMedicBite();
}
