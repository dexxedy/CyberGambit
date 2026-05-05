using System.Collections.Generic;
using UnityEngine;

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

    private readonly Dictionary<Unit, TacticalWorldUnitIcon> icons = new Dictionary<Unit, TacticalWorldUnitIcon>();
    private Unit lastHoverFromIcon;

    public bool IsConfigured()
    {
        return iconPrefab != null && iconsRoot != null;
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
    }

    private void LateUpdate()
    {
        if (CameraManager.Instance == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused()) return;

        bool tactical = !CameraManager.Instance.IsActionMode();
        if (!IsConfigured())
            return;

        if (iconsRoot != null)
            iconsRoot.gameObject.SetActive(tactical);

        if (!tactical)
            return;

        Camera tacCam = CameraManager.Instance.GetTacticalCamera();
        if (tacCam == null) return;

        SyncIconsWithLivingUnits();
        UpdateIconScreenPositions(tacCam);
    }

    private void SyncIconsWithLivingUnits()
    {
        Unit[] all = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        HashSet<Unit> alive = new HashSet<Unit>();

        foreach (Unit u in all)
        {
            if (u == null || u.GetHealth() <= 0) continue;
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
        foreach (var kv in icons)
        {
            Unit unit = kv.Key;
            TacticalWorldUnitIcon icon = kv.Value;
            if (unit == null || icon == null) continue;

            RectTransform rt = icon.transform as RectTransform;
            if (rt == null) continue;

            Vector3 world = unit.transform.position + Vector3.up * worldOffsetY;
            Vector3 screen = tacCam.WorldToScreenPoint(world);

            bool behind = hideIconsWhenBehindCamera && screen.z < 0.1f;
            icon.gameObject.SetActive(!behind);
            if (behind) continue;

            rt.position = new Vector3(screen.x, screen.y, 0f);
            icon.RefreshVisuals();
        }
    }

    public void NotifyIconClicked(Unit unit)
    {
        if (CameraManager.Instance != null)
            CameraManager.Instance.TrySwitchToActionModeFromMap(unit);
    }

    public void NotifyIconPointerEnter(Unit unit)
    {
        lastHoverFromIcon = unit;
        if (TacticalModeUI.Instance != null)
            TacticalModeUI.Instance.ShowUnitTooltipFromIcon(unit);
    }

    public void NotifyIconPointerExit(Unit unit)
    {
        if (lastHoverFromIcon == unit)
            lastHoverFromIcon = null;
        if (TacticalModeUI.Instance != null)
            TacticalModeUI.Instance.ClearIconTooltipIf(unit);
    }

    public Unit GetHoveredIconUnit()
    {
        return lastHoverFromIcon;
    }
}
