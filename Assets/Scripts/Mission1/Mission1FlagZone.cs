using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Mission1FlagZone : MonoBehaviour
{
    public enum FlagOwner
    {
        None,
        Player1,
        Player2
    }

    [Header("Debug / Setup")]
    [SerializeField] private int flagIndex = 0;

    // Keep-logic: если в зоне никого нет, владелец не сбрасывается.
    [SerializeField] private FlagOwner currentOwner = FlagOwner.None;

    private readonly HashSet<Unit> unitsInZone = new HashSet<Unit>();

    public int FlagIndex => flagIndex;
    public FlagOwner CurrentOwner => currentOwner;

    public void Initialize(int index)
    {
        flagIndex = index;
        // Начальное состояние - "никто не владеет", пока игроки не зайдут в зону.
        currentOwner = FlagOwner.None;
    }

    private void Awake()
    {
        // На случай, если инспектором не выставили collider как trigger.
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Unit unit = other.GetComponentInParent<Unit>();
        if (unit == null) return;
        if (unit.GetHealth() <= 0) return;

        unitsInZone.Add(unit);
        RecalculateOwner();
    }

    private void OnTriggerExit(Collider other)
    {
        Unit unit = other.GetComponentInParent<Unit>();
        if (unit == null) return;

        unitsInZone.Remove(unit);
        RecalculateOwner();
    }

    private void RecalculateOwner()
    {
        // Если юнит умер, но не успел триггернуться на выходе - убираем его при пересчёте.
        unitsInZone.RemoveWhere(u => u == null || u.GetHealth() <= 0);

        bool hasPlayer1Unit = unitsInZone.Any(u => u.owner == Player.Player1);
        bool hasPlayer2Unit = unitsInZone.Any(u => u.owner == Player.Player2);

        if (hasPlayer1Unit)
        {
            currentOwner = FlagOwner.Player1;
        }
        else if (hasPlayer2Unit)
        {
            currentOwner = FlagOwner.Player2;
        }
        // else: keep currentOwner (keep-логика)
    }

    public bool IsOwnedBy(Player player)
    {
        return (player == Player.Player1 && currentOwner == FlagOwner.Player1) ||
               (player == Player.Player2 && currentOwner == FlagOwner.Player2);
    }

    public Player? GetOwnerPlayerOrNull()
    {
        switch (currentOwner)
        {
            case FlagOwner.Player1: return Player.Player1;
            case FlagOwner.Player2: return Player.Player2;
            default: return null;
        }
    }
}

