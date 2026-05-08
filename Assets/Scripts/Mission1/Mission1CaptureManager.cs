using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Mission1CaptureManager : MonoBehaviour
{
    [Header("Mission Setup")]
    [SerializeField] private List<Mission1FlagZone> flags = new List<Mission1FlagZone>(3);
    [SerializeField] private Player playerOwner = Player.Player1;

    [Header("Timing")]
    [SerializeField] private float checkIntervalSeconds = 0.25f;

    private bool isResolved = false;
    private Coroutine captureLoopCoroutine;

    private void Awake()
    {
        EnsureFlagsExist();
    }

    private void Start()
    {
        captureLoopCoroutine ??= StartCoroutine(CaptureLoop());
    }

    private void EnsureFlagsExist()
    {
        if (flags != null && flags.Count >= 3) return;

        if (flags == null) flags = new List<Mission1FlagZone>(3);
        flags.Clear();

        // 1) пробуем найти уже существующие флаги в сцене
        Mission1FlagZone[] found = FindObjectsByType<Mission1FlagZone>(FindObjectsInactive.Exclude);
        if (found != null && found.Length > 0)
        {
            foreach (var f in found.OrderBy(x => x.FlagIndex))
            {
                flags.Add(f);
                if (flags.Count >= 3) break;
            }
            if (flags.Count >= 3) return;
        }

        // 2) без ChessGrid — три флага по линии в мире вокруг «центра» живых юнитов
        float unitWorldY = 0.5f;
        float spacing = 6f;
        Vector3 center = Vector3.zero;
        Unit[] units = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        int alive = 0;
        foreach (Unit u in units)
        {
            if (u == null || u.GetHealth() <= 0) continue;
            center += u.transform.position;
            alive++;
            unitWorldY = u.transform.position.y;
        }
        if (alive > 0) center /= alive;
        else center = Vector3.zero;

        for (int i = 0; i < 3; i++)
        {
            GameObject go = new GameObject($"Mission1FlagZone_{i}");
            Vector3 flagPos = center + new Vector3((i - 1) * spacing, 0f, 0f);
            flagPos.y = unitWorldY;
            go.transform.position = flagPos;
            go.transform.rotation = Quaternion.identity;

            float size = 1.5f;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(size, 2f, size);

            // Чтобы OnTriggerEnter/Exit гарантированно работали с CharacterController
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            Mission1FlagZone zone = go.AddComponent<Mission1FlagZone>();
            zone.Initialize(i);

            flags.Add(zone);
        }
    }

    private IEnumerator CaptureLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(checkIntervalSeconds);

        while (!isResolved)
        {
            EvaluateWinLoseConditions();
            yield return wait;
        }
    }

    private void EvaluateWinLoseConditions()
    {
        if (isResolved) return;
        if (GameManager.Instance == null) return;
        // Пока игрок расставляет армию на поле — ещё нет его юнитов; не считать это поражением.
        if (GameManager.Instance.IsArmyDeploymentPhase()) return;
        if (flags == null || flags.Count < 3) return;

        bool allFlagsCapturedByPlayer =
            flags.All(f => f != null && f.IsOwnedBy(playerOwner));

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);

        bool allEnemiesDead =
            !allUnits.Any(u => u != null && u.owner != playerOwner && u.GetHealth() > 0);

        bool allPlayerUnitsDead =
            !allUnits.Any(u => u != null && u.owner == playerOwner && u.GetHealth() > 0);

        // Порядок: сначала WIN, затем LOSE
        if (allFlagsCapturedByPlayer || allEnemiesDead)
        {
            ResolveWin();
            return;
        }

        if (allPlayerUnitsDead)
        {
            ResolveLose();
        }
    }

    private void ResolveWin()
    {
        if (isResolved) return;
        isResolved = true;

        // Если выигрывает playerOwner, то проигрывает другой игрок.
        Player loser = playerOwner == Player.Player1 ? Player.Player2 : Player.Player1;
        GameManager.Instance.EndGame(loser);
    }

    private void ResolveLose()
    {
        if (isResolved) return;
        isResolved = true;
        // Проигрывает playerOwner
        GameManager.Instance.EndGame(playerOwner);
    }
}

