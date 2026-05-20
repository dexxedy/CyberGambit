using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Панель целей миссии. Повесь на свой UI-объект в иерархии, укажи TMP и конфиг.
/// Прогресс: <see cref="SetProgress"/>(индекс, текущее, всего), <see cref="Complete"/>(индекс).
/// </summary>
public class MissionQuestUI : MonoBehaviour
{
    public static MissionQuestUI Instance { get; private set; }

    [Header("Конфиг")]
    [SerializeField] private MissionObjectivesConfig config;

    [Header("UI (назначь из иерархии)")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI objectivesText;

    [Header("Видимость")]
    [Tooltip("Дочерняя панель для скрытия. Пусто — скрытие через CanvasGroup на этом объекте (не выключаем GameObject, иначе Update не сработает).")]
    [SerializeField] private GameObject visibilityRoot;
    [SerializeField] private bool hideDuringArmyDeployment = true;
    [SerializeField] private bool hideDuringBotTurn = true;
    [SerializeField] private bool hideWhenPaused = true;
    [SerializeField] private bool hideOnGameOver = true;

    private ObjectiveState[] states;
    private CanvasGroup canvasGroup;
    private bool useCanvasGroupOnSelf;

    private struct ObjectiveState
    {
        public int current;
        public int target;
        public bool completed;
        public bool showCounter;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("MissionQuestUI: в сцене уже есть экземпляр, дубликат отключён.", this);
            enabled = false;
            return;
        }

        Instance = this;
        SetupVisibility();
        InitStates();
        Refresh();
        ApplyVisibility(ShouldBeVisible());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        ApplyVisibility(ShouldBeVisible());
    }

    private void SetupVisibility()
    {
        useCanvasGroupOnSelf = visibilityRoot == null || visibilityRoot == gameObject;
        if (!useCanvasGroupOnSelf)
            return;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void ApplyVisibility(bool show)
    {
        if (useCanvasGroupOnSelf)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = show ? 1f : 0f;
            canvasGroup.blocksRaycasts = show;
            canvasGroup.interactable = show;
            return;
        }

        if (visibilityRoot != null && visibilityRoot.activeSelf != show)
            visibilityRoot.SetActive(show);
    }

    private bool ShouldBeVisible()
    {
        if (config == null || config.objectiveTexts == null || config.objectiveTexts.Length == 0)
            return false;

        if (GameManager.Instance == null)
            return true;

        if (hideDuringArmyDeployment && GameManager.Instance.IsArmyDeploymentPhase())
            return false;
        if (hideDuringBotTurn && GameManager.Instance.IsBotTurn())
            return false;
        if (hideOnGameOver && GameManager.Instance.IsGameOver())
            return false;
        if (hideWhenPaused && GameManager.Instance.IsPaused())
            return false;

        return true;
    }

    private void InitStates()
    {
        if (config == null || config.objectiveTexts == null)
        {
            states = System.Array.Empty<ObjectiveState>();
            return;
        }

        states = new ObjectiveState[config.objectiveTexts.Length];
    }

    /// <summary>Счётчик: «текст (2/5)». При current &gt;= target цель считается выполненной.</summary>
    public void SetProgress(int index, int current, int target)
    {
        if (!IsValidIndex(index))
            return;

        target = Mathf.Max(1, target);
        current = Mathf.Clamp(current, 0, target);

        states[index] = new ObjectiveState
        {
            current = current,
            target = target,
            completed = current >= target,
            showCounter = true
        };

        Refresh();
    }

    /// <summary>Отметить цель выполненной (зачёркивание).</summary>
    public void Complete(int index)
    {
        if (!IsValidIndex(index))
            return;

        ObjectiveState st = states[index];
        st.completed = true;
        if (st.showCounter && st.target > 0)
            st.current = st.target;
        states[index] = st;
        Refresh();
    }

    public void Refresh()
    {
        if (titleText != null)
            titleText.text = config != null && !string.IsNullOrWhiteSpace(config.panelTitle)
                ? config.panelTitle
                : "Задачи миссии";

        if (objectivesText == null)
            return;

        if (config == null || config.objectiveTexts == null || config.objectiveTexts.Length == 0)
        {
            objectivesText.text = "—";
            return;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < config.objectiveTexts.Length; i++)
        {
            if (i > 0)
                sb.Append('\n');
            sb.Append(FormatLine(config.objectiveTexts[i], i));
        }

        objectivesText.text = sb.ToString();
    }

    private string FormatLine(string text, int index)
    {
        if (string.IsNullOrWhiteSpace(text))
            text = $"Цель {index + 1}";

        text = text.Trim();
        ObjectiveState st = IsValidIndex(index) ? states[index] : default;

        if (st.showCounter && st.target > 0)
            text = $"{text} ({st.current}/{st.target})";

        text = "• " + text;
        return st.completed ? $"<s>{text}</s>" : text;
    }

    private bool IsValidIndex(int index)
    {
        return states != null && index >= 0 && index < states.Length;
    }
}
