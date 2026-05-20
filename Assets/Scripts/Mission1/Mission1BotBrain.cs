using System.Collections;
using System.Linq;
using UnityEngine;

public class Mission1BotBrain : MonoBehaviour, IBotMissionBrain
{
    [SerializeField] private bool debug = false;

    [Header("Mission1 Weights / Tuning")]
    [SerializeField] private float flagThreatRadius = 10f;
    [SerializeField] private float defendOffsetMeters = 3f;
    [SerializeField] private float attackChaseScore = 520f;
    [SerializeField] private float advanceWhen2FlagsSafeBonus = 180f;
    [SerializeField] private float recaptureScore = 600f;
    [SerializeField] private float defendScore = 450f;
    [SerializeField] private float interceptScore = 350f;
    [SerializeField] private float flagStopRadius = 2.0f;
    [SerializeField] private float defendSpreadRadius = 8.0f;
    [SerializeField] private float alreadyDefendedPenalty = 220f;

    [Header("Mission1 Rotation")]
    [SerializeField] private float rotationMaxExtraDistanceMeters = 8f;

    [Header("Turn pacing")]
    [Tooltip("После действий бота: пауза, камера с врага, ход игроку.")]
    [SerializeField] private float postHandoffToPlayerDelay = 0.45f;

    private int turnCounter = 0;
    private Unit lastSelectedUnit = null;
    private Mission1FlagZone lockedTargetFlagForTurn;

    private static void FinishBotTurn(BotController controller) => controller.EndTurnInternal();

    private IEnumerator HandoffToPlayer(BotController controller)
    {
        if (postHandoffToPlayerDelay > 0f)
            yield return new WaitForSeconds(postHandoffToPlayerDelay);
        if (CameraManager.Instance != null && CameraManager.Instance.IsFollowingBotUnit())
            CameraManager.Instance.ReturnTacticalCameraToOriginalPosition();
        FinishBotTurn(controller);
    }

    public bool CanRun()
    {
        // Mission1 exists if there are flags in scene.
        Mission1FlagZone[] flags = FindObjectsByType<Mission1FlagZone>(FindObjectsInactive.Exclude);
        return flags != null && flags.Length > 0 && GetComponent<Mission1BotObjectiveProvider>() != null;
    }

    public IEnumerator PickActingUnitBeforeDice(BotController controller)
    {
        if (controller == null)
            yield break;

        Mission1BotObjectiveProvider provider = GetComponent<Mission1BotObjectiveProvider>();
        if (provider == null)
        {
            yield return HandoffToPlayer(controller);
            yield break;
        }

        Mission1FlagZone targetFlag = provider.SelectTargetFlag();
        if (targetFlag == null)
        {
            yield return HandoffToPlayer(controller);
            yield break;
        }

        lockedTargetFlagForTurn = targetFlag;

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        var botUnits = allUnits.Where(u => u != null && u.owner == Player.Player2 && u.GetHealth() > 0).ToList();
        if (botUnits.Count == 0)
        {
            lockedTargetFlagForTurn = null;
            yield return HandoffToPlayer(controller);
            yield break;
        }

        Unit bestAll = SelectUnit(botUnits);
        Unit selected = bestAll;
        if (lastSelectedUnit != null && botUnits.Count > 1)
        {
            var altList = botUnits.Where(u => u != null && u != lastSelectedUnit).ToList();
            Unit bestAlt = altList.Count > 0 ? SelectUnit(altList) : null;
            if (bestAlt != null)
            {
                float bestAllScore = UnitProximityScore(bestAll);
                float bestAltScore = UnitProximityScore(bestAlt);
                selected = (bestAltScore <= bestAllScore + Mathf.Max(0f, rotationMaxExtraDistanceMeters)) ? bestAlt : bestAll;
            }
        }

        if (selected == null)
        {
            lockedTargetFlagForTurn = null;
            yield return HandoffToPlayer(controller);
            yield break;
        }

        controller.SetPickedBotActingUnitForTurn(selected);
    }

    public IEnumerator ExecuteTurn(BotController controller, float moveBudgetMeters)
    {
        if (controller == null)
            yield break;

        Mission1BotObjectiveProvider provider = GetComponent<Mission1BotObjectiveProvider>();
        if (provider == null)
        {
            if (debug) Debug.LogWarning("[Mission1BotBrain] No Mission1BotObjectiveProvider on BotController.");
            yield return HandoffToPlayer(controller);
            yield break;
        }

        Mission1FlagZone targetFlag = lockedTargetFlagForTurn != null ? lockedTargetFlagForTurn : provider.SelectTargetFlag();
        lockedTargetFlagForTurn = null;
        if (targetFlag == null)
        {
            if (debug)
            {
                int count = FindObjectsByType<Mission1FlagZone>(FindObjectsInactive.Exclude)?.Length ?? 0;
                Debug.LogWarning($"[Mission1BotBrain] SelectTargetFlag returned null. FlagsInScene={count}");
            }
            yield return HandoffToPlayer(controller);
            yield break;
        }

        if (debug)
        {
            Debug.Log($"[Mission1BotBrain] TargetFlag={targetFlag.FlagIndex} owner={targetFlag.CurrentOwner} budget={moveBudgetMeters:0.00}m");
        }

        turnCounter++;
        yield return ExecuteMission1Turn(controller, provider, targetFlag, moveBudgetMeters);
    }

    private IEnumerator ExecuteMission1Turn(BotController controller, Mission1BotObjectiveProvider provider, Mission1FlagZone targetFlag, float moveBudgetMeters)
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        var botUnits = allUnits.Where(u => u != null && u.owner == Player.Player2 && u.GetHealth() > 0).ToList();
        if (botUnits.Count == 0)
        {
            yield return HandoffToPlayer(controller);
            yield break;
        }

        Unit pre = controller.ConsumePickedBotActingUnitForTurn();
        Unit selected;
        if (pre != null && pre.GetHealth() > 0 && pre.owner == Player.Player2 && botUnits.Contains(pre))
            selected = pre;
        else
        {
            Unit bestAll = SelectUnit(botUnits);
            selected = bestAll;
            if (lastSelectedUnit != null && botUnits.Count > 1)
            {
                var altList = botUnits.Where(u => u != null && u != lastSelectedUnit).ToList();
                Unit bestAlt = altList.Count > 0 ? SelectUnit(altList) : null;
                if (bestAlt != null)
                {
                    float bestAllScore = UnitProximityScore(bestAll);
                    float bestAltScore = UnitProximityScore(bestAlt);
                    selected = (bestAltScore <= bestAllScore + Mathf.Max(0f, rotationMaxExtraDistanceMeters)) ? bestAlt : bestAll;
                }
            }
        }

        if (selected == null)
        {
            yield return HandoffToPlayer(controller);
            yield break;
        }

        lastSelectedUnit = selected;
        selected.SetRemainingMoveMeters(moveBudgetMeters);

        // Attack now if visible enemy in range
        if (controller.TryGetBestVisibleEnemyInAttackRangeForBrain(selected, out Unit enemyNow))
        {
            yield return controller.PerformAttackInternal(selected, enemyNow);
            yield return HandoffToPlayer(controller);
            yield break;
        }

        // Decide movement target (flags/defend/intercept/chase)
        Vector3 moveTarget = ChooseMoveTarget(controller, selected, provider);
        yield return controller.MoveTowardsWorldTargetByBudgetInternal(selected, moveTarget, moveBudgetMeters);

        if (controller.TryGetBestVisibleEnemyInAttackRangeForBrain(selected, out Unit enemyAfter))
        {
            yield return controller.PerformAttackInternal(selected, enemyAfter);
        }

        yield return HandoffToPlayer(controller);
    }

    private Unit SelectUnit(System.Collections.Generic.List<Unit> botUnits)
    {
        var flags = FindObjectsByType<Mission1FlagZone>(FindObjectsInactive.Exclude);
        if (flags == null || flags.Length == 0) return botUnits.FirstOrDefault();

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        var playerUnits = allUnits.Where(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0).ToList();

        float Score(Unit u)
        {
            float best = float.PositiveInfinity;
            foreach (var f in flags)
            {
                if (f == null) continue;
                bool underThreat = playerUnits.Any(p => Vector3.Distance(p.transform.position, f.transform.position) <= flagThreatRadius);
                bool important = f.CurrentOwner != Mission1FlagZone.FlagOwner.Player2 || underThreat;
                if (!important) continue;
                float d = Vector3.Distance(u.transform.position, f.transform.position);
                if (d < best) best = d;
            }
            return best;
        }

        return botUnits.OrderBy(Score).FirstOrDefault();
    }

    private float UnitProximityScore(Unit u)
    {
        if (u == null) return 99999f;
        var flags = FindObjectsByType<Mission1FlagZone>(FindObjectsInactive.Exclude);
        if (flags == null || flags.Length == 0) return 99999f;

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        var playerUnits = allUnits.Where(x => x != null && x.owner == Player.Player1 && x.GetHealth() > 0).ToList();

        float best = float.PositiveInfinity;
        foreach (var f in flags)
        {
            if (f == null) continue;
            bool underThreat = playerUnits.Any(p => Vector3.Distance(p.transform.position, f.transform.position) <= flagThreatRadius);
            bool important = f.CurrentOwner != Mission1FlagZone.FlagOwner.Player2 || underThreat;
            if (!important) continue;
            float d = Vector3.Distance(u.transform.position, f.transform.position);
            if (d < best) best = d;
        }
        if (float.IsPositiveInfinity(best))
            best = flags.Where(f => f != null).Min(f => Vector3.Distance(u.transform.position, f.transform.position));
        return best;
    }

    private Vector3 ChooseMoveTarget(BotController controller, Unit botUnit, Mission1BotObjectiveProvider provider)
    {
        var flags = FindObjectsByType<Mission1FlagZone>(FindObjectsInactive.Exclude);
        if (flags == null || flags.Length == 0) return botUnit.transform.position;

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        var playerUnits = allUnits.Where(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0).ToList();
        var botUnits = allUnits.Where(u => u != null && u.owner == Player.Player2 && u.GetHealth() > 0).ToList();

        // 2-of-3 safe gating
        int safe = 0;
        foreach (var f in flags)
        {
            if (f == null) continue;
            bool playerNear = playerUnits.Any(p => Vector3.Distance(p.transform.position, f.transform.position) <= flagThreatRadius);
            if (f.CurrentOwner != Mission1FlagZone.FlagOwner.Player1 && !playerNear) safe++;
        }
        bool canAdvance = safe >= 2;

        // Chase visible enemy if safe enough
        if (canAdvance)
        {
            Unit bestVisible = null;
            float bestScore = float.NegativeInfinity;
            foreach (var e in playerUnits)
            {
                if (!controller.CanSeeTargetForBrain(botUnit, e)) continue;
                float d = Vector3.Distance(botUnit.transform.position, e.transform.position);
                if (d <= 3f) continue;
                float s = (attackChaseScore - d) + advanceWhen2FlagsSafeBonus;
                if (s > bestScore) { bestScore = s; bestVisible = e; }
            }
            if (bestVisible != null)
            {
                Vector3 dir = (botUnit.transform.position - bestVisible.transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
                dir.Normalize();
                return bestVisible.transform.position + dir * 2.5f;
            }
        }

        // Flag-based scoring with spread
        Mission1FlagZone bestFlag = null;
        float bestFlagScore = float.NegativeInfinity;
        bool bestFlagIsDefend = false;
        foreach (var f in flags)
        {
            if (f == null) continue;
            bool playerNear = playerUnits.Any(p => Vector3.Distance(p.transform.position, f.transform.position) <= flagThreatRadius);
            bool hasOtherDefenderNear = botUnits.Any(b => b != null && b != botUnit && Vector3.Distance(b.transform.position, f.transform.position) <= defendSpreadRadius);
            float defendedPenalty = hasOtherDefenderNear ? alreadyDefendedPenalty : 0f;

            float score = float.NegativeInfinity;
            bool isDefend = false;
            if (f.CurrentOwner == Mission1FlagZone.FlagOwner.Player1 || f.CurrentOwner == Mission1FlagZone.FlagOwner.None)
            {
                score = recaptureScore + (f.CurrentOwner == Mission1FlagZone.FlagOwner.Player1 ? 120f : 0f) + (playerNear ? 80f : 0f);
                score -= defendedPenalty * 0.35f;
            }
            else if (f.CurrentOwner == Mission1FlagZone.FlagOwner.Player2 && playerNear)
            {
                score = defendScore - defendedPenalty;
                isDefend = true;
            }
            else if (f.CurrentOwner != Mission1FlagZone.FlagOwner.Player1 && playerNear)
            {
                score = interceptScore - defendedPenalty;
            }
            else
            {
                continue;
            }

            score -= Vector3.Distance(botUnit.transform.position, f.transform.position);
            if (score > bestFlagScore)
            {
                bestFlagScore = score;
                bestFlag = f;
                bestFlagIsDefend = isDefend;
            }
        }

        if (bestFlag != null)
        {
            // If this is a defend decision, stand off a bit instead of stacking on flag center.
            if (bestFlagIsDefend)
                return GetDefendPointNearFlag(botUnit, bestFlag);
            return bestFlag.transform.position;
        }

        // Provider fallback
        var t = provider != null ? provider.SelectTargetFlag() : null;
        if (t != null) return t.transform.position;

        // Last fallback: patrol around nearest non-player flag (avoid standing exactly on it).
        Mission1FlagZone patrol = flags
            .Where(f => f != null && f.CurrentOwner != Mission1FlagZone.FlagOwner.Player1)
            .OrderBy(f => Vector3.Distance(botUnit.transform.position, f.transform.position))
            .FirstOrDefault()
            ?? flags.Where(f => f != null).OrderBy(f => Vector3.Distance(botUnit.transform.position, f.transform.position)).First();

        return GetPatrolPointAroundFlag(botUnit, patrol);
    }

    private Vector3 GetDefendPointNearFlag(Unit botUnit, Mission1FlagZone flag)
    {
        Vector3 toBot = (botUnit.transform.position - flag.transform.position);
        toBot.y = 0f;
        if (toBot.sqrMagnitude < 0.01f) toBot = botUnit.transform.forward;
        toBot.Normalize();
        return flag.transform.position + toBot * Mathf.Max(0f, defendOffsetMeters);
    }

    private Vector3 GetPatrolPointAroundFlag(Unit botUnit, Mission1FlagZone flag)
    {
        Vector3 basePos = flag.transform.position;
        Vector3 offset = botUnit.transform.position - basePos;
        offset.y = 0f;
        if (offset.sqrMagnitude < 0.01f) offset = Vector3.forward;
        offset.Normalize();
        Vector3 tangent = new Vector3(-offset.z, 0f, offset.x);
        float r = Mathf.Clamp(flagStopRadius, 1.25f, 4.0f);
        return basePos + tangent * r;
    }
}

