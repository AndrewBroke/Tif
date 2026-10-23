using System.Collections.Generic;
using UnityEngine;

public class InteractionIndicatorManager : MonoBehaviour
{
    [Header("Основные ссылки")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Canvas indicatorCanvas;
    [SerializeField] private GameObject indicatorPrefab;
    [Tooltip("Необязательно. Позволяет игнорировать коллайдеры игрока при проверке препятствий.")]
    [SerializeField] private Transform playerRoot;

    [Min(0.1f)][SerializeField] private float fadeSpeed = 12f;

    [Header("Проверка препятствий")]
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [Min(0.02f)][SerializeField] private float occlusionCheckInterval = 0.12f;

    [Header("Поиск объектов")]
    [Min(0.2f)][SerializeField] private float scanInterval = 1f;

    private sealed class MarkerView
    {
        public InteractionIndicatorView ui;
        public RectTransform root => ui.Root;
        public CanvasGroup group => ui.Group;
        public float nextOcclusionCheck;
        public bool unobstructed = true;
        public float alpha;
    }

    private readonly Dictionary<InteractionMarker, MarkerView> views =
        new Dictionary<InteractionMarker, MarkerView>();
    private readonly List<InteractionMarker> toRemove = new List<InteractionMarker>();
    private readonly RaycastHit[] raycastHits = new RaycastHit[32];

    private RectTransform canvasRect;
    private float nextScan;
    private bool indicatorsVisible = true;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (!PrepareCanvas())
        {
            enabled = false;
            return;
        }

        if (indicatorPrefab == null ||
            !indicatorPrefab.TryGetComponent(out InteractionIndicatorView prefabView) ||
            !prefabView.IsConfigured)
        {
            Debug.LogError("InteractionIndicatorManager: assign Indicator Prefab with configured InteractionIndicatorView (Root, Group, Dot, Interaction Prompt).", this);
            enabled = false;
        }
    }

    private void Start()
    {
        ScanMarkers();
    }

    private bool PrepareCanvas()
    {
        if (indicatorCanvas == null)
        {
            Debug.LogError("InteractionIndicatorManager: assign an existing Indicator Canvas.", this);
            return false;
        }

        if (indicatorCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceCamera &&
            indicatorCanvas.rootCanvas.worldCamera == null)
        {
            Debug.LogError("InteractionIndicatorManager: assign Render Camera on the Screen Space Camera Canvas.", this);
            return false;
        }

        if (indicatorCanvas.rootCanvas.renderMode == RenderMode.WorldSpace)
        {
            Debug.LogError(
                "InteractionIndicatorManager: нужен Screen Space Canvas, не World Space.",
                this
            );
            return false;
        }

        canvasRect = indicatorCanvas.GetComponent<RectTransform>();
        return canvasRect != null;
    }

    private void ScanMarkers()
    {
        InteractionMarker[] markers = Object.FindObjectsByType<InteractionMarker>();

        foreach (InteractionMarker marker in markers)
        {
            if (marker != null && marker.isActiveAndEnabled && !views.ContainsKey(marker))
                views.Add(marker, CreateView(marker));
        }

        toRemove.Clear();
        foreach (var pair in views)
        {
            if (pair.Key == null || !pair.Key.isActiveAndEnabled)
                toRemove.Add(pair.Key);
        }

        foreach (InteractionMarker marker in toRemove)
        {
            MarkerView view = views[marker];
            if (view.root != null)
                Destroy(view.root.gameObject);
            views.Remove(marker);
        }
    }

    private MarkerView CreateView(InteractionMarker marker)
    {
        GameObject instance = Instantiate(indicatorPrefab, canvasRect, false);
        instance.name = "Indicator - " + marker.name;
        InteractionIndicatorView ui = instance.GetComponent<InteractionIndicatorView>();
        ui.Group.alpha = 0f;
        ui.Group.interactable = false;
        ui.Group.blocksRaycasts = false;
        ui.SetFocused(false);
        instance.SetActive(false);
        return new MarkerView { ui = ui };
    }

    private void LateUpdate()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
                return;
        }

        if (Time.unscaledTime >= nextScan)
        {
            ScanMarkers();
            nextScan = Time.unscaledTime + scanInterval;
        }

        foreach (var pair in views)
            UpdateView(pair.Key, pair.Value);
    }

    private void UpdateView(InteractionMarker marker, MarkerView view)
    {
        if (PlayerControlLocks.InteractionsBlocked || marker == null || !marker.HasInteractionTag)
        {
            view.alpha = 0f;
            view.group.alpha = 0f;
            view.ui.SetFocused(false);
            view.root.gameObject.SetActive(false);
            return;
        }

        float targetAlpha = 0f;
        Vector3 screenPoint = Vector3.zero;
        bool onScreen = false;

        if (marker != null && indicatorsVisible && marker.CanDisplay)
        {
            screenPoint = playerCamera.WorldToScreenPoint(marker.MarkerPosition);
            onScreen = screenPoint.z > 0f &&
                       screenPoint.x >= 0f && screenPoint.x <= Screen.width &&
                       screenPoint.y >= 0f && screenPoint.y <= Screen.height;

            if (onScreen)
            {
                targetAlpha = marker.GetTargetOpacity(playerCamera.transform.position);

                if (targetAlpha > 0f)
                {
                    if (Time.unscaledTime >= view.nextOcclusionCheck)
                    {
                        view.unobstructed = IsUnobstructed(marker);
                        view.nextOcclusionCheck = Time.unscaledTime + occlusionCheckInterval;
                    }

                    if (!view.unobstructed)
                        targetAlpha = 0f;
                }
            }
        }

        float lerp = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);
        view.alpha = Mathf.Lerp(view.alpha, targetAlpha, lerp);

        if (view.alpha < 0.005f && targetAlpha <= 0f)
        {
            view.alpha = 0f;
            view.group.alpha = 0f;
            view.root.gameObject.SetActive(false);
            return;
        }

        view.root.gameObject.SetActive(true);
        view.group.alpha = view.alpha;

        if (onScreen)
        {
            Camera uiCamera = indicatorCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : indicatorCanvas.rootCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
            {
                view.root.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
            }
        }

        view.ui.SetFocused(marker != null && marker.IsFocused);
    }

    private bool IsUnobstructed(InteractionMarker marker)
    {
        if (marker.Occlusion == OcclusionMode.AlwaysVisible)
            return true;

        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = marker.MarkerPosition - origin;
        float distance = direction.magnitude;

        if (distance < 0.001f)
            return true;

        direction /= distance;

        int count = Physics.RaycastNonAlloc(
            origin, direction, raycastHits, distance,
            obstacleLayers, QueryTriggerInteraction.Ignore
        );

        if (count == raycastHits.Length)
        {
            // Редкий случай: буфера не хватило, проверяем все пересечения.
            RaycastHit[] allHits = Physics.RaycastAll(
                origin, direction, distance,
                obstacleLayers, QueryTriggerInteraction.Ignore
            );

            foreach (RaycastHit hit in allHits)
                if (BlocksMarker(marker, hit.collider))
                    return false;
        }
        else
        {
            for (int i = 0; i < count; i++)
                if (BlocksMarker(marker, raycastHits[i].collider))
                    return false;
        }

        return true;
    }

    private bool BlocksMarker(InteractionMarker marker, Collider collider)
    {
        if (collider == null)
            return false;

        Transform hitTransform = collider.transform;

        if (playerRoot != null && hitTransform.IsChildOf(playerRoot))
            return false;

        if (marker.Occlusion == OcclusionMode.IgnoreOwnColliders &&
            hitTransform.IsChildOf(marker.OcclusionOwner))
            return false;

        return true;
    }

    // Вызывается из UnityEvent при запуске / завершении кат-сцены.
    public void SetIndicatorsVisible(bool visible)
    {
        indicatorsVisible = visible;
    }

    public void HideIndicators() => SetIndicatorsVisible(false);
    public void ShowIndicators() => SetIndicatorsVisible(true);

    private void OnDisable()
    {
        foreach (var view in views.Values)
            if (view.root != null)
            {
                view.alpha = 0f;
                view.group.alpha = 0f;
                view.root.gameObject.SetActive(false);
            }
    }

    private void OnDestroy()
    {
        foreach (var view in views.Values)
            if (view.root != null)
                Destroy(view.root.gameObject);
        views.Clear();
    }
}
