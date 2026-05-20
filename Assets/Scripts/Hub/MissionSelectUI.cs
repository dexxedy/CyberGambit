using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

// Re-save to force recompile after MissionDefinition was added
public class MissionSelectUI : MonoBehaviour
{
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnPrev;
    [SerializeField] private Button btnNext;
    [SerializeField] private Button btnSelect;

    [SerializeField] private TextMeshProUGUI missionNameText;
    [SerializeField] private TextMeshProUGUI missionObjectiveText;
    [SerializeField] private Image previewImage;

    [SerializeField] private MissionDefinition[] missions;
 
    public static MissionSelectUI Instance;
 
    private int currentIndex = 0;
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
 
    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;
 
    public void Initialize(GameObject root, Button close, Button prev, Button next, Button select, TextMeshProUGUI nameTxt, TextMeshProUGUI objTxt, Image preview, MissionDefinition[] missionList)
    {
        rootPanel = root;
        btnClose = close;
        btnPrev = prev;
        btnNext = next;
        btnSelect = select;
        missionNameText = nameTxt;
        missionObjectiveText = objTxt;
        previewImage = preview;
        missions = missionList;
    }

    private void Awake()
    {
        Instance = this;
    }
 
    private void Start()
    {
        if (btnClose != null) btnClose.onClick.AddListener(Hide);
        if (btnPrev != null) btnPrev.onClick.AddListener(Prev);
        if (btnNext != null) btnNext.onClick.AddListener(Next);
        if (btnSelect != null) btnSelect.onClick.AddListener(SelectCurrent);

        if (rootPanel != null) rootPanel.SetActive(false);
    }

    public void Show()
    {
        if (missions == null || missions.Length == 0) return;

        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (rootPanel != null) rootPanel.SetActive(true);
        RefreshView();
    }

    public void Hide()
    {
        PlayClickSound();
        if (rootPanel != null) rootPanel.SetActive(false);

        if (CameraManager.Instance != null && CameraManager.Instance.IsHubExploreMode())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;
        }
    }

    public void Next()
    {
        PlayClickSound();
        currentIndex = (currentIndex + 1) % missions.Length;
        RefreshView();
    }

    public void Prev()
    {
        PlayClickSound();
        currentIndex = (currentIndex - 1 + missions.Length) % missions.Length;
        RefreshView();
    }

    public void SelectCurrent()
    {
        PlayClickSound();
        string sceneToLoad = missions[currentIndex].sceneName;
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    private void RefreshView()
    {
        if (currentIndex < 0 || currentIndex >= missions.Length) return;

        var m = missions[currentIndex];
        missionNameText.text = m.displayName;
        missionObjectiveText.text = m.objectiveShort;
        
        if (m.previewSprite != null)
        {
            previewImage.sprite = m.previewSprite;
            previewImage.color = Color.white;
        }
        else
        {
            previewImage.sprite = null;
            previewImage.color = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark gray placeholder
        }

        bool multi = missions.Length > 1;
        btnPrev.gameObject.SetActive(multi);
        btnNext.gameObject.SetActive(multi);
    }

    private void PlayClickSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
    }
}