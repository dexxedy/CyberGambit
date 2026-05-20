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
    [Tooltip("Минимальная пауза между выстрелами (сек). Главная настройка анти-спама.")]
    [Min(0.05f)] public float secondsBetweenShots = 0.45f;
    [Tooltip("Опционально: если > 0, интервал = max(secondsBetweenShots, 60/RPM). 0 = только secondsBetweenShots.")]
    [Min(0f)] public float rpm = 0f;

    [Header("Ammo")]
    [Min(1)] public int magazineSize = 30;
    [Min(0)] public int startingAmmoInMag = 30;
    [Min(0)] public int startingReserveAmmo = 90;
    [Min(0f)] public float reloadTimeSeconds = 2.0f;

    public float FireIntervalSeconds
    {
        get
        {
            float interval = Mathf.Max(0.05f, secondsBetweenShots);
            if (rpm > 0f)
                interval = Mathf.Max(interval, 60f / rpm);
            return interval;
        }
    }
}

