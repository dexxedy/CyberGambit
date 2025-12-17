using UnityEngine;

/// <summary>
/// Централизованный менеджер звуков для управления UI звуками, QTE звуками и фоновой музыкой.
/// Боевые звуки и звуки движения воспроизводятся локально на юнитах.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    
    [Header("UI Sounds")]
    [SerializeField] private AudioClip buttonClick;
    [SerializeField] private AudioClip enterActionMode;
    [SerializeField] private AudioClip unitSelect;
    
    [Header("QTE Sounds")]
    [SerializeField] private AudioClip qteActivate;
    [SerializeField] private AudioClip qteSuccess;
    [SerializeField] private AudioClip qteFail;
    
    [Header("Music")]
    [SerializeField] private AudioClip gameMusic;
    [SerializeField] private AudioClip victoryMusic;
    [SerializeField] private AudioClip defeatMusic;
    
    [Header("Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;
    
    private AudioSource musicSource; // Для фоновой музыки
    private AudioSource sfxSource;   // Для UI и QTE звуков
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
            LoadVolumeSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Создает AudioManager автоматически, если его нет в сцене
    /// </summary>
    public static void EnsureInstanceExists()
    {
        if (Instance == null)
        {
            GameObject audioManagerObject = new GameObject("AudioManager");
            audioManagerObject.AddComponent<AudioManager>();
        }
    }
    
    void Start()
    {
        // Автоматически запускаем фоновую музыку игры только если мы в игровой сцене
        // (не в главном меню, где уже есть своя музыка)
        if (gameMusic != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu")
        {
            PlayGameMusic();
        }
    }
    
    void Update()
    {
        // Проверяем, что музыка не остановилась (защита от случайной остановки)
        if (musicSource != null && musicSource.clip != null && musicSource.clip == gameMusic && 
            musicSource.loop && !musicSource.isPlaying && musicVolume > 0f)
        {
            // Если музыка должна играть, но остановилась, перезапускаем её
            musicSource.Play();
        }
    }
    
    /// <summary>
    /// Инициализирует AudioSource компоненты
    /// </summary>
    private void InitializeAudioSources()
    {
        // Создаем AudioSource для музыки
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.volume = musicVolume;
        musicSource.spatialBlend = 0f; // 2D звук
        musicSource.ignoreListenerPause = true; // Музыка продолжает играть при паузе
        
        // Создаем AudioSource для звуковых эффектов
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.volume = sfxVolume;
        sfxSource.spatialBlend = 0f; // 2D звук
        sfxSource.ignoreListenerPause = true; // UI звуки продолжают играть при паузе
    }
    
    // ========== UI ЗВУКИ ==========
    
    /// <summary>
    /// Воспроизводит звук клика кнопки
    /// </summary>
    public void PlayButtonClick()
    {
        if (buttonClick != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(buttonClick);
        }
    }
    
    /// <summary>
    /// Воспроизводит звук перехода в экшен-режим
    /// </summary>
    public void PlayEnterActionMode()
    {
        if (enterActionMode != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(enterActionMode);
        }
    }
    
    /// <summary>
    /// Воспроизводит звук выбора юнита
    /// </summary>
    public void PlayUnitSelect()
    {
        if (unitSelect != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(unitSelect);
        }
    }
    
    // ========== QTE ЗВУКИ ==========
    
    /// <summary>
    /// Воспроизводит звук активации QTE
    /// </summary>
    public void PlayQTEAactivate()
    {
        if (qteActivate != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(qteActivate);
        }
    }
    
    /// <summary>
    /// Воспроизводит звук успешного QTE
    /// </summary>
    public void PlayQTESuccess()
    {
        if (qteSuccess != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(qteSuccess);
        }
    }
    
    /// <summary>
    /// Воспроизводит звук провала QTE
    /// </summary>
    public void PlayQTEFail()
    {
        if (qteFail != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(qteFail);
        }
    }
    
    // ========== МУЗЫКА ==========
    
    /// <summary>
    /// Воспроизводит фоновую музыку игры
    /// </summary>
    public void PlayGameMusic()
    {
        if (gameMusic != null && musicSource != null)
        {
            // Если уже играет та же музыка, не перезапускаем
            if (musicSource.clip == gameMusic && musicSource.isPlaying)
            {
                return;
            }
            
            musicSource.clip = gameMusic;
            musicSource.loop = true; // Убеждаемся, что музыка зациклена
            // Убеждаемся, что громкость установлена правильно перед воспроизведением
            musicSource.volume = musicVolume;
            musicSource.enabled = true; // Убеждаемся, что AudioSource включен
            musicSource.Play();
        }
    }
    
    /// <summary>
    /// Воспроизводит музыку победы
    /// </summary>
    public void PlayVictoryMusic()
    {
        if (victoryMusic != null && musicSource != null)
        {
            musicSource.clip = victoryMusic;
            musicSource.loop = false;
            // Убеждаемся, что громкость установлена правильно перед воспроизведением
            musicSource.volume = musicVolume;
            musicSource.Play();
        }
    }
    
    /// <summary>
    /// Воспроизводит музыку поражения
    /// </summary>
    public void PlayDefeatMusic()
    {
        if (defeatMusic != null && musicSource != null)
        {
            musicSource.clip = defeatMusic;
            musicSource.loop = false;
            // Убеждаемся, что громкость установлена правильно перед воспроизведением
            musicSource.volume = musicVolume;
            musicSource.Play();
        }
    }
    
    /// <summary>
    /// Останавливает музыку
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }
    
    // ========== НАСТРОЙКИ ГРОМКОСТИ ==========
    
    /// <summary>
    /// Устанавливает громкость музыки
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }
    }
    
    /// <summary>
    /// Устанавливает громкость звуковых эффектов
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }
    }
    
    /// <summary>
    /// Получает текущую громкость музыки
    /// </summary>
    public float GetMusicVolume() => musicVolume;
    
    /// <summary>
    /// Получает текущую громкость звуковых эффектов
    /// </summary>
    public float GetSFXVolume() => sfxVolume;
    
    /// <summary>
    /// Публичное свойство для доступа к громкости музыки
    /// </summary>
    public float MusicVolume => musicVolume;
    
    /// <summary>
    /// Публичное свойство для доступа к громкости звуковых эффектов
    /// </summary>
    public float SFXVolume => sfxVolume;
    
    /// <summary>
    /// Сохраняет настройки громкости в PlayerPrefs
    /// </summary>
    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Загружает настройки громкости из PlayerPrefs
    /// </summary>
    private void LoadVolumeSettings()
    {
        if (PlayerPrefs.HasKey("MusicVolume"))
        {
            musicVolume = PlayerPrefs.GetFloat("MusicVolume");
        }
        if (PlayerPrefs.HasKey("SFXVolume"))
        {
            sfxVolume = PlayerPrefs.GetFloat("SFXVolume");
        }
        
        // Применяем загруженные настройки
        SetMusicVolume(musicVolume);
        SetSFXVolume(sfxVolume);
    }
    
    /// <summary>
    /// Устанавливает громкость музыки с сохранением в PlayerPrefs
    /// </summary>
    public void SetMusicVolumeWithSave(float volume)
    {
        SetMusicVolume(volume);
        SaveVolumeSettings();
    }
    
    /// <summary>
    /// Устанавливает громкость звуковых эффектов с сохранением в PlayerPrefs
    /// </summary>
    public void SetSFXVolumeWithSave(float volume)
    {
        SetSFXVolume(volume);
        SaveVolumeSettings();
    }
}

