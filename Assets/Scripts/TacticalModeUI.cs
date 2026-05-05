using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using System.Collections;

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
    
    [Header("Turn Panel")]
    [SerializeField] private GameObject turnPanel; // Панель с информацией о текущем ходе
    [SerializeField] private TextMeshProUGUI turnText; // Текст "Ход Игрока 1" или "Ход Игрока 2"

    [Header("Dice UI")]
    [SerializeField] private TextMeshProUGUI diceText; // Текст броска костей в тактике
    
    [Header("Tutorial/Instructions Panel")]
    [SerializeField] private GameObject instructionsPanel; // Панель с инструкциями
    [SerializeField] private TextMeshProUGUI instructionsText; // Текст инструкций
    [SerializeField] private Button closeInstructionsButton; // Кнопка закрытия (опционально)
    [SerializeField] private float autoHideDelay = 8f; // Автоматически скрыть через N секунд (0 = не скрывать)
    
    [Header("HUD Container")]
    [SerializeField] private GameObject tacticalHUDContainer; // Родительский GameObject для всего HUD тактического режима
    
    [Header("Settings")]
    [SerializeField] private float raycastDistance = 100f; // Дистанция raycast
    [SerializeField] private LayerMask unitLayer; // Слой юнитов

    [Header("Icon Hover Delay")]
    [SerializeField] private float iconHoverDelaySeconds = 0.5f;
    
    private Unit currentHoveredUnit = null;
    private Camera tacticalCamera;
    private bool hasShownInstructions = false; // Флаг, показывали ли уже инструкции

    private Coroutine iconHoverCoroutine;
    private Unit pendingIconHoverUnit;
    
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
        
        // Инициализация панели хода
        UpdateTurnPanel();
        
        // Показываем инструкции при первом запуске
        ShowInstructions();
    }
    
    void Update()
    {
        // Проверяем паузу - если игра на паузе, не обновляем HUD
        if (GameManager.Instance != null && GameManager.Instance.IsPaused())
        {
            return;
        }
        
        // Обновляем панель хода
        UpdateTurnPanel();
        
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

        Unit hoveredUnit = null;

        if (TacticalWorldIconsController.Instance != null)
        {
            hoveredUnit = TacticalWorldIconsController.Instance.GetHoveredIconUnit();
        }

        if (hoveredUnit == null && TacticalWorldIconsController.Instance == null)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = tacticalCamera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, unitLayer))
            {
                hoveredUnit = hit.collider.GetComponentInParent<Unit>();
                if (hoveredUnit == null)
                    hoveredUnit = hit.collider.GetComponent<Unit>();
            }
        }
        
        // В режиме BF-иконок: показ панели управляется через PointerEnter/Exit с задержкой.
        // Здесь только обновляем содержимое, если панель уже показана.
        if (TacticalWorldIconsController.Instance != null)
        {
            if (currentHoveredUnit != null && unitInfoPanel.activeSelf)
                UpdateInfoDisplay(currentHoveredUnit);
            return;
        }

        // Без BF-иконок: классическое поведение по raycast — показываем сразу.
        if (hoveredUnit != null)
        {
            if (currentHoveredUnit != hoveredUnit)
                currentHoveredUnit = hoveredUnit;

            if (!unitInfoPanel.activeSelf)
                unitInfoPanel.SetActive(true);

            UpdateInfoDisplay(hoveredUnit);
            return;
        }

        if (unitInfoPanel.activeSelf)
            unitInfoPanel.SetActive(false);
        currentHoveredUnit = null;
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
            string custom = unit.GetUnitDisplayName();
            unitNameText.text = string.IsNullOrEmpty(custom) ? GetUnitDisplayName(unit.chessType) : custom;
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
    
    /// <summary>
    /// Обновляет панель с информацией о текущем ходе
    /// </summary>
    private void UpdateTurnPanel()
    {
        if (turnPanel == null || turnText == null) return;
        
        if (GameManager.Instance != null)
        {
            // Показываем панель в тактическом режиме (и в PvP, и против бота)
            bool shouldShow = true;
            if (CameraManager.Instance != null)
            {
                shouldShow = !CameraManager.Instance.IsActionMode();
            }
            
            turnPanel.SetActive(shouldShow);
            
            if (shouldShow)
            {
                // Обновляем текст на русском
                Player currentPlayer = GameManager.Instance.currentPlayer;
                GameMode gameMode = GameManager.Instance.GetGameMode();
                
                if (currentPlayer == Player.Player1)
                {
                    turnText.text = "Ваш Ход";
                }
                else
                {
                    // В режиме против бота показываем "Ход Бота", в PvP - "Ход Игрока 2"
                    if (gameMode == GameMode.PlayerVsBot)
                    {
                        turnText.text = "Ход Бота";
                    }
                    else
                    {
                        turnText.text = "Ход Игрока 2";
                    }
                }

                // Показываем бросок костей в тактическом режиме
                if (diceText != null)
                {
                    int dice = GameManager.Instance.HasRolledDiceThisTurn() ? GameManager.Instance.GetCurrentTurnDice() : 0;
                    float meters = GameManager.Instance.HasRolledDiceThisTurn()
                        ? GameManager.Instance.GetCurrentTurnMoveBudgetMeters()
                        : 0f;
                    diceText.text = dice > 0 ? $"Кости: {dice}  |  Ход: {meters:F0} м" : "Кости: -";
                }
            }
        }
        else
        {
            // Если GameManager не найден, скрываем панель
            turnPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// Показывает панель с инструкциями для игрока
    /// </summary>
    private void ShowInstructions()
    {
        if (instructionsPanel == null || hasShownInstructions) return;
        
        hasShownInstructions = true;
        instructionsPanel.SetActive(true);
        
        // Устанавливаем текст инструкций
        if (instructionsText != null)
        {
            GameMode gameMode = GameManager.Instance != null ? GameManager.Instance.GetGameMode() : GameMode.PlayerVsPlayer;
            
            string introText = "ЦЕЛЬ ИГРЫ:\n\n" +
                               "Уничтожьте короля противника, не дав ему сделать то же самое с вашим королем.\n\n" +
                               "КАК ИГРАТЬ:\n\n" +
                               "• В тактике кликните по карточке-иконке своего юнита на экране\n" +
                               "• Вы перейдёте в режим от первого лица\n" +
                               "• Управляйте юнитом и атакуйте врагов\n\n";
            
            if (gameMode == GameMode.PlayerVsBot)
            {
                introText += "Вы играете против бота. Ходы чередуются.";
            }
            else
            {
                introText += "Вы играете против другого игрока. Ходы чередуются.";
            }
            
            
            instructionsText.text = introText;
        }
        
        // Настраиваем кнопку закрытия
        if (closeInstructionsButton != null)
        {
            closeInstructionsButton.onClick.RemoveAllListeners();
            closeInstructionsButton.onClick.AddListener(HideInstructions);
        }
        
        // Автоматически скрываем через указанное время
        if (autoHideDelay > 0f)
        {
            StartCoroutine(AutoHideInstructions());
        }
    }
    
    /// <summary>
    /// Скрывает панель инструкций
    /// </summary>
    public void HideInstructions()
    {
        if (instructionsPanel != null)
        {
            instructionsPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// Автоматически скрывает инструкции через указанное время
    /// </summary>
    private IEnumerator AutoHideInstructions()
    {
        yield return new WaitForSeconds(autoHideDelay);
        HideInstructions();
    }
    
    /// <summary>
    /// Скрывает весь HUD тактического режима (вызывается при паузе)
    /// </summary>
    public void HideHUD()
    {
        if (tacticalHUDContainer != null)
        {
            tacticalHUDContainer.SetActive(false);
        }
        else
        {
            // Если контейнер не назначен, скрываем все элементы вручную
            if (unitInfoPanel != null)
            {
                unitInfoPanel.SetActive(false);
            }
            if (turnPanel != null)
            {
                turnPanel.SetActive(false);
            }
            if (instructionsPanel != null)
            {
                instructionsPanel.SetActive(false);
            }
        }
    }
    
    /// <summary>Подсказка при наведении на BF-иконку в тактике.</summary>
    public void ShowUnitTooltipFromIcon(Unit unit)
    {
        if (unit == null || unitInfoPanel == null) return;

        pendingIconHoverUnit = unit;

        if (iconHoverCoroutine != null)
        {
            StopCoroutine(iconHoverCoroutine);
            iconHoverCoroutine = null;
        }

        iconHoverCoroutine = StartCoroutine(ShowIconTooltipDelayed(unit));
    }

    /// <summary>Скрыть подсказку, если она была от этой иконки.</summary>
    public void ClearIconTooltipIf(Unit unit)
    {
        if (pendingIconHoverUnit == unit)
        {
            pendingIconHoverUnit = null;
            if (iconHoverCoroutine != null)
            {
                StopCoroutine(iconHoverCoroutine);
                iconHoverCoroutine = null;
            }
        }

        if (unit != null && currentHoveredUnit == unit)
        {
            currentHoveredUnit = null;
            if (unitInfoPanel != null)
                unitInfoPanel.SetActive(false);
        }
    }

    private IEnumerator ShowIconTooltipDelayed(Unit unit)
    {
        float d = Mathf.Max(0f, iconHoverDelaySeconds);
        if (d > 0f)
            yield return new WaitForSeconds(d);

        // За время ожидания курсор мог уйти или режим мог смениться.
        if (pendingIconHoverUnit != unit) yield break;
        if (CameraManager.Instance != null && CameraManager.Instance.IsActionMode()) yield break;

        currentHoveredUnit = unit;
        unitInfoPanel.SetActive(true);
        UpdateInfoDisplay(unit);
    }

    /// <summary>
    /// Показывает HUD тактического режима (вызывается при возобновлении игры)
    /// </summary>
    public void ShowHUD()
    {
        if (tacticalHUDContainer != null)
        {
            tacticalHUDContainer.SetActive(true);
        }
        // Элементы будут показаны автоматически в Update() когда войдём в тактический режим
    }
}

