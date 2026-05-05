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

        [Header("Debug")]
        [SerializeField] private bool debugDraw = false;

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
            if (isMyTurn && isSelected && actionMode && autoFireEnabled)
            {
                TickAutoFire();
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
            nextFireTime = Time.time + Mathf.Max(0.05f, fireCooldownSeconds);

            if (debugDraw)
            {
                Debug.DrawLine(origin, aimPoint, Color.red, 0.2f);
            }
        }

        private DestructibleObjective FindBestVisibleObjective()
        {
            DestructibleObjective[] all = FindObjectsByType<DestructibleObjective>(FindObjectsSortMode.None);
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

