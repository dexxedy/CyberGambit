using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// UI тактического режима: ход, кости, инструкции.
/// </summary>
public class TacticalModeUI : MonoBehaviour
{
    public static TacticalModeUI Instance;
    
    [Header("Turn Panel")]
    [SerializeField] private GameObject turnPanel;
    [SerializeField] private TextMeshProUGUI turnText;

    [Header("Dice UI")]
    [SerializeField] private TextMeshProUGUI diceText;
    
    [Header("Tutorial/Instructions Panel")]
    [SerializeField] private GameObject instructionsPanel;
    [SerializeField] private TextMeshProUGUI instructionsText;
    [SerializeField] private Button closeInstructionsButton;
    [SerializeField] private float autoHideDelay = 8f;
    
    [Header("HUD Container")]
    [SerializeField] private GameObject tacticalHUDContainer;
    
    private bool hasShownInstructions;
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    void Start()
    {
        UpdateTurnPanel();
        ShowInstructions();
    }
    
    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused())
            return;
        
        UpdateTurnPanel();
    }
    
    private void UpdateTurnPanel()
    {
        if (turnPanel == null || turnText == null) return;
        
        if (GameManager.Instance != null)
        {
            bool inTactical = CameraManager.Instance == null || !CameraManager.Instance.IsActionMode();
            bool shouldShow = inTactical && !GameManager.Instance.IsArmyDeploymentPhase();
            
            turnPanel.SetActive(shouldShow);
            
            if (shouldShow)
            {
                Player currentPlayer = GameManager.Instance.currentPlayer;
                GameMode gameMode = GameManager.Instance.GetGameMode();
                
                if (currentPlayer == Player.Player1)
                    turnText.text = "Ваш Ход";
                else if (gameMode == GameMode.PlayerVsBot)
                    turnText.text = "Ход Бота";
                else
                    turnText.text = "Ход Игрока 2";

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
            turnPanel.SetActive(false);
        }
    }
    
    private void ShowInstructions()
    {
        if (instructionsPanel == null || hasShownInstructions) return;
        
        hasShownInstructions = true;
        instructionsPanel.SetActive(true);
        
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
                introText += "Вы играете против бота. Ходы чередуются.";
            else
                introText += "Вы играете против другого игрока. Ходы чередуются.";
            
            instructionsText.text = introText;
        }
        
        if (closeInstructionsButton != null)
        {
            closeInstructionsButton.onClick.RemoveAllListeners();
            closeInstructionsButton.onClick.AddListener(HideInstructions);
        }
        
        if (autoHideDelay > 0f)
            StartCoroutine(AutoHideInstructions());
    }
    
    public void HideInstructions()
    {
        if (instructionsPanel != null)
            instructionsPanel.SetActive(false);
    }
    
    private IEnumerator AutoHideInstructions()
    {
        yield return new WaitForSeconds(autoHideDelay);
        HideInstructions();
    }
    
    public void HideHUD()
    {
        if (tacticalHUDContainer != null)
            tacticalHUDContainer.SetActive(false);
        else
        {
            if (turnPanel != null) turnPanel.SetActive(false);
            if (instructionsPanel != null) instructionsPanel.SetActive(false);
        }
    }
    
    public void ShowHUD()
    {
        if (tacticalHUDContainer != null)
            tacticalHUDContainer.SetActive(true);
    }
}
