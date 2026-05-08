using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Глобальный менеджер способностей. Отслеживает КД всех юнитов и обновляет их при смене хода.
/// </summary>
public class AbilitySystem : MonoBehaviour
{
    public static AbilitySystem Instance;
    
    private List<UnitAbilities> allUnitAbilities = new List<UnitAbilities>();
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Находим все компоненты способностей на сцене
        RefreshUnitAbilities();
    }
    
    /// <summary>
    /// Обновляет список всех компонентов способностей (вызывается при создании новых юнитов)
    /// </summary>
    public void RefreshUnitAbilities()
    {
        UnitAbilities[] found = FindObjectsByType<UnitAbilities>(FindObjectsInactive.Exclude);
        allUnitAbilities = new List<UnitAbilities>(found);
    }
    
    /// <summary>
    /// Вызывается при смене хода для обновления КД всех способностей
    /// </summary>
    public void OnTurnSwitch()
    {
        // Обновляем список на случай, если появились новые юниты
        RefreshUnitAbilities();
        
        // Обновляем КД всех способностей
        foreach (UnitAbilities abilities in allUnitAbilities)
        {
            if (abilities != null)
            {
                abilities.UpdateCooldowns();
            }
        }
    }
}

