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
    [Header("Guardian vs tank (acting unit)")]
    [Tooltip("Бонус к ScoreUnit для Guardian, когда у игрока жив только танк — ходит и подходит к танку в основном ракетчик, остальные почти не выбираются.")]
    [SerializeField] private float guardianSoloTankActingBonus = 20000f;
    [Tooltip("Небольшой бонус Guardian к выбору ходящего юнита, пока у игрока есть живая пехота (не ломает общий приоритет).")]
    [SerializeField] private float guardianFootmenAliveActingNudge = 120f;
    [Header("Targeting")]
    [Tooltip("Радиус вокруг танка, где юнит считается 'охраной' (бот может переключаться на неё).")]
    [SerializeField] private float guardRadiusAroundTank = 10f;
    [Tooltip("Как часто бот будет предпочитать бить охрану вместо танка (если охрана рядом). 0..1")]
    [Range(0f, 1f)]
    [SerializeField] private float focusGuardChance = 0.35f;
    [Header("Fallback when non-Guardian cannot target P1 units")]
    [Tooltip("Если нет целей-юнитов, не-Guardian идёт к ближайшему DestructibleObjective, иначе к терминалу, иначе на кольцо вокруг танка (не в центр).")]
    [SerializeField] private float flankRingMinMeters = 12f;
    [Header("Turn pacing (Mission 2)")]
    [Tooltip("Доп. пауза перед движением/атакой после кубика (камера до кубика в BotController).")]
    [SerializeField] private float preEnemyActionDelay = 0.25f;
    [Tooltip("Пауза после действия врага, затем камера с врага и ход игроку.")]
    [SerializeField] private float postEnemyActionDelay = 0.45f;

    private Unit lastSelectedUnit;

    private IEnumerator HandoffBotTurnToPlayer(BotController controller)
    {
        if (postEnemyActionDelay > 0f)
            yield return new WaitForSeconds(postEnemyActionDelay);
        if (CameraManager.Instance != null && CameraManager.Instance.IsFollowingBotUnit())
            CameraManager.Instance.ReturnTacticalCameraToOriginalPosition();
        controller.EndTurnInternal();
    }

    public bool CanRun()
    {
        DestructibleObjective[] objs = FindObjectsByType<DestructibleObjective>(FindObjectsInactive.Exclude);
        return objs != null && objs.Any(o => o != null && !o.IsDestroyed);
    }

    public IEnumerator PickActingUnitBeforeDice(BotController controller)
    {
        if (controller == null)
            yield break;

        Unit[] all = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        Unit[] botUnits = all.Where(u => u != null && u.owner == Player.Player2 && u.GetHealth() > 0).ToArray();
        if (botUnits.Length == 0)
        {
            yield return HandoffBotTurnToPlayer(controller);
            yield break;
        }

        Unit tankUnit = all.FirstOrDefault(u => u != null && u.owner == Player.Player1 && u.IsTankUnit);
        if (tankUnit == null || tankUnit.GetHealth() <= 0)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.EndGame(Player.Player1);
            yield break;
        }

        float estBudget = GameManager.Instance != null ? GameManager.Instance.GetMaxPossibleMoveBudgetMeters() : 0f;
        Unit selected = PickActingUnit(botUnits, tankUnit, estBudget, all);
        controller.SetPickedBotActingUnitForTurn(selected);
    }

    public IEnumerator ExecuteTurn(BotController controller, float moveBudgetMeters)
    {
        if (controller == null)
            yield break;

        Unit[] all = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        Unit[] botUnits = all.Where(u => u != null && u.owner == Player.Player2 && u.GetHealth() > 0).ToArray();
        if (botUnits.Length == 0)
        {
            yield return HandoffBotTurnToPlayer(controller);
            yield break;
        }

        Unit tankUnit = all.FirstOrDefault(u => u != null && u.owner == Player.Player1 && u.IsTankUnit);
        if (tankUnit == null || tankUnit.GetHealth() <= 0)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.EndGame(Player.Player1);
            yield break;
        }

        Unit pre = controller != null ? controller.ConsumePickedBotActingUnitForTurn() : null;
        Unit selected;
        if (pre != null && pre.GetHealth() > 0 && pre.owner == Player.Player2 && botUnits.Contains(pre))
            selected = pre;
        else
            selected = PickActingUnit(botUnits, tankUnit, moveBudgetMeters, all);

        lastSelectedUnit = selected;
        selected.SetRemainingMoveMeters(moveBudgetMeters);

        Unit chosenTarget = ChooseTarget(all, tankUnit, selected);
        bool hasUnitTarget = chosenTarget != null;
        Vector3 moveTarget = ComputeMoveTargetWorld(selected, chosenTarget, hasUnitTarget, controller, tankUnit);

        if (debug)
        {
            string tlabel = !hasUnitTarget ? "WorldAnchor" : (chosenTarget == tankUnit ? "Tank" : chosenTarget.name);
            Debug.Log($"[Mission2BotBrain] Selected={selected.name} target={tlabel} budget={moveBudgetMeters:0.00}m");
        }

        if (preEnemyActionDelay > 0f)
            yield return new WaitForSeconds(preEnemyActionDelay);

        // Для стрелка: без LOS не тратим PerformAttack впустую (LOS для танка учитывает дочерние коллайдеры — см. BotController.CanSeeTarget).
        bool mayTryImmediateAttack = hasUnitTarget && IsInAttackRange(selected, chosenTarget);
        if (mayTryImmediateAttack && selected.GetEquippedWeapon() != null && selected.GetEquippedWeapon().Config != null)
        {
            if (!controller.CanSeeTargetForBrain(selected, chosenTarget))
                mayTryImmediateAttack = false;
        }

        if (mayTryImmediateAttack)
        {
            yield return controller.PerformAttackInternal(selected, chosenTarget);
            yield return HandoffBotTurnToPlayer(controller);
            yield break;
        }

        yield return controller.MoveTowardsWorldTargetByBudgetInternal(selected, moveTarget, moveBudgetMeters);

        if (hasUnitTarget && IsInAttackRange(selected, chosenTarget))
        {
            Weapon wr = selected.GetEquippedWeapon();
            if (wr != null && wr.Config != null && !controller.CanSeeTargetForBrain(selected, chosenTarget))
            {
                // Стрелок без LOS — не вызываем пустой PerformAttack.
            }
            else
            {
                yield return controller.PerformAttackInternal(selected, chosenTarget);
            }
        }

        yield return HandoffBotTurnToPlayer(controller);
    }

    private Unit PickActingUnit(Unit[] botUnits, Unit tankUnit, float moveBudgetMeters, Unit[] allUnits)
    {
        if (botUnits == null || botUnits.Length == 0 || tankUnit == null)
            return null;

        bool soloTank = PlayerHasOnlyTankAlive(allUnits);

        Unit selected = botUnits
            .OrderByDescending(u => ScoreUnit(u, tankUnit, moveBudgetMeters, soloTank))
            .First();

        if (lastSelectedUnit != null && botUnits.Length > 1)
        {
            Unit bestAlt = botUnits
                .Where(u => u != null && u != lastSelectedUnit)
                .OrderByDescending(u => ScoreUnit(u, tankUnit, moveBudgetMeters, soloTank))
                .FirstOrDefault();

            if (bestAlt != null)
            {
                float sMain = ScoreUnit(selected, tankUnit, moveBudgetMeters, soloTank);
                float sAlt = ScoreUnit(bestAlt, tankUnit, moveBudgetMeters, soloTank);
                if (sAlt >= sMain * 0.92f)
                    selected = bestAlt;
            }
        }

        return selected;
    }

    /// <summary>Живой игрок P1 есть, но все не-танки мертвы — цель только танк, ходить к нему должен в первую очередь Guardian.</summary>
    private static bool PlayerHasOnlyTankAlive(Unit[] allUnits)
    {
        if (allUnits == null) return false;
        bool anyP1Alive = false;
        foreach (Unit u in allUnits)
        {
            if (u == null || u.owner != Player.Player1 || u.GetHealth() <= 0) continue;
            anyP1Alive = true;
            if (!u.IsTankUnit)
                return false;
        }
        return anyP1Alive;
    }

    private float ScoreUnit(Unit unit, Unit tankUnit, float moveBudgetMeters, bool soloTank)
    {
        if (unit == null || tankUnit == null) return float.NegativeInfinity;

        bool nonGuardianSolo = soloTank && unit.chessType != ChessUnitType.Guardian;
        Vector3 anchor = nonGuardianSolo
            ? GetNearestPressureAnchorWorld(unit.transform.position, tankUnit)
            : tankUnit.transform.position;
        float distAnchor = HorizontalDistance(unit.transform.position, anchor);
        float distTank = HorizontalDistance(unit.transform.position, tankUnit.transform.position);

        bool ranged = unit.GetEquippedWeapon() != null && unit.GetEquippedWeapon().Config != null;
        float score = 0f;

        score += nonGuardianSolo ? (1100f - distAnchor) : (1000f - distTank);

        if (ranged)
        {
            float range = Mathf.Max(0.1f, unit.GetEquippedWeapon().Config.maxHitRangeMeters);
            float d = nonGuardianSolo ? distAnchor : distTank;
            if (d <= range) score += 400f;
        }
        else
        {
            float desiredMelee = Mathf.Max(0.1f, meleeAttackRange);
            float pathLen = unit.CalculateNavMeshPathLength(anchor);
            if (pathLen > 0.01f && pathLen <= Mathf.Max(0f, moveBudgetMeters) + desiredMelee)
                score += pawnReachMeleeBonus;
        }

        if (unit.chessType == ChessUnitType.Guardian)
        {
            if (soloTank)
                score += guardianSoloTankActingBonus;
            else
                score += guardianFootmenAliveActingNudge;
        }

        return score;
    }

    private bool IsInAttackRange(Unit attacker, Unit target)
    {
        if (attacker == null || target == null) return false;
        float d = Vector3.Distance(attacker.transform.position, target.transform.position);
        Weapon weapon = attacker.GetEquippedWeapon();
        if (weapon != null && weapon.Config != null)
        {
            return d <= Mathf.Max(0.1f, weapon.Config.maxHitRangeMeters);
        }
        return d <= Mathf.Max(0.1f, meleeAttackRange);
    }

    /// <summary>
    /// Цель движения: Guardian+танк со стволом — не уходим со стойки, если уже в max range и есть LOS;
    /// без LOS — кольцо на дистанции, не центр танка.
    /// </summary>
    private Vector3 ComputeMoveTargetWorld(Unit selected, Unit chosenTarget, bool hasUnitTarget, BotController controller, Unit tankUnit)
    {
        if (!hasUnitTarget || chosenTarget == null || selected == null)
            return GetNearestPressureAnchorWorld(selected.transform.position, tankUnit);

        Vector3 targetPos = chosenTarget.transform.position;
        Weapon w = selected.GetEquippedWeapon();
        if (w == null || w.Config == null)
            return targetPos;

        float maxR = Mathf.Max(0.1f, w.Config.maxHitRangeMeters);
        float dist = HorizontalDistance(selected.transform.position, targetPos);
        bool guardianVsTank = selected.chessType == ChessUnitType.Guardian && chosenTarget.IsTankUnit;
        bool canSee = controller != null && controller.CanSeeTargetForBrain(selected, chosenTarget);

        float standoff = Mathf.Clamp(w.Config.maxHitRangeMeters * 0.8f, rangedPreferredMinDistance, rangedPreferredMaxDistance);
        Vector3 radial = selected.transform.position - targetPos;
        radial.y = 0f;
        if (radial.sqrMagnitude < 0.0001f) radial = Vector3.forward;
        radial.Normalize();
        Vector3 standoffPoint = targetPos + radial * standoff;

        if (guardianVsTank && dist <= maxR && canSee)
            return selected.transform.position;

        return standoffPoint;
    }

    private Unit ChooseTarget(Unit[] allUnits, Unit tankUnit, Unit attacker)
    {
        if (allUnits == null || tankUnit == null) return tankUnit;
        if (attacker != null && attacker.chessType == ChessUnitType.Guardian)
            return tankUnit;

        var footmen = allUnits
            .Where(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0 && !u.IsTankUnit)
            .ToList();

        if (footmen.Count == 0)
        {
            // Только танк (или нет пехоты): не-Guardian не должен «целиться» в танк как в юнит — движение к объективам/якорю.
            return null;
        }

        var nearGuards = footmen
            .Where(u => Vector3.Distance(u.transform.position, tankUnit.transform.position) <= guardRadiusAroundTank)
            .ToList();

        if (nearGuards.Count > 0 && Random.value <= focusGuardChance)
        {
            Unit best = nearGuards
                .OrderByDescending(u => u.GetEquippedWeapon() != null && u.GetEquippedWeapon().Config != null)
                .ThenBy(u => Vector3.Distance(u.transform.position, tankUnit.transform.position))
                .FirstOrDefault();
            if (best != null)
                return best;
        }

        Vector3 from = attacker != null ? attacker.transform.position : tankUnit.transform.position;
        return footmen
            .OrderBy(u => Vector3.Distance(u.transform.position, from))
            .First();
    }

    /// <summary>Точка давления для бота без валидной юнит-цели: цели миссии → терминал → кольцо вокруг танка.</summary>
    private Vector3 GetNearestPressureAnchorWorld(Vector3 fromWorld, Unit tankUnit)
    {
        Vector3 best = Vector3.zero;
        float bestD = float.PositiveInfinity;

        DestructibleObjective[] objs = FindObjectsByType<DestructibleObjective>(FindObjectsInactive.Exclude);
        if (objs != null)
        {
            foreach (DestructibleObjective o in objs)
            {
                if (o == null || o.IsDestroyed) continue;
                float d = HorizontalDistance(fromWorld, o.transform.position);
                if (d < bestD)
                {
                    bestD = d;
                    best = o.transform.position;
                }
            }
        }

        if (bestD < float.PositiveInfinity)
            return best;

        Mission2DefenseTerminal[] terms = FindObjectsByType<Mission2DefenseTerminal>(FindObjectsInactive.Exclude);
        if (terms != null)
        {
            foreach (Mission2DefenseTerminal t in terms)
            {
                if (t == null) continue;
                float d = HorizontalDistance(fromWorld, t.transform.position);
                if (d < bestD)
                {
                    bestD = d;
                    best = t.transform.position;
                }
            }
        }

        if (bestD < float.PositiveInfinity)
            return best;

        return FlankRingPointNearTank(fromWorld, tankUnit);
    }

    private Vector3 FlankRingPointNearTank(Vector3 fromWorld, Unit tankUnit)
    {
        if (tankUnit == null) return fromWorld;
        Vector3 t = tankUnit.transform.position;
        Vector3 delta = fromWorld - t;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.01f)
            delta = Vector3.forward;
        delta.Normalize();
        float ring = Mathf.Max(guardRadiusAroundTank + 4f, flankRingMinMeters);
        return t + delta * ring;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
