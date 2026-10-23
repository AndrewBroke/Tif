using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class GraphicsController : MonoBehaviour
{
    [Tooltip("Renderer Data asset containing the Full Screen Pass feature (for example, PC_Renderer).")]
    [SerializeField] private ScriptableRendererData rendererData;

    [Tooltip("Enable or disable Full Screen Pass features while this controller is active.")]
    [SerializeField] private bool fullScreenPassEnabled;

    private readonly Dictionary<FullScreenPassRendererFeature, bool> originalStates = new();
    private bool appliedState;

    public bool FullScreenPassEnabled
    {
        get => fullScreenPassEnabled;
        set => SetFullScreenPassEnabled(value);
    }

    private void OnEnable()
    {
        if (rendererData == null)
        {
            Debug.LogWarning("GraphicsController: assign a Renderer Data asset in the Inspector.", this);
            return;
        }

        foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
        {
            if (feature is FullScreenPassRendererFeature fullScreenPass)
                originalStates[fullScreenPass] = fullScreenPass.isActive;
        }

        if (originalStates.Count == 0)
            Debug.LogWarning("GraphicsController: the selected Renderer Data has no Full Screen Pass feature.", this);

        ApplyFullScreenPass();
    }

    private void Update()
    {
        // Apply changes made through the Inspector during Play Mode.
        if (appliedState != fullScreenPassEnabled)
            ApplyFullScreenPass();
    }

    public void SetFullScreenPassEnabled(bool enabled)
    {
        fullScreenPassEnabled = enabled;

        if (isActiveAndEnabled)
            ApplyFullScreenPass();
    }

    private void ApplyFullScreenPass()
    {
        foreach (var entry in originalStates)
        {
            if (entry.Key != null)
                entry.Key.SetActive(fullScreenPassEnabled);
        }

        appliedState = fullScreenPassEnabled;
    }

    private void OnDisable()
    {
        // Renderer Data is shared: restore its state when leaving the scene or Play Mode.
        foreach (var entry in originalStates)
        {
            if (entry.Key != null)
                entry.Key.SetActive(entry.Value);
        }

        originalStates.Clear();
    }
}
