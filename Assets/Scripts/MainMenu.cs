using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject howToPlayPanel;
    [SerializeField] private TutorialVideoManager tutorialVideoManager; // Ссылка на менеджер видео (опционально)
    
    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel; // Панель настроек
    
    [Header("Main Menu Buttons")]
    [SerializeField] private GameObject mainMenuButtons; // Родительский объект со всеми кнопками главного меню
    
    [Header("Main Menu Music")]
    [SerializeField] private AudioSource mainMenuMusicSource; // AudioSource для музыки главного меню
    
    [Header("Volume Settings")]
    [SerializeField] private Slider musicVolumeSlider; // Слайдер громкости музыки
    [SerializeField] private Slider sfxVolumeSlider; // Слайдер громкости звуковых эффектов
    
    [Header("Game Mode Selection")]
    [SerializeField] private GameObject gameModePanel; // Панель выбора режима игры
    [SerializeField] private GameObject mainMenuPanel; // Основная панель меню
    
    // Временные значения громкости (до применения)
    private float tempMusicVolume = 1f;
    private float tempSFXVolume = 1f;
    
    public void PlayGame()
    {
        PlayButtonClickSound();
        // Показываем панель выбора режима игры вместо прямой загрузки сцены
        if (gameModePanel != null)
        {
            gameModePanel.SetActive(true);
            if (mainMenuPanel != null)
            {
                mainMenuButtons.SetActive(false);
            }
        }
        else
        {
            // Если панель не назначена, загружаем сцену напрямую (режим по умолчанию)
            GameManager.SetGameMode(GameMode.PlayerVsPlayer);
            SceneManager.LoadScene("SampleScene");
        }
    }
    
    /// <summary>
    /// Запускает игру против игрока
    /// </summary>
    public void StartPlayerVsPlayer()
    {
        PlayButtonClickSound();
        GameManager.SetGameMode(GameMode.PlayerVsPlayer);
        SceneManager.LoadScene("SampleScene");
    }
    
    /// <summary>
    /// Запускает игру против бота
    /// </summary>
    public void StartPlayerVsBot()
    {
        PlayButtonClickSound();
        GameManager.SetGameMode(GameMode.PlayerVsBot);
        SceneManager.LoadScene("SampleScene");
    }
    
    /// <summary>
    /// Возвращается к главному меню из панели выбора режима
    /// </summary>
    public void BackToMainMenu()
    {
        PlayButtonClickSound();
        if (gameModePanel != null)
        {
            gameModePanel.SetActive(false);
        }
        if (mainMenuButtons != null)
        {
            mainMenuButtons.SetActive(true);
        }
    }

    public void QuitGame()
    {
        PlayButtonClickSound();
        Application.Quit();
    }
    
    /// <summary>
    /// Открывает панель обучения
    /// </summary>
    public void OpenHowToPlay()
    {
        PlayButtonClickSound();
        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(true);
        }
        
        // Если есть менеджер видео, можно запустить воспроизведение
        if (tutorialVideoManager != null)
        {
            tutorialVideoManager.ResumeVideo();
        }
    }

    /// <summary>
    /// Закрывает панель обучения
    /// </summary>
    public void CloseHowToPlay()
    {
        PlayButtonClickSound();
        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(false);
        }
        
        // Останавливаем видео при закрытии
        if (tutorialVideoManager != null)
        {
            tutorialVideoManager.StopVideo();
        }
    }
    
    /// <summary>
    /// Открывает панель настроек
    /// </summary>
    public void OpenSettings()
    {
        PlayButtonClickSound();
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            // Инициализируем слайдеры при открытии панели настроек
            InitializeVolumeSliders();
        }
        
        // Скрываем кнопки главного меню
        if (mainMenuButtons != null)
        {
            mainMenuButtons.SetActive(false);
        }
    }
    
    /// <summary>
    /// Закрывает панель настроек
    /// </summary>
    public void CloseSettings()
    {
        PlayButtonClickSound();
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        
        // Показываем кнопки главного меню обратно
        if (mainMenuButtons != null)
        {
            mainMenuButtons.SetActive(true);
        }
    }
    
    /// <summary>
    /// Воспроизводит звук клика кнопки
    /// </summary>
    private void PlayButtonClickSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
    }
    
    void Start()
    {
        // Убеждаемся, что AudioManager существует
        AudioManager.EnsureInstanceExists();
        
        // Закрываем панель настроек при старте (если она была открыта)
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        
        // Закрываем панель выбора режима игры при старте
        if (gameModePanel != null)
        {
            gameModePanel.SetActive(false);
        }
        
        // Применяем сохраненные настройки громкости к музыке главного меню
        if (mainMenuMusicSource != null)
        {
            float savedMusicVolume = PlayerPrefs.HasKey("MusicVolume") ? PlayerPrefs.GetFloat("MusicVolume") : 1f;
            mainMenuMusicSource.volume = savedMusicVolume;
        }
    }
    
    /// <summary>
    /// Инициализирует слайдеры громкости текущими значениями из PlayerPrefs
    /// </summary>
    private void InitializeVolumeSliders()
    {
        // Загружаем сохраненные значения из PlayerPrefs
        float savedMusicVolume = PlayerPrefs.HasKey("MusicVolume") ? PlayerPrefs.GetFloat("MusicVolume") : 1f;
        float savedSFXVolume = PlayerPrefs.HasKey("SFXVolume") ? PlayerPrefs.GetFloat("SFXVolume") : 1f;
        
        // Инициализируем временные значения
        tempMusicVolume = savedMusicVolume;
        tempSFXVolume = savedSFXVolume;
        
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = savedMusicVolume;
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeSliderChanged);
        }
        
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = savedSFXVolume;
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeSliderChanged);
        }
    }
    
    /// <summary>
    /// Вызывается при изменении слайдера громкости музыки (сохраняет во временную переменную)
    /// </summary>
    private void OnMusicVolumeSliderChanged(float value)
    {
        tempMusicVolume = value;
        // Применяем сразу к музыке главного меню
        if (mainMenuMusicSource != null)
        {
            mainMenuMusicSource.volume = value;
        }
        // Применяем к AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
    }
    
    /// <summary>
    /// Вызывается при изменении слайдера громкости звуковых эффектов (сохраняет во временную переменную)
    /// </summary>
    private void OnSFXVolumeSliderChanged(float value)
    {
        tempSFXVolume = value;
        // Применяем к AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }
    }
    
    /// <summary>
    /// Применяет изменения громкости (вызывается кнопкой "Применить")
    /// </summary>
    public void ApplySettings()
    {
        PlayButtonClickSound();
        
        // Сохраняем настройки в PlayerPrefs
        PlayerPrefs.SetFloat("MusicVolume", tempMusicVolume);
        PlayerPrefs.SetFloat("SFXVolume", tempSFXVolume);
        PlayerPrefs.Save();
        
        // Применяем к музыке главного меню (если есть)
        if (mainMenuMusicSource != null)
        {
            mainMenuMusicSource.volume = tempMusicVolume;
        }
        
        // Применяем к AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(tempMusicVolume);
            AudioManager.Instance.SetSFXVolume(tempSFXVolume);
        }
        
        // Закрываем панель настроек
        CloseSettings();
    }
}
