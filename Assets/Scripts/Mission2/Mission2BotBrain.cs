using System.Collections;
using System.Linq;
using UnityEngine;
using Mission2;

public class Mission2BotBrain : MonoBehaviour, IBotMissionBrain
{
    [SerializeField] private bool debug = false;
    [Header("Mission2 Tuning")]
    [SerializeField] private float meleeAttackRange = 3.0f;
    [SerializeField] private float rangedPreferredMinDistance = 8.0f;
    [SerializeField] private float rangedPreferredMaxDistance = 18.0f;
    [Tooltip("Если пешка может за этот ход дойти до мили-дистанции танка — предпочитаем пешку, чтобы давить и срывать закрепление.")]
    [SerializeField] private float pawnReachMeleeBonus = 9999f;
    [Header("Targeting")]
    [Tooltip("Радиус вокруг танка, где юнит считается 'охраной' (бот может переключаться на неё).")]
    [SerializeField] private float guardRadiusAroundTank = 10f;
    [Tooltip("Как часто бот будет предпочитать бить охрану вместо танка (если охрана рядом). 0..1")]
    [Range(0f, 1f)]
    [SerializeField] private float focusGuardChance = 0.35f;

    private Unit lastSelectedUnit;

    public bool CanRun()
    {
        // Mission2 exists if there are destructible objectives in scene.
        DestructibleObjective[] objs = FindObjectsByType<DestructibleObjective>(FindObjectsInactive.Exclude);
        return objs != null && objs.Any(o => o != null && !o.IsDestroyed);
    }

    public IEnumerator ExecuteTurn(BotController controller, float moveBudgetMeters)
    {
        if (controller == null)
            yield break;

        Unit[] all = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        Unit[] botUnits = all.Where(u => u != null && u.owner == Player.Player2 && u.GetHealth() > 0).ToArray();
        if (botUnits.Length == 0)
        {
            controller.EndTurnInternal();
            yield break;
        }

        // Mission2 goal: stop the tank from charging/firing. After tank dies -> player loses.
        Unit tankUnit = all.FirstOrDefault(u => u != null && u.owner == Player.Player1 && u.GetComponent<TankController>() != null);
        if (tankUnit == null || tankUnit.GetHealth() <= 0)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.EndGame(Player.Player1);
            yield break;
        }

        // Choose target: usually tank, sometimes a nearby guard unit protecting it.
        Unit chosenTarget = ChooseTarget(all, tankUnit);
        Vector3 targetPos = chosenTarget.transform.position;

        // Pick bot unit with rotation + pawn pressure: don't play only Bishop forever.
        Unit selected = botUnits
            .OrderByDescending(u => ScoreUnit(u, tankUnit, moveBudgetMeters))
            .First();

        // Soft rotation: if equal-ish score, avoid repeating same unit.
        if (lastSelectedUnit != null && botUnits.Length > 1)
        {
            Unit bestAlt = botUnits
                .Where(u => u != null && u != lastSelectedUnit)
                .OrderByDescending(u => ScoreUnit(u, tankUnit, moveBudgetMeters))
                .FirstOrDefault();

            if (bestAlt != null)
            {
                float sMain = ScoreUnit(selected, tankUnit, moveBudgetMeters);
                float sAlt = ScoreUnit(bestAlt, tankUnit, moveBudgetMeters);
                if (sAlt >= sMain * 0.92f) // почти так же хорошо
                    selected = bestAlt;
            }
        }

        lastSelectedUnit = selected;
        selected.SetRemainingMoveMeters(moveBudgetMeters);

        if (debug)
            Debug.Log($"[Mission2BotBrain] Selected={selected.name} target={(chosenTarget == tankUnit ? "Tank" : chosenTarget.name)} budget={moveBudgetMeters:0.00}m");

        if (CameraManager.Instance != null)
        {
            // Кинематографический показ хода бота — только если этот бот-юнит уже "известен" игроку (spotted).
            bool canShow = EnemyIntelTracker.Instance != null && EnemyIntelTracker.Instance.IsSpotted(selected);
            if (canShow)
            {
                CameraManager.Instance.SwitchToBotUnitView(selected);
                if (CameraManager.Instance.IsBotFollowCameraEnabled())
                    yield return new WaitForSeconds(0.35f);
            }
        }

        // If target is in attack range (melee or weapon range), attack; else reposition.
        if (IsInAttackRange(selected, chosenTarget))
        {
            yield return controller.PerformAttackInternal(selected, chosenTarget);
            controller.EndTurnInternal();
            yield break;
        }

        Vector3 moveTarget = targetPos;
        Weapon w = selected.GetEquippedWeapon();
        if (w != null && w.Config != null)
        {
            // Ranged units try to stay in a band, not hug the target.
            float desired = Mathf.Clamp(w.Config.maxHitRangeMeters * 0.8f, rangedPreferredMinDistance, rangedPreferredMaxDistance);
            Vector3 from = selected.transform.position;
            Vector3 to = chosenTarget.transform.position;
            Vector3 dir = (from - to);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            moveTarget = to + dir * desired;
        }

        yield return controller.MoveTowardsWorldTargetByBudgetInternal(selected, moveTarget, moveBudgetMeters);

        if (IsInAttackRange(selected, chosenTarget))
        {
            yield return controller.PerformAttackInternal(selected, chosenTarget);
        }

        controller.EndTurnInternal();
    }

    private float ScoreUnit(Unit unit, Unit tankUnit, float moveBudgetMeters)
    {
        if (unit == null || tankUnit == null) return float.NegativeInfinity;
        float dist = Vector3.Distance(unit.transform.position, tankUnit.transform.position);

        bool ranged = unit.GetEquippedWeapon() != null && unit.GetEquippedWeapon().Config != null;
        float score = 0f;

        // Base: closer to tank is better.
        score += 1000f - dist;

        if (ranged)
        {
            // Ranged: prefer to be within weapon range band.
            float range = Mathf.Max(0.1f, unit.GetEquippedWeapon().Config.maxHitRangeMeters);
            if (dist <= range) score += 400f;
        }
        else
        {
            // Pawn pressure: if can reach melee this turn, huge priority.
            float desiredMelee = Mathf.Max(0.1f, meleeAttackRange);
            float pathLen = unit.CalculateNavMeshPathLength(tankUnit.transform.position);
            if (pathLen > 0.01f && pathLen <= Mathf.Max(0f, moveBudgetMeters) + desiredMelee)
                score += pawnReachMeleeBonus;
        }

        return score;
    }

    private bool IsInAttackRange(Unit attacker, Unit tankUnit)
    {
        if (attacker == null || tankUnit == null) return false;
        float d = Vector3.Distance(attacker.transform.position, tankUnit.transform.position);
        Weapon w = attacker.GetEquippedWeapon();
        if (w != null && w.Config != null)
        {
            return d <= Mathf.Max(0.1f, w.Config.maxHitRangeMeters);
        }
        return d <= Mathf.Max(0.1f, meleeAttackRange);
    }

    private Unit ChooseTarget(Unit[] allUnits, Unit tankUnit)
    {
        if (allUnits == null || tankUnit == null) return tankUnit;
        var guards = allUnits
            .Where(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0 && u != tankUnit)
            .Where(u => Vector3.Distance(u.transform.position, tankUnit.transform.position) <= guardRadiusAroundTank)
            .ToList();

        if (guards.Count == 0) return tankUnit;

        // Pick most relevant guard: closest to tank, and ranged guards slightly higher priority
        Unit best = guards
            .OrderByDescending(u => u.GetEquippedWeapon() != null && u.GetEquippedWeapon().Config != null)
            .ThenBy(u => Vector3.Distance(u.transform.position, tankUnit.transform.position))
            .FirstOrDefault();

        // Sometimes still focus tank.
        if (Random.value > focusGuardChance) return tankUnit;
        return best != null ? best : tankUnit;
    }
}

