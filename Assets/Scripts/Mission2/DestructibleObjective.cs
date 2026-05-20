using System;
using UnityEngine;

namespace Mission2
{
    public class DestructibleObjective : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 200;
        [SerializeField] private int currentHealth = 200;

        [Header("Optional")]
        [SerializeField] private GameObject hitVfxPrefab;
        [SerializeField] private GameObject destroyedVfxPrefab;
        [SerializeField] private bool destroyGameObjectOnDeath = false;

        [Header("Tactical map marker (Mission 2)")]
        [Tooltip("Текст на тактической карте. Пусто — автоматически O1, O2, … по порядку.")]
        [SerializeField] private string tacticalMapLabel = "";

        public event Action<DestructibleObjective> OnDestroyed;

        public int MaxHealth => Mathf.Max(1, maxHealth);
        public int CurrentHealth => Mathf.Clamp(currentHealth, 0, MaxHealth);
        public bool IsDestroyed => CurrentHealth <= 0;

        /// <summary>null или пусто — контроллер подставит O1, O2…</summary>
        public string TacticalMapLabel =>
            string.IsNullOrWhiteSpace(tacticalMapLabel) ? null : tacticalMapLabel.Trim();

        private void Awake()
        {
            currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
        }

        public void ApplyDamage(int damage)
        {
            if (IsDestroyed) return;
            foreach (var sh in GetComponentsInChildren<Mission2ObjectiveShield>(true))
            {
                if (sh != null && sh.IsProtectionActive)
                    return;
            }

            int dmg = Mathf.Max(0, damage);
            if (dmg == 0) return;

            currentHealth = Mathf.Clamp(currentHealth - dmg, 0, MaxHealth);
            if (!IsDestroyed && hitVfxPrefab != null)
            {
                Instantiate(hitVfxPrefab, transform.position, transform.rotation);
            }
            if (currentHealth <= 0)
            {
                HandleDestroyed();
            }
        }

        private void HandleDestroyed()
        {
            if (destroyedVfxPrefab != null)
            {
                Instantiate(destroyedVfxPrefab, transform.position, transform.rotation);
            }

            OnDestroyed?.Invoke(this);

            if (destroyGameObjectOnDeath)
                Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }
#endif
    }
}

