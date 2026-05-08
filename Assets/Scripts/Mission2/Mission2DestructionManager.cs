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
            TryFinishIfNone();
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

