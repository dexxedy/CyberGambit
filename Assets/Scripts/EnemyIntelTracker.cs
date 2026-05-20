using UnityEngine;

/// <summary>
/// Обратная совместимость: делегирует в <see cref="FogWarIntelTracker"/>.
/// </summary>
public class EnemyIntelTracker : MonoBehaviour
{
    public static EnemyIntelTracker Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        FogWarIntelTracker.EnsureExists();
        if (Instance != null) return;
        GameObject go = new GameObject("EnemyIntelTracker");
        DontDestroyOnLoad(go);
        go.AddComponent<EnemyIntelTracker>();
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ResetIntel() => FogWarIntelTracker.Instance?.ResetIntel();

    public void MarkSpotted(Unit enemy, Vector3 seenWorldPos) =>
        FogWarIntelTracker.Instance?.MarkVisible(enemy, seenWorldPos);

    public bool IsSpotted(Unit enemy) =>
        FogWarIntelTracker.Instance != null && FogWarIntelTracker.Instance.HasIntel(enemy);

    public bool TryGetLastKnown(Unit enemy, out Vector3 pos)
    {
        pos = default;
        return FogWarIntelTracker.Instance != null &&
               FogWarIntelTracker.Instance.TryGetLastKnown(enemy, out pos);
    }

    /// <summary>Устарело: зрение обновляет FogOfWarManager.</summary>
    public void UpdateSpottingFromAction(Unit playerUnit, Camera actionCamera) { }
}
