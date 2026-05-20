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
    [SerializeField] private Image crosshairImage;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color enemyColor = Color.red;
    [SerializeField] private Color allyColor = Color.green;
    
    [Header("Enemy Health")]
    [SerializeField] private GameObject enemyHealthPanel;
    [SerializeField] private TextMeshProUGUI enemyHealthText;
    [SerializeField] private TextMeshProUGUI enemyNameText;
    
    [Header("Ability Info")]
    [SerializeField] private GameObject abilityPanel;
    [SerializeField] private TextMeshProUGUI abilityNameText;
    [SerializeField] private TextMeshProUGUI abilityCooldownText;
    [SerializeField] private TextMeshProUGUI abilityDescriptionText;
    
    [Header("Action Mode Stats")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private GameObject integrityPanel;
    [SerializeField] private TextMeshProUGUI integrityText;

    [Header("Weapon / Ammo")]
    [SerializeField] private GameObject ammoPanel;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI reloadText;
    
    [Header("HUD Container")]
    [SerializeField] private GameObject actionHUDContainer;
    
    [Header("Settings")]
    [SerializeField] private float raycastDistance = 50f;
    [SerializeField] private float updateInterval = 0.1f;
    [SerializeField] private Color readyColor = Color.skyBlue;
    [SerializeField] private Color cooldownColor = Color.red;
    
    private Camera actionCamera;
    private Unit currentTarget;
    private Unit currentUnit;
    private float lastUpdateTime;
    private Transform healthPointPanel;
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    void Start()
    {
        if (crosshairImage != null)
            crosshairImage.gameObject.SetActive(false);
        if (enemyHealthPanel != null)
            enemyHealthPanel.SetActive(false);
        if (abilityPanel != null)
            abilityPanel.SetActive(false);
        
        HideStatsPanels();

        if (ammoPanel != null)
            ammoPanel.SetActive(false);

        EnsureHealthBar();
        AutoBindStatsTextsIfMissing();
    }

    private void EnsureHealthBar()
    {
        if (actionHUDContainer == null) return;

        if (healthPointPanel == null)
        {
            Transform found = actionHUDContainer.transform.Find("HealthPointPanel");
            if (found != null)
                healthPointPanel = found;
        }

        if (healthSlider == null && healthPointPanel != null)
            healthSlider = healthPointPanel.GetComponentInChildren<Slider>(true);

        if (healthSlider == null && healthPointPanel != null)
            healthSlider = CreateHealthSlider(healthPointPanel);
    }

    private static Slider CreateHealthSlider(Transform parent)
    {
        var root = new GameObject("HealthSlider");
        root.transform.SetParent(parent, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.05f, 0.12f);
        rootRt.anchorMax = new Vector2(0.95f, 0.88f);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var slider = root.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(root.transform, false);
        var bgRt = bgGo.AddComponent<RectTransform>();
        StretchRect(bgRt);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        slider.targetGraphic = bgImg;

        var fillAreaGo = new GameObject("Fill Area");
        fillAreaGo.transform.SetParent(root.transform, false);
        var fillAreaRt = fillAreaGo.AddComponent<RectTransform>();
        StretchRect(fillAreaRt);
        fillAreaRt.offsetMin = new Vector2(4f, 4f);
        fillAreaRt.offsetMax = new Vector2(-4f, -4f);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        var fillRt = fillGo.AddComponent<RectTransform>();
        StretchRect(fillRt);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(1f, 0.22f, 0.32f, 1f);

        slider.fillRect = fillRt;
        slider.direction = Slider.Direction.LeftToRight;

        return slider;
    }

    private static void StretchRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void ApplyHealthSlider(Slider slider, int current, int max)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.interactable = false;
        slider.value = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
    }

    private void AutoBindStatsTextsIfMissing()
    {
        if (enemyHealthText == null && enemyHealthPanel != null)
            enemyHealthText = enemyHealthPanel.GetComponentInChildren<TextMeshProUGUI>(true);
        if (integrityText == null && integrityPanel != null)
            integrityText = integrityPanel.GetComponentInChildren<TextMeshProUGUI>(true);
    }
    
    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused())
            return;
        
        if (CameraManager.Instance != null && CameraManager.Instance.IsActionMode())
        {
            if (actionCamera == null)
                actionCamera = CameraManager.Instance.GetActionCamera();
            
            UpdateCrosshair();
            
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateEnemyHealth();
                lastUpdateTime = Time.time;
            }
            
            UpdateAbilityInfo();
            UpdateStats();
        }
        else
        {
            HideAllUI();
        }
    }
    
    private void UpdateCrosshair()
    {
        if (crosshairImage == null || actionCamera == null) return;
        
        if (!crosshairImage.gameObject.activeSelf)
            crosshairImage.gameObject.SetActive(true);
        
        Unit targetUnit = GetUnitUnderCrosshair();
        
        if (targetUnit != null)
        {
            Unit currentControlledUnit = CameraManager.Instance.GetCurrentControlledUnit();
            if (currentControlledUnit != null)
            {
                crosshairImage.color = targetUnit.owner == currentControlledUnit.owner ? allyColor : enemyColor;
            }
            else
            {
                crosshairImage.color = defaultColor;
            }
        }
        else
        {
            crosshairImage.color = defaultColor;
        }
    }
    
    private void UpdateEnemyHealth()
    {
        if (actionCamera == null || enemyHealthPanel == null) return;
        
        Unit targetUnit = GetUnitUnderCrosshair();
        Unit currentControlledUnit = CameraManager.Instance.GetCurrentControlledUnit();
        if (targetUnit != null && currentControlledUnit != null && targetUnit.owner != currentControlledUnit.owner)
        {
            if (currentTarget != targetUnit)
                currentTarget = targetUnit;
            
            if (!enemyHealthPanel.activeSelf)
                enemyHealthPanel.SetActive(true);
            
            if (enemyHealthText != null)
            {
                int currentHP = targetUnit.GetHealth();
                int maxHP = targetUnit.GetMaxHealth();
                enemyHealthText.text = $"HP: {currentHP}/{maxHP}";
            }
            
            if (enemyNameText != null)
                enemyNameText.text = GetUnitDisplayName(targetUnit.chessType);
        }
        else
        {
            if (enemyHealthPanel.activeSelf)
                enemyHealthPanel.SetActive(false);
            currentTarget = null;
        }
    }
    
    private void UpdateAbilityInfo()
    {
        if (abilityPanel == null) return;
        
        Unit controlledUnit = CameraManager.Instance.GetCurrentControlledUnit();
        
        if (controlledUnit != null)
        {
            if (controlledUnit.chessType == ChessUnitType.Pawn)
            {
                if (abilityPanel.activeSelf)
                    abilityPanel.SetActive(false);
                return;
            }
            
            if (currentUnit != controlledUnit)
                currentUnit = controlledUnit;
            
            if (!abilityPanel.activeSelf)
                abilityPanel.SetActive(true);
            
            UpdateAbilityDisplay(controlledUnit);
        }
        else
        {
            if (abilityPanel.activeSelf)
                abilityPanel.SetActive(false);
            currentUnit = null;
        }
    }
    
    private void UpdateAbilityDisplay(Unit unit)
    {
        if (unit == null) return;
        
        if (unit.chessType == ChessUnitType.Pawn)
        {
            if (abilityPanel != null && abilityPanel.activeSelf)
                abilityPanel.SetActive(false);
            return;
        }
        
        UnitAbilities abilities = unit.GetComponent<UnitAbilities>();
        if (abilities == null)
        {
            if (abilityPanel != null && abilityPanel.activeSelf)
                abilityPanel.SetActive(false);
            return;
        }
        
        if (abilityNameText != null)
            abilityNameText.text = GetAbilityName(unit.chessType);
        
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
        
        if (abilityDescriptionText != null)
            abilityDescriptionText.text = GetAbilityDescription(unit.chessType);
    }
    
    private Unit GetUnitUnderCrosshair()
    {
        if (actionCamera == null) return null;
        
        Ray ray = actionCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance))
        {
            Unit targetUnit = hit.collider.GetComponentInParent<Unit>();
            if (targetUnit == null)
                targetUnit = hit.collider.GetComponent<Unit>();
            return targetUnit;
        }
        
        return null;
    }
    
    private void UpdateStats()
    {
        Unit controlledUnit = CameraManager.Instance.GetCurrentControlledUnit();

        if (healthSlider == null)
            EnsureHealthBar();

        if (integrityText == null && integrityPanel != null)
            AutoBindStatsTextsIfMissing();
        
        if (healthSlider != null && controlledUnit != null)
            ApplyHealthSlider(healthSlider, controlledUnit.GetHealth(), controlledUnit.GetMaxHealth());
        
        if (integrityText != null && controlledUnit != null)
            integrityText.text = $"Дистанция: {controlledUnit.GetRemainingMoveMeters():F1} м";

        UpdateAmmoPanel(controlledUnit);
    }

    private void UpdateAmmoPanel(Unit controlledUnit)
    {
        if (ammoPanel == null) return;

        Weapon w = controlledUnit != null ? controlledUnit.GetEquippedWeapon() : null;
        bool show = w != null && w.Config != null;
        if (!show)
        {
            if (ammoPanel.activeSelf) ammoPanel.SetActive(false);
            return;
        }

        if (!ammoPanel.activeSelf) ammoPanel.SetActive(true);

        if (ammoText != null)
        {
            int magSize = w.MagazineSize;
            ammoText.text = $"{w.AmmoInMag}/{magSize} | {w.AmmoReserve}";
        }

        if (reloadText != null)
            reloadText.gameObject.SetActive(w.IsReloading);
    }
    
    public void ShowStatsPanels()
    {
        if (healthPointPanel != null) healthPointPanel.gameObject.SetActive(true);
        else if (healthSlider != null) healthSlider.gameObject.SetActive(true);
        if (integrityPanel != null) integrityPanel.SetActive(true);
    }
    
    public void HideStatsPanels()
    {
        if (healthPointPanel != null) healthPointPanel.gameObject.SetActive(false);
        else if (healthSlider != null) healthSlider.gameObject.SetActive(false);
        if (integrityPanel != null) integrityPanel.SetActive(false);
        if (ammoPanel != null) ammoPanel.SetActive(false);
    }
    
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
    
    public void HideHUD()
    {
        if (actionHUDContainer != null)
            actionHUDContainer.SetActive(false);
        else
            HideAllUI();
    }
    
    public void ShowHUD()
    {
        if (actionHUDContainer != null)
            actionHUDContainer.SetActive(true);
    }
}
