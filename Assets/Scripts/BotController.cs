using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AI;

/// <summary>
/// Управляет поведением бота.
/// Простая логика: Первый ход - пешка, затем атакуем ближайшего врага.
/// За ход: перемещение (с разворотом к врагу) + одна атака.
/// При приближении к врагу - получает автоатаку с шансом блока.
/// </summary>
public class BotController : MonoBehaviour
{
    public static BotController Instance;
    
    [Header("Bot Settings")]
    [SerializeField] private float turnStartDelay = 0.5f;
    [SerializeField] private float moveDelay = 0.3f;
    [SerializeField] private float attackDelay = 0.3f;
    [SerializeField] private float attackRange = 3.0f; // Увеличен радиус атаки
    [SerializeField] private float enemyDetectionRange = 8.0f; // Радиус обнаружения врагов (4 клетки, больше радиуса атаки)
    [SerializeField] private float approachDistance = 2.0f; // Насколько близко бот может подойти к врагу после перемещения на клетку
    
    [Header("QTE Settings")]
    [SerializeField] [Range(0f, 1f)] private float qteSuccessChance = 0.5f;
    
    private bool isExecutingTurn = false;
    private bool isFirstTurn = true;
    private HashSet<Unit> usedUnitsThisGame = new HashSet<Unit>();
    private HashSet<Unit> enemiesThatAttackedThisTurn = new HashSet<Unit>(); // Враги которые уже атаковали в этом ходу
    
    // История использования юнитов (для ротации)
    private Dictionary<Unit, int> unitLastUsedTurn = new Dictionary<Unit, int>();
    private int currentTurnNumber = 0;
    
    // Ценности фигур
    private static readonly Dictionary<ChessUnitType, int> PieceValues = new Dictionary<ChessUnitType, int>
    {
        { ChessUnitType.Queen, 9 },
        { ChessUnitType.Guardian, 5 },
        { ChessUnitType.Bishop, 3 },
        { ChessUnitType.Horse, 3 },
        { ChessUnitType.Pawn, 1 },
        { ChessUnitType.King, 100 }
    };
    
    // Базовые оценки для режимов (из плана)
    private static readonly Dictionary<ChessUnitType, Dictionary<string, int>> BaseScores = new Dictionary<ChessUnitType, Dictionary<string, int>>
    {
        { ChessUnitType.Queen, new Dictionary<string, int> { { "Attack", 100 }, { "Defense", 70 }, { "Position", 85 } } },
        { ChessUnitType.Guardian, new Dictionary<string, int> { { "Attack", 70 }, { "Defense", 90 }, { "Position", 75 } } },
        { ChessUnitType.Bishop, new Dictionary<string, int> { { "Attack", 65 }, { "Defense", 60 }, { "Position", 70 } } },
        { ChessUnitType.Horse, new Dictionary<string, int> { { "Attack", 75 }, { "Defense", 55 }, { "Position", 80 } } },
        { ChessUnitType.Pawn, new Dictionary<string, int> { { "Attack", 40 }, { "Defense", 80 }, { "Position", 50 } } },
        { ChessUnitType.King, new Dictionary<string, int> { { "Attack", 10 }, { "Defense", 30 }, { "Position", 20 } } }
    };
    
    public bool IsExecutingTurn() => isExecutingTurn;
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    public void ExecuteBotTurn()
    {
        if (isExecutingTurn) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsBotTurn()) return;
        
        currentTurnNumber++;
        StartCoroutine(SimpleBotTurn());
    }
    
    // ========================================================================
    // ФАЗА 1: АНАЛИЗ СОСТОЯНИЯ ДОСКИ
    // ========================================================================
    
    /// <summary>
    /// Оценивает угрозу королю бота от всех вражеских юнитов
    /// </summary>
    private float EvaluateThreatToBotKing()
    {
        Unit botKing = GetBotKing();
        if (botKing == null) return 0f;
        
        float threatLevel = 0f;
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        List<Unit> enemies = allUnits
            .Where(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0)
            .ToList();
        
        foreach (Unit enemy in enemies)
        {
            float distance = GetDistance(botKing, enemy);
            int pieceValue = GetPieceValue(enemy.chessType);
            
            if (distance <= attackRange)
            {
                // Критическая угроза - враг в радиусе атаки
                threatLevel += pieceValue * 10f;
            }
            else if (distance <= attackRange * 2f)
            {
                // Близкая угроза
                threatLevel += pieceValue * 5f;
            }
            else if (distance <= attackRange * 4f)
            {
                // Потенциальная угроза
                threatLevel += pieceValue * 2f;
            }
        }
        
        return threatLevel;
    }
    
    /// <summary>
    /// Оценивает уязвимость вражеского короля (насколько мы можем его атаковать)
    /// </summary>
    private float EvaluateEnemyKingVulnerability()
    {
        Unit enemyKing = GetEnemyKing();
        if (enemyKing == null) return 0f;
        
        float vulnerabilityLevel = 0f;
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        List<Unit> botUnits = allUnits
            .Where(u => u != null && u.owner == Player.Player2 && u.GetHealth() > 0)
            .ToList();
        
        foreach (Unit botUnit in botUnits)
        {
            float distance = GetDistance(botUnit, enemyKing);
            int pieceValue = GetPieceValue(botUnit.chessType);
            
            if (distance <= attackRange)
            {
                // Можем атаковать прямо сейчас!
                vulnerabilityLevel += pieceValue * 15f;
            }
            else if (distance <= attackRange * 2f)
            {
                // Один ход до атаки
                vulnerabilityLevel += pieceValue * 8f;
            }
        }
        
        return vulnerabilityLevel;
    }
    
    /// <summary>
    /// Получает короля бота
    /// </summary>
    private Unit GetBotKing()
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        return allUnits
            .FirstOrDefault(u => u != null && u.owner == Player.Player2 && u.chessType == ChessUnitType.King && u.GetHealth() > 0);
    }
    
    /// <summary>
    /// Получает короля врага
    /// </summary>
    private Unit GetEnemyKing()
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        return allUnits
            .FirstOrDefault(u => u != null && u.owner == Player.Player1 && u.chessType == ChessUnitType.King && u.GetHealth() > 0);
    }
    
    // ========================================================================
    // ФАЗА 2: ОПРЕДЕЛЕНИЕ РЕЖИМА ИГРЫ
    // ========================================================================
    
    /// <summary>
    /// Режимы игры бота
    /// </summary>
    private enum BotGameMode
    {
        Defense,   // Защита - угроза королю высока
        Attack,    // Атака - вражеский король уязвим
        Position   // Позиция - обычная игра
    }
    
    /// <summary>
    /// Определяет режим игры на основе анализа доски
    /// </summary>
    private BotGameMode DetermineGameMode()
    {
        float threatLevel = EvaluateThreatToBotKing();
        float vulnerabilityLevel = EvaluateEnemyKingVulnerability();
        
        if (threatLevel > 50f)
        {
            return BotGameMode.Defense;
        }
        else if (vulnerabilityLevel > 80f)
        {
            return BotGameMode.Attack;
        }
        else
        {
            return BotGameMode.Position;
        }
    }
    
    /// <summary>
    /// Простой и понятный ход бота
    /// </summary>
    private IEnumerator SimpleBotTurn()
    {
        isExecutingTurn = true;
        enemiesThatAttackedThisTurn.Clear(); // Сброс автоатак
        
        yield return new WaitForSeconds(turnStartDelay);
        
        // 1. АНАЛИЗ ДОСКИ И ОПРЕДЕЛЕНИЕ РЕЖИМА
        BotGameMode gameMode = DetermineGameMode();
        
        // БРОСОК КОСТЕЙ НА ХОД БОТА
        float moveBudgetMeters = 0f;
        if (GameManager.Instance != null)
        {
            if (!GameManager.Instance.HasRolledDiceThisTurn())
            {
                GameManager.Instance.RollDiceForCurrentTurn();
            }
            moveBudgetMeters = GameManager.Instance.GetCurrentTurnMoveBudgetMeters();
        }

        // Mission1: если на этом же GameObject есть провайдер цели, приоритетно двигаемся к флагам.
        Mission1BotObjectiveProvider mission1ObjectiveProvider = GetComponent<Mission1BotObjectiveProvider>();
        if (mission1ObjectiveProvider != null)
        {
            Mission1FlagZone targetFlag = mission1ObjectiveProvider.SelectTargetFlag();
            if (targetFlag != null)
            {
                yield return StartCoroutine(ExecuteMission1BotTurn(mission1ObjectiveProvider, targetFlag, moveBudgetMeters));
                yield break;
            }
        }

        // 2. ВЫБОР ЮНИТА
        Unit selectedUnit = SelectBestUnit(gameMode);
        if (selectedUnit == null)
        {
            // Возвращаем камеру на исходную позицию, если она следовала за юнитом
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.ReturnTacticalCameraToOriginalPosition();
            }
            EndTurn();
            yield break;
        }
        
        selectedUnit.SetRemainingMoveMeters(moveBudgetMeters);
        
        // Переключаем камеру игрока на приближенный вид над юнитом бота
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToBotUnitView(selectedUnit);
            yield return new WaitForSeconds(0.6f); // Ждем завершения перемещения камеры
        }
        
        // 2. ПРОВЕРКА: Есть ли враги в радиусе атаки ПРЯМО СЕЙЧАС?
        Unit enemyInRange = FindEnemyInAttackRange(selectedUnit);
        
        // 3. Если нет врагов в радиусе атаки, ищем ближайшего для перемещения
        Unit targetEnemy = null;
        if (enemyInRange == null)
        {
            targetEnemy = FindNearestEnemy(selectedUnit);
            
            // Поворачиваемся к цели для перемещения
            if (targetEnemy != null)
            {
                RotateTowardsTarget(selectedUnit, targetEnemy.transform.position);
                yield return new WaitForSeconds(0.2f);
            }
        }
        else
        {
            // Если враг уже в радиусе, используем его как цель
            targetEnemy = enemyInRange;
        }
        
        if (enemyInRange != null)
        {
            // Разворот к врагу
            RotateTowardsTarget(selectedUnit, enemyInRange.transform.position);
            yield return new WaitForSeconds(0.2f);
            
            // Способность перед атакой
            TryUseAbility(selectedUnit);
            yield return new WaitForSeconds(0.2f);
            
            // АТАКУЕМ!
            yield return StartCoroutine(PerformAttack(selectedUnit, enemyInRange));
        }
        else
        {
            // 5. ПЕРЕМЕЩЕНИЕ К ЦЕЛИ (двигаемся вперёд к врагам)
            yield return StartCoroutine(MoveTowardsEnemy(selectedUnit, targetEnemy, moveBudgetMeters));
            
            // 6. Способность после перемещения
            TryUseAbility(selectedUnit);
            yield return new WaitForSeconds(0.2f);
            
            // 7. ПРОВЕРЯЕМ ВСЕХ ВРАГОВ В РАДИУСЕ после перемещения (с учетом приближения)
            yield return new WaitForSeconds(0.1f); // Небольшая задержка для завершения приближения
            enemyInRange = FindEnemyInAttackRange(selectedUnit);
            
            if (enemyInRange != null)
            {
                // Разворот к врагу
                RotateTowardsTarget(selectedUnit, enemyInRange.transform.position);
                yield return new WaitForSeconds(0.2f);
                
                yield return StartCoroutine(PerformAttack(selectedUnit, enemyInRange));
            }
            else
            {
                // Нет врагов в радиусе атаки после перемещения
            }
        }
        
        // 8. КОНЕЦ ХОДА
        yield return new WaitForSeconds(0.3f);
        EndTurn();
    }
    
    /// <summary>
    /// Завершение хода
    /// </summary>
    private void EndTurn()
    {
        isFirstTurn = false;
        isExecutingTurn = false;
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SwitchTurn();
            
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.UpdateCameraForCurrentPlayer();
            }
        }
    }
    
    // ========================================================================
    // ФАЗА 3: ВЫБОР ЮНИТА (НОВАЯ СИСТЕМА ОЦЕНКИ)
    // ========================================================================
    
    /// <summary>
    /// Выбирает лучшего юнита для хода с учетом режима игры
    /// </summary>
    private Unit SelectBestUnit(BotGameMode gameMode)
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        List<Unit> botUnits = allUnits
            .Where(u => u != null && u.owner == Player.Player2 && u.GetHealth() > 0)
            .ToList();
        
        if (botUnits.Count == 0) return null;
        
        usedUnitsThisGame.RemoveWhere(u => u == null || u.GetHealth() <= 0);
        
        // ПЕРВЫЙ ХОД - пешка
        if (isFirstTurn)
        {
            Unit pawn = botUnits.FirstOrDefault(u => u.chessType == ChessUnitType.Pawn);
            if (pawn != null)
            {
                usedUnitsThisGame.Add(pawn);
                unitLastUsedTurn[pawn] = currentTurnNumber;
                return pawn;
            }
        }
        
        // ОСОБЫЕ ПРАВИЛА ДЛЯ РЕЖИМА ЗАЩИТА
        if (gameMode == BotGameMode.Defense)
        {
            Unit botKing = GetBotKing();
            if (botKing != null)
            {
                // Ищем врагов рядом с королем
                Unit[] allEnemies = FindObjectsByType<Unit>(FindObjectsSortMode.None);
                List<Unit> threats = allEnemies
                    .Where(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0)
                    .Where(u => GetDistance(botKing, u) <= attackRange * 2f)
                    .ToList();
                
                if (threats.Count > 0)
                {
                    // Приоритет 1: Юнит, который может убить угрозу
                    foreach (Unit threat in threats)
                    {
                        Unit killer = botUnits
                            .Where(u => u.chessType != ChessUnitType.King)
                            .Where(u => GetDistance(u, threat) <= attackRange)
                            .FirstOrDefault();
                        
                        if (killer != null)
                        {
                            usedUnitsThisGame.Add(killer);
                            unitLastUsedTurn[killer] = currentTurnNumber;
                            return killer;
                        }
                    }
                    
                    // Приоритет 2: Юнит, который может заблокировать путь к королю
                    // (упрощенная версия - выбираем ближайшего к угрозе)
                    Unit blocker = botUnits
                        .Where(u => u.chessType != ChessUnitType.King)
                        .OrderBy(u => {
                            float minDist = threats.Min(t => GetDistance(u, t));
                            return minDist;
                        })
                        .FirstOrDefault();
                    
                    if (blocker != null)
                    {
                        usedUnitsThisGame.Add(blocker);
                        unitLastUsedTurn[blocker] = currentTurnNumber;
                        return blocker;
                    }
                }
            }
        }
        
        // ОСОБЫЕ ПРАВИЛА ДЛЯ РЕЖИМА АТАКА
        if (gameMode == BotGameMode.Attack)
        {
            Unit enemyKing = GetEnemyKing();
            if (enemyKing != null)
            {
                // Приоритет: Юнит, который может атаковать короля
                Unit attacker = botUnits
                    .Where(u => u.chessType != ChessUnitType.King)
                    .Where(u => GetDistance(u, enemyKing) <= attackRange)
                    .OrderByDescending(u => GetPieceValue(u.chessType))
                    .FirstOrDefault();
                
                if (attacker != null)
                {
                    usedUnitsThisGame.Add(attacker);
                    unitLastUsedTurn[attacker] = currentTurnNumber;
                    return attacker;
                }
            }
        }
        
        // ОБЩАЯ СИСТЕМА ОЦЕНКИ
        var scoredUnits = botUnits
            .Where(u => u.chessType != ChessUnitType.King || ShouldUseKing(u, gameMode))
            .Select(u => new {
                Unit = u,
                Score = EvaluateUnitScore(u, gameMode)
            })
            .OrderByDescending(x => x.Score)
            .ToList();
        
        if (scoredUnits.Count > 0)
        {
            Unit bestUnit = scoredUnits[0].Unit;
            usedUnitsThisGame.Add(bestUnit);
            unitLastUsedTurn[bestUnit] = currentTurnNumber;
            return bestUnit;
        }
        
        return botUnits.FirstOrDefault();
    }
    
    /// <summary>
    /// Оценивает юнита по формуле из плана
    /// </summary>
    private float EvaluateUnitScore(Unit unit, BotGameMode gameMode)
    {
        if (unit == null) return 0f;
        
        // Базовая оценка по типу и режиму
        string modeKey = gameMode == BotGameMode.Defense ? "Defense" : 
                        gameMode == BotGameMode.Attack ? "Attack" : "Position";
        
        float baseScore = 0f;
        if (BaseScores.ContainsKey(unit.chessType) && BaseScores[unit.chessType].ContainsKey(modeKey))
        {
            baseScore = BaseScores[unit.chessType][modeKey];
        }
        
        // Бонус за возможность атаковать
        float attackBonus = 0f;
        Unit enemyInRange = FindEnemyInAttackRange(unit);
        if (enemyInRange != null)
        {
            if (enemyInRange.chessType == ChessUnitType.King)
            {
                attackBonus = 200f; // Максимальный приоритет для атаки короля
            }
            else
            {
                attackBonus = 100f * GetPieceValue(enemyInRange.chessType);
            }
        }
        
        // Бонус за близость к цели
        float positionBonus = 0f;
        Unit target = GetTargetForMode(unit, gameMode);
        if (target != null)
        {
            float distance = GetDistance(unit, target);
            positionBonus = (1f / (distance + 1f)) * 50f;
        }
        
        // Бонус за здоровье
        float healthRatio = (float)unit.GetHealth() / unit.GetMaxHealth();
        float healthBonus = healthRatio * 30f;
        
        // Бонус за очки перемещения
        float integrityBonus = 0f;
        
        // Штраф за недавнее использование (ротация)
        float recentUsePenalty = 0f;
        if (unitLastUsedTurn.ContainsKey(unit))
        {
            int turnsSinceUse = currentTurnNumber - unitLastUsedTurn[unit];
            if (turnsSinceUse == 1) recentUsePenalty = -60f;
            else if (turnsSinceUse == 2) recentUsePenalty = -30f;
            else if (turnsSinceUse == 3) recentUsePenalty = -10f;
        }
        
        // Штраф за блокировку (упрощенная проверка)
        float blockingPenalty = 0f;
        if (IsUnitBlocked(unit))
        {
            blockingPenalty = -40f;
        }
        
        float totalScore = baseScore + attackBonus + positionBonus + healthBonus + integrityBonus + recentUsePenalty + blockingPenalty;
        
        return totalScore;
    }
    
    /// <summary>
    /// Получает цель для режима
    /// </summary>
    private Unit GetTargetForMode(Unit unit, BotGameMode gameMode)
    {
        switch (gameMode)
        {
            case BotGameMode.Defense:
                // В режиме защиты цель - угроза королю
                Unit botKing = GetBotKing();
                if (botKing != null)
                {
                    Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
                    return allUnits
                        .Where(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0)
                        .Where(u => GetDistance(botKing, u) <= attackRange * 2f)
                        .OrderBy(u => GetDistance(botKing, u))
                        .FirstOrDefault();
                }
                break;
                
            case BotGameMode.Attack:
                // В режиме атаки цель - вражеский король
                return GetEnemyKing();
                
            case BotGameMode.Position:
            default:
                // В позиционном режиме - ближайший враг
                return FindNearestEnemy(unit);
        }
        
        return null;
    }
    
    /// <summary>
    /// Проверяет, должен ли использоваться король
    /// </summary>
    private bool ShouldUseKing(Unit king, BotGameMode gameMode)
    {
        // Король используется только если:
        // 1. Нет других юнитов
        // 2. Король может хилить раненого союзника
        // 3. Король должен отступить от угрозы (режим защиты)
        
        if (gameMode == BotGameMode.Defense)
        {
            // В режиме защиты король может отступить
            Unit botKing = GetBotKing();
            if (botKing == king)
            {
                Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
                bool hasThreat = allUnits
                    .Any(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0 && 
                              GetDistance(botKing, u) <= attackRange);
                
                if (hasThreat) return true;
            }
        }
        
        // Проверяем, есть ли раненый союзник для хилла
        UnitAbilities abilities = king.GetComponent<UnitAbilities>();
        if (abilities != null && abilities.IsAbilityReady())
        {
            Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            bool hasWoundedAlly = allUnits
                .Any(u => u != null && u.owner == king.owner && u != king && u.GetHealth() > 0 &&
                          (float)u.GetHealth() / u.GetMaxHealth() < 0.5f);
            
            if (hasWoundedAlly) return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Проверяет, заблокирован ли юнит своими союзниками
    /// </summary>
    private bool IsUnitBlocked(Unit unit)
    {
        if (ChessGrid.Instance == null) return false;
        
        Vector2Int unitPos = ChessGrid.Instance.WorldToGridCoords(unit.transform.position);
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        
        // Проверяем соседние клетки
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int checkPos = new Vector2Int(unitPos.x + dx, unitPos.y + dy);
                
                if (ChessGrid.Instance.IsValidCoord(checkPos.x, checkPos.y))
                {
                    bool hasAlly = allUnits.Any(u => u != null && u != unit && u.owner == unit.owner && 
                                                     u.GetHealth() > 0 &&
                                                     ChessGrid.Instance.WorldToGridCoords(u.transform.position) == checkPos);
                    
                    if (!hasAlly) return false; // Есть свободное место
                }
            }
        }
        
        return true; // Со всех сторон окружен союзниками
    }
    
    private Unit FindDefenderUnit(List<Unit> botUnits)
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        List<Unit> enemies = allUnits
            .Where(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0)
            .ToList();
        
        // Враг рядом с союзником?
        foreach (Unit ally in botUnits)
        {
            foreach (Unit enemy in enemies)
            {
                if (GetDistance(ally, enemy) <= attackRange * 2f)
                {
                    // Ищем кто может атаковать этого врага
                    Unit attacker = botUnits
                        .Where(u => u.chessType != ChessUnitType.King)
                        .Where(u => GetDistance(u, enemy) <= attackRange * 3f)
                        .OrderBy(u => GetDistance(u, enemy))
                        .FirstOrDefault();
                    
                    if (attacker != null) return attacker;
                }
            }
        }
        
        return null;
    }
    
    // ========================================================================
    // ПЕРЕМЕЩЕНИЕ
    // ========================================================================
    
    /// <summary>
    /// Перемещение к врагу с разворотом и проверкой автоатак
    /// </summary>
    private IEnumerator MoveTowardsEnemy(Unit unit, Unit target, float moveBudgetMeters)
    {
        if (unit == null || ChessGrid.Instance == null) yield break;
        
        moveBudgetMeters = Mathf.Max(0f, moveBudgetMeters);
        if (moveBudgetMeters <= 0f) yield break;

        Unit effectiveTarget = target ?? FindNearestEnemy(unit);
        if (effectiveTarget == null) yield break;

        yield return StartCoroutine(MoveTowardsWorldTargetByBudget(unit, effectiveTarget.transform.position, moveBudgetMeters));
    }
    
    private IEnumerator ExecuteMission1BotTurn(Mission1BotObjectiveProvider objectiveProvider, Mission1FlagZone targetFlag, float moveBudgetMeters)
    {
        if (objectiveProvider == null || targetFlag == null || ChessGrid.Instance == null)
        {
            EndTurn();
            yield break;
        }

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        List<Unit> botUnits = allUnits
            .Where(u => u != null && u.owner == Player.Player2 && u.GetHealth() > 0)
            .ToList();

        if (botUnits.Count == 0)
        {
            EndTurn();
            yield break;
        }

        // Выбираем ближайший юнит бота к целевому флагу.
        Unit selectedUnit = botUnits
            .OrderBy(u => Vector3.Distance(u.transform.position, targetFlag.transform.position))
            .FirstOrDefault();

        if (selectedUnit == null)
        {
            EndTurn();
            yield break;
        }

        selectedUnit.SetRemainingMoveMeters(moveBudgetMeters);

        // Переключаем камеру игрока на приближенный вид над юнитом бота
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToBotUnitView(selectedUnit);
            yield return new WaitForSeconds(0.6f);
        }

        yield return StartCoroutine(MoveTowardsFlag(selectedUnit, targetFlag, moveBudgetMeters));

        // Опционально: если после перемещения враг оказался в радиусе атаки — делаем атаку.
        Unit enemyInRange = FindEnemyInAttackRange(selectedUnit);
        if (enemyInRange != null)
        {
            RotateTowardsTarget(selectedUnit, enemyInRange.transform.position);
            yield return new WaitForSeconds(0.2f);

            TryUseAbility(selectedUnit);
            yield return new WaitForSeconds(0.2f);

            yield return StartCoroutine(PerformAttack(selectedUnit, enemyInRange));
        }

        yield return new WaitForSeconds(0.3f);
        EndTurn();
    }

    private IEnumerator MoveTowardsFlag(Unit unit, Mission1FlagZone targetFlag, float moveBudgetMeters)
    {
        if (unit == null || targetFlag == null || ChessGrid.Instance == null) yield break;
        
        moveBudgetMeters = Mathf.Max(0f, moveBudgetMeters);
        if (moveBudgetMeters <= 0f) yield break;

        yield return StartCoroutine(MoveTowardsWorldTargetByBudget(unit, targetFlag.transform.position, moveBudgetMeters));
    }

    private IEnumerator MoveTowardsWorldTargetByBudget(Unit unit, Vector3 targetWorldPos, float moveBudgetMeters)
    {
        if (unit == null) yield break;

        Vector3 startPosition = unit.transform.position;
        if (!TryGetReachablePointByBudget(startPosition, targetWorldPos, moveBudgetMeters, out Vector3 reachablePoint, out float travelledMeters))
        {
            yield break;
        }

        if (travelledMeters <= 0.01f) yield break;
        RotateTowardsTarget(unit, reachablePoint);
        yield return new WaitForSeconds(moveDelay);
        yield return StartCoroutine(MoveUnitToWorldPoint(unit, reachablePoint, travelledMeters));
    }

    private bool TryGetReachablePointByBudget(Vector3 from, Vector3 target, float budgetMeters, out Vector3 reachablePoint, out float travelledMeters)
    {
        reachablePoint = from;
        travelledMeters = 0f;
        if (budgetMeters <= 0f) return false;

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, target, NavMesh.AllAreas, path) ||
            path.corners == null || path.corners.Length < 2)
        {
            return false;
        }

        float totalLength = 0f;
        for (int i = 1; i < path.corners.Length; i++)
        {
            totalLength += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }

        if (totalLength <= 0.01f) return false;
        float allowed = Mathf.Min(totalLength, budgetMeters);
        travelledMeters = allowed;
        reachablePoint = GetPointAlongCorners(path.corners, allowed);
        return true;
    }

    private Vector3 GetPointAlongCorners(Vector3[] corners, float distanceFromStart)
    {
        if (corners == null || corners.Length == 0) return Vector3.zero;
        if (corners.Length == 1 || distanceFromStart <= 0f) return corners[0];

        float remaining = distanceFromStart;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 a = corners[i - 1];
            Vector3 b = corners[i];
            float segmentLength = Vector3.Distance(a, b);
            if (remaining <= segmentLength || i == corners.Length - 1)
            {
                float t = segmentLength > 0f ? Mathf.Clamp01(remaining / segmentLength) : 1f;
                return Vector3.Lerp(a, b, t);
            }
            remaining -= segmentLength;
        }

        return corners[corners.Length - 1];
    }

    private IEnumerator MoveUnitToWorldPoint(Unit unit, Vector3 worldPos, float travelledMeters)
    {
        CharacterController controller = unit.GetComponent<CharacterController>();
        Vector3 startPos = unit.transform.position;
        float duration = Mathf.Clamp(travelledMeters / Mathf.Max(0.1f, unit.MoveSpeed), 0.2f, 2.5f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 nextPos = Vector3.Lerp(startPos, worldPos, t);
            Vector3 move = nextPos - unit.transform.position;
            move.y = 0f;

            if (controller != null && controller.enabled)
            {
                controller.Move(move);
            }
            else
            {
                unit.transform.position = nextPos;
            }

            unit.RefreshGridPositionFromWorld();
            yield return null;
        }

        if (controller != null && controller.enabled)
        {
            controller.enabled = false;
            unit.transform.position = worldPos;
            controller.enabled = true;
        }
        else
        {
            unit.transform.position = worldPos;
        }

        unit.ConsumeMoveMeters(travelledMeters);
        unit.RefreshGridPositionFromWorld();
    }

    private Vector2Int? FindNextStepTowardsFlag(Unit unit, Vector2Int currentPos, Vector2Int targetPos)
    {
        if (ChessGrid.Instance == null) return null;

        // 4 направления
        Vector2Int[] dirs = new[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        // Выбираем соседнюю клетку, которая уменьшает Manhattan distance до цели.
        float bestScore = float.NegativeInfinity;
        Vector2Int? best = null;

        foreach (var d in dirs)
        {
            Vector2Int next = currentPos + d;
            if (!ChessGrid.Instance.IsValidCoord(next.x, next.y)) continue;
            if (IsCellOccupied(next, unit)) continue;

            int distNow = Mathf.Abs(currentPos.x - targetPos.x) + Mathf.Abs(currentPos.y - targetPos.y);
            int distNext = Mathf.Abs(next.x - targetPos.x) + Mathf.Abs(next.y - targetPos.y);
            int improvement = distNow - distNext;

            // Бонус за движение "вперед" (для Player2 — к меньшему y)
            float score = improvement * 10f;
            if (next.y < currentPos.y) score += 2f;

            if (score > bestScore)
            {
                bestScore = score;
                best = next;
            }
        }

        // Если ни один шаг не улучшает, делаем любой доступный шаг (чтобы бот не стоял).
        if (!best.HasValue)
        {
            foreach (var d in dirs)
            {
                Vector2Int next = currentPos + d;
                if (!ChessGrid.Instance.IsValidCoord(next.x, next.y)) continue;
                if (IsCellOccupied(next, unit)) continue;
                return next;
            }
        }

        return best;
    }
    
    // Автоатака отключена - QTE теперь запускается только при атаке бота на юнит игрока
    
    /// <summary>
    /// Плавное перемещение юнита с возможностью дополнительного приближения к врагу
    /// </summary>
    private IEnumerator MoveUnitToGridCell(Unit unit, Vector2Int gridPos, Vector3 worldPos)
    {
        float distance = Vector3.Distance(unit.transform.position, worldPos);
        float duration = Mathf.Clamp(distance / unit.MoveSpeed, 0.3f, 1.5f);
        
        unit.MoveToGridPosition(gridPos);
        yield return new WaitForSeconds(duration + 0.1f);
    }
    
    private Vector2Int? FindNextStepTowardsTarget(Unit unit, Vector2Int currentPos, Unit target)
    {
        if (ChessGrid.Instance == null) return null;
        
        // 4 направления
        Vector2Int[] dirs = new[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };
        
        Vector2Int targetPos = currentPos;
        if (target != null)
        {
            targetPos = ChessGrid.Instance.WorldToGridCoords(target.transform.position);
        }
        
        // Выбираем соседнюю клетку, которая уменьшает Manhattan distance до цели
        float bestScore = float.NegativeInfinity;
        Vector2Int? best = null;
        
        foreach (var d in dirs)
        {
            Vector2Int next = currentPos + d;
            if (!ChessGrid.Instance.IsValidCoord(next.x, next.y)) continue;
            if (IsCellOccupied(next, unit)) continue;
            
            int distNow = Mathf.Abs(currentPos.x - targetPos.x) + Mathf.Abs(currentPos.y - targetPos.y);
            int distNext = Mathf.Abs(next.x - targetPos.x) + Mathf.Abs(next.y - targetPos.y);
            int improvement = distNow - distNext;
            
            // Бонус за движение "вперед" (для Player2 — к меньшему y)
            float score = improvement * 10f;
            if (next.y < currentPos.y) score += 2f;
            
            if (score > bestScore)
            {
                bestScore = score;
                best = next;
            }
        }
        
        // Если ни один шаг не улучшает, делаем любой доступный шаг (чтобы бот не стоял)
        if (!best.HasValue)
        {
            foreach (var d in dirs)
            {
                Vector2Int next = currentPos + d;
                if (!ChessGrid.Instance.IsValidCoord(next.x, next.y)) continue;
                if (IsCellOccupied(next, unit)) continue;
                return next;
            }
        }
        
        return best;
    }
    
    /// <summary>
    /// Подходит ближе к врагу, если враг в радиусе обнаружения, но не в радиусе атаки
    /// </summary>
    private IEnumerator ApproachEnemyIfNeeded(Unit unit)
    {
        if (unit == null) yield break;
        
        // Ищем ближайшего врага
        Unit nearestEnemy = FindNearestEnemy(unit);
        if (nearestEnemy == null) yield break;
        
        float distanceToEnemy = GetDistance(unit, nearestEnemy);
        
        // Если враг в радиусе обнаружения, но не в радиусе атаки - подходим ближе
        // Также подходим, если враг очень близко к радиусу атаки (с небольшим запасом)
        if (distanceToEnemy > attackRange * 0.95f && distanceToEnemy <= enemyDetectionRange)
        {
            Vector3 directionToEnemy = (nearestEnemy.transform.position - unit.transform.position).normalized;
            directionToEnemy.y = 0;
            
            // Вычисляем целевую позицию - ближе к врагу, на оптимальном расстоянии для атаки
            float desiredDistance = attackRange * 0.85f; // 85% от радиуса атаки для оптимальной позиции
            Vector3 targetPosition = nearestEnemy.transform.position - directionToEnemy * desiredDistance;
            targetPosition.y = unit.transform.position.y; // Сохраняем высоту
            
            Vector3 currentPos = unit.transform.position;
            Vector3 approachVector = targetPosition - currentPos;
            float approachDistanceNeeded = approachVector.magnitude;
            
            // Вычисляем, сколько нужно пройти, чтобы попасть в радиус атаки
            float distanceToReachAttackRange = distanceToEnemy - attackRange * 0.9f;
            
            // Используем большее из двух: нужное расстояние или максимальное разрешенное
            float maxApproachDistance = Mathf.Max(approachDistance, distanceToReachAttackRange + 0.3f);
            
            // Ограничиваем максимальное расстояние приближения
            if (approachDistanceNeeded > maxApproachDistance)
            {
                approachVector = approachVector.normalized * maxApproachDistance;
                targetPosition = currentPos + approachVector;
            }
            
            // Проверяем, что не выходим за пределы разумного (не слишком далеко от клетки)
            if (ChessGrid.Instance != null)
            {
                Vector2Int currentGridPos = ChessGrid.Instance.WorldToGridCoords(currentPos);
                Vector3 cellCenter = ChessGrid.Instance.GridToWorldPosition(currentGridPos.x, currentGridPos.y);
                float distanceFromCell = Vector3.Distance(targetPosition, cellCenter);
                
                // Увеличиваем максимальное расстояние от центра клетки, чтобы бот мог подойти ближе
                float maxDistanceFromCell = 2.5f; // Увеличено для возможности подойти ближе к врагу
                
                // Если нужно подойти ближе к врагу, разрешаем выйти дальше от клетки
                if (distanceToEnemy > attackRange * 1.1f)
                {
                    maxDistanceFromCell = 3.0f; // Еще больше, если враг далеко
                }
                
                if (distanceFromCell > maxDistanceFromCell)
                {
                    Vector3 directionFromCell = (targetPosition - cellCenter).normalized;
                    targetPosition = cellCenter + directionFromCell * maxDistanceFromCell;
                }
            }
            
            // Подходим к врагу
            float actualApproachDistance = Vector3.Distance(currentPos, targetPosition);
            if (actualApproachDistance > 0.1f)
            {
                CharacterController controller = unit.GetComponent<CharacterController>();
                if (controller != null && controller.enabled)
                {
                    float approachDuration = actualApproachDistance / unit.MoveSpeed;
                    approachDuration = Mathf.Clamp(approachDuration, 0.1f, 0.5f);
                    
                    float elapsed = 0f;
                    Vector3 startPos = unit.transform.position;
                    
                    // Разворачиваемся к цели
                    RotateTowardsTarget(unit, targetPosition);
                    
                    while (elapsed < approachDuration)
                    {
                        elapsed += Time.deltaTime;
                        float t = elapsed / approachDuration;
                        Vector3 newPos = Vector3.Lerp(startPos, targetPosition, t);
                        Vector3 moveVector = newPos - unit.transform.position;
                        moveVector.y = 0;
                        
                        controller.Move(moveVector);
                        yield return null;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Разворот юнита к цели
    /// </summary>
    private void RotateTowardsTarget(Unit unit, Vector3 targetPosition)
    {
        if (unit == null) return;
        
        Vector3 direction = (targetPosition - unit.transform.position).normalized;
        direction.y = 0;
        
        if (direction != Vector3.zero)
        {
            unit.transform.rotation = Quaternion.LookRotation(direction);
        }
    }
    
    /// <summary>
    /// Находит лучший ход к врагу, избегая столкновения с союзниками
    /// </summary>
    private Vector2Int? FindBestMoveTowards(Unit unit, Unit target)
    {
        if (ChessGrid.Instance == null || ChessRulesManager.Instance == null) return null;
        
        Vector2Int currentPos = ChessGrid.Instance.WorldToGridCoords(unit.transform.position);
        Vector2Int targetPos = ChessGrid.Instance.WorldToGridCoords(target.transform.position);
        
        List<Vector2Int> moves = GetValidMoves(unit, currentPos);
        moves = moves.Where(m => m != currentPos).ToList();
        
        if (moves.Count == 0) return null;
        
        // Получаем позиции союзников (чтобы не кучковаться)
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        List<Vector2Int> allyPositions = allUnits
            .Where(u => u != null && u.owner == unit.owner && u != unit && u.GetHealth() > 0)
            .Select(u => ChessGrid.Instance.WorldToGridCoords(u.transform.position))
            .ToList();
        
        float currentDist = Vector2Int.Distance(currentPos, targetPos);
        
        // Оцениваем каждый ход
        var scoredMoves = moves
            .Where(m => !IsCellOccupied(m, unit))
            .Where(m => !HasUnitsOnPath(currentPos, m, unit))
            .Select(m => {
                float score = 0f;
                float newDist = Vector2Int.Distance(m, targetPos);
                
                // ГЛАВНОЕ: Бонус за приближение к врагу
                float improvement = currentDist - newDist;
                score += improvement * 150f; // Увеличен вес!
                
                // ОГРОМНЫЙ бонус за движение вперёд (к линии врага) - Player2 движется вниз (y уменьшается)
                if (m.y < currentPos.y)
                {
                    score += 200f; // Очень важный бонус!
                }
                else if (m.y == currentPos.y)
                {
                    score += 50f; // Боковое движение тоже ок
                }
                else
                {
                    score -= 300f; // ШТРАФ за отступление назад!
                }
                
                // Бонус если после хода можем атаковать (проверяем всех врагов в радиусе)
                Vector3 moveWorldPos = ChessGrid.Instance.GridToWorldPosition(m.x, m.y);
                float distAfterMove = Vector3.Distance(moveWorldPos, target.transform.position);
                
                // Учитываем, что после перемещения бот может дополнительно подойти ближе
                float effectiveDistance = Mathf.Max(0f, distAfterMove - approachDistance);
                
                // Проверяем, можем ли атаковать выбранную цель
                if (effectiveDistance <= attackRange)
                {
                    score += 500f; // МАКСИМАЛЬНЫЙ приоритет!
                }
                else if (distAfterMove <= enemyDetectionRange)
                {
                    // Бонус если враг в радиусе обнаружения (можем подойти и атаковать)
                    score += 300f;
                }
                
                // Дополнительный бонус: проверяем всех врагов в радиусе после хода
                Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
                int enemiesInRangeAfterMove = allUnits
                    .Count(u => u != null && u.owner != unit.owner && u.GetHealth() > 0 && 
                               Vector3.Distance(moveWorldPos, u.transform.position) <= attackRange + approachDistance);
                
                if (enemiesInRangeAfterMove > 0)
                {
                    score += enemiesInRangeAfterMove * 100f; // Бонус за каждого врага в радиусе
                }
                
                // ШТРАФ за близость к союзникам (не кучковаться!)
                foreach (var allyPos in allyPositions)
                {
                    float distToAlly = Vector2Int.Distance(m, allyPos);
                    if (distToAlly < 2f)
                    {
                        score -= (2f - distToAlly) * 100f; // Увеличен штраф
                    }
                }
                
                // Бонус за сохранение своей колонки (распределение по фронту)
                if (m.x == currentPos.x) score += 40f;
                
                return new { Move = m, Score = score, Dist = newDist };
            })
            .Where(x => x.Dist <= currentDist + 1f) // Не отступаем назад (с небольшим допуском)
            .OrderByDescending(x => x.Score)
            .ToList();
        
        if (scoredMoves.Count > 0)
        {
            var bestMove = scoredMoves[0];
            return bestMove.Move;
        }
        
        // Если нет хороших ходов - любой вперёд (к врагу)
        var forwardMove = moves
            .Where(m => !IsCellOccupied(m, unit))
            .Where(m => m.y < currentPos.y) // Только вперёд!
            .OrderBy(m => m.y) // Ближе к врагу
            .FirstOrDefault();
        
        if (forwardMove != Vector2Int.zero)
        {
            return forwardMove;
        }
        
        // Крайний случай - любой ход
        return moves
            .Where(m => !IsCellOccupied(m, unit))
            .FirstOrDefault();
    }
    
    private Vector2Int? FindAnyForwardMove(Unit unit, Vector2Int currentPos)
    {
        List<Vector2Int> moves = GetValidMoves(unit, currentPos);
        
        // Ищем ближайшего врага для направления
        Unit nearestEnemy = FindNearestEnemy(unit);
        
        // Получаем позиции союзников
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        List<Vector2Int> allyPositions = allUnits
            .Where(u => u != null && u.owner == unit.owner && u != unit && u.GetHealth() > 0)
            .Select(u => ChessGrid.Instance.WorldToGridCoords(u.transform.position))
            .ToList();
        
        // Для Player2 "вперёд" = y уменьшается (к Player1)
        // Выбираем ход вперёд который приближает к врагу
        var forwardMoves = moves
            .Where(m => m != currentPos && m.y < currentPos.y) // Только вперёд!
            .Where(m => !IsCellOccupied(m, unit))
            .Select(m => {
                float score = 0f;
                
                // Бонус за движение вперёд
                score += (currentPos.y - m.y) * 50f; // Чем дальше вперёд, тем лучше
                
                // Если есть враг - бонус за приближение к нему
                if (nearestEnemy != null)
                {
                    Vector2Int enemyPos = ChessGrid.Instance.WorldToGridCoords(nearestEnemy.transform.position);
                    float currentDist = Vector2Int.Distance(currentPos, enemyPos);
                    float newDist = Vector2Int.Distance(m, enemyPos);
                    if (newDist < currentDist)
                    {
                        score += 100f; // Приближение к врагу
                    }
                }
                
                // Штраф за близость к союзникам
                foreach (var allyPos in allyPositions)
                {
                    float distToAlly = Vector2Int.Distance(m, allyPos);
                    if (distToAlly < 2f)
                    {
                        score -= (2f - distToAlly) * 50f;
                    }
                }
                
                return new { Move = m, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .ToList();
        
        if (forwardMoves.Count > 0)
        {
            return forwardMoves[0].Move;
        }
        
        return null;
    }
    
    private Vector2Int? FindAnyMove(Unit unit, Vector2Int currentPos)
    {
        List<Vector2Int> moves = GetValidMoves(unit, currentPos);
        
        Vector2Int? move = moves
            .Where(m => m != currentPos)
            .Where(m => !IsCellOccupied(m, unit))
            .Where(m => !HasUnitsOnPath(currentPos, m, unit))
            .Cast<Vector2Int?>()
            .FirstOrDefault();
        
        if (move.HasValue) return move;
        
        // Соседние клетки
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int newPos = new Vector2Int(currentPos.x + dx, currentPos.y + dy);
                if (ChessGrid.Instance.IsValidCoord(newPos.x, newPos.y) && !IsCellOccupied(newPos, unit))
                {
                    return newPos;
                }
            }
        }
        
        return null;
    }
    
    private List<Vector2Int> GetValidMoves(Unit unit, Vector2Int currentPos)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        if (ChessGrid.Instance == null) return moves;
        
        // Базовое перемещение: 4-соседа
        Vector2Int[] dirs = new[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };
        
        foreach (var d in dirs)
        {
            Vector2Int pos = currentPos + d;
            if (!ChessGrid.Instance.IsValidCoord(pos.x, pos.y)) continue;
            if (IsCellOccupied(pos, unit)) continue;
            moves.Add(pos);
        }
        
        // Добавляем соседние если нет валидных
        if (moves.Count == 0)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Vector2Int pos = new Vector2Int(currentPos.x + dx, currentPos.y + dy);
                    if (ChessGrid.Instance.IsValidCoord(pos.x, pos.y))
                    {
                        moves.Add(pos);
                    }
                }
            }
        }
        
        return moves;
    }
    
    // ========================================================================
    // АТАКА
    // ========================================================================
    
    /// <summary>
    /// Находит врага в радиусе атаки с улучшенной системой приоритетов
    /// </summary>
    private Unit FindEnemyInAttackRange(Unit attacker)
    {
        if (attacker == null) return null;
        
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        
        List<Unit> enemiesInRange = allUnits
            .Where(u => u != null && u.owner != attacker.owner && u.GetHealth() > 0)
            .Where(u => {
                float distance = GetDistance(attacker, u);
                // Учитываем, что бот может подойти ближе после перемещения
                float effectiveDistance = distance - approachDistance;
                return effectiveDistance <= attackRange || distance <= attackRange;
            })
            .ToList();
        
        if (enemiesInRange.Count == 0)
        {
            return null;
        }
        
        // Оценка целей по приоритетам из плана
        var scoredTargets = enemiesInRange
            .Select(e => {
                float distance = GetDistance(attacker, e);
                float score = GetPieceValue(e.chessType) * 10f;
                
                // Максимальный приоритет для короля
                if (e.chessType == ChessUnitType.King)
                {
                    score += 500f;
                }
                
                // Гарантированное убийство
                int damage = Mathf.RoundToInt(attacker.Damage * attacker.GetDamageMultiplier());
                if (e.GetHealth() <= damage)
                {
                    score += 200f;
                }
                
                // Бонус за близость (ближе = лучше)
                score += (enemyDetectionRange - distance) * 5f;
                
                // Бонус за низкое HP (легче убить)
                float healthRatio = (float)e.GetHealth() / e.GetMaxHealth();
                if (healthRatio < 0.5f)
                {
                    score += (1f - healthRatio) * 50f;
                }
                
                // Штраф за активный щит (Ладья с активным щитом)
                if (e.chessType == ChessUnitType.Guardian)
                {
                    score -= 50f; // Небольшой штраф
                }
                
                // Штраф за отражение (опасно атаковать Слона с отражением)
                if (e.chessType == ChessUnitType.Bishop)
                {
                    score -= 100f; // Больший штраф, так как отражение опасно
                }
                
                return new { Enemy = e, Score = score, Distance = distance };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Distance) // При одинаковом приоритете выбираем ближайшего
            .ToList();
        
        if (scoredTargets.Count > 0)
        {
            return scoredTargets[0].Enemy;
        }
        
        return null;
    }
    
    /// <summary>
    /// Выполнение атаки
    /// </summary>
    private IEnumerator PerformAttack(Unit attacker, Unit target)
    {
        if (attacker == null || target == null)
        {
            yield break;
        }
        
        float distance = GetDistance(attacker, target);
        
        // Если враг немного дальше радиуса атаки, но в радиусе обнаружения - подходим ближе
        if (distance > attackRange && distance <= enemyDetectionRange)
        {
            yield return StartCoroutine(ApproachEnemyIfNeeded(attacker));
            distance = GetDistance(attacker, target); // Обновляем расстояние после приближения
        }
        
        // Проверяем расстояние после попытки приближения
        if (distance > attackRange * 1.1f) // Небольшой запас для погрешности
        {
            yield break;
        }
        
        // Разворот к врагу
        RotateTowardsTarget(attacker, target.transform.position);
        yield return new WaitForSeconds(attackDelay);
        
        // Рывок к врагу
        Vector3 direction = (target.transform.position - attacker.transform.position).normalized;
        float lungeDistance = Mathf.Min(0.5f, distance - 1.0f);
        
        if (lungeDistance > 0.1f)
        {
            Vector3 lungeTarget = attacker.transform.position + direction * lungeDistance;
            CharacterController controller = attacker.GetComponent<CharacterController>();
            
            float elapsed = 0f;
            float lungeTime = 0.2f;
            Vector3 startPos = attacker.transform.position;
            
            while (elapsed < lungeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lungeTime;
                
                Vector3 newPos = Vector3.Lerp(startPos, lungeTarget, t);
                if (controller != null && controller.enabled)
                {
                    controller.Move(newPos - attacker.transform.position);
                }
                else
                {
                    attacker.transform.position = newPos;
                }
                
                yield return null;
            }
        }
        
        // АТАКА!
        // Если цель - юнит игрока, пытаемся запустить QTE
        bool qteWasTriggered = false;
        if (target.owner == Player.Player1 && QTESystem.Instance != null)
        {
            // Пытаемся запустить QTE (с шансом)
            // QTE система сама запустит анимацию атаки, остановит время и заморозит её
            qteWasTriggered = QTESystem.Instance.StartQTE(target, attacker);
            
            if (qteWasTriggered)
            {
                // QTE запущен - ждем его завершения
                // Время остановлено, анимация атаки застыла на начальных кадрах
                // Ждем, пока QTE не завершится
                while (QTESystem.Instance.IsQTEActive())
                {
                    yield return null;
                }
                
                // После завершения QTE проверяем, была ли атака заблокирована
                // Если атака была заблокирована, анимация уже отменена в QTESystem
                // Если атака не была заблокирована, анимация продолжается и урон будет нанесен через WeaponCollider
                yield return new WaitForSeconds(0.5f); // Небольшая задержка для завершения анимации (если она не была отменена)
            }
            else
            {
                // QTE не запустился - атакуем как обычно
                attacker.Attack();
                yield return new WaitForSeconds(1.0f);
            }
        }
        else
        {
            // Цель не юнит игрока или QTE система недоступна - атакуем как обычно
            attacker.Attack();
            yield return new WaitForSeconds(1.0f);
        }
    }
    
    // ========================================================================
    // СПОСОБНОСТИ
    // ========================================================================
    
    /// <summary>
    /// Улучшенная логика использования способностей для каждого типа фигуры
    /// </summary>
    private void TryUseAbility(Unit unit)
    {
        if (unit == null) return;
        
        UnitAbilities abilities = unit.GetComponent<UnitAbilities>();
        if (abilities == null || !abilities.IsAbilityReady()) return;
        
        switch (unit.chessType)
        {
            case ChessUnitType.Queen:
                // Ферзь: Буст урона +20% - использовать ПЕРЕД атакой
                TryUseQueenAbility(unit, abilities);
                break;
                
            case ChessUnitType.Guardian:
                // Ладья: Щит (снижение урона 50%) - использовать когда враг рядом
                TryUseGuardianAbility(unit, abilities);
                break;
                
            case ChessUnitType.Bishop:
                // Слон: Отражение урона - использовать перед входом в бой
                TryUseBishopAbility(unit, abilities);
                break;
                
            case ChessUnitType.Horse:
                // Конь: Телепорт - использовать для быстрого маневра
                TryUseHorseAbility(unit, abilities);
                break;
                
            case ChessUnitType.King:
                // Король: Хил союзника 20% HP - использовать для сохранения ценных фигур
                TryUseKingAbility(unit, abilities);
                break;
        }
    }
    
    /// <summary>
    /// Логика использования способности Ферзя (буст урона)
    /// </summary>
    private void TryUseQueenAbility(Unit queen, UnitAbilities abilities)
    {
        // Использовать ЕСЛИ:
        // - Ферзь может атаковать в этом ходу
        // - Цель - высокоценная фигура (Ладья, Ферзь, Король)
        // - Цель имеет мало HP и буст гарантирует убийство
        
        Unit enemyInRange = FindEnemyInAttackRange(queen);
        if (enemyInRange == null) return;
        
        int pieceValue = GetPieceValue(enemyInRange.chessType);
        bool isHighValueTarget = pieceValue >= 5; // Ладья, Ферзь, Король
        
        int damage = Mathf.RoundToInt(queen.Damage * queen.GetDamageMultiplier());
        int boostedDamage = Mathf.RoundToInt(queen.Damage * 1.2f); // +20% буст
        
        bool canKillWithBoost = enemyInRange.GetHealth() <= boostedDamage;
        bool canKillWithoutBoost = enemyInRange.GetHealth() <= damage;
        
        // Используем если: высокоценная цель ИЛИ буст гарантирует убийство
            if (isHighValueTarget || (canKillWithBoost && !canKillWithoutBoost))
            {
                abilities.ActivateAbility();
            }
    }
    
    /// <summary>
    /// Логика использования способности Ладьи (щит)
    /// </summary>
    private void TryUseGuardianAbility(Unit guardian, UnitAbilities abilities)
    {
        // Использовать ЕСЛИ:
        // - Враг в радиусе атаки (1-2 клетки)
        // - Ладья планирует оставаться на месте или идти в бой
        // - HP ладьи < 80%
        
        bool enemyNearby = HasEnemyNearby(guardian);
        if (!enemyNearby) return;
        
        float healthRatio = (float)guardian.GetHealth() / guardian.GetMaxHealth();
        
        if (healthRatio < 0.8f)
        {
            abilities.ActivateAbility();
        }
    }
    
    /// <summary>
    /// Логика использования способности Слона (отражение)
    /// </summary>
    private void TryUseBishopAbility(Unit bishop, UnitAbilities abilities)
    {
        // Использовать ЕСЛИ:
        // - Слон окружен врагами (>= 2 врага рядом)
        // - HP слона < 70%
        // - Слон собирается атаковать (контратака)
        
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        int nearbyEnemies = allUnits
            .Count(u => u != null && u.owner != bishop.owner && u.GetHealth() > 0 && 
                       GetDistance(bishop, u) <= attackRange * 1.5f);
        
        float healthRatio = (float)bishop.GetHealth() / bishop.GetMaxHealth();
        bool canAttack = FindEnemyInAttackRange(bishop) != null;
        
        if ((nearbyEnemies >= 2 || canAttack) && healthRatio < 0.7f)
        {
            abilities.ActivateAbility();
        }
    }
    
    /// <summary>
    /// Логика использования способности Коня (телепорт)
    /// </summary>
    private void TryUseHorseAbility(Unit horse, UnitAbilities abilities)
    {
        // Использовать ЕСЛИ:
        // - Нужно быстро добраться до вражеского короля
        // - Конь заблокирован своими пешками
        // - Нужно уйти от угрозы
        
        Unit enemyKing = GetEnemyKing();
        if (enemyKing != null)
        {
            float distanceToKing = GetDistance(horse, enemyKing);
            
            // Если король далеко и телепорт поможет приблизиться
            if (distanceToKing > attackRange * 2f && distanceToKing < attackRange * 4f)
            {
                // Проверяем, заблокирован ли конь
                if (IsUnitBlocked(horse))
                {
                    TryHorseJump(horse, enemyKing);
                    return;
                }
            }
        }
        
        // Проверяем угрозу королю
        Unit botKing = GetBotKing();
        if (botKing != null)
        {
            Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
            bool hasThreat = allUnits
                .Any(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0 && 
                          GetDistance(botKing, u) <= attackRange);
            
            if (hasThreat && IsUnitBlocked(horse))
            {
                // Телепортируемся к угрозе
                Unit threat = allUnits
                    .FirstOrDefault(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0 && 
                                       GetDistance(botKing, u) <= attackRange);
                
                if (threat != null)
                {
                    TryHorseJump(horse, threat);
                }
            }
        }
    }
    
    /// <summary>
    /// Логика использования способности Короля (хил)
    /// </summary>
    private void TryUseKingAbility(Unit king, UnitAbilities abilities)
    {
        // Использовать ЕСЛИ:
        // - Цель.HP < 50%
        // - Цель - ценная фигура (Ферзь, Ладья)
        // - Король в безопасности (нет угрозы рядом)
        
        // Проверяем безопасность короля
        bool isKingSafe = true;
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        bool hasThreat = allUnits
            .Any(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0 && 
                     GetDistance(king, u) <= attackRange);
        
        if (hasThreat) isKingSafe = false;
        
        if (!isKingSafe) return; // Король в опасности, не используем хил
        
        // Находим союзника с минимальным HP
        Unit wounded = allUnits
            .Where(u => u != null && u.owner == king.owner && u != king && u.GetHealth() > 0)
            .Select(u => new {
                Unit = u,
                HealthRatio = (float)u.GetHealth() / u.GetMaxHealth(),
                PieceValue = GetPieceValue(u.chessType)
            })
            .Where(x => x.HealthRatio < 0.5f) // HP < 50%
            .OrderBy(x => x.HealthRatio) // Минимальное HP
            .ThenByDescending(x => x.PieceValue) // Ценные фигуры приоритетнее
            .Select(x => x.Unit)
            .FirstOrDefault();
        
        if (wounded != null)
        {
            int pieceValue = GetPieceValue(wounded.chessType);
            // Приоритет для ценных фигур (Ферзь, Ладья)
            if (pieceValue >= 5)
            {
                TryKingHeal(king);
            }
            else if (wounded.GetHealth() < wounded.GetMaxHealth() * 0.3f)
            {
                // Критически раненый союзник
                TryKingHeal(king);
            }
        }
    }
    
    private void TryHorseJump(Unit horse, Unit target)
    {
        if (ChessGrid.Instance == null || ChessRulesManager.Instance == null) return;
        
        Vector2Int currentPos = ChessGrid.Instance.WorldToGridCoords(horse.transform.position);
        Vector2Int targetPos = ChessGrid.Instance.WorldToGridCoords(target.transform.position);
        
        List<Vector2Int> moves = ChessRulesManager.Instance.GetHorsePossibleMoves(currentPos);
        
        Vector2Int? best = moves
            .Where(m => !IsCellOccupied(m, horse))
            .OrderBy(m => Vector2Int.Distance(m, targetPos))
            .Cast<Vector2Int?>()
            .FirstOrDefault();
        
        if (best.HasValue)
        {
            horse.MoveToGridPosition(best.Value);
            
            UnitAbilities abilities = horse.GetComponent<UnitAbilities>();
            if (abilities != null) abilities.SetAbilityCooldown(1);
            
            AbilityVisualEffects effects = horse.GetComponent<AbilityVisualEffects>();
            if (effects != null) effects.PlayHorseJumpEffect();
        }
    }
    
    private void TryKingHeal(Unit king)
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        
        Unit wounded = allUnits
            .Where(u => u != null && u.owner == king.owner && u != king && u.GetHealth() > 0)
            .Where(u => (float)u.GetHealth() / u.GetMaxHealth() < 0.5f)
            .OrderBy(u => (float)u.GetHealth() / u.GetMaxHealth())
            .FirstOrDefault();
        
        if (wounded != null)
        {
            int heal = Mathf.RoundToInt(wounded.GetMaxHealth() * 0.2f);
            wounded.Heal(heal);
            
            UnitAbilities abilities = king.GetComponent<UnitAbilities>();
            if (abilities != null) abilities.SetAbilityCooldown(4);
            
            AbilityVisualEffects effects = king.GetComponent<AbilityVisualEffects>();
            if (effects != null) effects.PlayKingHealEffect(wounded);
        }
    }
    
    // ========================================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // ========================================================================
    
    private float GetDistance(Unit a, Unit b)
    {
        if (a == null || b == null) return float.MaxValue;
        return Vector3.Distance(a.transform.position, b.transform.position);
    }
    
    private float GetDistanceToNearestEnemy(Unit unit)
    {
        Unit nearest = FindNearestEnemy(unit);
        return nearest != null ? GetDistance(unit, nearest) : float.MaxValue;
    }
    
    private Unit FindNearestEnemy(Unit unit)
    {
        if (unit == null) return null;
        
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        var enemies = allUnits
            .Where(u => u != null && u.owner != unit.owner && u.GetHealth() > 0)
            .Select(u => new { Unit = u, Distance = GetDistance(unit, u) })
            .ToList();
        
        if (enemies.Count == 0) return null;
        
        // Находим минимальное расстояние
        float minDistance = enemies.Min(e => e.Distance);
        
        // Если несколько врагов на одинаковом расстоянии, выбираем лучшего по приоритету
        var enemiesAtSameDistance = enemies
            .Where(e => Mathf.Abs(e.Distance - minDistance) < 0.1f) // Учитываем погрешность
            .Select(e => {
                float priority = GetPieceValue(e.Unit.chessType) * 10f;
                
                // Приоритет королю
                if (e.Unit.chessType == ChessUnitType.King)
                {
                    priority += 500f;
                }
                
                // Приоритет гарантированному убийству
                int damage = Mathf.RoundToInt(unit.Damage * unit.GetDamageMultiplier());
                if (e.Unit.GetHealth() <= damage)
                {
                    priority += 200f;
                }
                
                // Приоритет низкому HP
                priority += (100f - e.Unit.GetHealth()) * 0.5f;
                
                return new { e.Unit, e.Distance, Priority = priority };
            })
            .OrderByDescending(e => e.Priority)
            .ToList();
        
        // Возвращаем лучшего по приоритету из врагов на одинаковом расстоянии
        if (enemiesAtSameDistance.Count > 0)
        {
            return enemiesAtSameDistance[0].Unit;
        }
        
        // Если нет врагов на одинаковом расстоянии, возвращаем ближайшего
        return enemies
            .OrderBy(e => e.Distance)
            .FirstOrDefault()?.Unit;
    }
    
    private bool HasEnemyNearby(Unit unit)
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        return allUnits.Any(u => u != null && u.owner != unit.owner && u.GetHealth() > 0 && GetDistance(unit, u) <= enemyDetectionRange);
    }
    
    private int GetPieceValue(ChessUnitType type)
    {
        return PieceValues.ContainsKey(type) ? PieceValues[type] : 1;
    }
    
    private bool IsCellOccupied(Vector2Int pos, Unit excludeUnit)
    {
        if (ChessGrid.Instance == null || !ChessGrid.Instance.IsValidCoord(pos.x, pos.y)) return true;
        
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        return allUnits.Any(u => u != null && u != excludeUnit && u.GetHealth() > 0 &&
            ChessGrid.Instance.WorldToGridCoords(u.transform.position) == pos);
    }
    
    private bool HasUnitsOnPath(Vector2Int from, Vector2Int to, Unit excludeUnit)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        
        if (steps <= 1) return false;
        
        for (int i = 1; i < steps; i++)
        {
            int x = from.x + (dx * i / steps);
            int y = from.y + (dy * i / steps);
            if (IsCellOccupied(new Vector2Int(x, y), excludeUnit)) return true;
        }
        
        return false;
    }
    
    // ========================================================================
    // QTE СИСТЕМА
    // ========================================================================
    
    /// <summary>
    /// Бот НЕ может блокировать атаки (всегда возвращает false)
    /// </summary>
    public bool TryBotQTEBlock(Unit botUnit, Unit attackerUnit)
    {
        // Бот не может блокировать урон
        return false;
    }
    
    public float GetQTESuccessChance() => qteSuccessChance;
    public void SetQTESuccessChance(float chance) => qteSuccessChance = Mathf.Clamp01(chance);
}
