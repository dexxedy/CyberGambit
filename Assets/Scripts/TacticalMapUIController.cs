using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TacticalMapUIController : MonoBehaviour
{
    public enum MapOrientationMode
    {
        Default,
        SwapXY,
        SwapXYFlipY
    }

    public static TacticalMapUIController Instance;

    [Header("Map Root")]
    [SerializeField] private GameObject mapRoot;
    [SerializeField] private RectTransform mapRect;
    [SerializeField] private RectTransform iconContainer;

    [Header("Icon Prefabs")]
    [SerializeField] private TacticalMapUnitIcon genericIconPrefab;
    [SerializeField] private TacticalMapUnitIcon player1IconPrefab;
    [SerializeField] private TacticalMapUnitIcon player2IconPrefab;
    [SerializeField] private TacticalMapUnitIcon player1KingIconPrefab;
    [SerializeField] private TacticalMapUnitIcon player2KingIconPrefab;

    [Header("Map Motion")]
    [SerializeField] private bool smoothMove = true;
    [SerializeField] private float smoothSpeed = 12f;

    [Header("Map Orientation")]
    [SerializeField] private MapOrientationMode orientationMode = MapOrientationMode.Default;
    
    [Header("World Mapping (Free Minimap)")]
    [Tooltip("Если включено, иконки на карте берутся из world-позиции (X/Z) вместо ChessGrid.")]
    [SerializeField] private bool useWorldSpaceMapping = true;
    [Tooltip("World bounds по XZ, которые соответствуют прямоугольнику миникарты.")]
    [SerializeField] private Vector2 worldMinXZ = new Vector2(0f, 0f);
    [SerializeField] private Vector2 worldMaxXZ = new Vector2(16f, 16f);

    private readonly Dictionary<Unit, TacticalMapUnitIcon> iconsByUnit = new Dictionary<Unit, TacticalMapUnitIcon>();
    private readonly Dictionary<Unit, Vector2> targetAnchoredPos = new Dictionary<Unit, Vector2>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        Unit.OnGridPositionChanged += OnUnitGridPositionChanged;
    }

    private void OnDisable()
    {
        Unit.OnGridPositionChanged -= OnUnitGridPositionChanged;
    }

    private void Start()
    {
        RefreshAllIconsFromUnits();
        ShowMap();
    }

    private void Update()
    {
        List<Unit> invalidUnits = null;
        foreach (var pair in iconsByUnit)
        {
            if (pair.Key == null || pair.Value == null || pair.Key.GetHealth() <= 0)
            {
                if (invalidUnits == null) invalidUnits = new List<Unit>();
                invalidUnits.Add(pair.Key);
            }
        }

        if (invalidUnits != null)
        {
            foreach (Unit deadUnit in invalidUnits)
            {
                RemoveUnitIcon(deadUnit);
            }
        }

        // В free-minimap режиме постоянно обновляем цель для иконок от world-позиции.
        if (useWorldSpaceMapping)
        {
            foreach (var pair in iconsByUnit)
            {
                Unit unit = pair.Key;
                if (unit == null || unit.GetHealth() <= 0) continue;

                // PvBot fog-of-war: врагов показываем только если они были замечены в экшене.
                if (GameManager.Instance != null && GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot &&
                    unit.owner == Player.Player2)
                {
                    if (EnemyIntelTracker.Instance == null || !EnemyIntelTracker.Instance.IsSpotted(unit))
                        continue;

                    if (EnemyIntelTracker.Instance.TryGetLastKnown(unit, out Vector3 lastKnown))
                        MoveIconToWorld(unit, lastKnown, snapInstantly: false);
                    else
                        continue;
                }
                else
                {
                    MoveIconToWorld(unit, unit.transform.position, snapInstantly: false);
                }
            }
        }

        if (!smoothMove) return;

        foreach (var pair in iconsByUnit)
        {
            Unit unit = pair.Key;
            TacticalMapUnitIcon icon = pair.Value;
            if (unit == null || icon == null) continue;

            RectTransform iconRect = icon.transform as RectTransform;
            if (iconRect == null) continue;
            if (!targetAnchoredPos.TryGetValue(unit, out Vector2 targetPos)) continue;

            iconRect.anchoredPosition = Vector2.Lerp(
                iconRect.anchoredPosition,
                targetPos,
                Time.deltaTime * smoothSpeed
            );
        }
    }

    public void ShowMap()
    {
        if (mapRoot != null) mapRoot.SetActive(true);
    }

    public void HideMap()
    {
        if (mapRoot != null) mapRoot.SetActive(false);
    }

    public void RefreshAllIconsFromUnits()
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        HashSet<Unit> aliveUnits = new HashSet<Unit>();

        foreach (Unit unit in allUnits)
        {
            if (unit == null || unit.GetHealth() <= 0) continue;
            aliveUnits.Add(unit);

            // PvBot fog-of-war: не создаём иконки врагов до обнаружения.
            if (GameManager.Instance != null && GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot &&
                unit.owner == Player.Player2)
            {
                if (EnemyIntelTracker.Instance == null || !EnemyIntelTracker.Instance.IsSpotted(unit))
                    continue;
            }

            EnsureIconExists(unit);
            if (useWorldSpaceMapping)
            {
                if (GameManager.Instance != null && GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot &&
                    unit.owner == Player.Player2 &&
                    EnemyIntelTracker.Instance != null &&
                    EnemyIntelTracker.Instance.TryGetLastKnown(unit, out Vector3 lastKnown))
                {
                    MoveIconToWorld(unit, lastKnown, !smoothMove);
                }
                else
                {
                    MoveIconToWorld(unit, unit.transform.position, !smoothMove);
                }
            }
            else
            {
                MoveIconToGrid(unit, unit.currentGridPosition, !smoothMove);
            }
        }

        List<Unit> toRemove = new List<Unit>();
        foreach (var pair in iconsByUnit)
        {
            if (pair.Key == null || !aliveUnits.Contains(pair.Key))
            {
                toRemove.Add(pair.Key);
            }
        }

        foreach (Unit deadUnit in toRemove)
        {
            RemoveUnitIcon(deadUnit);
        }
    }

    public void OnUnitIconClicked(Unit unit)
    {
        if (unit == null || GameManager.Instance == null || CameraManager.Instance == null) return;
        if (GameManager.Instance.IsArmyDeploymentPhase()) return;
        if (CameraManager.Instance.IsActionMode()) return;
        if (GameManager.Instance.IsPaused()) return;
        if (unit.owner != GameManager.Instance.currentPlayer) return;
        if (GameManager.Instance.IsBotTurn()) return;
        if (!GameManager.Instance.HasRolledDiceThisTurn()) return;

        CameraManager.Instance.TrySwitchToActionModeFromMap(unit);
    }

    private void OnUnitGridPositionChanged(Unit unit, Vector2Int oldPos, Vector2Int newPos)
    {
        if (useWorldSpaceMapping) return;
        if (unit == null || unit.GetHealth() <= 0)
        {
            RemoveUnitIcon(unit);
            return;
        }

        EnsureIconExists(unit);
        MoveIconToGrid(unit, newPos, false);
    }

    private void EnsureIconExists(Unit unit)
    {
        if (unit == null || iconsByUnit.ContainsKey(unit)) return;

        // PvBot fog-of-war safety: не создаём врагов, пока они не spotted.
        if (GameManager.Instance != null && GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot &&
            unit.owner == Player.Player2)
        {
            if (EnemyIntelTracker.Instance == null || !EnemyIntelTracker.Instance.IsSpotted(unit))
                return;
        }

        TacticalMapUnitIcon prefab = SelectIconPrefab(unit);
        if (prefab == null) return;

        RectTransform parent = iconContainer != null ? iconContainer : mapRect;
        TacticalMapUnitIcon icon = Instantiate(prefab, parent);
        icon.Initialize(unit, this);
        iconsByUnit[unit] = icon;
    }

    private TacticalMapUnitIcon SelectIconPrefab(Unit unit)
    {
        if (unit.chessType == ChessUnitType.King)
        {
            if (unit.owner == Player.Player1 && player1KingIconPrefab != null) return player1KingIconPrefab;
            if (unit.owner == Player.Player2 && player2KingIconPrefab != null) return player2KingIconPrefab;
        }

        if (unit.owner == Player.Player1 && player1IconPrefab != null) return player1IconPrefab;
        if (unit.owner == Player.Player2 && player2IconPrefab != null) return player2IconPrefab;
        return genericIconPrefab;
    }

    private void MoveIconToGrid(Unit unit, Vector2Int gridPos, bool snapInstantly)
    {
        if (!iconsByUnit.TryGetValue(unit, out TacticalMapUnitIcon icon) || icon == null) return;
        Vector2 mapPos = GridToMapAnchoredPosition(gridPos);
        targetAnchoredPos[unit] = mapPos;

        if (snapInstantly || !smoothMove)
        {
            RectTransform iconRect = icon.transform as RectTransform;
            if (iconRect != null) iconRect.anchoredPosition = mapPos;
        }
    }
    
    private void MoveIconToWorld(Unit unit, Vector3 worldPos, bool snapInstantly)
    {
        if (!iconsByUnit.TryGetValue(unit, out TacticalMapUnitIcon icon) || icon == null) return;
        Vector2 mapPos = WorldToMapAnchoredPosition(worldPos);
        targetAnchoredPos[unit] = mapPos;

        if (snapInstantly || !smoothMove)
        {
            RectTransform iconRect = icon.transform as RectTransform;
            if (iconRect != null) iconRect.anchoredPosition = mapPos;
        }
    }
    
    private Vector2 WorldToMapAnchoredPosition(Vector3 worldPos)
    {
        if (mapRect == null) return Vector2.zero;

        float width = mapRect.rect.width;
        float height = mapRect.rect.height;

        float minX = worldMinXZ.x;
        float minZ = worldMinXZ.y;
        float maxX = worldMaxXZ.x;
        float maxZ = worldMaxXZ.y;

        float nx = Mathf.InverseLerp(minX, maxX, worldPos.x);
        float nz = Mathf.InverseLerp(minZ, maxZ, worldPos.z);

        float mapX;
        float mapY;

        switch (orientationMode)
        {
            case MapOrientationMode.SwapXY:
                mapX = nz * width;
                mapY = nx * height;
                break;

            case MapOrientationMode.SwapXYFlipY:
                mapX = nz * width;
                mapY = (1f - nx) * height;
                break;

            case MapOrientationMode.Default:
            default:
                mapX = nx * width;
                mapY = nz * height;
                break;
        }

        return new Vector2(mapX, mapY);
    }

    private Vector2 GridToMapAnchoredPosition(Vector2Int gridPos)
    {
        if (mapRect == null || ChessGrid.Instance == null) return Vector2.zero;

        float width = mapRect.rect.width;
        float height = mapRect.rect.height;
        float cellW = width / Mathf.Max(1, ChessGrid.Instance.width);
        float cellH = height / Mathf.Max(1, ChessGrid.Instance.height);
        float mapX;
        float mapY;

        switch (orientationMode)
        {
            case MapOrientationMode.SwapXY:
                mapX = (gridPos.y + 0.5f) * cellW;
                mapY = (gridPos.x + 0.5f) * cellH;
                break;

            case MapOrientationMode.SwapXYFlipY:
                mapX = (gridPos.y + 0.5f) * cellW;
                mapY = height - ((gridPos.x + 0.5f) * cellH);
                break;

            case MapOrientationMode.Default:
            default:
                mapX = (gridPos.x + 0.5f) * cellW;
                mapY = (gridPos.y + 0.5f) * cellH;
                break;
        }

        return new Vector2(mapX, mapY);
    }

    private void RemoveUnitIcon(Unit unit)
    {
        if (ReferenceEquals(unit, null)) return;
        if (!iconsByUnit.TryGetValue(unit, out TacticalMapUnitIcon icon)) return;

        if (icon != null) Destroy(icon.gameObject);
        iconsByUnit.Remove(unit);
        targetAnchoredPos.Remove(unit);
    }
}
