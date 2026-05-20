using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mission2
{
    /// <summary>
    /// В тактике показывает подписи над <see cref="DestructibleObjective"/> и <see cref="Mission2DefenseTerminal"/> (как флаги M1).
    /// Повесь на тот же Canvas, что и <see cref="TacticalFlagMarkersController"/>, с отдельным префабом маркера.
    /// </summary>
    public class TacticalMission2MarkersController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private RectTransform markersRoot;
        [SerializeField] private TacticalMission2Marker markerPrefab;
        [SerializeField] private float worldOffsetY = 2f;

        [Header("Colors")]
        [SerializeField] private Color objectiveColor = new Color(1f, 0.55f, 0.1f, 1f);
        [SerializeField] private Color objectiveShieldedColor = new Color(0.4f, 0.85f, 1f, 1f);
        [SerializeField] private Color terminalColor = new Color(0.5f, 1f, 0.45f, 1f);
        [SerializeField] private Color terminalDoneColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        [Header("Options")]
        [SerializeField] private bool hideMarkersWhenBehindCamera = true;
        [Tooltip("Если на сцене нет целей M2 и терминала — не трогать markersRoot.")]
        [SerializeField] private bool hideRootWhenNothingToShow = true;

        private readonly Dictionary<DestructibleObjective, TacticalMission2Marker> objectiveMarkers =
            new Dictionary<DestructibleObjective, TacticalMission2Marker>();

        private readonly Dictionary<Mission2DefenseTerminal, TacticalMission2Marker> terminalMarkers =
            new Dictionary<Mission2DefenseTerminal, TacticalMission2Marker>();

        private void LateUpdate()
        {
            if (CameraManager.Instance == null) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPaused()) return;
            if (markersRoot == null || markerPrefab == null) return;

            bool tactical = !CameraManager.Instance.IsActionMode();
            if (!tactical)
            {
                markersRoot.gameObject.SetActive(false);
                return;
            }

            DestructibleObjective[] objectives = FindObjectsByType<DestructibleObjective>(FindObjectsInactive.Exclude);
            Mission2DefenseTerminal[] terminals = FindObjectsByType<Mission2DefenseTerminal>(FindObjectsInactive.Exclude);

            bool hasContent = (objectives != null && objectives.Any(o => o != null && !o.IsDestroyed)) ||
                              (terminals != null && terminals.Any(t => t != null));

            if (!hasContent && hideRootWhenNothingToShow)
            {
                markersRoot.gameObject.SetActive(false);
                return;
            }

            markersRoot.gameObject.SetActive(true);

            Camera tacCam = CameraManager.Instance.GetTacticalCamera();
            if (tacCam == null) return;

            SyncObjectives(objectives);
            SyncTerminals(terminals);
            UpdateObjectiveMarkers(tacCam);
            UpdateTerminalMarkers(tacCam);
        }

        private void SyncObjectives(DestructibleObjective[] objectives)
        {
            List<DestructibleObjective> alive = objectives == null
                ? new List<DestructibleObjective>()
                : objectives.Where(o => o != null && !o.IsDestroyed).OrderBy(o => o.name).ThenBy(o => o.transform.GetSiblingIndex()).ToList();

            HashSet<DestructibleObjective> aliveSet = alive.ToHashSet();

            for (int i = 0; i < alive.Count; i++)
            {
                DestructibleObjective o = alive[i];
                if (objectiveMarkers.ContainsKey(o)) continue;

                TacticalMission2Marker m = Instantiate(markerPrefab, markersRoot);
                string label = o.TacticalMapLabel ?? $"O{i + 1}";
                m.SetLabel(label);
                objectiveMarkers[o] = m;
            }

            List<DestructibleObjective> toRemove = null;
            foreach (DestructibleObjective key in objectiveMarkers.Keys)
            {
                if (key == null || !aliveSet.Contains(key))
                {
                    toRemove ??= new List<DestructibleObjective>();
                    toRemove.Add(key);
                }
            }

            if (toRemove != null)
            {
                foreach (DestructibleObjective key in toRemove)
                {
                    if (objectiveMarkers.TryGetValue(key, out TacticalMission2Marker m) && m != null)
                        Destroy(m.gameObject);
                    objectiveMarkers.Remove(key);
                }
            }
        }

        private void SyncTerminals(Mission2DefenseTerminal[] terminals)
        {
            if (terminals == null) terminals = new Mission2DefenseTerminal[0];

            HashSet<Mission2DefenseTerminal> aliveSet = terminals.Where(t => t != null).ToHashSet();

            foreach (Mission2DefenseTerminal t in aliveSet)
            {
                if (terminalMarkers.ContainsKey(t)) continue;

                TacticalMission2Marker m = Instantiate(markerPrefab, markersRoot);
                m.SetLabel(t.TacticalMapLabel);
                terminalMarkers[t] = m;
            }

            List<Mission2DefenseTerminal> toRemove = null;
            foreach (Mission2DefenseTerminal key in terminalMarkers.Keys)
            {
                if (key == null || !aliveSet.Contains(key))
                {
                    toRemove ??= new List<Mission2DefenseTerminal>();
                    toRemove.Add(key);
                }
            }

            if (toRemove != null)
            {
                foreach (Mission2DefenseTerminal key in toRemove)
                {
                    if (terminalMarkers.TryGetValue(key, out TacticalMission2Marker m) && m != null)
                        Destroy(m.gameObject);
                    terminalMarkers.Remove(key);
                }
            }
        }

        private void UpdateObjectiveMarkers(Camera tacCam)
        {
            foreach (var kv in objectiveMarkers)
            {
                DestructibleObjective o = kv.Key;
                TacticalMission2Marker marker = kv.Value;
                if (o == null || marker == null || o.IsDestroyed) continue;

                RectTransform rt = marker.transform as RectTransform;
                if (rt == null) continue;

                Vector3 world = o.transform.position + Vector3.up * worldOffsetY;
                Vector3 screen = tacCam.WorldToScreenPoint(world);

                bool behind = hideMarkersWhenBehindCamera && screen.z < 0.1f;
                marker.gameObject.SetActive(!behind);
                if (behind) continue;

                rt.position = new Vector3(screen.x, screen.y, 0f);

                bool shielded = HasActiveShield(o);
                marker.SetColor(shielded ? objectiveShieldedColor : objectiveColor);
            }
        }

        private static bool HasActiveShield(DestructibleObjective o)
        {
            foreach (Mission2ObjectiveShield sh in o.GetComponentsInChildren<Mission2ObjectiveShield>(true))
            {
                if (sh != null && sh.IsProtectionActive)
                    return true;
            }

            return false;
        }

        private void UpdateTerminalMarkers(Camera tacCam)
        {
            foreach (var kv in terminalMarkers)
            {
                Mission2DefenseTerminal t = kv.Key;
                TacticalMission2Marker marker = kv.Value;
                if (t == null || marker == null) continue;

                RectTransform rt = marker.transform as RectTransform;
                if (rt == null) continue;

                Vector3 world = t.transform.position + Vector3.up * worldOffsetY;
                Vector3 screen = tacCam.WorldToScreenPoint(world);

                bool behind = hideMarkersWhenBehindCamera && screen.z < 0.1f;
                marker.gameObject.SetActive(!behind);
                if (behind) continue;

                rt.position = new Vector3(screen.x, screen.y, 0f);
                marker.SetColor(t.IsHacked ? terminalDoneColor : terminalColor);
            }
        }
    }
}
