using System.Collections.Generic;
using UnityEngine;

// Типы шахматных фигур
public enum ChessUnitType
{
    Pawn,    // Пешка
    Horse,   // Конь
    Bishop,  // Слон
    Queen,   // Ферзь
    King,    // Король
    Guardian // Кастомный класс
}

public class ChessRulesManager : MonoBehaviour
{
    public static ChessRulesManager Instance;
    
    [Header("Setup")]
    [Tooltip("Если выключено, менеджер НЕ будет вызывать SnapToGrid() для всех юнитов на старте. Для mission1 лучше выключить.")]
    [SerializeField] private bool snapUnitsToGridOnStart = true;

    // Список для отслеживания всех активных юнитов на доске
    private List<Unit> allUnits = new List<Unit>();

    void Awake()
    {
        // Паттерн Синглтон
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        InitializeBoard();
    }

    private void InitializeBoard()
    {
        Unit[] foundUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);

        foreach (var unit in foundUnits)
        {
            if (snapUnitsToGridOnStart && ChessGrid.Instance != null)
            {
                unit.SnapToGrid();
            }

            allUnits.Add(unit);
        }
    }

    /**
     * Проверяет, является ли ход из точки START в END легальным для данного типа фигуры.
     * @param type - Тип фигуры (Pawn, Horse, etc.)
     * @param start - Координаты сетки начала (Vector2Int)
     * @param end - Координаты сетки конца (Vector2Int)
     * @param isFirstMove - Используется только для Пешки
     * @return true, если ход разрешен правилами
     */
    public bool IsMoveValid(ChessUnitType type, Vector2Int start, Vector2Int end, bool isFirstMove = false)
    {
        int dx = Mathf.Abs(end.x - start.x);
        int dy = Mathf.Abs(end.y - start.y);
        
        // Перемещение на 0 клеток не считается ходом
        if (dx == 0 && dy == 0) return false; 
        
        // Если движение превышает 2 клетки, оно считается свободным (Free Movement)
        // Но для проверки по правилам шахмат, нам нужно проверить точную дальность.
        // Здесь мы пока проверяем только паттерн хода, а не дальность (Rook, Queen)
        
        switch (type)
        {
            case ChessUnitType.Pawn: // Пешка
                // Для упрощения, просто проверяем, что она идет на 1 клетку вперед по оси Y (или 2, если первый ход)
                if (dx == 1 && dy == 0) return true; 
                if (isFirstMove && dx == 2 && dy == 0) return true;
                return false;

            case ChessUnitType.Horse: // Конь (2 в одну сторону, 1 в другую) - буква Г
                return (dx == 1 && dy == 2) || (dx == 2 && dy == 1);

            case ChessUnitType.Guardian: // Ладья (Только вертикально или горизонтально)
                return (dx == 0 || dy == 0);

            case ChessUnitType.Bishop: // Слон (Только по диагонали)
                return (dx == dy);

            case ChessUnitType.Queen: // Ферзь (Ладья + Слон)
                return (dx == 0 || dy == 0 || dx == dy);

            case ChessUnitType.King: // Король (На 1 клетку в любом направлении)
                return (dx <= 1 && dy <= 1);

        }

        return false;
    }
    
    /// <summary>
    /// Получает все возможные ходы для коня из указанной позиции (все 8 позиций буквой Г)
    /// </summary>
    /// <param name="start">Начальная позиция коня</param>
    /// <returns>Список всех возможных позиций для хода коня</returns>
    public List<Vector2Int> GetHorsePossibleMoves(Vector2Int start)
    {
        List<Vector2Int> possibleMoves = new List<Vector2Int>();
        
        // Все 8 возможных ходов коня (буквой Г)
        int[] dx = { 2, 2, -2, -2, 1, 1, -1, -1 };
        int[] dy = { 1, -1, 1, -1, 2, -2, 2, -2 };
        
        for (int i = 0; i < 8; i++)
        {
            Vector2Int targetPos = new Vector2Int(start.x + dx[i], start.y + dy[i]);
            
            // Проверяем, что позиция в пределах доски
            if (ChessGrid.Instance != null && ChessGrid.Instance.IsValidCoord(targetPos.x, targetPos.y))
            {
                possibleMoves.Add(targetPos);
            }
        }
        
        return possibleMoves;
    }
}