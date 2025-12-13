using UnityEngine;
using System.Collections.Generic; 

public class WeaponCollider : MonoBehaviour
{
    private Unit ownerUnit;
    private Collider weaponCollider;
    
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
        // 1. ПРОВЕРКА ХОДА (КЛЮЧЕВОЙ ФИКС):
        // Разрешаем наносить урон ТОЛЬКО, если сейчас ход владельца этого оружия.
        if (GameManager.Instance == null || ownerUnit == null || GameManager.Instance.currentPlayer != ownerUnit.owner) 
        {
            return;
        }
        
        // (Удалите публичный флаг CanDamage из скрипта и Инспектора, он больше не нужен)
        
        // 2. Проверяем, был ли этот объект уже поражен в этом контакте
        if (targetsHitInContact.Contains(other)) return; 

        Unit target = other.GetComponentInParent<Unit>();

        // 3. ПРОВЕРКА ЦЕЛИ: Убеждаемся, что цель принадлежит противнику.
        if (target != null && target != ownerUnit && target.owner != ownerUnit.owner)
        {
            // Вычисляем финальный урон с учетом множителя способности (Ферзь)
            int finalDamage = Mathf.RoundToInt(ownerUnit.Damage * ownerUnit.GetDamageMultiplier());
            
            // Наносим урон цели, передавая атакующего для отражения урона (Слон)
            target.TakeDamage(finalDamage, ownerUnit);
            
            // !!! ИСПРАВЛЕНИЕ: Используем геттер target.GetHealth() вместо прямого доступа к полю health
            Debug.Log($"Melee hit on {target.gameObject.name} for {finalDamage} damage (Base: {ownerUnit.Damage}, Multiplier: {ownerUnit.GetDamageMultiplier():F2}x). Target HP: {target.GetHealth()}");
            
            // Запоминаем цель, чтобы не ударить дважды
            targetsHitInContact.Add(other);
        }
    }
    
    // Очищаем список целей, когда они выходят из триггера
    void OnTriggerExit(Collider other)
    {
        targetsHitInContact.Remove(other);
    }
}