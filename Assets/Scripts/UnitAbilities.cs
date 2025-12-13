using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Управляет активными способностями для всех типов фигур.
/// Каждая фигура имеет свою уникальную способность с индивидуальным КД в ходах.
/// </summary>
public class UnitAbilities : MonoBehaviour
{
    private Unit unit;
    
    [Header("Ability Cooldowns (in turns)")]
    private int cooldownHorseJump = 0;           // Конь - Скачок (КД: 1 ход)
    private int cooldownBishopReflection = 0;     // Слон - Отражение (КД: 3 хода)
    private int cooldownGuardianShield = 0;      // Ладья - Щит (КД: 3 хода)
    private int cooldownQueenDamageBoost = 0;    // Ферзь - Увеличение урона (КД: 5 ходов)
    private int cooldownKingHeal = 0;            // Король - Хил (КД: 4 хода)
    
    [Header("Ability States")]
    private bool isKingHealMode = false;         // Режим выбора цели для хилла короля
    
    void Start()
    {
        unit = GetComponent<Unit>();
        if (unit == null)
        {
            Debug.LogError($"UnitAbilities на {gameObject.name}: Unit компонент не найден!");
        }
    }
    
    void Update()
    {
        // Обработка активации способности только для контролируемого юнита в экшен-режиме
        if (unit == null) return;
        if (!unit.IsControlled()) return;
        if (!CameraManager.Instance.IsActionMode()) return;
        if (GameManager.Instance.IsPaused()) return;
        
        // Проверяем нажатие клавиши способности (Q)
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            ActivateAbility();
        }
        
        // Обработка режима выбора цели для короля
        if (isKingHealMode)
        {
            HandleKingHealSelection();
        }
    }
    
    /// <summary>
    /// Активирует способность в зависимости от типа фигуры
    /// </summary>
    public void ActivateAbility()
    {
        if (unit == null) return;
        
        switch (unit.chessType)
        {
            case ChessUnitType.Horse:
                ActivateHorseJump();
                break;
            case ChessUnitType.Bishop:
                ActivateBishopReflection();
                break;
            case ChessUnitType.Guardian:
                ActivateGuardianShield();
                break;
            case ChessUnitType.Queen:
                ActivateQueenDamageBoost();
                break;
            case ChessUnitType.King:
                ActivateKingHeal();
                break;
            case ChessUnitType.Pawn:
                // Пешка не имеет способностей
                Debug.Log("Пешка не имеет активных способностей");
                break;
        }
    }
    
    // ========== КОНЬ: Скачок на валидные клетки (КД: 1 ход) ==========
    
    /// <summary>
    /// Активирует способность коня - скачок на ближайшую валидную клетку в направлении взгляда
    /// </summary>
    private void ActivateHorseJump()
    {
        if (cooldownHorseJump > 0)
        {
            Debug.Log($"Способность коня на перезарядке! Осталось ходов: {cooldownHorseJump}");
            return;
        }
        
        // Получаем направление взгляда из action camera
        Camera actionCamera = CameraManager.Instance != null ? CameraManager.Instance.GetActionCamera() : null;
        if (actionCamera == null)
        {
            Debug.LogWarning("Не удалось получить action camera для способности коня!");
            return;
        }
        
        // Направление взгляда (forward камеры)
        Vector3 lookDirection = actionCamera.transform.forward;
        lookDirection.y = 0; // Игнорируем вертикальную составляющую
        lookDirection.Normalize();
        
        // Получаем текущую позицию коня
        Vector2Int currentPos = ChessGrid.Instance.WorldToGridCoords(unit.transform.position);
        
        // Получаем все возможные ходы коня
        List<Vector2Int> possibleMoves = ChessRulesManager.Instance.GetHorsePossibleMoves(currentPos);
        
        if (possibleMoves.Count == 0)
        {
            Debug.Log("Нет доступных клеток для скачка коня!");
            return;
        }
        
        // Находим ближайшую валидную клетку в направлении взгляда
        Vector2Int bestTarget = currentPos;
        float bestDot = -1f; // Косинус угла между направлением взгляда и направлением к клетке
        
        foreach (Vector2Int targetPos in possibleMoves)
        {
            Vector3 targetWorldPos = ChessGrid.Instance.GridToWorldPosition(targetPos.x, targetPos.y);
            Vector3 directionToTarget = (targetWorldPos - unit.transform.position);
            directionToTarget.y = 0;
            directionToTarget.Normalize();
            
            // Вычисляем косинус угла (чем ближе к 1, тем ближе к направлению взгляда)
            float dot = Vector3.Dot(lookDirection, directionToTarget);
            
            if (dot > bestDot)
            {
                bestDot = dot;
                bestTarget = targetPos;
            }
        }
        
        // Телепортируемся на выбранную клетку
        Vector3 targetWorldPosFinal = ChessGrid.Instance.GridToWorldPosition(bestTarget.x, bestTarget.y);
        unit.transform.position = targetWorldPosFinal;
        unit.SnapToGrid();
        
        // Устанавливаем КД
        cooldownHorseJump = 1;
        
        // Воспроизводим визуальный эффект и звук
        AbilityVisualEffects visualEffects = unit.GetComponent<AbilityVisualEffects>();
        if (visualEffects != null)
        {
            visualEffects.PlayHorseJumpEffect();
        }
        
        Debug.Log($"Конь прыгнул на {ChessGrid.Instance.GridToChessNotation(bestTarget.x, bestTarget.y)}");
    }
    
    // ========== СЛОН: Отражение урона (КД: 3 хода) ==========
    
    /// <summary>
    /// Активирует способность слона - отражение урона на следующий ход врага
    /// </summary>
    private void ActivateBishopReflection()
    {
        if (cooldownBishopReflection > 0)
        {
            Debug.Log($"Способность слона на перезарядке! Осталось ходов: {cooldownBishopReflection}");
            return;
        }
        
        // Активируем отражение на 2 хода (весь следующий ход врага + начало следующего хода игрока для отключения)
        // Эффект будет активен весь ход врага и отключится в начале следующего хода игрока
        unit.SetReflectionActive(true, 2);
        cooldownBishopReflection = 3;
        
        // Воспроизводим визуальный эффект и звук
        AbilityVisualEffects visualEffects = unit.GetComponent<AbilityVisualEffects>();
        if (visualEffects != null)
        {
            visualEffects.PlayBishopReflectionEffect(true);
        }
        
        Debug.Log("Слон активировал отражение урона! Действует весь следующий ход врага.");
    }
    
    // ========== ЛАДЬЯ: Уменьшение урона (КД: 3 хода) ==========
    
    /// <summary>
    /// Активирует способность ладьи - уменьшение входящего урона на 50% на следующий ход врага
    /// </summary>
    private void ActivateGuardianShield()
    {
        if (cooldownGuardianShield > 0)
        {
            Debug.Log($"Способность ладьи на перезарядке! Осталось ходов: {cooldownGuardianShield}");
            return;
        }
        
        // Активируем снижение урона на 2 хода (весь следующий ход врага + начало следующего хода игрока для отключения)
        // Эффект будет активен весь ход врага и отключится в начале следующего хода игрока
        unit.SetDamageReduction(true, 2);
        cooldownGuardianShield = 3;
        
        // Воспроизводим визуальный эффект и звук
        AbilityVisualEffects visualEffects = unit.GetComponent<AbilityVisualEffects>();
        if (visualEffects != null)
        {
            visualEffects.PlayGuardianShieldEffect(true);
        }
        
        Debug.Log("Ладья активировала щит! Урон уменьшен на 50% в следующем ходу врага.");
    }
    
    // ========== ФЕРЗЬ: Увеличение урона (КД: 5 ходов) ==========
    
    /// <summary>
    /// Активирует способность ферзя - увеличение урона на 20% в текущем ходу
    /// </summary>
    private void ActivateQueenDamageBoost()
    {
        if (cooldownQueenDamageBoost > 0)
        {
            Debug.Log($"Способность ферзя на перезарядке! Осталось ходов: {cooldownQueenDamageBoost}");
            return;
        }
        
        // Активируем увеличение урона на 1 ход (текущий ход)
        unit.SetDamageMultiplier(1.2f, 1); // +20% урона
        cooldownQueenDamageBoost = 5;
        
        // Воспроизводим визуальный эффект и звук
        AbilityVisualEffects visualEffects = unit.GetComponent<AbilityVisualEffects>();
        if (visualEffects != null)
        {
            visualEffects.PlayQueenBoostEffect(true);
        }
        
        Debug.Log("Ферзь активировала увеличение урона! +20% урона в этом ходу.");
    }
    
    // ========== КОРОЛЬ: Хил союзника (КД: 4 хода) ==========
    
    /// <summary>
    /// Активирует способность короля - исцеление союзного юнита на 20% HP
    /// </summary>
    private void ActivateKingHeal()
    {
        if (cooldownKingHeal > 0)
        {
            Debug.Log($"Способность короля на перезарядке! Осталось ходов: {cooldownKingHeal}");
            return;
        }
        
        // Включаем режим выбора цели
        isKingHealMode = true;
        Debug.Log("Выберите союзного юнита для исцеления (ЛКМ) или отмените (ПКМ)");
    }
    
    /// <summary>
    /// Обрабатывает выбор цели для хилла короля
    /// </summary>
    private void HandleKingHealSelection()
    {
        // Отмена по правой кнопке мыши
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isKingHealMode = false;
            Debug.Log("Исцеление короля отменено.");
            return;
        }
        
        // Выбор цели по левой кнопке мыши
        if (Mouse.current.leftButton.wasPressedThisFrame && CameraManager.Instance != null)
        {
            // Используем action camera для raycast в экшен-режиме
            Camera actionCamera = CameraManager.Instance.GetActionCamera();
            if (actionCamera == null)
            {
                // Если action camera недоступна, используем tactical camera
                actionCamera = CameraManager.Instance.GetTacticalCamera();
            }
            
            if (actionCamera != null)
            {
                Ray ray = actionCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                RaycastHit hit;
                
                // Raycast на все объекты, затем проверяем Unit компонент
                if (Physics.Raycast(ray, out hit, Mathf.Infinity))
                {
                    Unit target = hit.collider.GetComponentInParent<Unit>();
                    
                    // Если не нашли в родителе, пробуем в самом объекте
                    if (target == null)
                    {
                        target = hit.collider.GetComponent<Unit>();
                    }
                    
                    // Проверяем, что цель - союзник
                    if (target != null && target.owner == unit.owner && target != unit)
                    {
                        // Исцеляем на 20% от максимального здоровья
                        int healAmount = Mathf.RoundToInt(target.GetMaxHealth() * 0.2f);
                        target.Heal(healAmount);
                        
                        // Устанавливаем КД
                        cooldownKingHeal = 4;
                        
                        // Воспроизводим визуальный эффект и звук
                        AbilityVisualEffects visualEffects = unit.GetComponent<AbilityVisualEffects>();
                        if (visualEffects != null)
                        {
                            visualEffects.PlayKingHealEffect(target);
                        }
                        
                        // Отключаем режим выбора
                        isKingHealMode = false;
                        
                        Debug.Log($"Король исцелил {target.chessType} на {healAmount} HP");
                    }
                    else if (target != null)
                    {
                        Debug.Log("Можно исцелять только союзников!");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Обновляет КД способностей при смене хода (вызывается из GameManager)
    /// </summary>
    public void UpdateCooldowns()
    {
        if (cooldownHorseJump > 0) cooldownHorseJump--;
        if (cooldownBishopReflection > 0) cooldownBishopReflection--;
        if (cooldownGuardianShield > 0) cooldownGuardianShield--;
        if (cooldownQueenDamageBoost > 0) cooldownQueenDamageBoost--;
        if (cooldownKingHeal > 0) cooldownKingHeal--;
    }
    
    /// <summary>
    /// Получает оставшееся КД способности (для UI)
    /// </summary>
    public int GetAbilityCooldown()
    {
        switch (unit.chessType)
        {
            case ChessUnitType.Horse: return cooldownHorseJump;
            case ChessUnitType.Bishop: return cooldownBishopReflection;
            case ChessUnitType.Guardian: return cooldownGuardianShield;
            case ChessUnitType.Queen: return cooldownQueenDamageBoost;
            case ChessUnitType.King: return cooldownKingHeal;
            default: return 0;
        }
    }
    
    /// <summary>
    /// Проверяет, готова ли способность к использованию
    /// </summary>
    public bool IsAbilityReady()
    {
        return GetAbilityCooldown() == 0;
    }
}

