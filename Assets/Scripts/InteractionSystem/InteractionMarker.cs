
using UnityEngine;

public enum OcclusionMode
{
    IgnoreOwnColliders, // Игнорировать своё тело
    RespectAll,         // Учитывать все препятствия
    AlwaysVisible       // Игнорировать все препятствия
}

[DisallowMultipleComponent]
public class InteractionMarker : MonoBehaviour
{
    [Header("Препятствия")]
    [SerializeField]
    private OcclusionMode occlusionMode =
    OcclusionMode.IgnoreOwnColliders;

    public OcclusionMode Occlusion => occlusionMode;

    // Use only the immediate parent; resolve dynamically to support reparenting.
    public Transform OcclusionOwner
    {
        get
        {
            Transform parent = transform.parent;
            return parent != null ? parent : transform;
        }
    }

    [Header("Положение маркера")]
    [Tooltip("Точка, к которой привязан маркер")]
    [SerializeField] private Transform markerAnchor;

    [Tooltip("Смещение, если Marker Anchor не указан")]
    [SerializeField] private Vector3 localOffset = Vector3.up;

    [Header("Расстояние")]
    [Tooltip("На каком расстоянии маркер начинает появляться")]
    [Min(0.1f)]
    [SerializeField] private float maxVisibleDistance = 6f;

    [Tooltip("Расстояние, на котором маркер максимально яркий")]
    [Min(0f)]
    [SerializeField] private float fullOpacityDistance = 2f;

    [Tooltip("Зона плавного появления на границе видимости")]
    [Min(0f)]
    [SerializeField] private float edgeFadeDistance = 0.7f;

    [Header("Прозрачность")]
    [Range(0f, 1f)]
    [SerializeField] private float minOpacity = 0.15f;

    [Range(0f, 1f)]
    [SerializeField] private float maxOpacity = 1f;

    [Header("Взаимодействие")]
    [SerializeField] private string interactionText = "Взаимодействовать";

    [Tooltip("Можно ли сейчас взаимодействовать с объектом")]
    [SerializeField] private bool interactionAvailable = true;

    [Tooltip("Показывать ли маркер")]
    [SerializeField] private bool showMarker = true;

    [Tooltip("Скрывать маркер за препятствиями")]
    [SerializeField] private bool hideWhenOccluded = true;

    // Наведён ли взгляд игрока на объект
    private bool isFocused;

    // Данные для менеджера
    public Vector3 MarkerPosition =>
        markerAnchor != null
            ? markerAnchor.position
            : transform.TransformPoint(localOffset);

    public float MaxVisibleDistance => maxVisibleDistance;

    public bool HideWhenOccluded => hideWhenOccluded;

    public string InteractionText => interactionText;

    public bool IsFocused => isFocused && CanDisplay;

    public bool HasInteractionTag
    {
        get
        {
            Interactable owner = GetComponentInParent<Interactable>();
            return owner != null ? owner.CompareTag("Interact") : CompareTag("Interact");
        }
    }

    public bool CanDisplay =>
        isActiveAndEnabled &&
        HasInteractionTag &&
        showMarker &&
        interactionAvailable;

    // Получить прозрачность с учётом расстояния
    public float GetTargetOpacity(Vector3 viewerPosition)
    {
        if (!CanDisplay)
            return 0f;

        float distance = Vector3.Distance(
            viewerPosition,
            MarkerPosition
        );

        if (distance >= maxVisibleDistance)
            return 0f;

        // От 0 (далеко) до 1 (близко)
        float t = Mathf.InverseLerp(
            maxVisibleDistance,
            fullOpacityDistance,
            distance
        );

        // Более плавная кривая
        t = Mathf.SmoothStep(0f, 1f, t);

        float opacity = Mathf.Lerp(
            minOpacity,
            maxOpacity,
            t
        );

        // Плавное исчезновение на внешней границе
        if (edgeFadeDistance > 0f)
        {
            float edge = Mathf.InverseLerp(
                maxVisibleDistance,
                maxVisibleDistance - edgeFadeDistance,
                distance
            );

            opacity *= edge;
        }

        return Mathf.Clamp01(opacity);
    }

    // Игрок навёл взгляд
    public void SetFocused(bool focused)
    {
        isFocused = focused && interactionAvailable;
    }

    // Разрешить / запретить взаимодействие
    public void SetAvailable(bool available)
    {
        interactionAvailable = available;

        if (!available)
            isFocused = false;
    }

    // Включить / выключить отображение маркера
    public void SetMarkerVisible(bool visible)
    {
        showMarker = visible;

        if (!visible)
            isFocused = false;
    }

    // Изменить радиус обнаружения
    public void SetVisibleDistance(float distance)
    {
        maxVisibleDistance = Mathf.Max(0.1f, distance);

        fullOpacityDistance = Mathf.Min(
            fullOpacityDistance,
            maxVisibleDistance
        );
    }

    private void OnDisable()
    {
        isFocused = false;
    }

    private void OnValidate()
    {
        maxVisibleDistance =
            Mathf.Max(0.1f, maxVisibleDistance);

        fullOpacityDistance = Mathf.Clamp(
            fullOpacityDistance,
            0f,
            maxVisibleDistance - 0.01f
        );

        edgeFadeDistance = Mathf.Clamp(
            edgeFadeDistance,
            0f,
            maxVisibleDistance - fullOpacityDistance
        );

        maxOpacity = Mathf.Max(maxOpacity, minOpacity);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            MarkerPosition,
            maxVisibleDistance
        );

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(
            MarkerPosition,
            fullOpacityDistance
        );

        Gizmos.color = Color.white;
        Gizmos.DrawSphere(MarkerPosition, 0.06f);
    }
}
