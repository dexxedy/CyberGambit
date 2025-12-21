using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum Player { Player1, Player2 }
public enum GameMode { PlayerVsPlayer, PlayerVsBot }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
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

    private bool isGameOver = false; // Флаг, чтобы остановить игру
    private bool isPaused = false;

    void Awake()
    {
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
        // Если режим не был установлен из MainMenu, используем режим из Inspector
        // Это позволяет тестировать режимы, запуская SampleScene напрямую
        if (!gameModeSetFromMenu)
        {
            gameMode = testGameMode;
        }
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
        {
            CameraManager.Instance.OnGameOver();
            
        }
        Time.timeScale = 0f; 
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
        
        // Показываем HUD при возобновлении игры
        if (ActionModeUI.Instance != null)
        {
            ActionModeUI.Instance.ShowHUD();
        }
        if (TacticalModeUI.Instance != null)
        {
            TacticalModeUI.Instance.ShowHUD();
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
        currentPlayer = (currentPlayer == Player.Player1) ? Player.Player2 : Player.Player1;
        
        // Обновляем эффекты способностей всех юнитов при смене хода
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
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