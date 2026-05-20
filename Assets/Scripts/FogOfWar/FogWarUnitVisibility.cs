using UnityEngine;

/// <summary>
/// На врагах: 3D-рендереры видны только в белой зоне тумана (экшен и тактика).
/// </summary>
[DisallowMultipleComponent]
public class FogWarUnitVisibility : MonoBehaviour
{
    private Unit unit;
    private Renderer[] cachedRenderers;
    private bool lastVisible = true;

    private void Awake()
    {
        unit = GetComponent<Unit>();
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public static void RefreshAll()
    {
        FogWarUnitVisibility[] all = FindObjectsByType<FogWarUnitVisibility>(FindObjectsInactive.Exclude);
        foreach (FogWarUnitVisibility v in all)
        {
            if (v != null) v.ApplyVisibility();
        }
    }

    private void ApplyVisibility()
    {
        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null || unit.owner != Player.Player2)
            return;

        if (GetComponent<FogWarExempt>() != null)
            return;

        // В тактике тела скрыты глобально (TacticalUnitPresentation.ApplyGlobal).
        if (CameraManager.Instance != null && !CameraManager.Instance.IsActionMode())
            return;

        bool visible = ShouldShowEnemyBody();
        if (visible == lastVisible) return;
        lastVisible = visible;

        if (cachedRenderers == null || cachedRenderers.Length == 0)
            CacheRenderers();

        foreach (Renderer r in cachedRenderers)
        {
            if (r != null)
                r.enabled = visible;
        }
    }

    private bool ShouldShowEnemyBody()
    {
        if (GameManager.Instance == null || GameManager.Instance.GetGameMode() != GameMode.PlayerVsBot)
            return true;
        if (FogWarIntelTracker.Instance == null)
            return true;
        return FogWarIntelTracker.Instance.IsCurrentlyVisible(unit);
    }
}
