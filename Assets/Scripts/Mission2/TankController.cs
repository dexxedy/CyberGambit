using System.Collections.Generic;
using UnityEngine;

namespace Mission2
{
    /// <summary>
    /// Танк: движение корня (CharacterController), поворот корпуса по направлению езды,
    /// башня — по горизонтали (мышь X), ствол — наклон (мышь Y). Авто-огонь отключён.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public class TankController : MonoBehaviour
    {
        [Header("Parts (пусто = по имени Hull / Turret / Barrel)")]
        [SerializeField] private Transform hull;
        [SerializeField] private Transform turret;
        [SerializeField] private Transform gunPitchPivot;
        [SerializeField] private Transform muzzle;

        [Header("Hierarchy")]
        [Tooltip("Башня станет дочерней к Hull — едет и крутится вместе с корпусом (позиция не «уезжает» на край).")]
        [SerializeField] private bool reparentTurretUnderHull = true;
        [Tooltip("Если иерархию уже собрал вручную — сними галочку, скрипт не будет менять родителей (кроме reparent Turret, см. выше).")]
        [SerializeField] private bool applyAutomatedHierarchyChanges = true;
        [Tooltip("Ствол и дуло подвешиваются под башню с сохранением мировых координат.")]
        [SerializeField] private bool reparentGunUnderTurret = true;
        [Tooltip("Точка камеры (cameraAttachPoint у Unit) станет дочерней к башне — обзор крутится с башней.")]
        [SerializeField] private bool mountCameraToTurret = true;

        [Header("Mesh orientation (FBX/Blender часто дают «вверх» без этого)")]
        [Tooltip("Берём localRotation Hull/Turret/ствола из префаба и умножаем после поворота по миру — так не затирается поворот ~90° по X у меша.")]
        [SerializeField] private bool usePrefabLocalRotationAsAimOffset = true;
        [Tooltip("Если выключено: направление «вперёд» модели в локальных осях (например 0,1,0 если вперёд был +Y в Blender).")]
        [SerializeField] private Vector3 hullDriveForwardLocal = new Vector3(0f, 0f, 1f);
        [SerializeField] private Vector3 turretAimForwardLocal = new Vector3(0f, 0f, 1f);
        [Tooltip("Ось наклона ствола в локальных координатах pivot (обычно 1,0,0).")]
        [SerializeField] private Vector3 gunPitchAxisLocal = new Vector3(1f, 0f, 0f);

        [Header("Movement & hull")]
        [SerializeField] private float hullTurnSpeedDegPerSec = 120f;

        [Header("Turret & gun")]
        [SerializeField] private float turretTurnSpeedDegPerSec = 180f;
        [Tooltip("Наклон ствола = тот же pitch, что и CameraPoint (локальный X как у камеры).")]
        [SerializeField] private bool syncGunPitchToCamera = true;
        [Tooltip("Доп. градусы к pitch камеры на ствол (если меш чуть «косит»).")]
        [SerializeField] private float gunPitchExtraOffsetDeg = 0f;
        [Tooltip("Вертикаль камеры и пушки (если sync с камерой): те же градусы, что pitch на CameraPoint / Unit.xRotation. Нижняя граница.")]
        [SerializeField] private float tankVerticalAimMinDeg = -25f;
        [Tooltip("Верхняя граница вертикали (насколько можно «задрать» ствол / взгляд).")]
        [SerializeField] private float tankVerticalAimMaxDeg = 12f;
        [Tooltip("Только если syncGunPitchToCamera выключен — отдельный счётчик наклона.")]
        [SerializeField] private float minGunPitchDeg = -12f;
        [SerializeField] private float maxGunPitchDeg = 20f;

        [Header("Bot path (NavMesh corners)")]
        [SerializeField] private float pathMoveSpeed = 6.0f;
        [SerializeField] private float pathHullTurnSpeedDegPerSec = 240f;
        [SerializeField] private float arriveDistance = 0.35f;

        [Header("Player manual fire (ЛКМ)")]
        [SerializeField] private float fireRange = 25f;
        [SerializeField] private float fireCooldownSeconds = 2.0f;
        [SerializeField] private int damagePerShot = 50;
        [SerializeField] private LayerMask vfxRayMask = ~0;
        [SerializeField] private float fireShakeDuration = 0.1f;
        [SerializeField] private float fireShakeMagnitude = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool debugDraw = false;

        [Header("VFX")]
        [SerializeField] private GameObject muzzleFlashVfxPrefab;
        [SerializeField] private GameObject tracerVfxPrefab;
        [SerializeField] private GameObject impactVfxPrefab;

        private Unit unit;
        private CharacterController characterController;

        private readonly List<Vector3> pathPoints = new List<Vector3>();
        private int pathIndex = 0;
        private float nextFireTime;

        private Quaternion hullFlatOffset = Quaternion.identity;
        private Quaternion turretFlatOffset = Quaternion.identity;
        private Quaternion gunPitchBaseLocalRot = Quaternion.identity;

        private bool tankAimInitialized;
        private float baseWorldAimYawDeg;
        private float cumulativeAimYawOffsetDeg;
        private float gunPitchDegIndependent;

        public bool HasPlannedPath => pathPoints.Count >= 2 && pathIndex < pathPoints.Count;

        private void Awake()
        {
            unit = GetComponent<Unit>();
            characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            BindPartTransforms();
            if (reparentTurretUnderHull && hull != null && turret != null && turret.parent != hull)
                turret.SetParent(hull, true);

            if (applyAutomatedHierarchyChanges)
            {
                if (reparentGunUnderTurret && turret != null && gunPitchPivot != null && gunPitchPivot.parent != turret)
                    gunPitchPivot.SetParent(turret, true);
                if (reparentGunUnderTurret && gunPitchPivot != null && muzzle != null && muzzle.parent != gunPitchPivot)
                    muzzle.SetParent(gunPitchPivot, true);

                if (mountCameraToTurret && unit != null && unit.cameraAttachPoint != null && turret != null &&
                    unit.cameraAttachPoint.parent != turret)
                    unit.cameraAttachPoint.SetParent(turret, true);
            }

            CacheVisualOffsets();
        }

        private void BindPartTransforms()
        {
            if (hull == null) hull = FindChildRecursive(transform, "Hull");
            if (turret == null) turret = FindChildRecursive(transform, "Turret");
            if (gunPitchPivot == null)
            {
                gunPitchPivot = FindChildRecursive(transform, "Barrel");
                if (gunPitchPivot == null) gunPitchPivot = FindChildRecursive(transform, "Gun");
            }

            if (muzzle == null)
            {
                muzzle = FindChildRecursive(transform, "muzzle");
                if (muzzle == null) muzzle = FindChildRecursive(transform, "Muzzle");
            }
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

        private void CacheVisualOffsets()
        {
            if (hull != null)
            {
                hullFlatOffset = usePrefabLocalRotationAsAimOffset
                    ? hull.localRotation
                    : ComputeYawAlignOffsetFromLocalForward(hull, hullDriveForwardLocal);
            }

            if (turret != null)
            {
                turretFlatOffset = usePrefabLocalRotationAsAimOffset
                    ? turret.localRotation
                    : ComputeYawAlignOffsetFromLocalForward(turret, turretAimForwardLocal);
            }

            if (gunPitchPivot != null)
                gunPitchBaseLocalRot = gunPitchPivot.localRotation;
        }

        /// <summary>
        /// Сохраняем «косой» поворот меша: worldYaw * offset = текущий rotation, если forward модели в мире горизонтален.
        /// </summary>
        private static Quaternion ComputeYawAlignOffsetFromLocalForward(Transform t, Vector3 localForward)
        {
            if (t == null) return Quaternion.identity;
            Vector3 lf = localForward.sqrMagnitude > 1e-8f ? localForward.normalized : Vector3.forward;
            Vector3 f = Vector3.ProjectOnPlane(t.TransformDirection(lf), Vector3.up);
            if (f.sqrMagnitude < 1e-8f) return t.localRotation;
            f.Normalize();
            return Quaternion.Inverse(Quaternion.LookRotation(f, Vector3.up)) * t.rotation;
        }

        private void Update()
        {
            if (!enabled || unit == null) return;
            if (GameManager.Instance == null || CameraManager.Instance == null) return;
            if (GameManager.Instance.IsPaused()) return;

            bool isMyTurn = GameManager.Instance.currentPlayer == unit.owner;
            bool isSelected = CameraManager.Instance.GetCurrentControlledUnit() == unit;
            bool actionMode = CameraManager.Instance.IsActionMode();
            bool botControlled =
                GameManager.Instance.GetGameMode() == GameMode.PlayerVsBot &&
                unit.owner == Player.Player2;

            if (isMyTurn && isSelected && actionMode && !botControlled)
                TickManualFire();
        }

        /// <summary>Сброс наведения башни при снятии контроля с юнита.</summary>
        public void NotifyTankControlEnded()
        {
            tankAimInitialized = false;
        }

        /// <summary>
        /// Движение относительно камеры экшена, поворот корпуса к вектору движения, башня/ствол от мыши.
        /// Возвращает true, если танк обработал кадр (Unit не должен крутить root и обычный look-yaw).
        /// </summary>
        public bool ApplyPlayerTankFrame(
            Unit hostUnit,
            CharacterController cc,
            Vector2 moveInput,
            Vector2 lookInput,
            float moveSpeed,
            float mouseSensitivity)
        {
            if (!enabled || hostUnit == null || cc == null || !cc.enabled) return false;
            if (hull == null || turret == null || gunPitchPivot == null) return false;

            if (ChessGrid.Instance != null)
                hostUnit.RefreshGridPositionFromWorld();

            Vector3 moveWorld = GetCameraRelativeMoveVector(moveInput);
            float requestedDistance = moveSpeed * moveInput.magnitude * Time.deltaTime;
            float allowedDistance = Mathf.Min(requestedDistance, hostUnit.GetRemainingMoveMeters());
            Vector3 moveVector = moveWorld * allowedDistance;
            moveVector = hostUnit.ConstrainActionMoveToNavMesh(moveVector);
            if (moveVector.sqrMagnitude > 0f)
            {
                float actualDistance = moveVector.magnitude;
                cc.Move(moveVector);
                hostUnit.ConsumeMoveMeters(actualDistance);
                if (ChessGrid.Instance != null)
                    hostUnit.RefreshGridPositionFromWorld();
            }

            Vector3 hullDir = moveWorld.sqrMagnitude > 1e-6f
                ? moveWorld
                : GetHorizontalForward(hull, hullDriveForwardLocal);
            if (hullDir.sqrMagnitude > 1e-6f)
            {
                Quaternion desiredHull = Quaternion.LookRotation(hullDir, Vector3.up) * hullFlatOffset;
                hull.rotation = Quaternion.RotateTowards(hull.rotation, desiredHull, hullTurnSpeedDegPerSec * Time.deltaTime);
            }

            EnsureTankAimInit();
            cumulativeAimYawOffsetDeg += lookInput.x * mouseSensitivity;
            float totalYaw = baseWorldAimYawDeg + cumulativeAimYawOffsetDeg;
            Vector3 turretForward = Quaternion.Euler(0f, totalYaw, 0f) * Vector3.forward;
            Quaternion desiredTurret = Quaternion.LookRotation(turretForward, Vector3.up) * turretFlatOffset;
            turret.rotation = Quaternion.RotateTowards(turret.rotation, desiredTurret, turretTurnSpeedDegPerSec * Time.deltaTime);

            hostUnit.ApplyActionLookPitchDelta(lookInput.y * mouseSensitivity);
            hostUnit.ClampActionLookPitchAbsolute(tankVerticalAimMinDeg, tankVerticalAimMaxDeg);
            ApplyGunPitch(hostUnit, lookInput, mouseSensitivity);

            return true;
        }

        private void ApplyGunPitch(Unit hostUnit, Vector2 lookInput, float mouseSensitivity)
        {
            if (gunPitchPivot == null || hostUnit == null) return;
            Vector3 pitchAxis = gunPitchAxisLocal.sqrMagnitude > 1e-8f ? gunPitchAxisLocal.normalized : Vector3.right;

            if (syncGunPitchToCamera)
            {
                float pitch = hostUnit.GetActionLookPitchDegrees() + gunPitchExtraOffsetDeg;
                gunPitchPivot.localRotation = Quaternion.AngleAxis(pitch, pitchAxis) * gunPitchBaseLocalRot;
            }
            else
            {
                gunPitchDegIndependent -= lookInput.y * mouseSensitivity;
                gunPitchDegIndependent = Mathf.Clamp(gunPitchDegIndependent, minGunPitchDeg, maxGunPitchDeg);
                gunPitchPivot.localRotation = Quaternion.AngleAxis(gunPitchDegIndependent, pitchAxis) * gunPitchBaseLocalRot;
            }
        }

        private Vector3 GetCameraRelativeMoveVector(Vector2 moveInput)
        {
            Camera cam = CameraManager.Instance != null ? CameraManager.Instance.GetActionCamera() : null;
            if (cam == null)
            {
                Vector3 raw = transform.right * moveInput.x + transform.forward * moveInput.y;
                return raw.sqrMagnitude > 1e-6f ? raw.normalized : Vector3.zero;
            }

            Vector3 f = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            Vector3 r = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
            if (f.sqrMagnitude < 1e-6f || r.sqrMagnitude < 1e-6f) return Vector3.zero;
            f.Normalize();
            r.Normalize();
            Vector3 rawMove = r * moveInput.x + f * moveInput.y;
            return rawMove.sqrMagnitude > 1e-6f ? rawMove.normalized : Vector3.zero;
        }

        private void EnsureTankAimInit()
        {
            if (tankAimInitialized) return;
            Camera cam = CameraManager.Instance != null ? CameraManager.Instance.GetActionCamera() : null;
            Vector3 f = cam != null
                ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up)
                : GetHorizontalForward(turret, turretAimForwardLocal);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            f.Normalize();
            baseWorldAimYawDeg = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            cumulativeAimYawOffsetDeg = 0f;
            tankAimInitialized = true;
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

            float y = transform.position.y;
            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 p = corners[i];
                p.y = y;
                pathPoints.Add(p);
            }
        }

        /// <summary>Движение по углам пути (бот и т.д.): крутит корпус, не root.</summary>
        public void TickMoveAlongPath()
        {
            if (unit == null) return;
            if (unit.GetRemainingMoveMeters() <= 0.01f) return;
            if (!HasPlannedPath) return;
            if (hull == null)
            {
                TickMoveAlongPathLegacyRoot();
                return;
            }

            Vector3 target = pathPoints[pathIndex];
            Vector3 delta = target - transform.position;
            delta.y = 0f;

            if (delta.magnitude <= arriveDistance)
            {
                pathIndex++;
                return;
            }

            Vector3 dir = delta.normalized;
            float step = Mathf.Min(pathMoveSpeed * Time.deltaTime, unit.GetRemainingMoveMeters());
            Vector3 move = dir * step;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredHull = Quaternion.LookRotation(dir, Vector3.up) * hullFlatOffset;
                hull.rotation = Quaternion.RotateTowards(hull.rotation, desiredHull, pathHullTurnSpeedDegPerSec * Time.deltaTime);
            }

            if (characterController != null && characterController.enabled)
                characterController.Move(move);
            else
                transform.position += move;

            unit.ConsumeMoveMeters(step);
        }

        private void TickMoveAlongPathLegacyRoot()
        {
            Vector3 target = pathPoints[pathIndex];
            Vector3 delta = target - transform.position;
            delta.y = 0f;

            if (delta.magnitude <= arriveDistance)
            {
                pathIndex++;
                return;
            }

            Vector3 dir = delta.normalized;
            float step = Mathf.Min(pathMoveSpeed * Time.deltaTime, unit.GetRemainingMoveMeters());
            Vector3 move = dir * step;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, pathHullTurnSpeedDegPerSec * Time.deltaTime);
            }

            if (characterController != null && characterController.enabled)
                characterController.Move(move);
            else
                transform.position += move;

            unit.ConsumeMoveMeters(step);
        }

        private void TickManualFire()
        {
            if (Time.time < nextFireTime) return;
            if (UnityEngine.InputSystem.Mouse.current == null) return;
            if (!UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) return;

            Vector3 origin = muzzle != null ? muzzle.position : (transform.position + Vector3.up * 1.5f);
            Vector3 dir = muzzle != null && muzzle.forward.sqrMagnitude > 1e-6f
                ? muzzle.forward.normalized
                : (CameraManager.Instance != null && CameraManager.Instance.GetActionCamera() != null
                    ? CameraManager.Instance.GetActionCamera().transform.forward
                    : transform.forward);

            float maxRange = Mathf.Max(0.1f, fireRange);
            LayerMask rayMask = vfxRayMask.value != 0 ? vfxRayMask : ~0;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxRange, rayMask, QueryTriggerInteraction.Ignore))
            {
                DestructibleObjective obj = hit.collider != null ? hit.collider.GetComponentInParent<DestructibleObjective>() : null;
                if (obj != null && !obj.IsDestroyed)
                {
                    var shield = hit.collider.GetComponentInParent<Mission2ObjectiveShield>();
                    if (shield != null && shield.IsProtectionActive)
                    {
                        SpawnMuzzleFlash(origin, Quaternion.LookRotation(dir));
                        SpawnTracer(origin, hit.point);
                        SpawnImpact(hit.point, hit.normal);
                        DoCameraShake();
                        if (debugDraw) Debug.DrawLine(origin, hit.point, Color.cyan, 0.2f);
                    }
                    else
                    {
                        obj.ApplyDamage(damagePerShot);
                        SpawnMuzzleFlash(origin, Quaternion.LookRotation(dir));
                        SpawnTracer(origin, hit.point);
                        SpawnImpact(hit.point, hit.normal);
                        DoCameraShake();
                        if (debugDraw) Debug.DrawLine(origin, hit.point, Color.yellow, 0.2f);
                    }
                }
                else
                {
                    SpawnMuzzleFlash(origin, Quaternion.LookRotation(dir));
                    SpawnTracer(origin, hit.point);
                    SpawnImpact(hit.point, hit.normal);
                    DoCameraShake();
                    if (debugDraw) Debug.DrawLine(origin, hit.point, Color.gray, 0.2f);
                }
            }
            else
            {
                if (debugDraw) Debug.DrawLine(origin, origin + dir * maxRange, Color.gray, 0.2f);
                else
                {
                    SpawnMuzzleFlash(origin, Quaternion.LookRotation(dir));
                    SpawnTracer(origin, origin + dir * maxRange);
                    DoCameraShake();
                }
            }

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
            Vector3 d = to - from;
            Quaternion rotation = d.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(d) : Quaternion.identity;
            GameObject go = Instantiate(tracerVfxPrefab);
            Transform tr = go.transform;
            tr.SetPositionAndRotation(from, rotation);
            float tracerTtl = 2.5f;
            if (go.TryGetComponent(out LineRenderer lr))
            {
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
                tracerTtl = 0.12f;
            }
            AutoDestroyVfx(go, tracerTtl);
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

        private static void AutoDestroyVfx(GameObject go, float ttl = 2.5f)
        {
            if (go == null) return;
            Destroy(go, Mathf.Max(0.05f, ttl));
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDraw) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, fireRange);
        }

        private Vector3 GetHorizontalForward(Transform part, Vector3 localForwardAxis)
        {
            if (part == null) return Vector3.forward;
            Vector3 lf = localForwardAxis.sqrMagnitude > 1e-8f ? localForwardAxis.normalized : Vector3.forward;
            Vector3 f = Vector3.ProjectOnPlane(part.TransformDirection(lf), Vector3.up);
            return f.sqrMagnitude > 1e-8f ? f.normalized : Vector3.ProjectOnPlane(part.forward, Vector3.up).normalized;
        }
    }
}
