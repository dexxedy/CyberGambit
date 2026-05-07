using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-разведданные игрока о врагах (PvBot):
/// - spotted: враг был замечен игроком в экшен-режиме (LOS/FOV)
/// - lastKnown: последняя подтвержденная world-позиция врага
/// </summary>
public class EnemyIntelTracker : MonoBehaviour
{
    public static EnemyIntelTracker Instance { get; private set; }

    [Header("Spotting (Action mode only)")]
    [SerializeField] private float spotRangeMeters = 30f;
    [SerializeField] [Range(10f, 180f)] private float spotFovDegrees = 95f;
    [SerializeField] private float eyeOffsetY = 1.4f;
    [SerializeField] private LayerMask losOcclusionMask = ~0;

    private readonly HashSet<Unit> spotted = new HashSet<Unit>();
    private readonly Dictionary<Unit, Vector3> lastKnownWorldPos = new Dictionary<Unit, Vector3>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("EnemyIntelTracker");
        DontDestroyOnLoad(go);
        go.AddComponent<EnemyIntelTracker>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ResetIntel()
    {
        spotted.Clear();
        lastKnownWorldPos.Clear();
    }

    public void MarkSpotted(Unit enemy, Vector3 seenWorldPos)
    {
        if (enemy == null) return;
        spotted.Add(enemy);
        lastKnownWorldPos[enemy] = seenWorldPos;
    }

    public bool IsSpotted(Unit enemy)
    {
        return enemy != null && spotted.Contains(enemy);
    }

    public bool TryGetLastKnown(Unit enemy, out Vector3 pos)
    {
        pos = default;
        if (enemy == null) return false;
        return lastKnownWorldPos.TryGetValue(enemy, out pos);
    }

    /// <summary>
    /// Обновляет разведданные: вызывать только в экшен-режиме, когда игрок контролирует юнита.
    /// </summary>
    public void UpdateSpottingFromAction(Unit playerUnit, Camera actionCamera)
    {
        if (playerUnit == null || actionCamera == null) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.GetGameMode() != GameMode.PlayerVsBot) return;
        if (playerUnit.owner != Player.Player1) return; // игрок = Player1

        Vector3 eye = actionCamera.transform.position;
        Vector3 forward = actionCamera.transform.forward;
        float half = Mathf.Max(1f, spotFovDegrees) * 0.5f;
        float range = Mathf.Max(0.1f, spotRangeMeters);

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach (Unit enemy in allUnits)
        {
            if (enemy == null) continue;
            if (enemy.GetHealth() <= 0) continue;
            if (enemy.owner != Player.Player2) continue;

            Vector3 target = enemy.transform.position + Vector3.up * Mathf.Max(0f, eyeOffsetY);
            Vector3 to = target - eye;
            float dist = to.magnitude;
            if (dist > range) continue;
            if (dist < 0.05f)
            {
                MarkSpotted(enemy, enemy.transform.position);
                continue;
            }

            Vector3 dir = to / dist;
            float angle = Vector3.Angle(forward, dir);
            if (angle > half) continue;

            // LOS: если луч до цели не упирается в препятствие (включая самого врага) — считаем видимым.
            if (Physics.Raycast(eye, dir, out RaycastHit hit, dist, losOcclusionMask))
            {
                // Если первым попали во врага/его детей — LOS ок.
                if (hit.collider != null)
                {
                    Unit hitUnit = hit.collider.GetComponentInParent<Unit>();
                    if (hitUnit != enemy) continue;
                }
            }

            MarkSpotted(enemy, enemy.transform.position);
        }
    }
}

