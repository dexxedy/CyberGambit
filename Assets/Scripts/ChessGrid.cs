using UnityEngine;
using TMPro; // Если захочешь выводить текст координат в мире

public class ChessGrid : MonoBehaviour
{
    public static ChessGrid Instance;

    [Header("Настройки сетки")]
    [SerializeField] public int width = 8;
    [SerializeField] public int height = 8;
    [SerializeField] public float cellSize = 2.0f; // Размер одной клетки в метрах Unity
    [SerializeField] private Vector3 originPosition = Vector3.zero; // Где начинается клетка A1

    [Header("Визуализация")]
    [SerializeField] private bool showGrid = true;
    [SerializeField] private Color gridColor = Color.green;

    private void Awake()
    {
        Instance = this;
    }

    // --- Конвертация координат ---

    // Перевод: Мировая позиция -> Координаты сетки (0,0)
    public Vector2Int WorldToGridCoords(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - originPosition.x) / cellSize);
        int y = Mathf.FloorToInt((worldPos.z - originPosition.z) / cellSize);
        return new Vector2Int(x, y);
    }

    // Перевод: Координаты сетки (0,0) -> Мировая позиция (центр клетки)
    public Vector3 GridToWorldPosition(int x, int y)
    {
        return new Vector3(x * cellSize, 0, y * cellSize) + originPosition + new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f);
    }

    // Перевод: Координаты (0,0) -> Шахматная нотация ("A1", "C4")
    public string GridToChessNotation(int x, int y)
    {
        if (!IsValidCoord(x, y)) return "Out";
        char letter = (char)('A' + x);
        int number = y + 1;
        return $"{letter}:{number}";
    }

    // Перевод: Шахматная нотация ("A1") -> Координаты (0,0)
    public Vector2Int ChessNotationToGrid(string notation)
    {
        if (string.IsNullOrEmpty(notation) || notation.Length < 3 || notation[1] != ':')
        {
            Debug.LogError($"Неверный формат координат: {notation}. Используйте формат 'A:1'");
            return new Vector2Int(-1, -1);
        }

        string[] parts = notation.Split(':');
        char letter = parts[0][0]; // 'A'
        if (!int.TryParse(parts[1], out int number)) return new Vector2Int(-1, -1);

        int x = char.ToUpper(letter) - 'A';
        int y = number - 1;

        return new Vector2Int(x, y);
    }

    public bool IsValidCoord(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    // --- Рисование сетки (для дебага и игры) ---
    private void OnDrawGizmos()
    {
        if (!showGrid) return;

        Gizmos.color = gridColor;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Рисуем квадраты
                Vector3 bottomLeft = new Vector3(x * cellSize, 0, y * cellSize) + originPosition;
                Vector3 bottomRight = new Vector3((x + 1) * cellSize, 0, y * cellSize) + originPosition;
                Vector3 topLeft = new Vector3(x * cellSize, 0, (y + 1) * cellSize) + originPosition;
                Vector3 topRight = new Vector3((x + 1) * cellSize, 0, (y + 1) * cellSize) + originPosition;

                Gizmos.DrawLine(bottomLeft, bottomRight);
                Gizmos.DrawLine(bottomLeft, topLeft);
                Gizmos.DrawLine(topLeft, topRight);
                Gizmos.DrawLine(bottomRight, topRight);
            }
        }
    }
    
}