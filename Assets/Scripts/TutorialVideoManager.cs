using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Управляет воспроизведением нескольких обучающих видео с навигацией вперед/назад.
/// Каждое видео зациклено и автоматически воспроизводится.
/// </summary>
public class TutorialVideoManager : MonoBehaviour
{
    [Header("Video Setup")]
    [SerializeField] private VideoPlayer videoPlayer; // Компонент VideoPlayer
    [SerializeField] private VideoClip[] tutorialVideos; // Массив видео для обучения
    [SerializeField] private RenderTexture renderTexture; // RenderTexture для отображения видео
    
    [Header("UI Elements")]
    [SerializeField] private Button previousButton; // Кнопка "Назад"
    [SerializeField] private Button nextButton; // Кнопка "Вперед"
    [SerializeField] private TextMeshProUGUI videoCounterText; // Текст с номером видео (опционально, например "1/3")
    [SerializeField] private GameObject videoPanel; // Панель с видео (для управления видимостью)
    
    [Header("Settings")]
    [SerializeField] private bool autoPlayOnStart = true; // Автоматически воспроизводить при открытии панели
    [SerializeField] private bool loopVideos = true; // Зацикливать каждое видео
    
    private int currentVideoIndex = 0; // Индекс текущего видео
    
    void Start()
    {
        // Инициализация VideoPlayer
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }
        
        if (videoPlayer == null)
        {
            Debug.LogError("TutorialVideoManager: VideoPlayer не найден! Добавьте VideoPlayer компонент.");
            return;
        }
        
        // Настройка VideoPlayer
        if (renderTexture != null)
        {
            videoPlayer.targetTexture = renderTexture;
        }
        
        videoPlayer.isLooping = loopVideos;
        videoPlayer.playOnAwake = false;
        
        // Настройка кнопок
        if (previousButton != null)
        {
            previousButton.onClick.AddListener(PreviousVideo);
        }
        
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextVideo);
        }
        
        // Инициализация первого видео
        if (tutorialVideos != null && tutorialVideos.Length > 0)
        {
            LoadVideo(0);
        }
        else
        {
            Debug.LogWarning("TutorialVideoManager: Нет видео для воспроизведения!");
        }
    }
    
    void OnEnable()
    {
        // Когда панель открывается, загружаем текущее видео
        if (tutorialVideos != null && tutorialVideos.Length > 0)
        {
            LoadVideo(currentVideoIndex);
            
            if (autoPlayOnStart)
            {
                PlayCurrentVideo();
            }
        }
    }
    
    void OnDisable()
    {
        // Останавливаем видео при закрытии панели
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }
    }
    
    /// <summary>
    /// Загружает видео по индексу
    /// </summary>
    private void LoadVideo(int index)
    {
        if (tutorialVideos == null || tutorialVideos.Length == 0)
        {
            Debug.LogWarning("TutorialVideoManager: Нет доступных видео!");
            return;
        }
        
        if (index < 0 || index >= tutorialVideos.Length)
        {
            Debug.LogWarning($"TutorialVideoManager: Неверный индекс видео {index}. Доступно видео: {tutorialVideos.Length}");
            return;
        }
        
        if (tutorialVideos[index] == null)
        {
            Debug.LogWarning($"TutorialVideoManager: Видео с индексом {index} равно null!");
            return;
        }
        
        currentVideoIndex = index;
        
        // Загружаем видео
        videoPlayer.clip = tutorialVideos[index];
        
        // Обновляем UI
        UpdateUI();
    }
    
    /// <summary>
    /// Воспроизводит текущее видео
    /// </summary>
    private void PlayCurrentVideo()
    {
        if (videoPlayer != null && videoPlayer.clip != null)
        {
            videoPlayer.Play();
        }
    }
    
    /// <summary>
    /// Переходит к следующему видео
    /// </summary>
    public void NextVideo()
    {
        if (tutorialVideos == null || tutorialVideos.Length == 0) return;
        
        int nextIndex = (currentVideoIndex + 1) % tutorialVideos.Length;
        LoadVideo(nextIndex);
        PlayCurrentVideo();
    }
    
    /// <summary>
    /// Переходит к предыдущему видео
    /// </summary>
    public void PreviousVideo()
    {
        if (tutorialVideos == null || tutorialVideos.Length == 0) return;
        
        int prevIndex = (currentVideoIndex - 1 + tutorialVideos.Length) % tutorialVideos.Length;
        LoadVideo(prevIndex);
        PlayCurrentVideo();
    }
    
    /// <summary>
    /// Обновляет UI (кнопки, счетчик)
    /// </summary>
    private void UpdateUI()
    {
        // Обновляем счетчик видео (если есть)
        if (videoCounterText != null && tutorialVideos != null && tutorialVideos.Length > 0)
        {
            videoCounterText.text = $"{currentVideoIndex + 1}/{tutorialVideos.Length}";
        }
        
        // Кнопки всегда активны (можно переключаться по кругу)
        // Если нужно отключать кнопки на первом/последнем видео, раскомментируйте:
        /*
        if (previousButton != null)
        {
            previousButton.interactable = currentVideoIndex > 0;
        }
        
        if (nextButton != null)
        {
            nextButton.interactable = currentVideoIndex < tutorialVideos.Length - 1;
        }
        */
    }
    
    /// <summary>
    /// Останавливает воспроизведение видео
    /// </summary>
    public void StopVideo()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }
    }
    
    /// <summary>
    /// Возобновляет воспроизведение видео
    /// </summary>
    public void ResumeVideo()
    {
        if (videoPlayer != null && videoPlayer.clip != null && !videoPlayer.isPlaying)
        {
            videoPlayer.Play();
        }
    }
    
    /// <summary>
    /// Переходит к конкретному видео по индексу
    /// </summary>
    public void GoToVideo(int index)
    {
        if (tutorialVideos != null && index >= 0 && index < tutorialVideos.Length)
        {
            LoadVideo(index);
            PlayCurrentVideo();
        }
    }
}

