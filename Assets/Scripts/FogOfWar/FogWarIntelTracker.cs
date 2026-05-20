using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Разведданные игрока на основе тумана: white = точная позиция, gray = last known.
/// </summary>
public class FogWarIntelTracker : MonoBehaviour
{
    public static FogWarIntelTracker Instance { get; private set; }

    private readonly Dictionary<Unit, Vector3> lastKnownWorldPos = new Dictionary<Unit, Vector3>();
    private readonly HashSet<Unit> everVisible = new HashSet<Unit>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void EnsureExists()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("FogWarIntelTracker");
        DontDestroyOnLoad(go);
        go.AddComponent<FogWarIntelTracker>();
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

    public void ResetIntel()
    {
        lastKnownWorldPos.Clear();
        everVisible.Clear();
    }

    public void MarkVisible(Unit enemy, Vector3 seenWorldPos)
    {
        if (enemy == null) return;
        everVisible.Add(enemy);
        lastKnownWorldPos[enemy] = seenWorldPos;
    }

    public bool HasIntel(Unit enemy)
    {
        return enemy != null && everVisible.Contains(enemy);
    }

    public bool IsCurrentlyVisible(Unit enemy)
    {
        if (enemy == null || enemy.GetHealth() <= 0) return false;
        if (enemy.GetComponent<FogWarExempt>() != null) return true;
        if (FogOfWarManager.Instance == null || !FogOfWarManager.Instance.ShouldRunFog())
            return true;
        return FogOfWarManager.Instance.IsWorldPositionVisible(enemy.transform.position);
    }

    public bool TryGetLastKnown(Unit enemy, out Vector3 pos)
    {
        pos = default;
        if (enemy == null) return false;
        return lastKnownWorldPos.TryGetValue(enemy, out pos);
    }

    /// <summary>Враг показывается на миникарте/в кино хода бота без разведки игрока.</summary>
    public bool IsEnemyShownForBotTurnPresentation(Unit enemy)
    {
        if (enemy == null || enemy.owner != Player.Player2) return false;
        if (GameManager.Instance == null || !GameManager.Instance.IsBotTurn()) return false;

        if (CameraManager.Instance != null && CameraManager.Instance.IsFollowingBotUnit() &&
            CameraManager.Instance.GetFollowedBotUnit() == enemy)
            return true;

        if (BotController.Instance != null && BotController.Instance.GetPickedActingUnitForPresentation() == enemy)
            return true;

        return false;
    }

    public bool ShouldShowEnemyOnTacticalMap(Unit enemy) =>
        enemy != null && (HasIntel(enemy) || IsEnemyShownForBotTurnPresentation(enemy));

    /// <summary>Позиция для миникарты / BF-иконки: точная в white, иначе last known.</summary>
    public bool TryGetDisplayPosition(Unit enemy, out Vector3 pos, out bool isExact)
    {
        pos = default;
        isExact = false;
        if (enemy == null) return false;

        if (IsEnemyShownForBotTurnPresentation(enemy))
        {
            pos = enemy.transform.position;
            isExact = FogOfWarManager.Instance != null &&
                      FogOfWarManager.Instance.IsWorldPositionVisibleOnDisplay(enemy.transform.position);
            return true;
        }

        if (!HasIntel(enemy)) return false;

        if (IsCurrentlyVisible(enemy))
        {
            pos = enemy.transform.position;
            isExact = true;
            return true;
        }

        if (TryGetLastKnown(enemy, out pos))
        {
            isExact = false;
            return true;
        }

        return false;
    }

    public void UpdateFromFog()
    {
        if (FogOfWarManager.Instance == null || !FogOfWarManager.Instance.ShouldRunFog())
            return;

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        foreach (Unit enemy in allUnits)
        {
            if (enemy == null || enemy.GetHealth() <= 0) continue;
            if (enemy.owner != Player.Player2) continue;

            if (IsCurrentlyVisible(enemy))
                MarkVisible(enemy, enemy.transform.position);
        }
    }
}
