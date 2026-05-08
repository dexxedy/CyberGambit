using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mission2
{
    [RequireComponent(typeof(Unit))]
    public class TankController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6.0f;
        [SerializeField] private float turnSpeedDegPerSec = 240f;
        [SerializeField] private float arriveDistance = 0.35f;

        [Header("Auto Fire")]
        [SerializeField] private bool autoFireEnabled = true;
        [SerializeField] private float fireRange = 25f;
        [SerializeField] [Range(10f, 180f)] private float fireFovDegrees = 120f;
        [SerializeField] private float fireCooldownSeconds = 2.0f;
        [SerializeField] private int damagePerShot = 50;
        [SerializeField] private Transform muzzle;
        [SerializeField] private LayerMask losMask = ~0;

        [Header("Player Manual Fire")]
        [Tooltip("Если включено — игрок может стрелять из танка вручную (ЛКМ).")]
        [SerializeField] private bool allowPlayerManualFire = true;
        [Tooltip("Если включено — автo-огонь работает только у бота, а игрок стреляет сам.")]
        [SerializeField] private bool autoFireOnlyForBot = true;

        [Header("Debug")]
        [SerializeField] private bool debugDraw = false;

        [Header("VFX")]
        [SerializeField] private GameObject muzzleFlashVfxPrefab;
        [SerializeField] private GameObject tracerVfxPrefab;
        [SerializeField] private GameObject impactVfxPrefab;
        [Tooltip("Какие слои считаем 'поверхностями' для попадания/трассера. Если не трогать — будет Everything.")]
        [SerializeField] private LayerMask vfxRayMask = ~0;
        [Header("Camera shake (optional)")]
        [SerializeField] private float fireShakeDuration = 0.1f;
        [SerializeField] private float fireShakeMagnitude = 0.05f;

        private Unit unit;
        private CharacterController characterController;

        private readonly List<Vector3> pathPoints = new List<Vector3>();
        private int pathIndex = 0;
        private float nextFireTime = 0f;

        public bool HasPlannedPath => pathPoints.Count >= 2 && pathIndex < pathPoints.Count;

        private void Awake()
        {
            unit = GetComponent<Unit>();
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (GameManager.Instance == null || CameraManager.Instance == null) return;
            if (GameManager.Instance.IsPaused()) return;

            bool isMyTurn = GameManager.Instance.currentPlayer == unit.owner;
            bool isSelected = CameraManager.Instance.GetCurrentControlledUnit() == unit;
            bool actionMode = CameraManager.Instance.IsActionMode();

            // Tank is controlled like a normal unit in action mode.
            // Movement and move budget are handled by Unit; TankController only auto-fires at objectives.
            if (isMyTurn && isSelected && actionMode)
            {
                bool botControlled =
                    (GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot) &&
                    (unit.owner == Player.Player2);

                if (allowPlayerManualFire && !botControlled)
                {
                    TickManualFire();
                }

                if (autoFireEnabled && (!autoFireOnlyForBot || botControlled))
                {
                    TickAutoFire();
                }
            }
        }

        public void ClearPath()
        {
            pathPoints.Clear();
            pathIndex = 0;
        }

        public void SetPlannedPath(IReadOnlyList<Vector3> corners)
        {
            pathPoints.Clear();
            pathIndex = 0;
            if (corners == null || corners.Count < 2) return;

            // flatten Y to current unit height to avoid CC bumps
            float y = transform.position.y;
            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 p = corners[i];
                p.y = y;
                pathPoints.Add(p);
            }
        }

        private void TickMoveAlongPath()
        {
            if (unit == null) return;
            if (unit.GetRemainingMoveMeters() <= 0.01f) return;
            if (!HasPlannedPath) return;

            Vector3 target = pathPoints[pathIndex];
            Vector3 delta = target - transform.position;
            delta.y = 0f;

            if (delta.magnitude <= arriveDistance)
            {
                pathIndex++;
                return;
            }

            Vector3 dir = delta.normalized;
            float step = Mathf.Min(moveSpeed * Time.deltaTime, unit.GetRemainingMoveMeters());
            Vector3 move = dir * step;

            // rotate towards direction
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeedDegPerSec * Time.deltaTime);
            }

            if (characterController != null && characterController.enabled)
                characterController.Move(move);
            else
                transform.position += move;

            unit.ConsumeMoveMeters(step);
        }

        private void TickAutoFire()
        {
            if (Time.time < nextFireTime) return;

            DestructibleObjective target = FindBestVisibleObjective();
            if (target == null) return;

            Vector3 origin = muzzle != null ? muzzle.position : (transform.position + Vector3.up * 1.5f);
            Vector3 aimPoint = target.transform.position;
            Vector3 dir = (aimPoint - origin);
            float dist = dir.magnitude;
            if (dist < 0.001f) return;
            dir /= dist;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, losMask, QueryTriggerInteraction.Ignore))
            {
                DestructibleObjective hitObj = hit.collider != null ? hit.collider.GetComponentInParent<DestructibleObjective>() : null;
                if (hitObj != target) return;
            }

            // Fire!
            target.ApplyDamage(damagePerShot);
            SpawnMuzzleFlash(origin, Quaternion.LookRotation(dir));
            SpawnTracer(origin, aimPoint);
            SpawnImpact(aimPoint, -dir);
            DoCameraShake();
            nextFireTime = Time.time + Mathf.Max(0.05f, fireCooldownSeconds);

            if (debugDraw)
            {
                Debug.DrawLine(origin, aimPoint, Color.red, 0.2f);
            }
        }

        private void TickManualFire()
        {
            if (Time.time < nextFireTime) return;
            if (UnityEngine.InputSystem.Mouse.current == null) return;
            if (!UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) return;

            Camera actionCam = CameraManager.Instance != null ? CameraManager.Instance.GetActionCamera() : null;
            Vector3 origin = muzzle != null
                ? muzzle.position
                : (actionCam != null ? actionCam.transform.position : (transform.position + Vector3.up * 1.5f));
            Vector3 dir = actionCam != null ? actionCam.transform.forward : transform.forward;

            float maxRange = Mathf.Max(0.1f, fireRange);

            // Raycast "по миру" для видимого попадания в любую поверхность.
            LayerMask rayMask = vfxRayMask.value != 0 ? vfxRayMask : ~0;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxRange, rayMask, QueryTriggerInteraction.Ignore))
            {
                DestructibleObjective obj = hit.collider != null ? hit.collider.GetComponentInParent<DestructibleObjective>() : null;
                if (obj != null && !obj.IsDestroyed)
                {
                    obj.ApplyDamage(damagePerShot);
                    SpawnMuzzleFlash(origin, Quaternion.LookRotation(dir));
                    SpawnTracer(origin, hit.point);
                    SpawnImpact(hit.point, hit.normal);
                    DoCameraShake();

                    if (debugDraw)
                    {
                        Debug.DrawLine(origin, hit.point, Color.yellow, 0.2f);
                    }
                }
                else if (debugDraw)
                {
                    Debug.DrawLine(origin, hit.point, Color.gray, 0.2f);
                }

                // Даже если это НЕ objective — всё равно показываем попадание по поверхности.
                if (obj == null || obj.IsDestroyed)
                {
                    SpawnMuzzleFlash(origin, Quaternion.LookRotation(dir));
                    SpawnTracer(origin, hit.point);
                    SpawnImpact(hit.point, hit.normal);
                    DoCameraShake();
                }
            }
            else if (debugDraw)
            {
                Debug.DrawLine(origin, origin + dir * maxRange, Color.gray, 0.2f);
            }
            else
            {
                // Miss tracer for feedback
                SpawnMuzzleFlash(origin, Quaternion.LookRotation(dir));
                SpawnTracer(origin, origin + dir * maxRange);
                DoCameraShake();
            }

            // Важно: выстрел НЕ завершает ход. Ход завершится по таймеру/бюджету перемещения, как обычно.
            nextFireTime = Time.time + Mathf.Max(0.05f, fireCooldownSeconds);
        }

        private void SpawnMuzzleFlash(Vector3 pos, Quaternion rot)
        {
            if (muzzleFlashVfxPrefab == null) return;
            GameObject go = Instantiate(muzzleFlashVfxPrefab, pos, rot);
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

        private DestructibleObjective FindBestVisibleObjective()
        {
            DestructibleObjective[] all = FindObjectsByType<DestructibleObjective>(FindObjectsInactive.Exclude);
            if (all == null || all.Length == 0) return null;

            Vector3 origin = muzzle != null ? muzzle.position : (transform.position + Vector3.up * 1.5f);
            Vector3 forward = transform.forward;
            float bestScore = float.NegativeInfinity;
            DestructibleObjective best = null;

            foreach (var obj in all)
            {
                if (obj == null || obj.IsDestroyed) continue;
                Vector3 to = obj.transform.position - origin;
                to.y = 0f;
                float d = to.magnitude;
                if (d > fireRange) continue;
                if (d < 0.01f) continue;
                Vector3 dir = to / d;
                float ang = Vector3.Angle(forward, dir);
                if (ang > fireFovDegrees * 0.5f) continue;

                // Quick LOS check
                if (Physics.Raycast(origin, dir, out RaycastHit hit, d, losMask, QueryTriggerInteraction.Ignore))
                {
                    var hitObj = hit.collider != null ? hit.collider.GetComponentInParent<DestructibleObjective>() : null;
                    if (hitObj != obj) continue;
                }

                // Score: closer = better, more centered = better
                float centered = 1f - (ang / (fireFovDegrees * 0.5f));
                float score = centered * 2f + (1f / Mathf.Max(1f, d));
                if (score > bestScore)
                {
                    bestScore = score;
                    best = obj;
                }
            }

            return best;
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDraw) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, fireRange);
        }
    }
}

