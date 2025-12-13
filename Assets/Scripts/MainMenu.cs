using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject howToPlayPanel;
    [SerializeField] private TutorialVideoManager tutorialVideoManager; // Ссылка на менеджер видео (опционально)
    
    public void PlayGame()
    {
        SceneManager.LoadScene("SampleScene");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
    
    /// <summary>
    /// Открывает панель обучения
    /// </summary>
    public void OpenHowToPlay()
    {
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
}
