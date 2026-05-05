using UnityEngine;

/// <summary>
/// Скрывает/показывает только визуал модели юнита (рендереры), не трогая коллайдеры и CharacterController.
/// Используется в тактике BF-стиля: в тактике тела скрыты и показываются только UI-иконки.
/// </summary>
public class TacticalUnitPresentation : MonoBehaviour
{
    private Renderer[] cachedRenderers;

    private void Awake()
    {
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    /// <param name="visible">false = тактический режим (иконки вместо моделей при глобальной тактике)</param>
    public void SetBodyVisualVisible(bool visible)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            CacheRenderers();

        foreach (Renderer r in cachedRenderers)
        {
            if (r != null)
                r.enabled = visible;
        }
    }

    /// <summary>
    /// Применить ко всем юнитам на сцене: в тактической фазе камеры все тела скрыты,
    /// в экшене все модели отображаются (бой от первого лица).
    /// </summary>
    public static void ApplyGlobal(bool hideBodiesForTacticalTopDown)
    {
        TacticalUnitPresentation[] all = FindObjectsByType<TacticalUnitPresentation>(FindObjectsSortMode.None);
        foreach (TacticalUnitPresentation p in all)
        {
            if (p == null) continue;
            p.SetBodyVisualVisible(!hideBodiesForTacticalTopDown);
        }
    }
}
