using UnityEngine;
using System.Collections.Generic;

public class GridHighlighter : MonoBehaviour
{
    // Префаб, который будет использоваться для подсветки (например, полупрозрачный квадрат)
    [SerializeField] private GameObject highlightPrefab;
    private List<GameObject> activeHighlights = new List<GameObject>();

    // Статическая ссылка для удобства доступа
    public static GridHighlighter Instance;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 1. Очистка всей подсветки
    public void ClearHighlights()
    {
        foreach (GameObject highlight in activeHighlights)
        {
            Destroy(highlight);
        }
        activeHighlights.Clear();
    }

    // 2. Отображение разрешенных ходов
    public void ShowAllowedMoves(Unit unit)
    {
        if (ChessGrid.Instance == null || highlightPrefab == null || unit == null) return;
        
        ClearHighlights();
        
        // Получаем текущую позицию юнита в координатах сетки
        Vector2Int currentPos = ChessGrid.Instance.WorldToGridCoords(unit.transform.position);
        float remainingMeters = Mathf.Max(0f, unit.GetRemainingMoveMeters());
        float cellMoveCost = ChessGrid.Instance.cellSize;
        if (remainingMeters < Mathf.Max(0.1f, cellMoveCost * 0.5f)) return;

        // Подсветка показывает только "следующий шаг" (ортогонально).
        // Первый шаг "только вперед" — только пока фигура ни разу не ходила за всю игру.
        Vector2Int forward = GameManager.Instance != null
            ? GameManager.Instance.GetForwardDirection(unit.owner)
            : (unit.owner == Player.Player1 ? new Vector2Int(0, 1) : new Vector2Int(0, -1));

        Vector2Int[] dirs = (unit.HasMovedAtLeastOnce() || unit.HasMovedThisTurn())
            ? new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) }
            : new[] { forward };

        foreach (var d in dirs)
        {
            Vector2Int next = currentPos + d;
            if (!ChessGrid.Instance.IsValidCoord(next.x, next.y)) continue;
            if (IsCellOccupied(next, unit)) continue;

            Vector3 worldPos = ChessGrid.Instance.GridToWorldPosition(next.x, next.y);
            worldPos.y += 0.01f;
            GameObject highlight = Instantiate(highlightPrefab, worldPos, Quaternion.identity, this.transform);
            activeHighlights.Add(highlight);
        }
    }
    
    private bool IsCellOccupied(Vector2Int gridPos, Unit excludeUnit)
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        foreach (var u in allUnits)
        {
            if (u == null || u == excludeUnit) continue;
            if (u.GetHealth() <= 0) continue;
            Vector2Int pos = ChessGrid.Instance.WorldToGridCoords(u.transform.position);
            if (pos == gridPos) return true;
        }
        return false;
    }
}