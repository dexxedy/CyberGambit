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
        Mission1FlagZone[] found = FindObjectsByType<Mission1FlagZone>(FindObjectsSortMode.None);
        if (found != null && found.Length > 0)
        {
            foreach (var f in found.OrderBy(x => x.FlagIndex))
            {
                flags.Add(f);
                if (flags.Count >= 3) break;
            }
            if (flags.Count >= 3) return;
        }

        // 2) если не нашли — создаём 3 флага программно по центру доски
        ChessGrid grid = ChessGrid.Instance;
        if (grid == null) return;

        int y = Mathf.Clamp(grid.height / 2, 0, grid.height - 1);
        int x1 = Mathf.Clamp(grid.width / 4, 0, grid.width - 1);
        int x2 = Mathf.Clamp(grid.width / 2, 0, grid.width - 1);
        int x3 = Mathf.Clamp((grid.width * 3) / 4, 0, grid.width - 1);

        // Поднимаем флаги по вертикали примерно на уровень юнитов,
        // чтобы trigger гарантированно пересекался с CharacterController'ом.
        float unitWorldY = 0.5f;
        Unit anyUnit = FindObjectsByType<Unit>(FindObjectsSortMode.None)
            .FirstOrDefault(u => u != null && u.GetHealth() > 0);
        if (anyUnit != null)
        {
            unitWorldY = anyUnit.transform.position.y;
        }

        Vector2Int[] coords = new[]
        {
            new Vector2Int(x1, y),
            new Vector2Int(x2, y),
            new Vector2Int(x3, y)
        };

        for (int i = 0; i < 3; i++)
        {
            GameObject go = new GameObject($"Mission1FlagZone_{i}");
            Vector3 flagPos = grid.GridToWorldPosition(coords[i].x, coords[i].y);
            flagPos.y = unitWorldY;
            go.transform.position = flagPos;
            go.transform.rotation = Quaternion.identity;

            // Collider trigger
            float size = grid.cellSize * 0.6f;
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
        if (flags == null || flags.Count < 3) return;

        bool allFlagsCapturedByPlayer =
            flags.All(f => f != null && f.IsOwnedBy(playerOwner));

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);

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

