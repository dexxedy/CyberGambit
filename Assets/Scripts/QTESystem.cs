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
    [SerializeField] [Range(0f, 1f)] private float qteChance = 0.6f; // Вероятность появления QTE при атаке врага (60%)
    
    private bool isQTEActive = false;
    private Unit playerUnit;
    private Unit enemyUnit;
    private Coroutine currentQTECoroutine;
    private List<QTEKey> qteSequence = new List<QTEKey>(); // Последовательность из 3 клавиш
    private int currentKeyIndex = 0; // Текущий индекс в последовательности
    private Animator enemyAnimator; // Аниматор врага для управления анимацией атаки
    private bool wasTimeStopped = false; // Флаг, был ли остановлен Time.timeScale
    private bool wasAttackBlocked = false; // Флаг, была ли последняя атака заблокирована через QTE
    private Unit lastBlockedAttacker; // Последний атакующий, чья атака была заблокирована
    private Unit lastBlockedTarget; // Последняя цель, которая заблокировала атаку
    
    /// <summary>
    /// Определяет возможности каждой фигуры для QTE:
    /// - Все фигуры могут блокировать атаки врага через QTE
    /// </summary>
    private Dictionary<ChessUnitType, QTECapabilities> unitCapabilities = new Dictionary<ChessUnitType, QTECapabilities>
    {
        { ChessUnitType.Pawn, new QTECapabilities { canBlock = true } },
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
    /// Запускает QTE событие при атаке врага на юнит игрока (в ход врага)
    /// </summary>
    /// <param name="player">Юнит игрока</param>
    /// <param name="enemy">Вражеский юнит, который атакует</param>
    /// <returns>true если QTE был запущен, false если нет (например, из-за шанса или если уже активен)</returns>
    public bool StartQTE(Unit player, Unit enemy)
    {
        if (isQTEActive) return false; // Если QTE уже активен, не запускаем новый
        
        // Проверяем шанс появления QTE
        if (Random.value > qteChance)
        {
            return false; // QTE не появился из-за шанса
        }
        
        playerUnit = player;
        enemyUnit = enemy;
        isQTEActive = true;
        enemyAnimator = enemy != null ? enemy.GetComponent<Animator>() : null;
        
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
        
        // Скрываем HUD экшен-режима при открытии QTE (если мы в экшен-режиме)
        if (ActionModeUI.Instance != null && CameraManager.Instance != null && CameraManager.Instance.IsActionMode())
        {
            ActionModeUI.Instance.HideHUD();
        }
        
        // Запускаем корутину QTE
        currentQTECoroutine = StartCoroutine(QTECoroutine());
        return true;
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
        
        // 1. Запускаем анимацию атаки врага (но еще не наносим урон)
        if (enemyUnit != null && enemyAnimator != null)
        {
            enemyAnimator.SetTrigger("Attack");
        }
        
        // 2. Небольшая задержка для визуального эффекта (враг начинает замах)
        // Используем unscaledDeltaTime, так как время еще не остановлено
        float delayElapsed = 0f;
        while (delayElapsed < enemyAttackDelay)
        {
            delayElapsed += Time.deltaTime;
            yield return null;
        }
        
        // 3. Показываем UI QTE с последовательностью клавиш ПЕРЕД остановкой времени
        // Это важно, чтобы UI успел обновиться
        if (QTEManager.Instance != null)
        {
            QTEManager.Instance.ShowQTE(playerUnit.chessType, unitCapabilities[playerUnit.chessType], qteSequence);
        }
        
        // Небольшая задержка, чтобы UI успел обновиться
        yield return null;
        yield return null;
        
        // 4. ОСТАНАВЛИВАЕМ ВРЕМЯ - анимация атаки застывает на начальных кадрах
        wasTimeStopped = true;
        Time.timeScale = 0f;
        
        // 5. Ожидаем ввода игрока - нужно нажать все 3 клавиши в правильном порядке
        // Используем unscaledDeltaTime, так как timeScale = 0
        float timer = qteWindowTime;
        QTEResult result = QTEResult.Failed;
        
        // Получаем возможности текущей фигуры
        QTECapabilities caps = unitCapabilities[playerUnit.chessType];
        
        if (!caps.canBlock)
        {
            // Пешка не может блокировать - сразу провал
            // Но панель QTE все равно показывается, чтобы игрок видел, что происходит
            result = QTEResult.Failed;
            
            // Показываем панель минимум 1.5 секунды, чтобы игрок успел увидеть сообщение
            float displayTime = 1.5f;
            while (displayTime > 0f)
            {
                displayTime -= Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else
        {
            // Ожидаем последовательность клавиш (используем unscaledDeltaTime)
            while (timer > 0f && currentKeyIndex < qteSequence.Count)
            {
                timer -= Time.unscaledDeltaTime;
                
                // Обновляем таймер в UI
                if (QTEManager.Instance != null)
                {
                    QTEManager.Instance.UpdateTimer(timer, qteWindowTime);
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
                
                yield return null; // yield return null работает даже при timeScale = 0
            }
            
            // Если время вышло и не все клавиши нажаты - провал
            if (currentKeyIndex < qteSequence.Count && result != QTEResult.Blocked)
            {
                result = QTEResult.Failed;
            }
        }
        
        // 6. Обрабатываем результат QTE (успех/неудача) ПЕРЕД возобновлением времени
        // Это важно, чтобы анимация успела остановиться
        ProcessQTEResult(result);
        
        // 7. Небольшая задержка перед скрытием UI, чтобы игрок успел увидеть результат
        yield return new WaitForSecondsRealtime(0.5f);
        
        // 8. Скрываем UI QTE
        if (QTEManager.Instance != null)
        {
            QTEManager.Instance.HideQTE();
        }
        
        // 9. ВОЗОБНОВЛЯЕМ ВРЕМЯ после скрытия UI
        Time.timeScale = 1f;
        wasTimeStopped = false;
        
        // 10. Возобновляем таймер хода после завершения QTE
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.ResumeTimer();
        }
        
        // 10. Показываем HUD экшен-режима обратно (если мы все еще в экшен-режиме)
        if (CameraManager.Instance != null && CameraManager.Instance.IsActionMode())
        {
            if (ActionModeUI.Instance != null)
            {
                ActionModeUI.Instance.ShowHUD();
            }
        }
        
        // 11. Сбрасываем состояние (но сохраняем информацию о блокировке для WeaponCollider)
        isQTEActive = false;
        // Не сбрасываем playerUnit и enemyUnit сразу - они нужны для проверки в WeaponCollider
        // Сбросим их через небольшую задержку
        StartCoroutine(ResetQTEStateDelayed());
        enemyAnimator = null;
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
                // Успешный блок - отменяем анимацию атаки врага
                if (enemyAnimator != null)
                {
                    // Сбрасываем триггер атаки - это важно для предотвращения продолжения анимации
                    enemyAnimator.ResetTrigger("Attack");
                    
                    // Останавливаем аниматор полностью
                    enemyAnimator.speed = 0f;
                    
                    // Получаем информацию о текущем состоянии
                    AnimatorStateInfo currentState = enemyAnimator.GetCurrentAnimatorStateInfo(0);
                    
                    // Переключаемся на состояние покоя, используя имя состояния
                    // Пробуем различные возможные имена состояний покоя
                    string[] possibleIdleStates = { "male-idle_279398 (1)", "Idle", "idle" };
                    bool stateSwitched = false;
                    
                    foreach (string stateName in possibleIdleStates)
                    {
                        try
                        {
                            // Пытаемся переключиться на состояние покоя
                            enemyAnimator.CrossFadeInFixedTime(stateName, 0f, 0);
                            stateSwitched = true;
                            break;
                        }
                        catch
                        {
                            // Продолжаем попытки с другим именем
                            continue;
                        }
                    }
                    
                    // Если не удалось переключиться, просто останавливаем аниматор
                    // Анимация останется на текущем кадре и не продолжится
                    if (!stateSwitched)
                    {
                        enemyAnimator.speed = 0f;
                    }
                    
                    // Восстанавливаем скорость аниматора после того, как время возобновится
                    StartCoroutine(RestoreAnimatorSpeedAfterTime(enemyAnimator));
                }
                
                // Помечаем, что атака была заблокирована
                wasAttackBlocked = true;
                lastBlockedAttacker = enemyUnit;
                lastBlockedTarget = playerUnit;
                
                // Урон не наносится - атака заблокирована
                playerUnit.SetBlocking(true);
                playerUnit.TakeDamage(0); // Урон полностью заблокирован
                playerUnit.SetBlocking(false);
                
                // Воспроизводим звук успешного QTE
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayQTESuccess();
                }
                break;
            
            case QTEResult.Failed:
                // Неудача - анимация атаки продолжается
                // Помечаем, что атака НЕ была заблокирована
                wasAttackBlocked = false;
                lastBlockedAttacker = null;
                lastBlockedTarget = null;
                
                // Анимация уже запущена, просто продолжаем её выполнение
                // Урон будет нанесен через WeaponCollider при контакте оружия с юнитом
                
                // Воспроизводим звук провала QTE
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayQTEFail();
                }
                break;
        }
    }
    
    /// <summary>
    /// Проверяет, была ли атака заблокирована через QTE (для предотвращения нанесения урона через WeaponCollider)
    /// </summary>
    public bool WasAttackBlocked(Unit attacker, Unit target)
    {
        // Если QTE был успешно пройден для этой пары юнитов, атака заблокирована
        if (wasAttackBlocked && lastBlockedAttacker == attacker && lastBlockedTarget == target)
        {
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Сбрасывает состояние QTE с задержкой (чтобы WeaponCollider успел проверить блокировку)
    /// </summary>
    private IEnumerator ResetQTEStateDelayed()
    {
        yield return new WaitForSeconds(0.5f); // Задержка для проверки в WeaponCollider
        playerUnit = null;
        enemyUnit = null;
        // Сбрасываем флаг блокировки только после того, как WeaponCollider проверит
        yield return new WaitForSeconds(0.5f);
        wasAttackBlocked = false;
        lastBlockedAttacker = null;
        lastBlockedTarget = null;
    }
    
    /// <summary>
    /// Восстанавливает скорость аниматора после того, как время возобновилось
    /// </summary>
    private IEnumerator RestoreAnimatorSpeedAfterTime(Animator animator)
    {
        // Ждем, пока время возобновится (timeScale станет 1)
        while (Time.timeScale < 0.1f)
        {
            yield return null;
        }
        
        // Ждем еще немного, чтобы убедиться, что анимация не продолжится
        yield return new WaitForSeconds(0.2f);
        
        if (animator != null)
        {
            // Восстанавливаем нормальную скорость аниматора
            animator.speed = 1f;
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
        
        // Восстанавливаем время, если оно было остановлено
        if (wasTimeStopped)
        {
            Time.timeScale = 1f;
            wasTimeStopped = false;
        }
        
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
        enemyAnimator = null;
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

