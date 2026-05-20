using UnityEngine;
using TMPro;

public class HubInteractionHintUI : MonoBehaviour
{
    public static HubInteractionHintUI Instance;

    [SerializeField] private GameObject hintPanel;
    [SerializeField] private TextMeshProUGUI hintText;

    public void Initialize(GameObject panel, TextMeshProUGUI text)
    {
        hintPanel = panel;
        hintText = text;
    }

    private void Awake()
    {
        Instance = this;
        if (hintPanel != null) hintPanel.SetActive(false);
    }

    public void ShowPrompt(string message = "Нажмите [E] на компьютер, чтобы выбрать миссию")
    {
        if (hintPanel == null) return;
        if (hintText != null) hintText.text = message;
        hintPanel.SetActive(true);
    }

    public void HidePrompt()
    {
        if (hintPanel != null) hintPanel.SetActive(false);
    }
}
