using UnityEngine;

public class SetTagObject : MonoBehaviour
{
    [SerializeField] private string defaultTag = "Untagged";
    [SerializeField] private string targetTag = "Untagged";

    /// <summary>
    /// Устанавливает тег текущего объекта, заданный в инспекторе.
    /// </summary>
    public void SetTag()
    {
        gameObject.tag = string.IsNullOrEmpty(targetTag) ? "Untagged" : targetTag;
    }

    /// <summary>
    /// Сбрасывает тег текущего объекта на выбранный по умолчанию.
    /// </summary>
    public void ResetTag()
    {
        gameObject.tag = string.IsNullOrEmpty(defaultTag) ? "Untagged" : defaultTag;
    }
}