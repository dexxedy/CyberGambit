using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public enum Player { Player1, Player2 }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public Player currentPlayer = Player.Player1;
    [Header("UI End Game")]
    [SerializeField] private GameObject gameOverPanel; // Ссылка на панель
    [SerializeField] private TextMeshProUGUI winnerText; // Ссылка на текст
    [Header("UI Pause")] // НОВОЕ: Пауза-меню
    [SerializeField] private GameObject pausePanel;

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

        // Определяем победителя
        string resultMessage = "";
        if (loser == Player.Player1)
        {
            resultMessage = "ПОБЕДА ИГРОКА 2!";
        }
        else
        {
            resultMessage = "ПОБЕДА ИГРОКА 1!";
        }

        if (winnerText != null) winnerText.text = resultMessage;
        
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.OnGameOver();
            
        }
        Time.timeScale = 0f; 
    }

    // Метод для кнопки "В меню"
    public void LoadMainMenu()
    {
        Time.timeScale = 1f; // Возвращаем время в норму перед выходом
        SceneManager.LoadScene("MainMenu");
    }
    // НОВОЕ: Метод рестарта (для кнопки "Рестарт")
    public void RestartGame()
    {
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
    }

    // НОВОЕ: Метод продолжения (для кнопки "Продолжить")
    public void ResumeGame()
    {
        if (!isPaused) return;
        isPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
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
        
        Debug.Log($"Ход перешёл к {currentPlayer}");
    }
    public bool IsPaused() => isPaused;
}