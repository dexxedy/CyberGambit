using UnityEngine;
using System.Collections.Generic; 

public class WeaponCollider : MonoBehaviour
{
    private Unit ownerUnit;
    private Collider weaponCollider;
    
    [Header("Combat Sounds")]
    [SerializeField] private AudioClip weaponHitSound; // Звук удара оружием
    
    // Отслеживаем цели, которые уже были поражены в текущем контакте
    private HashSet<Collider> targetsHitInContact = new HashSet<Collider>(); 

    void Start()
    {
        ownerUnit = GetComponentInParent<Unit>();
        
        if (ownerUnit == null)
        {
            Debug.LogError("WeaponCollider должен быть прикреплен к объекту, который является дочерним элементом юнита (Unit)!");
            return;
        }

        weaponCollider = GetComponent<Collider>();
        if (weaponCollider != null)
        {
            weaponCollider.isTrigger = true;
            // Коллайдер активен по умолчанию, как вы просили.
        }
    }
    
    // Этот метод теперь не используется, но оставлен пустым.
    public void SetColliderActive(bool isActive)
    {
        // Временно ничего не делаем, чтобы коллайдер был всегда активен.
    }

    void OnTriggerEnter(Collider other)
    {
        // 1. ПРОВЕРКА ХОДА: Разрешаем наносить урон ТОЛЬКО, если сейчас ход владельца этого оружия.
        if (GameManager.Instance == null || ownerUnit == null || GameManager.Instance.currentPlayer != ownerUnit.owner) 
        {
            return;
        }
        
        // 2. ПРОВЕРКА АТАКИ: Урон наносится ТОЛЬКО во время активной атаки (проверка состояния аниматора)
        if (!ownerUnit.IsAttacking())
        {
            return;
        }
        
        // 3. Проверяем, был ли этот объект уже поражен в этом контакте
        if (targetsHitInContact.Contains(other)) return; 

        Unit target = other.GetComponentInParent<Unit>();

        // 4. ПРОВЕРКА ЦЕЛИ: Убеждаемся, что цель принадлежит противнику.
        if (target != null && target != ownerUnit && target.owner != ownerUnit.owner)
        {
            // Вычисляем финальный урон с учетом множителя способности (Ферзь)
            int finalDamage = Mathf.RoundToInt(ownerUnit.Damage * ownerUnit.GetDamageMultiplier());
            
            // Воспроизводим звук удара оружием
            PlayWeaponHitSound();
            
            // Наносим урон цели, передавая атакующего для отражения урона (Слон)
            target.TakeDamage(finalDamage, ownerUnit);
            
            // Запоминаем цель, чтобы не ударить дважды
            targetsHitInContact.Add(other);
        }
    }
    
    // Очищаем список целей, когда они выходят из триггера
    void OnTriggerExit(Collider other)
    {
        targetsHitInContact.Remove(other);
    }
    
    /// <summary>
    /// Очищает список пораженных целей (вызывается при начале новой атаки)
    /// </summary>
    public void ClearHitTargets()
    {
        targetsHitInContact.Clear();
    }
    
    /// <summary>
    /// Воспроизводит звук удара оружием (использует combatSource из Unit)
    /// </summary>
    private void PlayWeaponHitSound()
    {
        if (weaponHitSound != null && ownerUnit != null)
        {
            // Используем combatSource из Unit для боевых звуков
            AudioSource combatSource = ownerUnit.GetCombatAudioSource();
            if (combatSource != null)
            {
                // Воспроизводим звук атаки сразу (первым, до звука получения урона)
                float volume = AudioManager.Instance != null ? AudioManager.Instance.SFXVolume : 1f;
                combatSource.PlayOneShot(weaponHitSound, volume);
            }
        }
    }
}