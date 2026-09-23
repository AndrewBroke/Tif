using CartoonFX;
using UnityEngine;

public class FireController : MonoBehaviour
{

    [SerializeField] CFXR_Effect effect;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetLightBrightness(float intensity)
    {
        if (effect == null) return;

        foreach (var lightAnim in effect.animatedLights)
        {
            if (lightAnim.light == null) continue;

            lightAnim.light.intensity = intensity;
            lightAnim.intensityStart = intensity;
            lightAnim.intensityEnd = intensity * 2f;
        }

        effect.ResetState(); // чтобы заново применить обновлённые значения, если это нужно
    }

    public void ScaleEmission(float multiplier)
	{
		if (effect == null)
		{
			return;
		}

		foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>())
		{
			var emission = ps.emission;
			var rate = emission.rateOverTime;
			rate.constant *= multiplier;
			rate.constantMin *= multiplier;
			rate.constantMax *= multiplier;
			emission.rateOverTime = rate;

			if (emission.rateOverDistance.mode == ParticleSystemCurveMode.Constant)
			{
				var dist = emission.rateOverDistance.constant;
				emission.rateOverDistance = dist * multiplier;
			}
		}
	}
}
