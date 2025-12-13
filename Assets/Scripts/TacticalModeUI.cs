using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Управляет UI в тактическом режиме:
/// - Информация о юните при наведении мыши (имя, владелец, HP, описание, КД способности)
/// </summary>
public class TacticalModeUI : MonoBehaviour
{
    public static TacticalModeUI Instance;
    
    [Header("Unit Info Panel")]
    [SerializeField] private GameObject unitInfoPanel; // Панель с информацией о юните
    [SerializeField] private TextMeshProUGUI unitNameText; // Имя юнита
    [SerializeField] private TextMeshProUGUI ownerText; // Владелец (Игрок 1 / Игрок 2)
    [SerializeField] private TextMeshProUGUI healthText; // HP (например, "HP: 80/100")
    [SerializeField] private TextMeshProUGUI descriptionText; // Описание способности
    [SerializeField] private TextMeshProUGUI cooldownText; // КД способности (например, "КД: 2 хода")
    [SerializeField] private Image healthBarFill; // Полоса здоровья (опционально)
    
    [Header("Settings")]
    [SerializeField] private float raycastDistance = 100f; // Дистанция raycast
    [SerializeField] private LayerMask unitLayer; // Слой юнитов
    
    private Unit currentHoveredUnit = null;
    private Camera tacticalCamera;
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    void Start()
    {
        if (unitInfoPanel != null)
        {
            unitInfoPanel.SetActive(false);
        }
    }
    
    void Update()
    {
        // Показываем информацию только в тактическом режиме
        if (CameraManager.Instance != null && !CameraManager.Instance.IsActionMode())
        {
            UpdateUnitInfo();
        }
        else
        {
            // Скрываем панель в экшен-режиме
            if (unitInfoPanel != null && unitInfoPanel.activeSelf)
            {
                unitInfoPanel.SetActive(false);
            }
            currentHoveredUnit = null;
        }
    }
    
    /// <summary>
    /// Обновляет информацию о юните под курсором
    /// </summary>
    private void UpdateUnitInfo()
    {
        if (tacticalCamera == null)
        {
            tacticalCamera = CameraManager.Instance != null ? CameraManager.Instance.GetTacticalCamera() : null;
        }
        
        if (tacticalCamera == null || unitInfoPanel == null) return;
        
        // Raycast из позиции мыши
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = tacticalCamera.ScreenPointToRay(mousePosition);
        RaycastHit hit;
        
        Unit hoveredUnit = null;
        
        if (Physics.Raycast(ray, out hit, raycastDistance, unitLayer))
        {
            hoveredUnit = hit.collider.GetComponentInParent<Unit>();
            if (hoveredUnit == null)
            {
                hoveredUnit = hit.collider.GetComponent<Unit>();
            }
        }
        
        // Если нашли юнит, показываем информацию
        if (hoveredUnit != null)
        {
            if (currentHoveredUnit != hoveredUnit)
            {
                currentHoveredUnit = hoveredUnit;
            }
            
            if (!unitInfoPanel.activeSelf)
            {
                unitInfoPanel.SetActive(true);
            }
            
            UpdateInfoDisplay(hoveredUnit);
        }
        else
        {
            // Нет юнита под курсором - скрываем панель
            if (unitInfoPanel.activeSelf)
            {
                unitInfoPanel.SetActive(false);
            }
            currentHoveredUnit = null;
        }
    }
    
    /// <summary>
    /// Обновляет отображение информации о юните
    /// </summary>
    private void UpdateInfoDisplay(Unit unit)
    {
        if (unit == null) return;
        
        // Имя юнита
        if (unitNameText != null)
        {
            unitNameText.text = GetUnitDisplayName(unit.chessType);
        }
        
        // Владелец
        if (ownerText != null)
        {
            string ownerName = unit.owner == Player.Player1 ? "Игрок 1" : "Игрок 2";
            string currentPlayerName = GameManager.Instance != null && GameManager.Instance.currentPlayer == unit.owner ? " (Ваш)" : " (Враг)";
            ownerText.text = $"Владелец: {ownerName}{currentPlayerName}";
        }
        
        // HP
        if (healthText != null)
        {
            int currentHP = unit.GetHealth();
            int maxHP = unit.GetMaxHealth();
            healthText.text = $"HP: {currentHP}/{maxHP}";
        }
        
        // Полоса здоровья (если есть)
        if (healthBarFill != null)
        {
            float healthPercent = (float)unit.GetHealth() / (float)unit.GetMaxHealth();
            healthBarFill.fillAmount = healthPercent;
        }
        
        // Описание способности
        if (descriptionText != null)
        {
            descriptionText.text = GetAbilityDescription(unit.chessType);
        }
        
        // КД способности
        if (cooldownText != null)
        {
            UnitAbilities abilities = unit.GetComponent<UnitAbilities>();
            if (abilities != null)
            {
                int cooldown = abilities.GetAbilityCooldown();
                if (cooldown > 0)
                {
                    cooldownText.text = $"КД способности: {cooldown} ход(ов)";
                }
                else
                {
                    cooldownText.text = "Способность готова";
                }
            }
            else
            {
                cooldownText.text = "Нет способности";
            }
        }
    }
    
    /// <summary>
    /// Получает отображаемое имя юнита
    /// </summary>
    private string GetUnitDisplayName(ChessUnitType type)
    {
        switch (type)
        {
            case ChessUnitType.Pawn: return "Пешка";
            case ChessUnitType.Horse: return "Конь";
            case ChessUnitType.Bishop: return "Слон";
            case ChessUnitType.Guardian: return "Ладья";
            case ChessUnitType.Queen: return "Ферзь";
            case ChessUnitType.King: return "Король";
            default: return "Неизвестно";
        }
    }
    
    /// <summary>
    /// Получает описание способности юнита
    /// </summary>
    private string GetAbilityDescription(ChessUnitType type)
    {
        switch (type)
        {
            case ChessUnitType.Pawn:
                return "Пешка не имеет активной способности.";
            case ChessUnitType.Horse:
                return "Способность: Скачок - телепортация на ближайшую валидную клетку в направлении взгляда. КД: 1 ход.";
            case ChessUnitType.Bishop:
                return "Способность: Отражение - отражает весь входящий урон обратно атакующему на следующий ход врага. КД: 3 хода.";
            case ChessUnitType.Guardian:
                return "Способность: Щит - снижает входящий урон на 50% на следующий ход врага. КД: 3 хода.";
            case ChessUnitType.Queen:
                return "Способность: Усиление - увеличивает урон на 20% в текущем ходу. КД: 5 ходов.";
            case ChessUnitType.King:
                return "Способность: Исцеление - восстанавливает 20% HP выбранному союзному юниту. КД: 4 хода.";
            default:
                return "Нет описания.";
        }
    }
}

