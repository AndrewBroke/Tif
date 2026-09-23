using UnityEngine;

/// <summary>
/// Static helper for changing the layer of a GameObject from any script.
/// </summary>
public static class SetLayerObject
{
    /// <summary>
    /// Устанавливает для объекта слой с указанным индексом.
    /// </summary>
    public static void SetLayer(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
    }

    /// <summary>
    /// Сбрасывает слой объекта на Default (0).
    /// </summary>
    public static void ResetLayer(GameObject obj)
    {
        if (obj == null) return;
        obj.layer = 0; // Default layer
    }
}