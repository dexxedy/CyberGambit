using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Оружие как prefab-компонент: хранит состояние (патроны/перезарядка/КД) и читает числа из RangedWeaponConfig.
/// </summary>
public class Weapon : MonoBehaviour
{
    [SerializeField] private RangedWeaponConfig config;
    [Tooltip("Точка выстрела (опционально). Если не задано — используем origin из камеры.")]
    [SerializeField] private Transform muzzle;

    [Header("Runtime State (read-only)")]
    [SerializeField] private int ammoInMag;
    [SerializeField] private int ammoReserve;
    [SerializeField] private bool isReloading;

    private float nextFireTime;
    private float reloadEndTime;

    public RangedWeaponConfig Config => config;
    public Transform Muzzle => muzzle;
    public int AmmoInMag => ammoInMag;
    public int AmmoReserve => ammoReserve;
    public bool IsReloading => isReloading;
    public int MagazineSize => config != null ? config.magazineSize : 0;

    public void InitializeFromConfigIfNeeded()
    {
        if (config == null) return;
        if (ammoInMag == 0 && ammoReserve == 0)
        {
            ammoInMag = Mathf.Clamp(config.startingAmmoInMag, 0, config.magazineSize);
            ammoReserve = Mathf.Max(0, config.startingReserveAmmo);
        }
    }

    public bool TryStartReload()
    {
        if (config == null) return false;
        if (isReloading) return false;
        if (ammoInMag >= config.magazineSize) return false;
        if (ammoReserve <= 0) return false;
        isReloading = true;
        reloadEndTime = Time.time + Mathf.Max(0f, config.reloadTimeSeconds);
        return true;
    }

    public void TickReload()
    {
        if (!isReloading) return;
        if (Time.time < reloadEndTime) return;
        FinishReload();
    }

    private void FinishReload()
    {
        isReloading = false;
        if (config == null) return;
        int need = Mathf.Max(0, config.magazineSize - ammoInMag);
        int take = Mathf.Min(need, Mathf.Max(0, ammoReserve));
        ammoInMag += take;
        ammoReserve -= take;
    }

    public bool CanFire()
    {
        if (config == null) return false;
        if (isReloading) return false;
        if (ammoInMag <= 0) return false;
        return Time.time >= nextFireTime;
    }

    /// <summary>
    /// Hitscan-выстрел. origin/direction обычно берутся из action камеры. Возвращает true, если выстрел состоялся.
    /// </summary>
    public bool TryFire(Unit owner, Vector3 origin, Vector3 direction)
    {
        if (owner == null) return false;
        InitializeFromConfigIfNeeded();
        TickReload();

        if (!CanFire()) return false;

        float interval = config.FireIntervalSeconds;
        nextFireTime = Time.time + Mathf.Max(0f, interval);

        float maxRange = Mathf.Max(0.1f, config.maxHitRangeMeters);
        LayerMask mask = config.hitMask;
        bool friendly = config.friendlyFire;
        int baseDamage = Mathf.Max(0, config.damagePerHit);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxRange, mask, QueryTriggerInteraction.Ignore))
        {
            Unit target = hit.collider != null ? hit.collider.GetComponentInParent<Unit>() : null;
            if (target != null && target != owner)
            {
                bool isEnemy = target.owner != owner.owner;
                if (isEnemy || friendly)
                {
                    int finalDamage = Mathf.RoundToInt(baseDamage * owner.GetDamageMultiplier());
                    target.TakeDamage(finalDamage, owner);
                }
            }
        }

        ammoInMag = Mathf.Max(0, ammoInMag - 1);
        return true;
    }

    /// <summary>Утилита: подобрать origin на NavMesh/с поверхности, если нужно.</summary>
    public static Vector3 SafeOrigin(Vector3 origin)
    {
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
            return hit.position + Vector3.up * 0.05f;
        return origin;
    }
}

