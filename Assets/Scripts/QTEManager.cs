using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Управляет UI для системы QTE (Quick Time Events).
/// Показывает подсказки игроку о том, какие клавиши нажать для реакции на атаку врага.
/// </summary>
public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance;
    
    [Header("UI Elements")]
    [SerializeField] private GameObject qtePanel; // Главная панель QTE
    [SerializeField] private TextMeshProUGUI qteInstructionText; // Текст с инструкциями
    
    [Header("QTE Sequence UI")]
    [SerializeField] private GameObject[] keyHints = new GameObject[3]; // Подсказки для 3 клавиш (GameObject)
    [SerializeField] private TextMeshProUGUI[] keyTexts = new TextMeshProUGUI[3]; // Тексты внутри keyHints (показывают клавиши Z/X/C)
    [SerializeField] private Color defaultKeyColor = Color.white; // Цвет неактивной клавиши
    [SerializeField] private Color activeKeyColor = Color.yellow; // Цвет текущей клавиши
    [SerializeField] private Color completedKeyColor = Color.green; // Цвет выполненной клавиши
    [SerializeField] private Color failedKeyColor = Color.red; // Цвет неправильной клавиши
    
    [Header("QTE Timer (only during QTE)")]
    [SerializeField] private TextMeshProUGUI qteTimerText;
    [SerializeField] private Image qteTimerFill;
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
        
        ResolveTimerReferences();
    }
    
    void ResolveTimerReferences()
    {
        if (qtePanel == null)
            return;
        
        var panelRoot = qtePanel.transform;
        if (qteTimerText == null)
        {
            var timerTransform = panelRoot.Find("QTE Timer Text");
            if (timerTransform != null)
                qteTimerText = timerTransform.GetComponent<TextMeshProUGUI>();
        }
        
        if (qteTimerFill == null)
        {
            var fillTransform = panelRoot.Find("QTE Timer Fill");
            if (fillTransform != null)
                qteTimerFill = fillTransform.GetComponent<Image>();
        }
    }
    
    /// <summary>
    /// Показывает UI QTE с последовательностью из 3 клавиш
    /// </summary>
    /// <param name="unitType">Тип фигуры игрока</param>
    /// <param name="capabilities">Возможности фигуры (может ли блокировать)</param>
    /// <param name="sequence">Последовательность из 3 клавиш для QTE</param>
    public void ShowQTE(ChessUnitType unitType, QTECapabilities capabilities, List<QTEKey> sequence)
    {
        // Активируем панель QTE
        if (qtePanel != null)
        {
            qtePanel.SetActive(true);
        }
        
        // Устанавливаем текст инструкции
        if (qteInstructionText != null)
        {
            string instruction = "";
            
            if (capabilities.canBlock)
            {
                instruction = "Враг атакует вашего юнита! Для блокирования атаки нажмите последовательность:";
            }
            
            qteInstructionText.text = instruction;
        }
        
        // Показываем последовательность из 3 клавиш
        if (capabilities.canBlock && sequence != null && sequence.Count >= 3)
        {
            for (int i = 0; i < 3; i++)
            {
                // Показываем подсказку для клавиши
                if (keyHints != null && i < keyHints.Length && keyHints[i] != null)
                {
                    keyHints[i].SetActive(true);
                }
                
                // Обновляем текст клавиши
                if (keyTexts != null && i < keyTexts.Length && keyTexts[i] != null)
                {
                    string keyName = sequence[i].ToString().ToUpper();
                    keyTexts[i].text = keyName;
                    keyTexts[i].color = i == 0 ? activeKeyColor : defaultKeyColor; // Первая клавиша активна
                }
            }
            
            // Скрываем лишние подсказки (если их больше 3)
            if (keyHints != null)
            {
                for (int i = 3; i < keyHints.Length; i++)
                {
                    if (keyHints[i] != null)
                    {
                        keyHints[i].SetActive(false);
                    }
                }
            }
        }
        else
        {
            // Скрываем все подсказки если фигура не может блокировать
            if (keyHints != null)
            {
                foreach (var hint in keyHints)
                {
                    if (hint != null)
                        hint.SetActive(false);
                }
            }
        }
        
        SetTimerVisible(false);
    }
    
    /// <summary>
    /// Скрывает UI QTE
    /// </summary>
    public void HideQTE()
    {
        SetTimerVisible(false);
        
        if (qtePanel != null)
            qtePanel.SetActive(false);
    }
    
    /// <summary>
    /// Показывает таймер QTE и сбрасывает отображение (вызывается при старте фазы ввода).
    /// </summary>
    public void ShowTimer(float durationSeconds)
    {
        SetTimerVisible(true);
        UpdateTimer(durationSeconds, durationSeconds);
    }
    
    /// <summary>
    /// Обновляет таймер QTE (работает при timeScale = 0 через unscaled время в QTESystem).
    /// </summary>
    public void UpdateTimer(float secondsRemaining, float totalSeconds)
    {
        if (qteTimerText != null)
        {
            int display = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining));
            qteTimerText.text = $"Таймер: {display}";
        }
        
        if (qteTimerFill != null && totalSeconds > 0f)
            qteTimerFill.fillAmount = Mathf.Clamp01(secondsRemaining / totalSeconds);
    }
    
    void SetTimerVisible(bool visible)
    {
        if (qteTimerText != null)
            qteTimerText.gameObject.SetActive(visible);
        
        if (qteTimerFill != null)
            qteTimerFill.gameObject.SetActive(visible);
    }
    
    /// <summary>
    /// Обновляет прогресс последовательности клавиш
    /// </summary>
    /// <param name="currentIndex">Текущий индекс в последовательности (сколько клавиш уже нажато)</param>
    /// <param name="totalKeys">Общее количество клавиш в последовательности</param>
    public void UpdateSequenceProgress(int currentIndex, int totalKeys)
    {
        if (keyTexts == null) return;
        
        // Обновляем цвета клавиш в зависимости от прогресса
        for (int i = 0; i < keyTexts.Length && i < totalKeys; i++)
        {
            if (keyTexts[i] != null)
            {
                if (i < currentIndex)
                {
                    // Клавиша уже нажата - зелёный
                    keyTexts[i].color = completedKeyColor;
                }
                else if (i == currentIndex)
                {
                    // Текущая клавиша - жёлтый
                    keyTexts[i].color = activeKeyColor;
                }
                else
                {
                    // Ещё не нажата - белый
                    keyTexts[i].color = defaultKeyColor;
                }
            }
        }
    }
    
    /// <summary>
    /// Подсвечивает клавишу (используется при правильном нажатии)
    /// </summary>
    /// <param name="keyIndex">Индекс клавиши в последовательности</param>
    /// <param name="isCorrect">Правильно ли нажата клавиша</param>
    public void HighlightKey(int keyIndex, bool isCorrect)
    {
        if (keyTexts == null || keyIndex < 0 || keyIndex >= keyTexts.Length)
            return;
        
        if (keyTexts[keyIndex] != null)
        {
            if (isCorrect)
            {
                keyTexts[keyIndex].color = completedKeyColor;
            }
            else
            {
                keyTexts[keyIndex].color = failedKeyColor;
            }
        }
    }
}

