using UnityEngine;

[CreateAssetMenu(menuName = "CyberGambit/Weapons/Ranged Weapon Config", fileName = "RangedWeaponConfig")]
public class RangedWeaponConfig : ScriptableObject
{
    [Header("Ballistics")]
    [Min(0.1f)] public float maxHitRangeMeters = 25f;
    [Tooltip("Слои для raycast (юниты + окружение/стены).")]
    public LayerMask hitMask = ~0;
    public bool friendlyFire = false;

    [Header("Damage")]
    [Min(0)] public int damagePerHit = 12;

    [Header("Fire Rate")]
    [Tooltip("Rounds per minute. 600 RPM ≈ 0.1s между выстрелами.")]
    [Min(1f)] public float rpm = 450f;

    [Header("Ammo")]
    [Min(1)] public int magazineSize = 30;
    [Min(0)] public int startingAmmoInMag = 30;
    [Min(0)] public int startingReserveAmmo = 90;
    [Min(0f)] public float reloadTimeSeconds = 2.0f;

    public float FireIntervalSeconds => 60f / Mathf.Max(1f, rpm);
}

