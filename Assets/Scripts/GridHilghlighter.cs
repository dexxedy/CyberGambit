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
        if (ChessGrid.Instance == null || ChessRulesManager.Instance == null || highlightPrefab == null) return;
        
        ClearHighlights();
        
        // Получаем текущую позицию юнита в координатах сетки
        Vector2Int currentPos = ChessGrid.Instance.WorldToGridCoords(unit.transform.position);
        ChessUnitType type = unit.chessType;
        bool isFirstMove = unit.isFirstMove;
        
        // Для коня используем специальный метод, который показывает все возможные ходы (все 8 позиций буквой Г)
        if (type == ChessUnitType.Horse)
        {
            List<Vector2Int> horseMoves = ChessRulesManager.Instance.GetHorsePossibleMoves(currentPos);
            
            foreach (Vector2Int targetPos in horseMoves)
            {
                // Создаем подсветку для каждой возможной позиции коня
                Vector3 worldPos = ChessGrid.Instance.GridToWorldPosition(targetPos.x, targetPos.y);
                worldPos.y += 0.01f; // Немного поднимаем подсветку над сеткой
                
                GameObject highlight = Instantiate(highlightPrefab, worldPos, Quaternion.identity, this.transform);
                activeHighlights.Add(highlight);
            }
        }
        else
        {
            // Для остальных фигур используем стандартный метод перебора всех клеток
            for (int x = 0; x < ChessGrid.Instance.width; x++)
            {
                for (int y = 0; y < ChessGrid.Instance.height; y++)
                {
                    Vector2Int targetPos = new Vector2Int(x, y);

                    // Нет смысла подсвечивать клетку, на которой мы стоим
                    if (currentPos == targetPos) continue; 

                    // Проверяем, легален ли ход на эту клетку
                    if (ChessRulesManager.Instance.IsMoveValid(type, currentPos, targetPos, isFirstMove))
                    {
                        // Ход легален, создаем объект подсветки
                        Vector3 worldPos = ChessGrid.Instance.GridToWorldPosition(x, y);
                        
                        // Немного поднимаем подсветку над сеткой (например, на 0.01)
                        worldPos.y += 0.01f; 
                        
                        GameObject highlight = Instantiate(highlightPrefab, worldPos, Quaternion.identity, this.transform);
                        activeHighlights.Add(highlight);
                    }
                }
            }
        }
    }
}