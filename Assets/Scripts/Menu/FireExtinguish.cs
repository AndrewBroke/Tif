using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class FireExtinguish : MonoBehaviour
{
    [Header("Партиклы")]
    public ParticleSystem fire;
    public ParticleSystem smoke;
    public ParticleSystem sparks;

    [Header("Свет")]
    public Light fireLight;

    [Header("Затемнение")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 2f;
    public Animator fadeImageAnimator;

    [Header("Сцена")]
    public string nextSceneName;

    [Header("Затухание")]
    public float extinguishDuration = 4f;
    public float waitAfterExtinguish = 2f;

    private bool isExtinguishing = false;

    public void ExtinguishFire()
    {
        if (!isExtinguishing)
        {
            StartCoroutine(Extinguish());
        }
    }

    private IEnumerator Extinguish()
    {
        isExtinguishing = true;

        // Запоминаем начальное количество частиц
        float fireStartRate = GetEmissionRate(fire);
        float smokeStartRate = GetEmissionRate(smoke);
        float sparksStartRate = GetEmissionRate(sparks);

        float timer = 0f;

        while (timer < extinguishDuration)
        {
            timer += Time.deltaTime;

            float progress = Mathf.Clamp01(timer / extinguishDuration);

            // Общая плавная кривая
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

            // Огонь затухает немного быстрее
            float fireProgress = Mathf.Clamp01(progress * 1.25f);
            fireProgress = Mathf.SmoothStep(0f, 1f, fireProgress);

            // Искры
            float sparksProgress = Mathf.Clamp01(progress * 1.1f);
            sparksProgress = Mathf.SmoothStep(0f, 1f, sparksProgress);

            // Дым остаётся дольше
            float smokeProgress = Mathf.Clamp01((progress - 0.15f) / 0.85f);
            smokeProgress = Mathf.SmoothStep(0f, 1f, smokeProgress);

            SetEmissionRate(
                fire,
                Mathf.Lerp(fireStartRate, 0f, fireProgress)
            );

            SetEmissionRate(
                sparks,
                Mathf.Lerp(sparksStartRate, 0f, sparksProgress)
            );

            SetEmissionRate(
                smoke,
                Mathf.Lerp(smokeStartRate, 0f, smokeProgress)
            );

            // Плавно уменьшаем свет вместе с огнём
            if (fireLight != null)
            {
                float startIntensity = fireLight.intensity;

                fireLight.intensity = Mathf.Lerp(
                    startIntensity,
                    0f,
                    fireProgress
                );
            }

            yield return null;
        }

        // Полностью прекращаем эмиссию
        SetEmissionRate(fire, 0f);
        SetEmissionRate(smoke, 0f);
        SetEmissionRate(sparks, 0f);

        // Выключаем свет
        if (fireLight != null)
        {
            fireLight.intensity = 0f;
            fireLight.enabled = false;
        }

        // Ждём исчезновения уже существующих частиц
        while (GetParticleCount(fire) > 0 ||
               GetParticleCount(smoke) > 0 ||
               GetParticleCount(sparks) > 0)
        {
            yield return null;
        }

        // После полного затухания ждём 2 секунды
        yield return new WaitForSeconds(waitAfterExtinguish);

        fadeImageAnimator.SetTrigger("Fade");
    }

    private float GetEmissionRate(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
            return 0f;

        var emission = particleSystem.emission;

        return emission.rateOverTime.constant;
    }

    private void SetEmissionRate(
        ParticleSystem particleSystem,
        float rate)
    {
        if (particleSystem == null)
            return;

        var emission = particleSystem.emission;

        emission.rateOverTime = rate;
    }

    private int GetParticleCount(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
            return 0;

        return particleSystem.particleCount;
    }
}

