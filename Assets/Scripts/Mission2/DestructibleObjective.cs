using System;
using UnityEngine;

namespace Mission2
{
    public class DestructibleObjective : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 200;
        [SerializeField] private int currentHealth = 200;

        [Header("Optional")]
        [SerializeField] private GameObject destroyedVfxPrefab;
        [SerializeField] private bool destroyGameObjectOnDeath = false;

        public event Action<DestructibleObjective> OnDestroyed;

        public int MaxHealth => Mathf.Max(1, maxHealth);
        public int CurrentHealth => Mathf.Clamp(currentHealth, 0, MaxHealth);
        public bool IsDestroyed => CurrentHealth <= 0;

        private void Awake()
        {
            currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
        }

        public void ApplyDamage(int damage)
        {
            if (IsDestroyed) return;
            int dmg = Mathf.Max(0, damage);
            if (dmg == 0) return;

            currentHealth = Mathf.Clamp(currentHealth - dmg, 0, MaxHealth);
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

