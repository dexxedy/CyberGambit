using UnityEngine;

public class UnitColorController : MonoBehaviour
{
    // 1. Поля для задания цветов в Инспекторе
    [Header("Player Colors")]
    [SerializeField] private Color player1Color = Color.blue; // Цвет для Player 1 (союзники)
    [SerializeField] private Color player2Color = Color.red;  // Цвет для Player 2 (враги)

    void Start()
    {
        // 2. Находим все юниты на сцене
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        
        if (allUnits.Length == 0)
        {
            Debug.LogWarning("UnitColorInitializer: Юниты на сцене не найдены.");
            return;
        }

        // 3. Применяем цвет к каждому юниту
        foreach (Unit unit in allUnits)
        {
            if (unit == null) continue;

            // Определяем целевой цвет на основе свойства 'owner' из Unit.cs
            Color targetColor = (unit.owner == Player.Player1) ? player1Color : player2Color;

            // Ищем компонент Renderer (может быть на дочернем объекте)
            Renderer unitRenderer = unit.GetComponentInChildren<Renderer>();

            if (unitRenderer != null)
            {
                // Изменяем цвет материала. 
                // ВАЖНО: Используем .material, чтобы не изменять исходный ассет
                Material material = unitRenderer.material; 
                material.color = targetColor;
                
                // В зависимости от шейдера, возможно, потребуется установить _BaseColor
                // material.SetColor("_BaseColor", targetColor);
            }
            else
            {
                Debug.LogWarning($"Unit {unit.gameObject.name} не имеет компонента Renderer, цвет не применен.");
            }
        }
    }
}