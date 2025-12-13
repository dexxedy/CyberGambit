using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Управляет зонами угрозы вокруг вражеских юнитов.
/// Отслеживает приближение игрока к врагам и запускает QTE при входе в зону угрозы.
/// </summary>
public class ThreatZoneManager : MonoBehaviour
{
    public static ThreatZoneManager Instance;
    
    [Header("Threat Zone Settings")]
    [SerializeField] private float threatZoneRadius = 2.5f; // Радиус зоны угрозы в Unity единицах
    [SerializeField] private LayerMask unitLayer;
    [SerializeField] private GameObject threatZoneVisualPrefab; // Префаб для визуализации зоны (опционально)
    
    private Dictionary<Unit, GameObject> threatZoneVisuals = new Dictionary<Unit, GameObject>();
    private Unit currentControlledUnit;
    private HashSet<Unit> enemiesThatAttacked = new HashSet<Unit>(); // Враги, которые уже атаковали в этом ходу
    
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    void Update()
    {
        // Проверяем зоны угрозы только в экшен-режиме
        if (CameraManager.Instance != null && CameraManager.Instance.IsActionMode())
        {
            // Получаем текущего контролируемого юнита
            Unit newControlledUnit = GetCurrentControlledUnit();
            
            // Если сменился контролируемый юнит, сбрасываем список атаковавших врагов
            if (newControlledUnit != currentControlledUnit)
            {
                enemiesThatAttacked.Clear();
                currentControlledUnit = newControlledUnit;
            }
            
            if (currentControlledUnit != null)
            {
                CheckThreatZones();
            }
        }
        else
        {
            // Если вышли из экшен-режима, сбрасываем список
            enemiesThatAttacked.Clear();
        }
    }
    
    /// <summary>
    /// Получает юнит, который сейчас контролируется игроком
    /// </summary>
    private Unit GetCurrentControlledUnit()
    {
        if (CameraManager.Instance != null)
        {
            return CameraManager.Instance.GetCurrentControlledUnit();
        }
        return null;
    }
    
    /// <summary>
    /// Проверяет все зоны угрозы и запускает QTE при входе игрока в зону врага
    /// Для пешки QTE не показывается, но враг атакует напрямую
    /// </summary>
    private void CheckThreatZones()
    {
        if (currentControlledUnit == null) return;
        
        // Находим всех юнитов на сцене
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        
        foreach (Unit enemy in allUnits)
        {
            // Пропускаем союзников и самого себя
            if (enemy.owner == currentControlledUnit.owner || enemy == currentControlledUnit)
                continue;
            
            // Пропускаем врагов, которые уже атаковали в этом ходу
            if (enemiesThatAttacked.Contains(enemy))
                continue;
            
            // Вычисляем расстояние до врага
            float distance = Vector3.Distance(currentControlledUnit.transform.position, enemy.transform.position);
            
            // Если игрок вошёл в зону угрозы врага
            if (distance <= threatZoneRadius)
            {
                // Помечаем врага как атаковавшего
                enemiesThatAttacked.Add(enemy);
                
                // Если это пешка - атакуем напрямую без QTE
                if (currentControlledUnit.chessType == ChessUnitType.Pawn)
                {
                    AttackPawnDirectly(currentControlledUnit, enemy);
                }
                // Для остальных фигур - запускаем QTE
                else if (QTESystem.Instance != null && !QTESystem.Instance.IsQTEActive())
                {
                    // Запускаем QTE событие
                    QTESystem.Instance.StartQTE(currentControlledUnit, enemy);
                }
            }
        }
    }
    
    /// <summary>
    /// Атакует пешку напрямую без QTE (враг атакует, пешка получает урон)
    /// </summary>
    /// <param name="pawn">Пешка игрока</param>
    /// <param name="enemy">Вражеский юнит</param>
    private void AttackPawnDirectly(Unit pawn, Unit enemy)
    {
        // Запускаем анимацию атаки врага
        Animator enemyAnimator = enemy.GetComponent<Animator>();
        if (enemyAnimator != null)
        {
            enemyAnimator.SetTrigger("Attack");
        }
        
        // Наносим урон пешке напрямую (без возможности блокировать)
        StartCoroutine(AttackPawnCoroutine(pawn, enemy));
    }
    
    /// <summary>
    /// Корутина для атаки пешки с небольшой задержкой (для визуального эффекта)
    /// </summary>
    private IEnumerator AttackPawnCoroutine(Unit pawn, Unit enemy)
    {
        // Небольшая задержка перед нанесением урона (для синхронизации с анимацией)
        yield return new WaitForSeconds(0.3f);
        
        // Наносим полный урон пешке
        if (pawn != null && enemy != null)
        {
            pawn.TakeDamage(enemy.Damage);
            Debug.Log($"{pawn.chessType} (Пешка) атакована врагом! Получен урон: {enemy.Damage}");
        }
    }
    
    /// <summary>
    /// Показывает визуализацию зоны угрозы вокруг юнита (для отладки)
    /// </summary>
    public void ShowThreatZone(Unit unit)
    {
        if (threatZoneVisualPrefab == null) return;
        
        if (!threatZoneVisuals.ContainsKey(unit))
        {
            GameObject visual = Instantiate(threatZoneVisualPrefab, unit.transform.position, Quaternion.identity);
            visual.transform.SetParent(unit.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * threatZoneRadius * 2f;
            threatZoneVisuals[unit] = visual;
        }
    }
    
    /// <summary>
    /// Скрывает визуализацию зоны угрозы
    /// </summary>
    public void HideThreatZone(Unit unit)
    {
        if (threatZoneVisuals.ContainsKey(unit))
        {
            Destroy(threatZoneVisuals[unit]);
            threatZoneVisuals.Remove(unit);
        }
    }
}

