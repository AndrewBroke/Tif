using UnityEngine;

public class LightController : MonoBehaviour
{
    [SerializeField] private Light targetLight;
    [SerializeField] private Color newColor = Color.white;

    private void Reset()
    {
        targetLight = GetComponent<Light>();
    }

    public void ChangeColor()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        if (targetLight == null)
        {
            Debug.LogWarning("Assign a Light to LightController.", this);
            return;
        }

        targetLight.color = newColor;
    }
}
