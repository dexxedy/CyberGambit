using UnityEngine;

[DisallowMultipleComponent]
public class TurretController : MonoBehaviour, IDamageableTarget
{
    [Header("Identity")]
    [SerializeField] private Player owner = Player.Player2;
    [SerializeField] private string displayName = "Турель";

    [Header("Parts")]
    [SerializeField] private Transform basePart;
    [SerializeField] private Transform gunPart;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform aimPoint;
    [Tooltip("Локальная ось модели, которая смотрит вдоль дула. Для Blender-модели обычно +Y.")]
    [SerializeField] private Vector3 gunForwardLocal = new Vector3(0f, 1f, 0f);

    [Header("Combat")]
    [SerializeField] private int maxHealth = 60;
    [SerializeField] private int currentHealth = 60;
    [SerializeField] private float detectionRadius = 12f;
    [SerializeField] private float fireRange = 15f;
    [SerializeField] private float gunTurnSpeedDegPerSec = 160f;
    [SerializeField] private float fireCooldownSeconds = 1.5f;
    [SerializeField] private int damagePerShot = 18;
    [SerializeField] private float fireAngleToleranceDeg = 8f;
    [SerializeField] private float targetAimHeight = 1.2f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    [Header("Death")]
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private bool destroyGameObjectOnDeath = false;
    [SerializeField] private float destroyDelaySeconds = 1.5f;

    [Header("VFX")]
    [SerializeField] private GameObject muzzleFlashVfxPrefab;
    [SerializeField] private GameObject tracerVfxPrefab;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private float tracerLineWidth = 0.06f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip deathSound;
    [Range(0f, 1f)] [SerializeField] private float fireSoundVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float deathSoundVolume = 1f;

    private float nextFireTime;
    private Quaternion gunAimOffset = Quaternion.identity;
    private bool dead;

    public int CurrentHealth => Mathf.Max(0, currentHealth);
    public int MaxHealth => Mathf.Max(1, maxHealth);
    public bool IsAlive => !dead && currentHealth > 0;
    public Player Owner => owner;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Турель" : displayName;
    public Transform AimPoint => aimPoint != null ? aimPoint : (muzzle != null ? muzzle : (gunPart != null ? gunPart : transform));

    private void Awake()
    {
        BindMissingReferences();
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, Mathf.Max(1, maxHealth));
        dead = currentHealth <= 0;
        CacheGunAimOffset();
    }

    private void Update()
    {
        if (!IsAlive) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused()) return;

        Unit target = FindClosestTarget();
        if (target == null) return;

        Vector3 targetPoint = GetTargetAimPoint(target);
        RotateGunToward(targetPoint);

        if (Time.time >= nextFireTime && CanShootTarget(target, targetPoint))
            FireAt(target, targetPoint);
    }

    public void ApplyDamage(int damage, Unit attacker)
    {
        if (!IsAlive || damage <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        if (currentHealth <= 0)
            Die();
    }

    private void BindMissingReferences()
    {
        if (basePart == null)
            basePart = FindChildRecursive(transform, "CG_Turret_MK1_Base") ?? FindChildRecursive(transform, "Base");

        if (gunPart == null)
            gunPart = FindChildRecursive(transform, "CG_Turret_MK1_Gun") ?? FindChildRecursive(transform, "Gun");

        if (muzzle == null)
            muzzle = FindChildRecursive(transform, "muzzle") ?? FindChildRecursive(transform, "Muzzle");

        if (aimPoint == null)
            aimPoint = muzzle != null ? muzzle : gunPart;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }
    }

    private void CacheGunAimOffset()
    {
        if (gunPart == null)
        {
            gunAimOffset = Quaternion.identity;
            return;
        }

        gunAimOffset = ComputeYawAlignOffsetFromLocalForward(gunPart, gunForwardLocal);
    }

    private Unit FindClosestTarget()
    {
        Unit closest = null;
        float bestSqrDistance = detectionRadius * detectionRadius;

        Unit[] units = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        foreach (Unit unit in units)
        {
            if (unit == null || unit.owner == owner || unit.GetHealth() <= 0)
                continue;

            float sqrDistance = (unit.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance > bestSqrDistance)
                continue;

            Vector3 targetPoint = GetTargetAimPoint(unit);
            if (!HasLineOfSight(unit, targetPoint))
                continue;

            bestSqrDistance = sqrDistance;
            closest = unit;
        }

        return closest;
    }

    private void RotateGunToward(Vector3 targetPoint)
    {
        if (gunPart == null) return;

        Vector3 flatDir = Vector3.ProjectOnPlane(targetPoint - gunPart.position, Vector3.up);
        if (flatDir.sqrMagnitude < 0.0001f) return;

        Quaternion desired = Quaternion.LookRotation(flatDir.normalized, Vector3.up) * gunAimOffset;
        gunPart.rotation = Quaternion.RotateTowards(
            gunPart.rotation,
            desired,
            Mathf.Max(0f, gunTurnSpeedDegPerSec) * Time.deltaTime);
    }

    private bool CanShootTarget(Unit target, Vector3 targetPoint)
    {
        if (target == null || target.GetHealth() <= 0)
            return false;

        Vector3 origin = GetMuzzlePosition();
        if ((targetPoint - origin).sqrMagnitude > fireRange * fireRange)
            return false;

        Vector3 flatTargetDir = Vector3.ProjectOnPlane(targetPoint - origin, Vector3.up);
        Vector3 flatGunDir = GetGunForwardFlat();
        if (flatTargetDir.sqrMagnitude > 0.0001f && flatGunDir.sqrMagnitude > 0.0001f)
        {
            float angle = Vector3.Angle(flatGunDir.normalized, flatTargetDir.normalized);
            if (angle > Mathf.Max(0f, fireAngleToleranceDeg))
                return false;
        }

        return HasLineOfSight(target, targetPoint);
    }

    private void FireAt(Unit target, Vector3 targetPoint)
    {
        nextFireTime = Time.time + Mathf.Max(0.05f, fireCooldownSeconds);

        Vector3 origin = GetMuzzlePosition();
        Vector3 direction = targetPoint - origin;
        if (direction.sqrMagnitude < 0.0001f)
            direction = gunPart != null ? GetGunForwardFlat() : transform.forward;
        direction.Normalize();

        Vector3 hitPoint = origin + direction * fireRange;
        Vector3 hitNormal = -direction;
        bool hasHit = false;

        SpawnMuzzleFlash();
        PlayClip(fireSound, fireSoundVolume);

        if (TryRaycastIgnoringSelf(origin, direction, fireRange, out RaycastHit hit))
        {
            hasHit = true;
            hitPoint = hit.point;
            hitNormal = hit.normal;

            Unit hitUnit = hit.collider != null ? hit.collider.GetComponentInParent<Unit>() : null;
            if (hitUnit != null && hitUnit.owner != owner && hitUnit.GetHealth() > 0)
            {
                hitUnit.TakeDamage(Mathf.Max(0, damagePerShot));
            }
            else
            {
                IDamageableTarget damageable = FindDamageableInParents(hit.collider);
                if (damageable != null && !ReferenceEquals(damageable, this) && damageable.Owner != owner && damageable.IsAlive)
                    damageable.ApplyDamage(Mathf.Max(0, damagePerShot), null);
            }
        }

        SpawnTracer(origin, hitPoint);
        if (hasHit)
            SpawnImpact(hitPoint, hitNormal);
    }

    private bool HasLineOfSight(Unit target, Vector3 targetPoint)
    {
        if (target == null) return false;

        Vector3 origin = GetMuzzlePosition();
        Vector3 dir = targetPoint - origin;
        float distance = dir.magnitude;
        if (distance <= 0.0001f)
            return true;

        dir /= distance;
        if (!TryRaycastIgnoringSelf(origin, dir, distance, out RaycastHit hit))
            return true;

        Unit hitUnit = hit.collider != null ? hit.collider.GetComponentInParent<Unit>() : null;
        return hitUnit == target;
    }

    private Vector3 GetTargetAimPoint(Unit target)
    {
        if (target == null) return transform.position;
        return target.transform.position + Vector3.up * Mathf.Max(0f, targetAimHeight);
    }

    private Vector3 GetMuzzlePosition()
    {
        if (muzzle != null) return muzzle.position;
        if (gunPart != null) return gunPart.position;
        return transform.position + Vector3.up * 0.8f;
    }

    private Vector3 GetGunForwardFlat()
    {
        Transform source = gunPart != null ? gunPart : transform;
        Vector3 localForward = gunForwardLocal.sqrMagnitude > 0.0001f ? gunForwardLocal.normalized : Vector3.forward;
        Vector3 forward = Vector3.ProjectOnPlane(source.TransformDirection(localForward), Vector3.up);
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : source.forward;
    }

    private void Die()
    {
        if (dead) return;
        dead = true;
        currentHealth = 0;

        PlayClip(deathSound, deathSoundVolume);

        if (disableCollidersOnDeath)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider c in colliders)
                c.enabled = false;
        }

        if (destroyGameObjectOnDeath)
            Destroy(gameObject, Mathf.Max(0f, destroyDelaySeconds));
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
        GameObject go = Instantiate(tracerVfxPrefab);
        go.transform.SetPositionAndRotation(from, rotation);

        if (go.TryGetComponent(out LineRenderer lr))
        {
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            float width = Mathf.Max(0.001f, tracerLineWidth);
            lr.startWidth = width;
            lr.endWidth = width;
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

    private void PlayClip(AudioClip clip, float volumeScale)
    {
        if (clip == null || audioSource == null || volumeScale <= 0f) return;
        float volume = (AudioManager.Instance != null ? AudioManager.Instance.SFXVolume : 1f) * volumeScale;
        audioSource.PlayOneShot(clip, volume);
    }

    private static void AutoDestroyVfx(GameObject go, LineRenderer line = null)
    {
        if (go == null) return;
        float ttl = line != null ? 0.12f : 2.5f;
        if (line == null)
        {
            ParticleSystem ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                ttl = Mathf.Max(0.1f, main.duration + main.startLifetime.constantMax);
            }
        }

        Destroy(go, ttl);
    }

    private static IDamageableTarget FindDamageableInParents(Collider collider)
    {
        if (collider == null) return null;
        MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IDamageableTarget damageable)
                return damageable;
        }

        return null;
    }

    private bool TryRaycastIgnoringSelf(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearestHit)
    {
        nearestHit = default;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, lineOfSightMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;

            nearestHit = hit;
            return true;
        }

        return false;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName)) return null;
        foreach (Transform c in parent)
        {
            if (c.name == childName) return c;
            Transform deep = FindChildRecursive(c, childName);
            if (deep != null) return deep;
        }

        return null;
    }

    private static Quaternion ComputeYawAlignOffsetFromLocalForward(Transform t, Vector3 localForward)
    {
        if (t == null) return Quaternion.identity;
        Vector3 lf = localForward.sqrMagnitude > 0.0001f ? localForward.normalized : Vector3.forward;
        Vector3 f = Vector3.ProjectOnPlane(t.TransformDirection(lf), Vector3.up);
        if (f.sqrMagnitude < 0.0001f) return t.localRotation;
        return Quaternion.Inverse(Quaternion.LookRotation(f.normalized, Vector3.up)) * t.rotation;
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
        detectionRadius = Mathf.Max(0.1f, detectionRadius);
        fireRange = Mathf.Max(0.1f, fireRange);
        fireCooldownSeconds = Mathf.Max(0.05f, fireCooldownSeconds);
        damagePerShot = Mathf.Max(0, damagePerShot);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.65f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, fireRange);
    }
}
