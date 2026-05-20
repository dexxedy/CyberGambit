using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;
using System.Collections;

public enum Player { Player1, Player2 }
public enum GameMode { PlayerVsPlayer, PlayerVsBot }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    /// <summary>
    /// Событие броска кубика (значение кубика).
    /// Используется UI/визуализациями (например, 3D-кость в тактике).
    /// </summary>
    public static event Action<int> OnDiceRolled;
    public Player currentPlayer = Player.Player1;
    private static GameMode gameMode = GameMode.PlayerVsPlayer;
    private static bool gameModeSetFromMenu = false; // Флаг, что режим был установлен из меню
    
    [Header("Game Mode (для тестирования)")]
    [Tooltip("Режим игры для тестирования. Используется только если игра запущена напрямую в SampleScene (не из MainMenu)")]
    [SerializeField] private GameMode testGameMode = GameMode.PlayerVsPlayer;
    
    [Header("UI End Game")]
    [SerializeField] private GameObject gameOverPanel; // Ссылка на панель
    [SerializeField] private TextMeshProUGUI winnerText; // Ссылка на текст
    [Header("UI Pause")] // НОВОЕ: Пауза-меню
    [SerializeField] private GameObject pausePanel;
    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel; // Панель настроек в паузе
    [Header("Pause Menu Buttons")]
    [SerializeField] private GameObject pauseMenuButtons; // Родительский объект со всеми кнопками паузы
    [Header("Volume Settings")]
    [SerializeField] private Slider musicVolumeSlider; // Слайдер громкости музыки
    [SerializeField] private Slider sfxVolumeSlider; // Слайдер громкости звуковых эффектов
    
    [Header("Dice Movement")]
    [SerializeField] private int diceMin = 1;
    [SerializeField] private int diceMax = 6;
    [SerializeField] private float metersPerDicePoint = 25f;
    private int currentTurnDice = 0;
    private bool hasRolledDiceThisTurn = false;

    private bool isGameOver = false; // Флаг, чтобы остановить игру
    private bool isPaused = false;

    // Направления "вперед" для каждого игрока (определяются по стартовой расстановке)
    private Vector2Int player1Forward = new Vector2Int(0, 1);
    private Vector2Int player2Forward = new Vector2Int(0, -1);

    /// <summary> Фаза расстановки армии (PvBot): игрок расставляет юнитов до броска кости. </summary>
    private bool armyDeploymentPhaseActive = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Режим из меню (SetGameMode) или Test Game Mode из инспектора — применяем в Awake, а не в Start,
        // иначе другие скрипты в Start/корутинах (например ArmyDeploymentController) читают gameMode
        // пока ещё равным дефолту PlayerVsPlayer.
        if (!gameModeSetFromMenu)
            gameMode = testGameMode;
    }
    
    void Start()
    {
        // Кубик для игрока бросается вручную (UI/клавиша). Бот бросает автоматически в своём ходу.

        // Определяем "вперед" после того, как юниты привязались к сетке
        StartCoroutine(DetectForwardDirectionsAfterInit());
    }

    /// <summary> Активна ли фаза расстановки армии перед боем (только миссии с ArmyDeploymentController). </summary>
    public bool IsArmyDeploymentPhase() => armyDeploymentPhaseActive;

    /// <summary> Вызывается ArmyDeploymentController при старте миссии с ручной расстановкой. </summary>
    public void BeginArmyDeploymentPhase()
    {
        armyDeploymentPhaseActive = true;
    }

    /// <summary> Завершает расстановку и включает обычный тактический ход с костью. </summary>
    public void CompleteArmyDeploymentPhase()
    {
        if (!armyDeploymentPhaseActive) return;
        armyDeploymentPhaseActive = false;
        ResetDiceForNextTurn();
        if (FogOfWarManager.Instance != null)
            FogOfWarManager.Instance.ResetFog();
        FogWarIntelTracker.Instance?.ResetIntel();
        EnemyIntelTracker.Instance?.ResetIntel();
        StartCoroutine(DetectForwardDirectionsAfterInit());
    }

    private IEnumerator DetectForwardDirectionsAfterInit()
    {
        // Ждем кадр, чтобы ChessRulesManager успел выполнить SnapToGrid в Start()
        yield return null;
        ApplyForwardDirectionsFromUnitPositions();
    }

    /// <summary> Пересчитывает направление «вперёд» по средним позициям игроков на сетке. </summary>
    private void ApplyForwardDirectionsFromUnitPositions()
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        if (allUnits == null || allUnits.Length == 0 || ChessGrid.Instance == null) return;

        Vector2 sum1 = Vector2.zero;
        Vector2 sum2 = Vector2.zero;
        int c1 = 0, c2 = 0;

        foreach (var u in allUnits)
        {
            if (u == null) continue;
            if (u.GetHealth() <= 0) continue;
            Vector2Int gp = ChessGrid.Instance.WorldToGridCoords(u.transform.position);
            if (u.owner == Player.Player1)
            {
                sum1 += new Vector2(gp.x, gp.y);
                c1++;
            }
            else
            {
                sum2 += new Vector2(gp.x, gp.y);
                c2++;
            }
        }

        if (c1 == 0 || c2 == 0) return;

        Vector2 avg1 = sum1 / c1;
        Vector2 avg2 = sum2 / c2;
        Vector2 delta = avg2 - avg1;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            int sx = delta.x >= 0 ? 1 : -1;
            player1Forward = new Vector2Int(sx, 0);
            player2Forward = new Vector2Int(-sx, 0);
        }
        else
        {
            int sy = delta.y >= 0 ? 1 : -1;
            player1Forward = new Vector2Int(0, sy);
            player2Forward = new Vector2Int(0, -sy);
        }
    }
    
    public bool HasRolledDiceThisTurn() => hasRolledDiceThisTurn;
    public int GetCurrentTurnDice() => currentTurnDice;
    public float GetMetersPerDicePoint() => metersPerDicePoint;
    public float ConvertDiceToMeters(int diceValue) => Mathf.Max(0, diceValue) * Mathf.Max(0f, metersPerDicePoint);
    public float GetCurrentTurnMoveBudgetMeters() => ConvertDiceToMeters(currentTurnDice);
    public bool IsGameOver() => isGameOver;

    /// <summary> Максимальное значение грани кубика (для оценки хода до броска). </summary>
    public int GetDiceFaceMax() => Mathf.Max(diceMin, diceMax);

    /// <summary> Верхняя оценка метража хода до броска (как при максимальном значении кубика). </summary>
    public float GetMaxPossibleMoveBudgetMeters() => ConvertDiceToMeters(GetDiceFaceMax());
    
    public int RollDiceForCurrentTurn()
    {
        if (isGameOver) return currentTurnDice;
        if (armyDeploymentPhaseActive) return currentTurnDice;

        int min = Mathf.Min(diceMin, diceMax);
        int max = Mathf.Max(diceMin, diceMax);
        min = Mathf.Max(0, min);
        max = Mathf.Max(min, max);
        
        // Random.Range int max is exclusive
        currentTurnDice = UnityEngine.Random.Range(min, max + 1);
        hasRolledDiceThisTurn = true;
        OnDiceRolled?.Invoke(currentTurnDice);
        return currentTurnDice;
    }
    
    public void ResetDiceForNextTurn()
    {
        currentTurnDice = 0;
        hasRolledDiceThisTurn = false;
    }
    public void EndGame(Player loser)
    {
        if (isGameOver) return; // Чтобы не вызвать дважды
        isGameOver = true;

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.ForceSwitchToTacticalMode();
        }

        // Включаем панель
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        HideGameplayHudForEndScreen();

        // Определяем победителя и сообщение в зависимости от режима игры
        string resultMessage = "";
        Player winner = (loser == Player.Player1) ? Player.Player2 : Player.Player1;
        bool isPlayerVictory = false; // Победил ли игрок (Player1)
        
        if (gameMode == GameMode.PlayerVsBot)
        {
            // Режим игры против бота
            if (loser == Player.Player1)
            {
                // Игрок проиграл (бот победил)
                resultMessage = "ПОРАЖЕНИЕ";
                isPlayerVictory = false;
            }
            else
            {
                // Игрок победил (бот проиграл)
                resultMessage = "ПОБЕДА!";
                isPlayerVictory = true;
            }
        }
        else
        {
            // Режим игры против игрока
            if (loser == Player.Player1)
            {
                resultMessage = "ПОБЕДА ИГРОКА 2!";
                isPlayerVictory = false;
            }
            else
            {
                resultMessage = "ПОБЕДА ИГРОКА 1!";
                isPlayerVictory = true;
            }
        }

        if (winnerText != null) winnerText.text = resultMessage;
        
        // Воспроизводим музыку победы/поражения
        if (AudioManager.Instance != null)
        {
            if (gameMode == GameMode.PlayerVsBot)
            {
                // В режиме против бота: для игрока (Player1) победа = победа, поражение = поражение
                if (isPlayerVictory)
                {
                    AudioManager.Instance.PlayVictoryMusic();
                }
                else
                {
                    AudioManager.Instance.PlayDefeatMusic();
                }
            }
            else
            {
                // В режиме против игрока: музыка зависит от того, кто выиграл относительно текущего игрока
                if (winner == currentPlayer)
                {
                    AudioManager.Instance.PlayVictoryMusic();
                }
                else
                {
                    AudioManager.Instance.PlayDefeatMusic();
                }
            }
        }
        
        if (CameraManager.Instance != null)
            CameraManager.Instance.OnGameOver();

        Time.timeScale = 0f;
    }

    /// <summary>Скрывает тактический HUD (кость, очередь/панель хода, иконки), когда показан экран победы/поражения.</summary>
    private void HideGameplayHudForEndScreen()
    {
        if (TacticalModeUI.Instance != null)
            TacticalModeUI.Instance.HideHUD();

        if (ActionModeUI.Instance != null)
            ActionModeUI.Instance.HideHUD();

        if (CameraManager.Instance != null)
            CameraManager.Instance.HideTurnHudForGameOver();

        DiceRenderUI[] diceUis = FindObjectsByType<DiceRenderUI>(FindObjectsInactive.Include);
        for (int i = 0; i < diceUis.Length; i++)
        {
            if (diceUis[i] != null)
                diceUis[i].SetHudVisible(false);
        }

        if (TacticalWorldIconsController.Instance != null)
            TacticalWorldIconsController.Instance.SetIconsRootVisible(false);
    }

    // Метод для кнопки "В меню"
    public void LoadMainMenu()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        Time.timeScale = 1f; // Возвращаем время в норму перед выходом
        SceneManager.LoadScene("MainMenu");
    }
    // НОВОЕ: Метод рестарта (для кнопки "Рестарт")
    public void RestartGame()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // НОВОЕ: Метод паузы (вызывается на Esc)
    public void PauseGame()
    {
        if (isGameOver || isPaused) return;
        isPaused = true;
        if (pausePanel != null) pausePanel.SetActive(true);
        Time.timeScale = 0f;
        // Разблокируем курсор для UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Закрываем панель настроек при открытии паузы (если она была открыта)
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        
        // Убеждаемся, что кнопки паузы видны
        if (pauseMenuButtons != null)
        {
            pauseMenuButtons.SetActive(true);
        }
        
        // Скрываем весь HUD при паузе
        if (ActionModeUI.Instance != null)
        {
            ActionModeUI.Instance.HideHUD();
        }
        if (TacticalModeUI.Instance != null)
        {
            TacticalModeUI.Instance.HideHUD();
        }
    }

    // НОВОЕ: Метод продолжения (для кнопки "Продолжить")
    public void ResumeGame()
    {
        if (!isPaused) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        isPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        
        // Показываем HUD при возобновлении игры (не после победы/поражения)
        if (!isGameOver)
        {
            if (ActionModeUI.Instance != null)
                ActionModeUI.Instance.ShowHUD();
            if (TacticalModeUI.Instance != null)
                TacticalModeUI.Instance.ShowHUD();
            if (TacticalWorldIconsController.Instance != null)
                TacticalWorldIconsController.Instance.SetIconsRootVisible(true);
            DiceRenderUI[] diceUis = FindObjectsByType<DiceRenderUI>(FindObjectsInactive.Include);
            for (int i = 0; i < diceUis.Length; i++)
            {
                if (diceUis[i] != null)
                    diceUis[i].SetHudVisible(true);
            }
        }
        
        // Восстанавливаем состояние курсора в зависимости от режима
        if (CameraManager.Instance != null)
        {
            if (CameraManager.Instance.IsActionMode())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
    public void SwitchTurn()
    {
        if (isGameOver) return;
        if (armyDeploymentPhaseActive) return;

        currentPlayer = (currentPlayer == Player.Player1) ? Player.Player2 : Player.Player1;
        Mission2.TankController.StopAllTankMoveSounds();
        Unit.StopAllUnitMoveSounds();
        ResetDiceForNextTurn();
        // Автобросок только для бота. Игрок бросает сам (см. DiceRenderUI / будущий UI).
        if (IsBotTurn())
        {
            RollDiceForCurrentTurn();
        }
        
        // Обновляем эффекты способностей всех юнитов при смене хода
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        foreach (Unit unit in allUnits)
        {
            if (unit != null)
            {
                unit.UpdateAbilityEffects();
            }
        }
        
        // Уведомляем AbilitySystem о смене хода для обновления КД
        if (AbilitySystem.Instance != null)
        {
            AbilitySystem.Instance.OnTurnSwitch();
        }
        
        // Если это ход бота, запускаем его автоматически
        if (IsBotTurn() && BotController.Instance != null)
        {
            BotController.Instance.ExecuteBotTurn();
        }
    }
    
    /// <summary>
    /// Устанавливает режим игры (вызывается из MainMenu)
    /// </summary>
    public static void SetGameMode(GameMode mode)
    {
        gameMode = mode;
        gameModeSetFromMenu = true; // Помечаем, что режим установлен из меню
    }
    
    /// <summary>
    /// Получает текущий режим игры (для UI и отладки)
    /// </summary>
    public GameMode GetGameMode()
    {
        return gameMode;
    }
    
    /// <summary>
    /// Проверяет, является ли текущий ход ботом
    /// </summary>
    public bool IsBotTurn()
    {
        return gameMode == GameMode.PlayerVsBot && currentPlayer == Player.Player2;
    }

    public Vector2Int GetForwardDirection(Player player)
    {
        return player == Player.Player1 ? player1Forward : player2Forward;
    }

    public void InitializePauseMenu(GameObject panel, GameObject settings, GameObject buttons, Slider music, Slider sfx)
    {
        pausePanel = panel;
        settingsPanel = settings;
        pauseMenuButtons = buttons;
        musicVolumeSlider = music;
        sfxVolumeSlider = sfx;
    }

    public bool IsPaused() => isPaused;
    
    /// <summary>
    /// Открывает панель настроек в паузе
    /// </summary>
    public void OpenSettings()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            // Инициализируем слайдеры при открытии панели настроек
            InitializeVolumeSliders();
        }
        
        // Скрываем кнопки паузы
        if (pauseMenuButtons != null)
        {
            pauseMenuButtons.SetActive(false);
        }
    }
    
    /// <summary>
    /// Закрывает панель настроек в паузе
    /// </summary>
    public void CloseSettings()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        
        // Показываем кнопки паузы обратно
        if (pauseMenuButtons != null)
        {
            pauseMenuButtons.SetActive(true);
        }
    }
    
    /// <summary>
    /// Инициализирует слайдеры громкости текущими значениями из AudioManager
    /// </summary>
    private void InitializeVolumeSliders()
    {
        if (AudioManager.Instance != null)
        {
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.value = AudioManager.Instance.GetMusicVolume();
                musicVolumeSlider.onValueChanged.RemoveAllListeners();
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }
            
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = AudioManager.Instance.GetSFXVolume();
                sfxVolumeSlider.onValueChanged.RemoveAllListeners();
                sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            }
        }
    }
    
    /// <summary>
    /// Применяет изменения громкости (вызывается кнопкой "Применить" в паузе)
    /// </summary>
    public void ApplySettings()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        
        // Настройки уже применены через OnMusicVolumeChanged и OnSFXVolumeChanged
        // Просто закрываем панель
        CloseSettings();
    }
    
    /// <summary>
    /// Вызывается при изменении громкости музыки
    /// </summary>
    public void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolumeWithSave(value);
        }
    }
    
    /// <summary>
    /// Вызывается при изменении громкости звуковых эффектов
    /// </summary>
    public void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolumeWithSave(value);
        }
    }
}