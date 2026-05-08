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

    [Header("VFX")]
    [SerializeField] private GameObject muzzleFlashVfxPrefab;
    [SerializeField] private GameObject tracerVfxPrefab;
    [SerializeField] private GameObject impactVfxPrefab;
    [Tooltip("Какие слои считаем 'поверхностями' для попадания/трассера. Если не трогать — будет Everything.")]
    [SerializeField] private LayerMask vfxRayMask = ~0;
    [Header("Camera shake (optional)")]
    [SerializeField] private float fireShakeDuration = 0.08f;
    [SerializeField] private float fireShakeMagnitude = 0.03f;

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
        bool friendly = config.friendlyFire;
        int baseDamage = Mathf.Max(0, config.damagePerHit);

        SpawnMuzzleFlash();
        DoCameraShake();

        bool hasHit = false;
        Vector3 hitPoint = origin + direction * maxRange;
        Vector3 hitNormal = -direction;

        // Один raycast "по миру": даёт и точку попадания (стены/пол/юниты), и не позволяет стрелять сквозь коллайдеры.
        LayerMask rayMask = vfxRayMask.value != 0 ? vfxRayMask : ~0;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxRange, rayMask, QueryTriggerInteraction.Ignore))
        {
            hasHit = true;
            hitPoint = hit.point;
            hitNormal = hit.normal;

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

        SpawnTracer(origin, hitPoint);
        if (hasHit)
            SpawnImpact(hitPoint, hitNormal);

        ammoInMag = Mathf.Max(0, ammoInMag - 1);
        return true;
    }

    private void SpawnMuzzleFlash()
    {
        if (muzzleFlashVfxPrefab == null) return;
        Transform t = muzzle != null ? muzzle : transform;
        GameObject go = Instantiate(muzzleFlashVfxPrefab, t.position, t.rotation);
        AutoDestroyVfx(go);
    }

    private void SpawnTracer(Vector3 from, Vector3 to)
    {
        if (tracerVfxPrefab == null) return;
        Vector3 dir = to - from;
        Quaternion rotation = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
        GameObject go = Instantiate(tracerVfxPrefab, from, rotation);
        if (go.TryGetComponent(out LineRenderer lr))
        {
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
        }
        AutoDestroyVfx(go);
    }

    private void SpawnImpact(Vector3 point, Vector3 normal)
    {
        if (impactVfxPrefab == null) return;
        Quaternion rot = normal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
        GameObject go = Instantiate(impactVfxPrefab, point, rot);
        AutoDestroyVfx(go);
    }

    private void DoCameraShake()
    {
        if (fireShakeDuration <= 0f || fireShakeMagnitude <= 0f) return;
        if (CameraShake.Instance == null) return;
        CameraShake.Instance.Shake(fireShakeDuration, fireShakeMagnitude);
    }

    private static void AutoDestroyVfx(GameObject go)
    {
        if (go == null) return;
        float ttl = 2.5f;
        ParticleSystem ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            ttl = Mathf.Max(0.1f, main.duration + main.startLifetime.constantMax);
        }
        Destroy(go, ttl);
    }

    /// <summary>Утилита: подобрать origin на NavMesh/с поверхности, если нужно.</summary>
    public static Vector3 SafeOrigin(Vector3 origin)
    {
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
            return hit.position + Vector3.up * 0.05f;
        return origin;
    }
}

