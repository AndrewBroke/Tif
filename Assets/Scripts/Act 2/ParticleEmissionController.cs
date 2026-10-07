using System.Collections;
using UnityEngine;

public class ParticleEmissionController : MonoBehaviour
{
    [SerializeField] private float initialEmission = 10f;
    [SerializeField] private float targetEmission = 50f;
    [SerializeField] private float transitionDuration = 2f;

    private ParticleSystem targetParticleSystem;
    private ParticleSystem.EmissionModule emissionModule;
    private Coroutine emissionCoroutine;

    private void Start()
    {
        targetParticleSystem = GetComponentInParent<ParticleSystem>();
        if (targetParticleSystem == null)
        {
            Debug.LogWarning("ParticleSystem не найден в родителях.", this);
            return;
        }

        emissionModule = targetParticleSystem.emission;
        SetEmission(initialEmission);

        // Удалить
        //StartEmissionTransition();
    }

    public void StartEmissionTransition()
    {
        if (emissionCoroutine != null)
            StopCoroutine(emissionCoroutine);

        emissionCoroutine = StartCoroutine(EmissionTransitionCoroutine());
    }

    private IEnumerator EmissionTransitionCoroutine()
    {
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            float currentRate = Mathf.Lerp(initialEmission, targetEmission, elapsed / transitionDuration);
            SetEmission(currentRate);

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetEmission(targetEmission);
        emissionCoroutine = null;
    }

    private void SetEmission(float rate)
    {
        emissionModule.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
    }
}
