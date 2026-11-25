using UnityEngine;

public interface IDamageableTarget
{
    int CurrentHealth { get; }
    int MaxHealth { get; }
    bool IsAlive { get; }
    Player Owner { get; }
    string DisplayName { get; }
    Transform AimPoint { get; }

    void ApplyDamage(int damage, Unit attacker);
}
