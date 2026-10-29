using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    public const int MaxHealth = 3;

    private static readonly int HitState = Animator.StringToHash("Base Layer.hit");
    private static readonly int HitTrigger = Animator.StringToHash("Hit");

    [Tooltip("Animator on the Hit Volume post-processing object.")]
    [SerializeField] private Animator hitVolumeAnimator;
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    private int currentHealth = MaxHealth;

    public int CurrentHealth => currentHealth;
    public bool IsDead => currentHealth == 0;

    private void Awake()
    {
        if (hitVolumeAnimator == null)
            TryGetComponent(out hitVolumeAnimator);

        if (hitVolumeAnimator == null)
            Debug.LogWarning("Assign the Hit Volume Animator to PlayerHealth to play the damage effect.", this);
    }

    public void TakeDamage()
    {
        TakeDamage(1);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDead)
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
        {
            PlayDeathAnimation();
            onDeath.Invoke();
        }
    }

    public virtual void PlayDeathAnimation()
    {
    }
}
