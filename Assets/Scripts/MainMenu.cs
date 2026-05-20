using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private const string HubSceneName = "HubBase";

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private GameObject mainMenuButtons;

    [Header("Main Menu Music")]
    [SerializeField] private AudioSource mainMenuMusicSource;

    [Header("Volume Settings")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    private float tempMusicVolume = 1f;
    private float tempSFXVolume = 1f;

    public void PlayGame()
    {
        PlayButtonClickSound();
        SceneManager.LoadScene(HubSceneName);
    }

    public void QuitGame()
    {
        PlayButtonClickSound();
        Application.Quit();
    }

    public void OpenSettings()
    {
        PlayButtonClickSound();
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            InitializeVolumeSliders();
        }

        if (mainMenuButtons != null)
            mainMenuButtons.SetActive(false);
    }

    public void CloseSettings()
    {
        PlayButtonClickSound();
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (mainMenuButtons != null)
            mainMenuButtons.SetActive(true);
    }

    private void PlayButtonClickSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
    }

    void Start()
    {
        AudioManager.EnsureInstanceExists();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (mainMenuMusicSource != null)
        {
            float savedMusicVolume = PlayerPrefs.HasKey("MusicVolume") ? PlayerPrefs.GetFloat("MusicVolume") : 1f;
            mainMenuMusicSource.volume = savedMusicVolume;
        }
    }

    private void InitializeVolumeSliders()
    {
        float savedMusicVolume = PlayerPrefs.HasKey("MusicVolume") ? PlayerPrefs.GetFloat("MusicVolume") : 1f;
        float savedSFXVolume = PlayerPrefs.HasKey("SFXVolume") ? PlayerPrefs.GetFloat("SFXVolume") : 1f;

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

    private void OnMusicVolumeSliderChanged(float value)
    {
        tempMusicVolume = value;
        if (mainMenuMusicSource != null)
            mainMenuMusicSource.volume = value;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);
    }

    private void OnSFXVolumeSliderChanged(float value)
    {
        tempSFXVolume = value;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSFXVolume(value);
    }

    public void ApplySettings()
    {
        PlayButtonClickSound();

        PlayerPrefs.SetFloat("MusicVolume", tempMusicVolume);
        PlayerPrefs.SetFloat("SFXVolume", tempSFXVolume);
        PlayerPrefs.Save();

        if (mainMenuMusicSource != null)
            mainMenuMusicSource.volume = tempMusicVolume;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(tempMusicVolume);
            AudioManager.Instance.SetSFXVolume(tempSFXVolume);
        }

        CloseSettings();
    }
}
