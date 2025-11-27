using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject howToPlayPanel;
    public void PlayGame()
    {
        SceneManager.LoadScene("SampleScene");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
    public void OpenHowToPlay()
    {
        howToPlayPanel.SetActive(true);
    }

    // ← НОВОЕ: Закрыть панель обучения
    public void CloseHowToPlay()
    {
        howToPlayPanel.SetActive(false);
    }
}
