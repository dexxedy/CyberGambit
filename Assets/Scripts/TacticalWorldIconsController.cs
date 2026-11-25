using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Полноэкранный оверлей иконок юнитов в тактике (Battlefield-style): позиции через WorldToScreenPoint.
/// </summary>
public class TacticalWorldIconsController : MonoBehaviour
{
    public static TacticalWorldIconsController Instance;

    [Header("UI")]
    [SerializeField] private RectTransform iconsRoot;
    [SerializeField] private TacticalWorldUnitIcon iconPrefab;
    [SerializeField] private float worldOffsetY = 2f;

    [Header("Опции")]
    [SerializeField] private bool hideIconsWhenBehindCamera = true;
    [SerializeField] private float nearIconSizePixels = 64f;
    [SerializeField] private float farIconSizePixels = 34f;
    [SerializeField] private float nearOrthographicSize = 25f;
    [SerializeField] private float farOrthographicSize = 70f;

    [Header("Attack Range Ring")]
    [SerializeField] private bool showRangedAttackRangeOnHover = true;
    [SerializeField] private Color rangeRingColor = new Color(1f, 0.25f, 0.15f, 0.9f);
    [SerializeField] private float rangeRingWidth = 0.12f;
    [SerializeField] private float rangeRingYOffset = 0.08f;
    [SerializeField] private int rangeRingSegments = 96;

    private readonly Dictionary<Unit, TacticalWorldUnitIcon> icons = new Dictionary<Unit, TacticalWorldUnitIcon>();
    private readonly List<IconLayoutEntry> visibleIconLayout = new List<IconLayoutEntry>();
    private Unit lastHoverFromIcon;
    private LineRenderer rangeRing;
    private Material runtimeRangeRingMaterial;

    private struct IconLayoutEntry
    {
        public TacticalWorldUnitIcon Icon;
        public RectTransform Rect;
        public Vector2 ScreenPosition;
    }

    public bool IsConfigured()
    {
        return iconPrefab != null && iconsRoot != null;
    }

    public void SetIconsRootVisible(bool visible)
    {
        if (iconsRoot != null)
            iconsRoot.gameObject.SetActive(visible);
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (runtimeRangeRingMaterial != null)
            Destroy(runtimeRangeRingMaterial);
    }

    private void LateUpdate()
    {
        if (CameraManager.Instance == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused())
        {
            if (iconsRoot != null) iconsRoot.gameObject.SetActive(false);
            ClearHoveredIcon();
            HideAttackRangeRing();
            return;
        }
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver())
        {
            SetIconsRootVisible(false);
            ClearHoveredIcon();
            HideAttackRangeRing();
            return;
        }

        bool tactical = !CameraManager.Instance.IsActionMode();
        if (!IsConfigured())
            return;

        // В фазе расстановки армии иконки не показываем — игрок видит модели юнитов.
        if (GameManager.Instance != null && GameManager.Instance.IsArmyDeploymentPhase())
        {
            if (iconsRoot != null) iconsRoot.gameObject.SetActive(false);
            ClearHoveredIcon();
            HideAttackRangeRing();
            return;
        }

        if (iconsRoot != null) iconsRoot.gameObject.SetActive(tactical);

        if (!tactical)
        {
            ClearHoveredIcon();
            HideAttackRangeRing();
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsBotTurn())
        {
            if (iconsRoot != null) iconsRoot.gameObject.SetActive(false);
            ClearHoveredIcon();
            HideAttackRangeRing();
            return;
        }

        Camera tacCam = CameraManager.Instance.GetTacticalCamera();
        if (tacCam == null) return;

        SyncIconsWithLivingUnits();
        UpdateIconScreenPositions(tacCam);
        UpdateAttackRangeRing(tacCam);
    }

    private void SyncIconsWithLivingUnits()
    {
        Unit[] all = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        HashSet<Unit> alive = new HashSet<Unit>();

        foreach (Unit u in all)
        {
            if (u == null || u.GetHealth() <= 0) continue;

            // PvBot fog-of-war: врагов показываем только после разведки (white), last known в gray.
            if (GameManager.Instance != null && GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot &&
                u.owner == Player.Player2)
            {
                if (FogWarIntelTracker.Instance == null ||
                    !FogWarIntelTracker.Instance.ShouldShowEnemyOnTacticalMap(u))
                    continue;
            }

            alive.Add(u);
            if (!icons.ContainsKey(u))
                CreateIcon(u);
        }

        List<Unit> toRemove = null;
        foreach (var kv in icons)
        {
            if (kv.Key == null || !alive.Contains(kv.Key))
            {
                if (toRemove == null) toRemove = new List<Unit>();
                toRemove.Add(kv.Key);
            }
        }

        if (toRemove != null)
        {
            foreach (Unit dead in toRemove)
            {
                if (icons.TryGetValue(dead, out TacticalWorldUnitIcon ic) && ic != null)
                    Destroy(ic.gameObject);
                icons.Remove(dead);
            }
        }
    }

    private void CreateIcon(Unit unit)
    {
        if (iconPrefab == null || iconsRoot == null || unit == null) return;

        TacticalWorldUnitIcon icon = Instantiate(iconPrefab, iconsRoot);
        icon.Setup(unit, this);
        icons[unit] = icon;
    }

    private void UpdateIconScreenPositions(Camera tacCam)
    {
        bool deployment = GameManager.Instance != null && GameManager.Instance.IsArmyDeploymentPhase();
        float zoomT = GetTacticalZoomT(tacCam);
        float iconSize = Mathf.Lerp(nearIconSizePixels, farIconSizePixels, zoomT);
        visibleIconLayout.Clear();

        foreach (var kv in icons)
        {
            Unit unit = kv.Key;
            TacticalWorldUnitIcon icon = kv.Value;
            if (unit == null || icon == null) continue;

            // Во время кино-показа хода бота: прячем BF-иконки для тех юнитов, чьи 3D-модели показываем.
            if (CameraManager.Instance != null && CameraManager.Instance.ShouldHideTacticalIconDuringCinematic(unit))
            {
                icon.gameObject.SetActive(false);
                if (lastHoverFromIcon == unit)
                    ClearHoveredIcon();
                continue;
            }
            else if (!icon.gameObject.activeSelf)
            {
                icon.gameObject.SetActive(true);
            }

            RectTransform rt = icon.transform as RectTransform;
            if (rt == null) continue;

            Vector3 world;
            if (deployment && ChessGrid.Instance != null)
            {
                // В расстановке привязываем иконку строго к центру клетки,
                // иначе при высоких позициях юнита/пивота иконка визуально "уплывает" и перекрывает соседние клетки.
                Vector2Int gp = unit.currentGridPosition;
                world = ChessGrid.Instance.GridToWorldPosition(gp.x, gp.y) + Vector3.up * worldOffsetY;
            }
            else
            {
                Vector3 basePos = unit.transform.position;
                if (GameManager.Instance != null && GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot &&
                    unit.owner == Player.Player2 &&
                    FogWarIntelTracker.Instance != null &&
                    FogWarIntelTracker.Instance.TryGetDisplayPosition(unit, out Vector3 displayPos, out _))
                {
                    basePos = displayPos;
                }

                world = basePos + Vector3.up * worldOffsetY;
            }
            Vector3 screen = tacCam.WorldToScreenPoint(world);

            bool behind = hideIconsWhenBehindCamera && screen.z < 0.1f;
            icon.gameObject.SetActive(!behind);
            if (behind)
            {
                if (lastHoverFromIcon == unit)
                    ClearHoveredIcon();
                continue;
            }

            icon.RefreshVisuals();
            icon.SetSquareSize(iconSize);
            visibleIconLayout.Add(new IconLayoutEntry
            {
                Icon = icon,
                Rect = rt,
                ScreenPosition = new Vector2(screen.x, screen.y)
            });
        }

        foreach (IconLayoutEntry entry in visibleIconLayout)
        {
            if (entry.Icon == null || entry.Rect == null) continue;
            entry.Rect.position = new Vector3(entry.ScreenPosition.x, entry.ScreenPosition.y, 0f);
        }
    }

    private float GetTacticalZoomT(Camera tacCam)
    {
        if (tacCam == null)
            return 0f;

        float zoomValue = tacCam.orthographic
            ? tacCam.orthographicSize
            : Mathf.Max(0f, tacCam.transform.position.y);

        float near = Mathf.Min(nearOrthographicSize, farOrthographicSize);
        float far = Mathf.Max(nearOrthographicSize, farOrthographicSize);
        if (Mathf.Approximately(near, far))
            return 0f;

        return Mathf.InverseLerp(near, far, zoomValue);
    }

    private void UpdateAttackRangeRing(Camera tacCam)
    {
        if (!showRangedAttackRangeOnHover)
        {
            HideAttackRangeRing();
            return;
        }

        Unit hovered = ResolveHoveredRangeUnit(tacCam);
        if (hovered == null || hovered.GetHealth() <= 0 || !hovered.TryGetRangedAttackRangeMeters(out float rangeMeters))
        {
            HideAttackRangeRing();
            return;
        }

        EnsureAttackRangeRing();
        if (rangeRing == null) return;

        rangeRing.gameObject.SetActive(true);
        rangeRing.startColor = rangeRingColor;
        rangeRing.endColor = rangeRingColor;
        rangeRing.startWidth = Mathf.Max(0.01f, rangeRingWidth);
        rangeRing.endWidth = Mathf.Max(0.01f, rangeRingWidth);

        int segments = Mathf.Clamp(rangeRingSegments, 24, 256);
        rangeRing.loop = true;
        rangeRing.positionCount = segments;

        Vector3 center = hovered.transform.position + Vector3.up * rangeRingYOffset;
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments;
            float angle = t * Mathf.PI * 2f;
            Vector3 p = center + new Vector3(Mathf.Cos(angle) * rangeMeters, 0f, Mathf.Sin(angle) * rangeMeters);
            rangeRing.SetPosition(i, p);
        }
    }

    private Unit ResolveHoveredRangeUnit(Camera tacCam)
    {
        if (lastHoverFromIcon != null && IsValidRangeHoverUnit(lastHoverFromIcon))
            return lastHoverFromIcon;
        lastHoverFromIcon = null;

        if (tacCam == null || Mouse.current == null)
            return null;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = tacCam.ScreenPointToRay(mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
            return null;

        Unit unit = hit.collider != null ? hit.collider.GetComponentInParent<Unit>() : null;
        if (unit == null && hit.collider != null)
            unit = hit.collider.GetComponent<Unit>();

        return IsValidRangeHoverUnit(unit) ? unit : null;
    }

    private static bool IsValidRangeHoverUnit(Unit unit)
    {
        if (unit == null || unit.GetHealth() <= 0)
            return false;

        if (GameManager.Instance != null && unit.owner != GameManager.Instance.currentPlayer)
            return false;

        return unit.TryGetRangedAttackRangeMeters(out _);
    }

    private void ClearHoveredIcon()
    {
        lastHoverFromIcon = null;
    }

    private void EnsureAttackRangeRing()
    {
        if (rangeRing != null) return;

        GameObject go = new GameObject("TacticalAttackRangeRing");
        go.transform.SetParent(transform, false);

        rangeRing = go.AddComponent<LineRenderer>();
        rangeRing.useWorldSpace = true;
        rangeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rangeRing.receiveShadows = false;
        rangeRing.textureMode = LineTextureMode.Stretch;
        rangeRing.alignment = LineAlignment.View;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader != null)
        {
            runtimeRangeRingMaterial = new Material(shader);
            runtimeRangeRingMaterial.color = rangeRingColor;
            rangeRing.material = runtimeRangeRingMaterial;
        }

        go.SetActive(false);
    }

    private void HideAttackRangeRing()
    {
        if (rangeRing != null)
            rangeRing.gameObject.SetActive(false);
    }

    public void NotifyIconClicked(Unit unit)
    {
        if (CameraManager.Instance != null)
            CameraManager.Instance.TrySwitchToActionModeFromMap(unit);
    }

    public void NotifyIconPointerEnter(Unit unit)
    {
        lastHoverFromIcon = unit;
    }

    public void NotifyIconPointerExit(Unit unit)
    {
        if (lastHoverFromIcon == unit)
            lastHoverFromIcon = null;
    }

    public Unit GetHoveredIconUnit() => lastHoverFromIcon;
}
