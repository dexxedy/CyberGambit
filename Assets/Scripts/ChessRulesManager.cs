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
        if (ChessGrid.Instance == null)
        {
            Debug.LogError("Отсутствует ChessGrid на сцене! Невозможно инициализировать доску.");
            return;
        }

        // 1. Находим всех юнитов, которые были расставлены вручную в редакторе
        // Используем FindObjectsByType вместо устаревшего FindObjectsOfType
        Unit[] foundUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        foreach (var unit in foundUnits)
        {
            // 2. Юнит сам вычисляет ближайшую клетку и "примагничивается" к ней
            unit.SnapToGrid();
            
            // 3. Добавляем юнита в список менеджера для дальнейшего учета
            allUnits.Add(unit);
        }

        Debug.Log($"Игра началась! Найдено и инициализировано юнитов на доске: {allUnits.Count}");
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

            case ChessUnitType.Horse: // Конь (2 в одну сторону, 1 в другую)
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
}