using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Управляет всем UI в экшен-режиме:
/// - Прицел в центре экрана
/// - HP врага при наведении прицела
/// - Информация о способности и КД
/// </summary>
public class ActionModeUI : MonoBehaviour
{
    public static ActionModeUI Instance;
    
    [Header("Crosshair")]
    [SerializeField] private Image crosshairImage; // Изображение прицела
    [SerializeField] private Color defaultColor = Color.white; // Цвет прицела по умолчанию
    [SerializeField] private Color enemyColor = Color.red; // Цвет прицела при наведении на врага
    [SerializeField] private Color allyColor = Color.green; // Цвет прицела при наведении на союзника
    
    [Header("Enemy Health")]
    [SerializeField] private GameObject enemyHealthPanel; // Панель с HP врага
    [SerializeField] private TextMeshProUGUI enemyHealthText; // Текст с HP врага
    [SerializeField] private TextMeshProUGUI enemyNameText; // Имя врага (опционально)
    
    [Header("Ability Info")]
    [SerializeField] private GameObject abilityPanel; // Панель с информацией о способности
    [SerializeField] private TextMeshProUGUI abilityNameText; // Название способности
    [SerializeField] private TextMeshProUGUI abilityCooldownText; // КД способности
    [SerializeField] private TextMeshProUGUI abilityDescriptionText; // Описание способности
    
    [Header("Action Mode Stats")]
    [SerializeField] private GameObject timerPanel; // Панель с таймером
    [SerializeField] private TextMeshProUGUI timerText; // Текст таймера
    [SerializeField] private GameObject healthPanel; // Панель с HP
    [SerializeField] private TextMeshProUGUI healthText; // Текст HP
    [SerializeField] private GameObject integrityPanel; // Панель с очками перемещения
    [SerializeField] private TextMeshProUGUI integrityText; // Текст очков перемещения
    
    [Header("HUD Container")]
    [SerializeField] private GameObject actionHUDContainer; // Родительский GameObject для всего HUD экшен-режима
    
    [Header("Settings")]
    [SerializeField] private float raycastDistance = 50f; // Дистанция raycast
    [SerializeField] private float updateInterval = 0.1f; // Как часто обновлять UI (в секундах)
    [SerializeField] private Color readyColor = Color.skyBlue; // Цвет когда способность готова
    [SerializeField] private Color cooldownColor = Color.red; // Цвет когда на КД
    
    private Camera actionCamera;
    private Unit currentTarget = null;
    private Unit currentUnit = null;
    private float lastUpdateTime = 0f;
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    void Start()
    {
        // Скрываем все UI по умолчанию
        if (crosshairImage != null)
            crosshairImage.gameObject.SetActive(false);
        if (enemyHealthPanel != null)
            enemyHealthPanel.SetActive(false);
        if (abilityPanel != null)
            abilityPanel.SetActive(false);
        
        // Скрываем панели статистики по умолчанию
        HideStatsPanels();
    }
    
    void Update()
    {
        // Проверяем паузу - если игра на паузе, не обновляем HUD
        if (GameManager.Instance != null && GameManager.Instance.IsPaused())
        {
            return;
        }
        
        // Работаем только в экшен-режиме
        if (CameraManager.Instance != null && CameraManager.Instance.IsActionMode())
        {
            // Получаем action camera
            if (actionCamera == null)
            {
                actionCamera = CameraManager.Instance.GetActionCamera();
            }
            
            // Обновляем прицел
            UpdateCrosshair();
            
            // Обновляем HP врага с интервалом
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateEnemyHealth();
                lastUpdateTime = Time.time;
            }
            
            // Обновляем информацию о способности
            UpdateAbilityInfo();
            
            // Обновляем статистику (таймер, HP, очки перемещения)
            UpdateStats();
        }
        else
        {
            // Скрываем все UI в тактическом режиме
            HideAllUI();
        }
    }
    
    /// <summary>
    /// Обновляет прицел и его цвет
    /// </summary>
    private void UpdateCrosshair()
    {
        if (crosshairImage == null || actionCamera == null) return;
        
        // Показываем прицел
        if (!crosshairImage.gameObject.activeSelf)
        {
            crosshairImage.gameObject.SetActive(true);
        }
        
        // Получаем юнит под прицелом
        Unit targetUnit = GetUnitUnderCrosshair();
        
        // Обновляем цвет прицела
        if (targetUnit != null)
        {
            Unit currentControlledUnit = CameraManager.Instance.GetCurrentControlledUnit();
            if (currentControlledUnit != null)
            {
                if (targetUnit.owner == currentControlledUnit.owner)
                {
                    crosshairImage.color = allyColor; // Союзник - зеленый
                }
                else
                {
                    crosshairImage.color = enemyColor; // Враг - красный
                }
            }
            else
            {
                crosshairImage.color = defaultColor;
            }
        }
        else
        {
            crosshairImage.color = defaultColor; // Нет цели - белый
        }
    }
    
    /// <summary>
    /// Обновляет отображение HP врага
    /// </summary>
    private void UpdateEnemyHealth()
    {
        if (actionCamera == null || enemyHealthPanel == null) return;
        
        // Получаем юнит под прицелом
        Unit targetUnit = GetUnitUnderCrosshair();
        
        // Проверяем, что это враг
        Unit currentControlledUnit = CameraManager.Instance.GetCurrentControlledUnit();
        if (targetUnit != null && currentControlledUnit != null && targetUnit.owner != currentControlledUnit.owner)
        {
            // Это враг - показываем его HP
            if (currentTarget != targetUnit)
            {
                currentTarget = targetUnit;
            }
            
            if (!enemyHealthPanel.activeSelf)
            {
                enemyHealthPanel.SetActive(true);
            }
            
            // Обновляем текст HP
            if (enemyHealthText != null)
            {
                int currentHP = targetUnit.GetHealth();
                int maxHP = targetUnit.GetMaxHealth();
                enemyHealthText.text = $"HP: {currentHP}/{maxHP}";
            }
            
            // Обновляем имя юнита (если есть)
            if (enemyNameText != null)
            {
                enemyNameText.text = GetUnitDisplayName(targetUnit.chessType);
            }
        }
        else
        {
            // Нет врага под прицелом - скрываем панель
            if (enemyHealthPanel.activeSelf)
            {
                enemyHealthPanel.SetActive(false);
            }
            currentTarget = null;
        }
    }
    
    /// <summary>
    /// Обновляет информацию о способности
    /// </summary>
    private void UpdateAbilityInfo()
    {
        if (abilityPanel == null) return;
        
        Unit controlledUnit = CameraManager.Instance.GetCurrentControlledUnit();
        
        if (controlledUnit != null)
        {
            // Если это пешка - скрываем панель способности
            if (controlledUnit.chessType == ChessUnitType.Pawn)
            {
                if (abilityPanel.activeSelf)
                {
                    abilityPanel.SetActive(false);
                }
                return;
            }
            
            if (currentUnit != controlledUnit)
            {
                currentUnit = controlledUnit;
            }
            
            if (!abilityPanel.activeSelf)
            {
                abilityPanel.SetActive(true);
            }
            
            UpdateAbilityDisplay(controlledUnit);
        }
        else
        {
            if (abilityPanel.activeSelf)
            {
                abilityPanel.SetActive(false);
            }
            currentUnit = null;
        }
    }
    
    /// <summary>
    /// Обновляет отображение информации о способности
    /// </summary>
    private void UpdateAbilityDisplay(Unit unit)
    {
        if (unit == null) return;
        
        // Если это пешка - скрываем панель
        if (unit.chessType == ChessUnitType.Pawn)
        {
            if (abilityPanel != null && abilityPanel.activeSelf)
            {
                abilityPanel.SetActive(false);
            }
            return;
        }
        
        UnitAbilities abilities = unit.GetComponent<UnitAbilities>();
        if (abilities == null)
        {
            // У этого юнита нет способностей - скрываем панель
            if (abilityPanel != null && abilityPanel.activeSelf)
            {
                abilityPanel.SetActive(false);
            }
            return;
        }
        
        // Название способности
        if (abilityNameText != null)
        {
            abilityNameText.text = GetAbilityName(unit.chessType);
        }
        
        // КД способности
        int cooldown = abilities.GetAbilityCooldown();
        bool isReady = cooldown == 0;
        
        if (abilityCooldownText != null)
        {
            if (isReady)
            {
                abilityCooldownText.text = "Готово! (Q)";
                abilityCooldownText.color = readyColor;
            }
            else
            {
                abilityCooldownText.text = $"КД: {cooldown} ход(ов)";
                abilityCooldownText.color = cooldownColor;
            }
        }
        
        // Описание способности
        if (abilityDescriptionText != null)
        {
            abilityDescriptionText.text = GetAbilityDescription(unit.chessType);
        }
    }
    
    /// <summary>
    /// Получает юнит под прицелом
    /// </summary>
    private Unit GetUnitUnderCrosshair()
    {
        if (actionCamera == null) return null;
        
        Ray ray = actionCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, raycastDistance))
        {
            Unit targetUnit = hit.collider.GetComponentInParent<Unit>();
            if (targetUnit == null)
            {
                targetUnit = hit.collider.GetComponent<Unit>();
            }
            return targetUnit;
        }
        
        return null;
    }
    
    /// <summary>
    /// Обновляет статистику (таймер, HP, очки перемещения)
    /// </summary>
    private void UpdateStats()
    {
        Unit controlledUnit = CameraManager.Instance.GetCurrentControlledUnit();
        
        // Обновляем таймер
        if (timerText != null && CameraManager.Instance != null)
        {
            float remainingTime = CameraManager.Instance.GetRemainingTime();
            timerText.text = $"Осталось: {remainingTime:F1} сек";
        }
        
        // Обновляем HP
        if (healthText != null && controlledUnit != null)
        {
            healthText.text = $"HP: {controlledUnit.GetHealth()}";
        }
        
        // Обновляем очки перемещения
        if (integrityText != null && controlledUnit != null)
        {
            integrityText.text = $"Очки перемещения: {controlledUnit.GetRuleIntegrityPoints():F0}";
        }
    }
    
    /// <summary>
    /// Показывает панели статистики (вызывается при входе в экшен-режим)
    /// </summary>
    public void ShowStatsPanels()
    {
        if (timerPanel != null) timerPanel.SetActive(true);
        if (healthPanel != null) healthPanel.SetActive(true);
        if (integrityPanel != null) integrityPanel.SetActive(true);
    }
    
    /// <summary>
    /// Скрывает панели статистики (вызывается при выходе из экшен-режима)
    /// </summary>
    public void HideStatsPanels()
    {
        if (timerPanel != null) timerPanel.SetActive(false);
        if (healthPanel != null) healthPanel.SetActive(false);
        if (integrityPanel != null) integrityPanel.SetActive(false);
    }
    
    /// <summary>
    /// Скрывает весь UI
    /// </summary>
    private void HideAllUI()
    {
        if (crosshairImage != null && crosshairImage.gameObject.activeSelf)
            crosshairImage.gameObject.SetActive(false);
        if (enemyHealthPanel != null && enemyHealthPanel.activeSelf)
            enemyHealthPanel.SetActive(false);
        if (abilityPanel != null && abilityPanel.activeSelf)
            abilityPanel.SetActive(false);
        
        HideStatsPanels();
        
        currentTarget = null;
        currentUnit = null;
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
    /// Получает название способности
    /// </summary>
    private string GetAbilityName(ChessUnitType type)
    {
        switch (type)
        {
            case ChessUnitType.Horse: return "Скачок";
            case ChessUnitType.Bishop: return "Отражение";
            case ChessUnitType.Guardian: return "Щит";
            case ChessUnitType.Queen: return "Усиление";
            case ChessUnitType.King: return "Исцеление";
            default: return "Нет способности";
        }
    }
    
    /// <summary>
    /// Получает описание способности
    /// </summary>
    private string GetAbilityDescription(ChessUnitType type)
    {
        switch (type)
        {
            case ChessUnitType.Horse:
                return "Телепортация на ближайшую валидную клетку в направлении взгляда";
            case ChessUnitType.Bishop:
                return "Отражает весь входящий урон обратно атакующему";
            case ChessUnitType.Guardian:
                return "Снижает входящий урон на 50%";
            case ChessUnitType.Queen:
                return "Увеличивает урон на 20%";
            case ChessUnitType.King:
                return "Восстанавливает 20% HP союзному юниту";
            default:
                return "";
        }
    }
    
    /// <summary>
    /// Получает максимальный КД для типа юнита
    /// </summary>
    private int GetMaxCooldown(ChessUnitType type)
    {
        switch (type)
        {
            case ChessUnitType.Horse: return 1;
            case ChessUnitType.Bishop: return 3;
            case ChessUnitType.Guardian: return 3;
            case ChessUnitType.Queen: return 5;
            case ChessUnitType.King: return 4;
            default: return 0;
        }
    }
    
    /// <summary>
    /// Скрывает весь HUD экшен-режима (вызывается при паузе)
    /// </summary>
    public void HideHUD()
    {
        if (actionHUDContainer != null)
        {
            actionHUDContainer.SetActive(false);
        }
        else
        {
            // Если контейнер не назначен, скрываем все элементы вручную
            HideAllUI();
        }
    }
    
    /// <summary>
    /// Показывает HUD экшен-режима (вызывается при возобновлении игры)
    /// </summary>
    public void ShowHUD()
    {
        if (actionHUDContainer != null)
        {
            actionHUDContainer.SetActive(true);
        }
        // Элементы будут показаны автоматически в Update() когда войдём в экшен-режим
    }
}

