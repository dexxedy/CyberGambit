using UnityEngine;
using TMPro;

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
    [SerializeField] private TextMeshProUGUI timerText; // Текст таймера (опционально)
    [SerializeField] private GameObject blockKeyHint; // Подсказка для клавиши блокирования (GameObject)
    [SerializeField] private TextMeshProUGUI blockKeyText; // Текст внутри blockKeyHint (показывает клавишу Z/X/C)
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    /// <summary>
    /// Показывает UI QTE с подсказками в зависимости от возможностей фигуры
    /// </summary>
    /// <param name="unitType">Тип фигуры игрока</param>
    /// <param name="capabilities">Возможности фигуры (может ли блокировать)</param>
    /// <param name="qteKey">Случайная клавиша для QTE (Z, X или C)</param>
    public void ShowQTE(ChessUnitType unitType, QTECapabilities capabilities, QTEKey qteKey)
    {
        // Активируем панель QTE
        if (qtePanel != null)
            qtePanel.SetActive(true);
        
        // Показываем/скрываем подсказку для блокирования в зависимости от возможностей фигуры
        if (blockKeyHint != null)
            blockKeyHint.SetActive(capabilities.canBlock);
        
        // Обновляем текст клавиши в подсказке (Z, X или C)
        if (capabilities.canBlock && blockKeyText != null)
        {
            string keyName = qteKey.ToString().ToUpper();
            blockKeyText.text = keyName;
        }
        
        // Устанавливаем текст инструкции в зависимости от возможностей
        if (qteInstructionText != null)
        {
            string instruction = "ВРАГ АТАКУЕТ! ";
            
            if (capabilities.canBlock)
            {
                // Фигура может блокировать - показываем случайную клавишу
                string keyName = qteKey.ToString().ToUpper();
                instruction += $"{keyName} - Блокировать";
            }
            else
            {
                // Фигура не может блокировать (например, Пешка) - нет доступных действий
                instruction += "Нет доступных действий!";
            }
            
            qteInstructionText.text = instruction;
        }
        
        // Инициализируем таймер (если есть)
        if (timerText != null)
        {
            timerText.text = "";
        }
    }
    
    /// <summary>
    /// Скрывает UI QTE
    /// </summary>
    public void HideQTE()
    {
        if (qtePanel != null)
            qtePanel.SetActive(false);
    }
    
    /// <summary>
    /// Обновляет таймер QTE (опционально, для визуального отображения оставшегося времени)
    /// </summary>
    /// <param name="remainingTime">Оставшееся время</param>
    /// <param name="totalTime">Общее время QTE</param>
    public void UpdateTimer(float remainingTime, float totalTime)
    {
        if (timerText != null)
        {
            float percentage = remainingTime / totalTime;
            timerText.text = $"{remainingTime:F1}";
            // Можно добавить визуальный индикатор (полоска прогресса)
        }
    }
}

