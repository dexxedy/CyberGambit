using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Управляет системой Quick Time Events (QTE) при входе в зону угрозы врага.
/// Обрабатывает реакцию игрока (блокирование) - успешный блок полностью блокирует урон, неудача - полный урон.
/// </summary>
public class QTESystem : MonoBehaviour
{
    public static QTESystem Instance;
    
    [Header("QTE Settings")]
    [SerializeField] private float qteWindowTime = 5.0f; // Время на реакцию игрока (в секундах)
    [SerializeField] private float enemyAttackDelay = 0.3f; // Задержка перед атакой врага (для визуального эффекта)
    
    
    private bool isQTEActive = false;
    private Unit playerUnit;
    private Unit enemyUnit;
    private Coroutine currentQTECoroutine;
    private List<QTEKey> qteSequence = new List<QTEKey>(); // Последовательность из 3 клавиш
    private int currentKeyIndex = 0; // Текущий индекс в последовательности
    
    /// <summary>
    /// Определяет возможности каждой фигуры для QTE:
    /// - Пешка: не может блокировать (только получает урон)
    /// - Остальные фигуры: могут блокировать атаки врага
    /// </summary>
    private Dictionary<ChessUnitType, QTECapabilities> unitCapabilities = new Dictionary<ChessUnitType, QTECapabilities>
    {
        { ChessUnitType.Pawn, new QTECapabilities { canBlock = false } },
        { ChessUnitType.Horse, new QTECapabilities { canBlock = true } },
        { ChessUnitType.Bishop, new QTECapabilities { canBlock = true } },
        { ChessUnitType.Queen, new QTECapabilities { canBlock = true } },
        { ChessUnitType.King, new QTECapabilities { canBlock = true } },
        { ChessUnitType.Guardian, new QTECapabilities { canBlock = true } }
    };
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    /// <summary>
    /// Проверяет, активен ли сейчас QTE
    /// </summary>
    public bool IsQTEActive() => isQTEActive;
    
    /// <summary>
    /// Запускает QTE событие при входе игрока в зону угрозы врага
    /// </summary>
    /// <param name="player">Юнит игрока</param>
    /// <param name="enemy">Вражеский юнит, который атакует</param>
    public void StartQTE(Unit player, Unit enemy)
    {
        if (isQTEActive) return; // Если QTE уже активен, не запускаем новый
        
        playerUnit = player;
        enemyUnit = enemy;
        isQTEActive = true;
        
        // Останавливаем таймер хода во время QTE
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.PauseTimer();
        }
        
        // Воспроизводим звук активации QTE
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayQTEAactivate();
        }
        
        // Скрываем HUD экшен-режима при открытии QTE
        if (ActionModeUI.Instance != null)
        {
            ActionModeUI.Instance.HideHUD();
        }
        
        // Запускаем корутину QTE
        currentQTECoroutine = StartCoroutine(QTECoroutine());
    }
    
    /// <summary>
    /// Основная корутина QTE: показывает UI, ждёт реакцию игрока, обрабатывает результат
    /// </summary>
    private IEnumerator QTECoroutine()
    {
        // 0. Генерируем случайную последовательность из 3 клавиш (могут повторяться)
        QTEKey[] availableKeys = { QTEKey.Z, QTEKey.X, QTEKey.C };
        qteSequence.Clear();
        for (int i = 0; i < 3; i++)
        {
            qteSequence.Add(availableKeys[Random.Range(0, availableKeys.Length)]);
        }
        currentKeyIndex = 0;
        
        // 1. Показываем UI QTE с последовательностью клавиш
        if (QTEManager.Instance != null)
        {
            QTEManager.Instance.ShowQTE(playerUnit.chessType, unitCapabilities[playerUnit.chessType], qteSequence);
        }
        
        // 2. Вражеский юнит начинает атаку (с небольшой задержкой для визуального эффекта)
        yield return new WaitForSeconds(enemyAttackDelay);
        
        // Запускаем анимацию атаки врага
        if (enemyUnit != null)
        {
            Animator enemyAnimator = enemyUnit.GetComponent<Animator>();
            if (enemyAnimator != null)
            {
                enemyAnimator.SetTrigger("Attack");
            }
        }
        
        // 3. Ожидаем ввода игрока - нужно нажать все 3 клавиши в правильном порядке
        float timer = qteWindowTime; // Увеличиваем время для 3 клавиш
        float startTime = Time.time;
        QTEResult result = QTEResult.Failed;
        
        // Получаем возможности текущей фигуры
        QTECapabilities caps = unitCapabilities[playerUnit.chessType];
        
        if (!caps.canBlock)
        {
            // Пешка не может блокировать - сразу провал
            result = QTEResult.Failed;
        }
        else
        {
            // Ожидаем последовательность клавиш
            while (timer > 0f && currentKeyIndex < qteSequence.Count)
            {
                timer -= Time.deltaTime;
                
                // Обновляем таймер в UI
                if (QTEManager.Instance != null)
                {
                    QTEManager.Instance.UpdateTimer(timer, qteWindowTime * 1.5f);
                    QTEManager.Instance.UpdateSequenceProgress(currentKeyIndex, qteSequence.Count);
                }
                
                // Проверяем текущую клавишу в последовательности
                QTEKey expectedKey = qteSequence[currentKeyIndex];
                
                // Проверяем, нажата ли правильная клавиша
                if (IsQTEKeyPressed(expectedKey))
                {
                    // Правильная клавиша нажата!
                    currentKeyIndex++;
                    
                    // Обновляем UI - подсвечиваем следующую клавишу
                    if (QTEManager.Instance != null)
                    {
                        QTEManager.Instance.HighlightKey(currentKeyIndex - 1, true);
                    }
                    
                    // Если все клавиши нажаты - успех!
                    if (currentKeyIndex >= qteSequence.Count)
                    {
                        result = QTEResult.Blocked;
                        break;
                    }
                }
                else if (IsAnyQTEKeyPressed())
                {
                    // Игрок нажал неправильную клавишу - провал
                    result = QTEResult.Failed;
                    if (QTEManager.Instance != null)
                    {
                        QTEManager.Instance.HighlightKey(currentKeyIndex, false);
                    }
                    break;
                }
                
                yield return null;
            }
            
            // Если время вышло и не все клавиши нажаты - провал
            if (currentKeyIndex < qteSequence.Count && result != QTEResult.Blocked)
            {
                result = QTEResult.Failed;
            }
        }
        
        // 4. Обрабатываем результат QTE (успех/неудача)
        ProcessQTEResult(result);
        
        // 5. Скрываем UI QTE (ВАЖНО: делаем это сразу после обработки)
        if (QTEManager.Instance != null)
        {
            QTEManager.Instance.HideQTE();
        }
        
        // Возобновляем таймер хода после завершения QTE
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.ResumeTimer();
        }
        
        // Показываем HUD экшен-режима обратно (если мы все еще в экшен-режиме)
        if (CameraManager.Instance != null && CameraManager.Instance.IsActionMode())
        {
            if (ActionModeUI.Instance != null)
            {
                ActionModeUI.Instance.ShowHUD();
            }
        }
        
        // Сбрасываем состояние
        isQTEActive = false;
        playerUnit = null;
        enemyUnit = null;
        currentQTECoroutine = null;
        qteSequence.Clear();
        currentKeyIndex = 0;
    }
    
    /// <summary>
    /// Обрабатывает результат QTE и применяет соответствующие эффекты
    /// </summary>
    /// <param name="result">Результат QTE (блок/неудача)</param>
    private void ProcessQTEResult(QTEResult result)
    {
        if (playerUnit == null || enemyUnit == null) return;
        
        switch (result)
        {
            case QTEResult.Blocked:
                // Успешный блок - полностью блокирует урон от врага
                playerUnit.SetBlocking(true);
                playerUnit.TakeDamage(0); // Урон полностью заблокирован
                playerUnit.SetBlocking(false);
                
                // Воспроизводим звук успешного QTE
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayQTESuccess();
                }
                
                Debug.Log($"{playerUnit.chessType} заблокировал атаку! Урон полностью заблокирован.");
                break;
                
            case QTEResult.Failed:
                // Неудача - игрок получает полный урон от врага
                playerUnit.TakeDamage(enemyUnit.Damage);
                
                // Воспроизводим звук провала QTE
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayQTEFail();
                }
                
                Debug.Log($"{playerUnit.chessType} не среагировал! Получен урон: {enemyUnit.Damage}");
                break;
        }
    }
    
    /// <summary>
    /// Отменяет активный QTE (например, при выходе из зоны угрозы или окончании таймера хода).
    /// При отмене урон не наносится, так как это не вина игрока.
    /// </summary>
    public void CancelQTE()
    {
        if (currentQTECoroutine != null)
        {
            StopCoroutine(currentQTECoroutine);
            currentQTECoroutine = null;
        }
        isQTEActive = false;
        
        // Возобновляем таймер хода при отмене QTE
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.ResumeTimer();
        }
        
        // Скрываем UI QTE
        if (QTEManager.Instance != null)
        {
            QTEManager.Instance.HideQTE();
        }
        
        // Показываем HUD экшен-режима обратно (если мы все еще в экшен-режиме)
        if (CameraManager.Instance != null && CameraManager.Instance.IsActionMode())
        {
            if (ActionModeUI.Instance != null)
            {
                ActionModeUI.Instance.ShowHUD();
            }
        }
        
        // Сбрасываем ссылки на юниты
        playerUnit = null;
        enemyUnit = null;
        qteSequence.Clear();
        currentKeyIndex = 0;
    }
    
    /// <summary>
    /// Проверяет, была ли нажата указанная клавиша QTE
    /// </summary>
    /// <param name="key">Клавиша для проверки</param>
    /// <returns>true если клавиша была нажата в этом кадре</returns>
    private bool IsQTEKeyPressed(QTEKey key)
    {
        switch (key)
        {
            case QTEKey.Z:
                return Keyboard.current.zKey.wasPressedThisFrame;
            case QTEKey.X:
                return Keyboard.current.xKey.wasPressedThisFrame;
            case QTEKey.C:
                return Keyboard.current.cKey.wasPressedThisFrame;
            default:
                return false;
        }
    }
    
    /// <summary>
    /// Проверяет, была ли нажата любая клавиша QTE (Z, X или C)
    /// </summary>
    /// <returns>true если любая клавиша QTE была нажата в этом кадре</returns>
    private bool IsAnyQTEKeyPressed()
    {
        return Keyboard.current.zKey.wasPressedThisFrame ||
               Keyboard.current.xKey.wasPressedThisFrame ||
               Keyboard.current.cKey.wasPressedThisFrame;
    }
}

/// <summary>
/// Структура, определяющая возможности фигуры в QTE
/// </summary>
public struct QTECapabilities
{
    public bool canBlock;  // Может ли фигура блокировать атаки (полностью блокирует урон)
}

/// <summary>
/// Результат QTE события
/// </summary>
public enum QTEResult
{
    Blocked,  // Успешный блок (полностью блокирует урон)
    Failed    // Неудача (не среагировал, получает полный урон)
}

/// <summary>
/// Клавиши для QTE (случайно выбирается одна из них)
/// </summary>
public enum QTEKey
{
    Z,
    X,
    C
}

