using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Mission1BotObjectiveProvider : MonoBehaviour
{
    [Header("Flags (optional)")]
    [SerializeField] private List<Mission1FlagZone> flags = new List<Mission1FlagZone>(3);

    [Header("Bot Side")]
    [SerializeField] private Player botOwner = Player.Player2;

    [Header("Scoring")]
    [SerializeField] private float distanceWeight = 0.5f;
    [SerializeField] private float flagThreatRadius = 10f;
    [SerializeField] private float threatenedFlagBonus = 140f;

    public bool HasFlags => flags != null && flags.Count >= 3;

    private void Awake()
    {
        RefreshFlagsIfNeeded();
    }

    private void RefreshFlagsIfNeeded()
    {
        if (flags != null && flags.Count >= 3) return;

        if (flags == null) flags = new List<Mission1FlagZone>(3);
        flags.Clear();

        Mission1FlagZone[] found = FindObjectsByType<Mission1FlagZone>(FindObjectsSortMode.None);
        if (found == null) return;

        foreach (Mission1FlagZone f in found.OrderBy(x => x.FlagIndex))
        {
            if (f == null) continue;
            flags.Add(f);
            if (flags.Count >= 3) break;
        }
    }

    public Mission1FlagZone SelectTargetFlag()
    {
        RefreshFlagsIfNeeded();
        if (flags == null || flags.Count == 0) return null;

        int playerOwnedCount = flags.Count(f => f != null && f.CurrentOwner == Mission1FlagZone.FlagOwner.Player1);

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        List<Unit> botUnits = allUnits
            .Where(u => u != null && u.owner == botOwner && u.GetHealth() > 0)
            .ToList();
        List<Unit> playerUnits = allUnits
            .Where(u => u != null && u.owner == Player.Player1 && u.GetHealth() > 0)
            .ToList();

        Mission1FlagZone best = null;
        float bestScore = float.NegativeInfinity;

        foreach (Mission1FlagZone flag in flags)
        {
            if (flag == null) continue;
            bool playerNear = playerUnits.Any(p => Vector3.Distance(p.transform.position, flag.transform.position) <= flagThreatRadius);
            bool alreadyOurs = flag.CurrentOwner == Mission1FlagZone.FlagOwner.Player2;

            // Если флаг наш и не под угрозой — обычно игнорируем.
            if (alreadyOurs && !playerNear) continue;

            int urgency = flag.CurrentOwner == Mission1FlagZone.FlagOwner.Player1 ? 200 : 80;
            float needBonus = playerOwnedCount >= 2 ? 250f : 0f; // когда игрок почти выиграл
            float threatBonus = playerNear ? threatenedFlagBonus : 0f;

            float distToClosestBot = float.MaxValue;
            if (botUnits.Count > 0)
            {
                distToClosestBot = botUnits.Min(u => Vector3.Distance(u.transform.position, flag.transform.position));
            }

            float distanceScore = distToClosestBot * distanceWeight;

            // Чем меньше дистанция, тем выше итоговый скор (distanceScore вычитаем).
            float score = urgency + needBonus + threatBonus - distanceScore;
            if (score > bestScore)
            {
                bestScore = score;
                best = flag;
            }
        }

        return best ?? flags.FirstOrDefault(f => f != null);
    }

    public bool TryGetTargetFlag(out Mission1FlagZone targetFlag)
    {
        targetFlag = SelectTargetFlag();
        return targetFlag != null;
    }
}

