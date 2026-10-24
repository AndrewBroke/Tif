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
        public Collider occlusionBlocker;
        public string lastDiagnosticReason;
        public bool lastDiagnosticFocus;
        public Collider lastDiagnosticBlocker;
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
            {
                foreach (var pair in views)
                    LogViewChange(pair.Key, pair.Value, "CameraMissing", 0f);
                return;
            }
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
            LogViewChange(marker, view, PlayerControlLocks.InteractionsBlocked
                ? "InteractionsBlocked" : marker == null ? "MarkerMissing" : "TagIsNotInteract", 0f);
            view.alpha = 0f;
            view.group.alpha = 0f;
            view.ui.SetFocused(false);
            view.root.gameObject.SetActive(false);
            return;
        }

        float targetAlpha = 0f;
        Vector3 screenPoint = Vector3.zero;
        bool onScreen = false;
        string visibilityReason = !indicatorsVisible ? "IndicatorsHiddenByEvent" : "MarkerCannotDisplay";

        if (marker != null && indicatorsVisible && marker.CanDisplay)
        {
            screenPoint = playerCamera.WorldToScreenPoint(marker.MarkerPosition);
            onScreen = screenPoint.z > 0f &&
                       screenPoint.x >= 0f && screenPoint.x <= Screen.width &&
                       screenPoint.y >= 0f && screenPoint.y <= Screen.height;
            visibilityReason = "MarkerOffScreenOrBehindCamera";

            if (onScreen)
            {
                targetAlpha = marker.GetTargetOpacity(playerCamera.transform.position);
                visibilityReason = targetAlpha > 0f ? "Visible" : "BeyondMarkerVisibleDistance";

                if (targetAlpha > 0f)
                {
                    if (Time.unscaledTime >= view.nextOcclusionCheck)
                    {
                        view.unobstructed = IsUnobstructed(marker, out view.occlusionBlocker);
                        view.nextOcclusionCheck = Time.unscaledTime + occlusionCheckInterval;
                    }

                    if (!view.unobstructed)
                    {
                        targetAlpha = 0f;
                        visibilityReason = "MarkerOccluded";
                    }
                }
            }
        }

        LogViewChange(marker, view, visibilityReason, targetAlpha);
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

    private bool IsUnobstructed(InteractionMarker marker, out Collider blocker)
    {
        blocker = null;
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
                {
                    blocker = hit.collider;
                    return false;
                }
        }
        else
        {
            for (int i = 0; i < count; i++)
                if (BlocksMarker(marker, raycastHits[i].collider))
                {
                    blocker = raycastHits[i].collider;
                    return false;
                }
        }

        return true;
    }

    private void LogViewChange(InteractionMarker marker, MarkerView view, string reason, float targetAlpha)
    {
        if (!InteractionDiagnostics.Matches(marker)) return;
        bool focused = marker.IsFocused;
        Collider blocker = reason == "MarkerOccluded" ? view.occlusionBlocker : null;
        if (view.lastDiagnosticReason == reason && view.lastDiagnosticFocus == focused &&
            view.lastDiagnosticBlocker == blocker)
            return;
        view.lastDiagnosticReason = reason;
        view.lastDiagnosticFocus = focused;
        view.lastDiagnosticBlocker = blocker;
        InteractionDiagnostics.Log(marker, "INDICATOR",
            $"reason={reason}; focused={focused}; targetAlpha={targetAlpha:F3}; alpha={view.alpha:F3}; " +
            $"camera={InteractionDiagnostics.Path(playerCamera)}; " +
            $"cameraPosition={(playerCamera != null ? playerCamera.transform.position.ToString("F3") : "<none>")}; " +
            $"marker={marker.MarkerPosition.ToString("F3")}; " +
            $"distance={(playerCamera != null ? Vector3.Distance(playerCamera.transform.position, marker.MarkerPosition) : -1f):F3}; " +
            $"blocker={InteractionDiagnostics.Path(blocker)}; occlusionMode={marker.Occlusion}; " +
            $"occlusionOwner={InteractionDiagnostics.Path(marker.OcclusionOwner)}; " +
            $"obstacleMask=0x{obstacleLayers.value:X}; {marker.DiagnosticState()}");
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
        foreach (var pair in views)
            LogViewChange(pair.Key, pair.Value, "IndicatorManagerDisabled", 0f);
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
