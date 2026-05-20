using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mission2
{
    public class Mission2DestructionManager : MonoBehaviour
    {
        [Header("Objectives (optional override)")]
        [SerializeField] private List<DestructibleObjective> objectives = new List<DestructibleObjective>();
        [SerializeField] private bool autoFindObjectivesInScene = true;

        [Header("Quest UI")]
        [SerializeField] private int destroyObjectivesQuestIndex = 1;

        private readonly HashSet<DestructibleObjective> alive = new HashSet<DestructibleObjective>();

        private void Awake()
        {
            RefreshObjectives();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void RefreshObjectives()
        {
            if (autoFindObjectivesInScene)
            {
                objectives = FindObjectsByType<DestructibleObjective>(FindObjectsInactive.Exclude)
                    .Where(o => o != null)
                    .ToList();
            }

            alive.Clear();
            foreach (var o in objectives)
            {
                if (o == null) continue;
                if (!o.IsDestroyed) alive.Add(o);
            }
        }

        private void Bind()
        {
            RefreshObjectives();
            foreach (var o in objectives)
            {
                if (o == null) continue;
                o.OnDestroyed -= HandleObjectiveDestroyed;
                o.OnDestroyed += HandleObjectiveDestroyed;
            }

            TryFinishIfNone();
            UpdateQuestProgress();
        }

        private void Unbind()
        {
            foreach (var o in objectives)
            {
                if (o == null) continue;
                o.OnDestroyed -= HandleObjectiveDestroyed;
            }
        }

        private void HandleObjectiveDestroyed(DestructibleObjective obj)
        {
            if (obj != null) alive.Remove(obj);
            UpdateQuestProgress();
            TryFinishIfNone();
        }

        private void UpdateQuestProgress()
        {
            if (MissionQuestUI.Instance == null || objectives == null)
                return;

            int total = 0;
            int destroyed = 0;
            foreach (DestructibleObjective o in objectives)
            {
                if (o == null) continue;
                total++;
                if (o.IsDestroyed)
                    destroyed++;
            }

            if (total > 0)
                MissionQuestUI.Instance.SetProgress(destroyObjectivesQuestIndex, destroyed, total);
        }

        private void TryFinishIfNone()
        {
            if (alive.Count > 0) return;
            if (GameManager.Instance == null) return;

            // Player1 wins → loser is Player2
            GameManager.Instance.EndGame(Player.Player2);
        }
    }
}

