using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Оружие как prefab-компонент: хранит состояние (патроны/перезарядка/КД) и читает числа из RangedWeaponConfig.
/// </summary>
public class Weapon : MonoBehaviour
{
    [SerializeField] private RangedWeaponConfig config;
    [Tooltip("Дуло: вспышка и старт трассера. Луч урона задаётся в TryFire (у игрока — из камеры).")]
    [SerializeField] private Transform muzzle;

    [Header("VFX")]
    [SerializeField] private GameObject muzzleFlashVfxPrefab;
    [SerializeField] private GameObject tracerVfxPrefab;
    [SerializeField] private GameObject impactVfxPrefab;
    [Tooltip("Толщина LineRenderer трассера (если префаб трассера с LineRenderer).")]
    [SerializeField] private float tracerLineWidth = 0.06f;
    [Tooltip("Какие слои считаем 'поверхностями' для попадания/трассера. Если не трогать — будет Everything.")]
    [SerializeField] private LayerMask vfxRayMask = ~0;

    [Header("Audio (Bishop / ranged)")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip reloadSound;
    [Range(0f, 1f)] [SerializeField] private float fireSoundVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float reloadSoundVolume = 1f;
    [Tooltip("Пауза после выстрела, затем звук перезарядки (до автоперезарядки при пустом магазине).")]
    [SerializeField] private float postFireReloadSoundDelay = 0.4f;
    [Header("Camera shake (optional)")]
    [SerializeField] private float fireShakeDuration = 0.08f;
    [SerializeField] private float fireShakeMagnitude = 0.03f;

    [Header("Runtime State (read-only)")]
    [SerializeField] private int ammoInMag;
    [SerializeField] private int ammoReserve;
    [SerializeField] private bool isReloading;

    private float nextFireTime;
    private float reloadEndTime;
    private Unit ownerUnit;
    /// <summary>Если в префабе не проставили muzzle — ищем по имени под иерархией оружия.</summary>
    private Transform runtimeMuzzleCache;

    public float FireCooldownRemaining => Mathf.Max(0f, nextFireTime - Time.time);
    public bool IsOnFireCooldown => Time.time < nextFireTime;

    public RangedWeaponConfig Config => config;
    public Transform Muzzle => muzzle != null ? muzzle : ResolveRuntimeMuzzle();
    public int AmmoInMag => ammoInMag;
    public int AmmoReserve => ammoReserve;
    public bool IsReloading => isReloading;
    public int MagazineSize => config != null ? config.magazineSize : 0;

    public void SetOwnerUnit(Unit unit) => ownerUnit = unit;

    public void InitializeFromConfigIfNeeded()
    {
        if (config == null) return;
        if (ammoInMag == 0 && ammoReserve == 0)
        {
            ammoInMag = Mathf.Clamp(config.startingAmmoInMag, 0, config.magazineSize);
            ammoReserve = Mathf.Max(0, config.startingReserveAmmo);
        }
    }

    public bool TryStartReload(bool playSoundAtStart = true)
    {
        if (config == null) return false;
        if (isReloading) return false;
        if (ammoInMag >= config.magazineSize) return false;
        if (ammoReserve <= 0) return false;
        isReloading = true;
        reloadEndTime = Time.time + Mathf.Max(0f, config.reloadTimeSeconds);
        if (playSoundAtStart)
            PlayReloadSound();
        return true;
    }

    /// <summary>Автоперезарядка при пустом магазине (выстрел, бот, и т.д.).</summary>
    public void TryAutoReloadIfEmpty()
    {
        if (config == null || isReloading) return;
        if (ammoInMag > 0) return;
        TryStartReload();
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
    /// Hitscan-выстрел. origin/direction — луч попадания/урона (у игрока с action-камеры).
    /// Трассер рисуется от <see cref="muzzle"/> (если задан) до точки попадания этого луча.
    /// </summary>
    public bool TryFire(Unit owner, Vector3 origin, Vector3 direction)
    {
        if (owner == null) return false;
        ownerUnit = owner;
        InitializeFromConfigIfNeeded();
        TickReload();

        if (!CanFire())
        {
            TryAutoReloadIfEmpty();
            return false;
        }

        float interval = config.FireIntervalSeconds;
        nextFireTime = Time.time + Mathf.Max(0f, interval);

        float maxRange = Mathf.Max(0.1f, config.maxHitRangeMeters);
        bool friendly = config.friendlyFire;
        int baseDamage = Mathf.Max(0, config.damagePerHit);

        SpawnMuzzleFlash();
        PlayFireSound();
        DoCameraShake();

        Transform muzzleXf = Muzzle;

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

        // Визуал: луч от дула; урон — по origin/direction (у игрока это камера).
        Vector3 tracerStart = muzzleXf != null ? muzzleXf.position : origin;
        SpawnTracer(tracerStart, hitPoint);
        if (hasHit)
            SpawnImpact(hitPoint, hitNormal);

        ammoInMag = Mathf.Max(0, ammoInMag - 1);

        // Звук перезарядки после выстрела — только Guardian (и танк в TankController). Bishop: R или пустой магазин.
        if (owner != null && owner.chessType == ChessUnitType.Guardian && ownerUnit != null)
            ownerUnit.SchedulePostFireWeaponAudio(this, postFireReloadSoundDelay);
        else
            TryAutoReloadIfEmpty();

        return true;
    }

    public float GetPostFireReloadSoundDelay() => Mathf.Max(0.05f, postFireReloadSoundDelay);

    private void SpawnMuzzleFlash()
    {
        if (muzzleFlashVfxPrefab == null) return;
        Transform t = Muzzle != null ? Muzzle : transform;
        GameObject go = Instantiate(muzzleFlashVfxPrefab, t.position, t.rotation);
        AutoDestroyVfx(go);
    }

    private Transform ResolveRuntimeMuzzle()
    {
        if (runtimeMuzzleCache != null) return runtimeMuzzleCache;
        runtimeMuzzleCache = FindChildTransformByName(transform, "muzzle")
            ?? FindChildTransformByName(transform, "Muzzle");
        return runtimeMuzzleCache;
    }

    private static Transform FindChildTransformByName(Transform root, string exactName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == exactName) return t;
        }
        return null;
    }

    private void SpawnTracer(Vector3 from, Vector3 to)
    {
        if (tracerVfxPrefab == null) return;
        Vector3 dir = to - from;
        Quaternion rotation = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;

        // Без позиции в Instantiate: у ParticleSystem с playOnAwake первый кадр иначе может симулироваться в (0,0,0).
        GameObject go = Instantiate(tracerVfxPrefab);
        Transform tr = go.transform;
        tr.SetPositionAndRotation(from, rotation);

        if (go.TryGetComponent(out LineRenderer lr))
        {
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            float w = Mathf.Max(0.001f, tracerLineWidth);
            lr.startWidth = w;
            lr.endWidth = w;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            AutoDestroyVfx(go, lr);
            return;
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

    private void PlayFireSound()
    {
        PlayCombatClip(ResolveFireSound(), fireSoundVolume);
    }

    private void PlayReloadSound()
    {
        PlayCombatClip(ResolveReloadSound(), reloadSoundVolume);
    }

    private AudioClip ResolveFireSound()
    {
        if (fireSound != null) return fireSound;

        CombatSfxLibrary lib = CombatSfxLibrary.Instance;
        if (ownerUnit != null && lib != null)
        {
            if (ownerUnit.chessType == ChessUnitType.Guardian && lib.GetGuardianFire() != null)
                return lib.GetGuardianFire();
        }

        return ownerUnit != null ? ownerUnit.GetBishopFireSoundFallback() : null;
    }

    private AudioClip ResolveReloadSound()
    {
        if (reloadSound != null) return reloadSound;

        CombatSfxLibrary lib = CombatSfxLibrary.Instance;
        if (ownerUnit != null && lib != null)
        {
            if (ownerUnit.chessType == ChessUnitType.Guardian && lib.GetGuardianReload() != null)
                return lib.GetGuardianReload();
        }

        return ownerUnit != null ? ownerUnit.GetBishopReloadSoundFallback() : null;
    }

    /// <summary>Звук перезарядки после выстрела (вызывается из Unit).</summary>
    public void PlayPostFireReloadSound()
    {
        PlayReloadSound();
    }

    /// <summary>Тихая перезарядка после того, как SFX уже проигран.</summary>
    public void TryStartReloadAfterPostFireAudio()
    {
        if (ammoInMag > 0) return;
        TryStartReload(playSoundAtStart: false);
    }

    private void PlayCombatClip(AudioClip clip, float volumeScale)
    {
        if (clip == null || volumeScale <= 0f) return;
        Unit host = ownerUnit;
        if (host == null) return;
        AudioSource src = host.GetCombatAudioSource();
        if (src == null) return;
        float vol = (AudioManager.Instance != null ? AudioManager.Instance.SFXVolume : 1f) * volumeScale;
        src.PlayOneShot(clip, vol);
    }

    private static void AutoDestroyVfx(GameObject go, LineRenderer line = null)
    {
        if (go == null) return;
        float ttl = 0.12f;
        if (line != null)
            ttl = 0.12f;
        else
        {
            ParticleSystem ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                ttl = Mathf.Max(0.1f, main.duration + main.startLifetime.constantMax);
            }
            else
                ttl = 2.5f;
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

