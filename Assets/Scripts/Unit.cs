using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Unit : MonoBehaviour
{
    public Transform cameraAttachPoint;
    public Player owner;

    [Header("Chess Settings")]
    public ChessUnitType chessType;             // Тип фигуры (задается в Инспекторе)
    public Vector2Int currentGridPosition;      // Текущая логическая координата на доске (A1, C4, ...)
    public bool isFirstMove = true;             // Флаг для пешек или других юнитов
    

    [SerializeField] private int health = 100;
    [SerializeField] private int maxHealth = 100;   // Максимальное здоровье (для хилла)
    [SerializeField] private int damage = 20;

    private CharacterController controller;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxVerticalAngle = 80f;
    [SerializeField] private bool isKing = false;
    
    [Header("Animation")]
    [SerializeField] private float baseMoveSpeed = 5f; // Базовая скорость, для которой анимация настроена
    [SerializeField] private float minAnimationSpeed = 0.6f; // Минимальная скорость анимации
    [SerializeField] private float maxAnimationSpeed = 1.2f; // Максимальная скорость анимации

    [Header("Rule Integrity")]
    [SerializeField] public float ruleIntegrityPoints = 100f; // Начальный запас очков целостности
    [SerializeField] private float integrityCostPerCell = 10f; // Фиксированная стоимость за переход на невалидную клетку
    
    [Header("QTE & Combat")]
    private bool isBlocking = false; // Флаг блокирования (снижает входящий урон)
    
    [Header("Combat Sounds")]
    [SerializeField] private AudioClip takeDamageSound; // Звук получения урона
    [SerializeField] private AudioClip unitDeathSound; // Звук смерти юнита
    
    
    [Header("Audio Source (Optional)")]
    [SerializeField] private AudioSource unitAudioSource; // Можно назначить вручную в Inspector, или создастся автоматически
    
    [Header("Attack Settings")]
    [SerializeField] private string attackStateName = "attack"; // Имя состояния атаки в Animator
    [SerializeField] private float attackDamageStartTime = 0.2f; // Нормализованное время начала нанесения урона (0-1)
    [SerializeField] private float attackDamageEndTime = 0.8f; // Нормализованное время конца нанесения урона (0-1)
    
    [Header("Ability Effects")]
    private bool hasReflectionActive = false;        // Слон - отражение урона
    private bool hasDamageReduction = false;         // Ладья - снижение урона на 50%
    private float damageMultiplier = 1.0f;          // Ферзь - увеличение урона (+20% = 1.2)
    private int reflectionTurnsRemaining = 0;        // Сколько ходов осталось эффекту отражения
    private int damageReductionTurnsRemaining = 0;   // Сколько ходов осталось эффекту снижения урона
    private int damageBoostTurnsRemaining = 0;       // Сколько ходов осталось эффекту увеличения урона

    private Vector3 playerVelocity;
    private Vector2Int startGridPosition;
    private Vector2Int lastLegalGridPosition;
    private Vector2Int lastCheckedGridPosition; // Последняя проверенная клетка для отслеживания переходов
    private float xRotation = 0f;
    private bool isGrounded;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool fireInput;
    private bool isControlled = false;
    
    
    private Animator animator;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
           // Debug.LogError($"Unit {gameObject.name}: CharacterController missing!");
            return;
        }
        if (cameraAttachPoint == null)
        {
            //Debug.LogError($"Unit {gameObject.name}: CameraAttachPoint not assigned!");
        }
        if (gameObject.layer != LayerMask.NameToLayer("Units"))
        {
            //Debug.LogWarning($"Unit {gameObject.name}: Ensure layer is set to 'Units'!");
        }
        animator = GetComponent<Animator>();
        if (ChessGrid.Instance != null)
        {
            SnapToGrid();
        }
        
        // Инициализируем AudioSource для звуков юнита
        InitializeAudioSources();
    }
    
    void Awake()
    {
        // Инициализируем AudioSource в Awake, чтобы он был готов до Start
        InitializeAudioSources();
    }
    
    void OnEnable()
    {
        // Убеждаемся, что AudioSource инициализирован при активации объекта
        // Это решает проблему, когда объект не был выбран в сцене перед запуском
        InitializeAudioSources();
    }
    
    /// <summary>
    /// Инициализирует AudioSource для звуков юнита (можно назначить вручную или создастся автоматически)
    /// </summary>
    private void InitializeAudioSources()
    {
        // Если AudioSource не назначен в Inspector, пытаемся найти или создать
        if (unitAudioSource == null)
        {
            // Сначала пытаемся найти существующий AudioSource на объекте
            unitAudioSource = GetComponent<AudioSource>();
            
            // Если AudioSource не найден, создаем новый
            if (unitAudioSource == null)
            {
                unitAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        // Всегда настраиваем AudioSource (даже если он был назначен вручную)
        // Это гарантирует, что настройки правильные для всех экземпляров
        if (unitAudioSource != null)
        {
            unitAudioSource.spatialBlend = 1f; // 3D звук
            unitAudioSource.minDistance = 5f;
            unitAudioSource.maxDistance = 15f;
            unitAudioSource.priority = 100; // Средний приоритет
            unitAudioSource.playOnAwake = false;
            unitAudioSource.loop = false;
            unitAudioSource.ignoreListenerPause = false; // Останавливается при паузе
            unitAudioSource.enabled = true; // Убеждаемся, что включен
            
            // Убеждаемся, что GameObject активен
            if (!unitAudioSource.gameObject.activeInHierarchy)
            {
                unitAudioSource.gameObject.SetActive(true);
            }
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
        if (isControlled && moveInput != Vector2.zero)
        {
            //Debug.Log($"Unit {gameObject.name}: Move Input = {moveInput}");
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
        if (isControlled && lookInput != Vector2.zero)
        {
            //Debug.Log($"Unit {gameObject.name}: Look Input = {lookInput}");
        }
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        fireInput = context.performed;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused()) return;
        isGrounded = controller.isGrounded;
        if (isGrounded && playerVelocity.y < 0)
        {
            playerVelocity.y = -2f;
        }

        if (!isGrounded)
        {
            playerVelocity.y += gravity * Time.deltaTime;
            controller.Move(playerVelocity * Time.deltaTime);
        }

        // Управление только для выбранного юнита
        if (!isControlled) return;

        if (!controller.enabled)
        {
            Debug.LogWarning($"Unit {gameObject.name}: CharacterController is disabled!");
            return;
        }
        HandleMovementCost();
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        move = move.normalized * moveSpeed;
        controller.Move(move * Time.deltaTime);

        float mouseY = lookInput.y * mouseSensitivity;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxVerticalAngle, maxVerticalAngle);
        cameraAttachPoint.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        float mouseX = lookInput.x * mouseSensitivity;
        transform.Rotate(Vector3.up * mouseX);

        if (fireInput)
        {
            fireInput = false;
            Attack();
        }
        if (animator != null)
        {
            float speed = moveInput.magnitude;
            animator.SetFloat("Speed", speed);
            
            // Синхронизируем скорость анимации с реальной скоростью движения
            if (baseMoveSpeed > 0)
            {
                float animationSpeedMultiplier = moveSpeed / baseMoveSpeed;
                animationSpeedMultiplier = Mathf.Clamp(animationSpeedMultiplier, minAnimationSpeed, maxAnimationSpeed);
                animator.speed = animationSpeedMultiplier;
            }
        }
    }

    private void Attack()
    {
        if (animator != null)
        {
            // Запускаем триггер анимации
            animator.SetTrigger("Attack");
            
            // Очищаем список пораженных целей для новой атаки
            WeaponCollider weaponCollider = GetComponentInChildren<WeaponCollider>();
            if (weaponCollider != null)
            {
                weaponCollider.ClearHitTargets();
            }
        }
    }
    
    /// <summary>
    /// Проверяет, атакует ли юнит в данный момент, проверяя состояние аниматора
    /// </summary>
    public bool IsAttacking()
    {
        if (animator == null) return false;
        
        // Проверяем, находится ли аниматор в состоянии атаки
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        
        // Проверяем по имени состояния
        if (stateInfo.IsName(attackStateName))
        {
            // Проверяем нормализованное время анимации
            // Урон наносится только в определенной части анимации (например, от 0.2 до 0.8)
            float normalizedTime = stateInfo.normalizedTime % 1.0f;
            bool inDamageWindow = normalizedTime >= attackDamageStartTime && normalizedTime <= attackDamageEndTime;
            
            return inDamageWindow;
        }
        
        return false;
    }

    /// <summary>
    /// Наносит урон юниту. Учитывает блокирование, снижение урона и отражение.
    /// </summary>
    /// <param name="amount">Количество урона</param>
    /// <param name="attacker">Юнит, который наносит урон (для отражения)</param>
    public void TakeDamage(int amount, Unit attacker = null)
    {
        int originalAmount = amount;
        
        // Если юнит блокирует, урон полностью блокируется (0 урона)
        if (isBlocking)
        {
            amount = 0; // Полная блокировка урона
        }
        
        // ЛАДЬЯ: Снижение урона на 50%
        if (hasDamageReduction)
        {
            amount = Mathf.RoundToInt(amount * 0.5f);
        }
        
        // Наносим урон
        health -= amount;
        
        // Воспроизводим звук получения урона (только если урон был нанесен)
        if (amount > 0)
        {
            PlayTakeDamageSound();
        }
        
        // СЛОН: Отражение урона обратно атакующему (если есть атакующий и урон был нанесен)
        if (hasReflectionActive && attacker != null && amount > 0)
        {
            // Отправляем урон обратно атакующему (отражаем уже уменьшенный урон)
            attacker.TakeDamage(amount, this);
        }
        
        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // Воспроизводим звук смерти перед уничтожением
        PlayDeathSound();
        
        if (isControlled)
        {
            // Вызываем ForceSwitchToTacticalMode перед Destroy
            CameraManager.Instance.ForceSwitchToTacticalMode();
            isControlled = false;
        }
        if (isKing)
        {
            // Если умер этот юнит, значит его владелец (owner) проиграл
            GameManager.Instance.EndGame(this.owner);
        }
        
        // Уничтожаем объект сразу, звук проиграется через PlayOneShot
        Destroy(gameObject);
    }
    public void SnapToGrid()
    {
        if (ChessGrid.Instance == null) return;
        currentGridPosition = ChessGrid.Instance.WorldToGridCoords(transform.position);
        Vector3 snapPosition = ChessGrid.Instance.GridToWorldPosition(currentGridPosition.x, currentGridPosition.y);
        
        if (controller != null)
        {
            controller.enabled = false;
            transform.position = snapPosition;
            controller.enabled = true;
        }
        else
        {
            transform.position = snapPosition;
        }
    }
    private void HandleMovementCost()
    {
        if (ChessGrid.Instance == null || ChessRulesManager.Instance == null) return;
        
        // Обновляем текущую логическую позицию
        currentGridPosition = ChessGrid.Instance.WorldToGridCoords(transform.position);
        
        // Проверяем, перешел ли юнит в другую клетку
        if (currentGridPosition != lastCheckedGridPosition)
        {
            // Юнит перешел в новую клетку - проверяем валидность перехода
            bool isTransitionLegal = false;
            
            // Если юнит находится на стартовой клетке - переход легален (не покинул стартовую позицию)
            if (currentGridPosition == startGridPosition)
            {
                isTransitionLegal = true;
            }
            else
            {
                // Юнит покинул стартовую клетку - проверяем валидность хода от startGridPosition к currentGridPosition
                // Это проверяет, соответствует ли текущая позиция правилам движения фигуры
                isTransitionLegal = ChessRulesManager.Instance.IsMoveValid(
                    chessType,
                    startGridPosition,
                    currentGridPosition,
                    isFirstMove
                );
            }
            
            // Если переход нелегален - снимаем фиксированное количество очков
            if (!isTransitionLegal)
            {
                ruleIntegrityPoints -= integrityCostPerCell;
                ruleIntegrityPoints = Mathf.Max(0f, ruleIntegrityPoints);
                
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning($"❌ ILLEGAL CELL TRANSITION! {chessType}. From: {ChessGrid.Instance.GridToChessNotation(lastCheckedGridPosition.x, lastCheckedGridPosition.y)} -> To: {ChessGrid.Instance.GridToChessNotation(currentGridPosition.x, currentGridPosition.y)}. Deducted {integrityCostPerCell} points.");
                }
            }
            
            // Обновляем последнюю проверенную позицию
            lastCheckedGridPosition = currentGridPosition;
        }
        
        // Проверка на смерть/штраф
        if (ruleIntegrityPoints <= 0)
        {
            Die();
        }
    }

    public void ResetAnimation()
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.speed = 1.0f; // Сбрасываем скорость анимации
            animator.Update(0f);
        }
        // Очищаем список пораженных целей при сбросе анимации
        WeaponCollider weaponCollider = GetComponentInChildren<WeaponCollider>();
        if (weaponCollider != null)
        {
            weaponCollider.ClearHitTargets();
        }
    }

    // Unit.cs

    public void SetControlled(bool controlled)
    {
        isControlled = controlled;
        if (controlled)
        {
            fireInput = false;
            // Начало хода: Запоминаем стартовую клетку
            if (ChessGrid.Instance != null)
            {
                startGridPosition = ChessGrid.Instance.WorldToGridCoords(transform.position);
                lastLegalGridPosition = startGridPosition;
                lastCheckedGridPosition = startGridPosition; // Инициализируем отслеживание переходов
            }
            // Если у юнита были потрачены очки целостности, и он начинает новый ход, 
            // мы можем рассмотреть возможность их частичного восстановления здесь (если бы ты хотел).
        }
        else
        {
            // Конец хода: Просто убеждаемся, что анимация остановлена
            ResetAnimation();
            // isAttacking уже сброшен в ResetAnimation()
            
            // Если юнит завершает ход, и он стоит не на стартовой клетке
            if (ChessGrid.Instance != null)
            {
                currentGridPosition = ChessGrid.Instance.WorldToGridCoords(transform.position);
                // Если ход был легален (isMoveLegal == true в последнем Update), 
                // можно пометить isFirstMove = false для пешек.
                if (chessType == ChessUnitType.Pawn && startGridPosition != currentGridPosition)
                {
                    // Для пешки, если она сдвинулась, это был первый ход
                    isFirstMove = false;
                }
            }
        }
        //Debug.Log($"Unit {gameObject.name}: Controlled = {isControlled}");
    }


    public int GetHealth()
    {
        return health;
    }
    
    /// <summary>
    /// Возвращает максимальное здоровье юнита (для хилла)
    /// </summary>
    public int GetMaxHealth()
    {
        return maxHealth;
    }
    
    public float GetRuleIntegrityPoints()
    {
    return ruleIntegrityPoints;
    }
    public int Damage { get { return damage; } }
    
    /// <summary>
    /// Проверяет, контролируется ли юнит в данный момент
    /// </summary>
    public bool IsControlled()
    {
        return isControlled;
    }
    
    // ========== СПОСОБНОСТИ: Методы для работы с эффектами ==========
    
    /// <summary>
    /// Исцеляет юнита на указанное количество HP
    /// </summary>
    /// <param name="amount">Количество HP для восстановления</param>
    public void Heal(int amount)
    {
        health = Mathf.Min(health + amount, maxHealth);
    }
    
    /// <summary>
    /// Устанавливает эффект отражения урона (Слон)
    /// </summary>
    /// <param name="active">Активен ли эффект</param>
    /// <param name="turns">На сколько ходов действует эффект</param>
    public void SetReflectionActive(bool active, int turns)
    {
        hasReflectionActive = active;
        reflectionTurnsRemaining = turns;
    }
    
    /// <summary>
    /// Устанавливает эффект снижения урона (Ладья)
    /// </summary>
    /// <param name="active">Активен ли эффект</param>
    /// <param name="turns">На сколько ходов действует эффект</param>
    public void SetDamageReduction(bool active, int turns)
    {
        hasDamageReduction = active;
        damageReductionTurnsRemaining = turns;
    }
    
    /// <summary>
    /// Устанавливает множитель урона (Ферзь)
    /// </summary>
    /// <param name="multiplier">Множитель урона (1.0 = обычный, 1.2 = +20%)</param>
    /// <param name="turns">На сколько ходов действует эффект</param>
    public void SetDamageMultiplier(float multiplier, int turns)
    {
        damageMultiplier = multiplier;
        damageBoostTurnsRemaining = turns;
    }
    
    /// <summary>
    /// Возвращает текущий множитель урона (для WeaponCollider)
    /// </summary>
    public float GetDamageMultiplier()
    {
        return damageMultiplier;
    }
    
    /// <summary>
    /// Обновляет эффекты способностей при смене хода (вызывается из GameManager)
    /// Эффекты отключаются в конце хода, в котором они должны действовать.
    /// </summary>
    public void UpdateAbilityEffects()
    {
        // Обновляем отражение (Слон) - эффект действует весь ход врага
        if (reflectionTurnsRemaining > 0)
        {
            reflectionTurnsRemaining--;
            if (reflectionTurnsRemaining <= 0)
            {
                hasReflectionActive = false;
                // Обновляем визуальный индикатор
                AbilityVisualEffects visualEffects = GetComponent<AbilityVisualEffects>();
                if (visualEffects != null)
                {
                    visualEffects.PlayBishopReflectionEffect(false);
                }
            }
        }
        
        // Обновляем снижение урона (Ладья) - эффект действует весь ход врага
        if (damageReductionTurnsRemaining > 0)
        {
            damageReductionTurnsRemaining--;
            if (damageReductionTurnsRemaining <= 0)
            {
                hasDamageReduction = false;
                // Обновляем визуальный индикатор
                AbilityVisualEffects visualEffects = GetComponent<AbilityVisualEffects>();
                if (visualEffects != null)
                {
                    visualEffects.PlayGuardianShieldEffect(false);
                }
            }
        }
        
        // Обновляем увеличение урона (Ферзь) - эффект действует в текущем ходу
        if (damageBoostTurnsRemaining > 0)
        {
            damageBoostTurnsRemaining--;
            if (damageBoostTurnsRemaining <= 0)
            {
                damageMultiplier = 1.0f;
                // Обновляем визуальный индикатор
                AbilityVisualEffects visualEffects = GetComponent<AbilityVisualEffects>();
                if (visualEffects != null)
                {
                    visualEffects.PlayQueenBoostEffect(false);
                }
            }
        }
    }
    
    // ========== QTE СИСТЕМА: Методы для работы с блокированием ==========
    
    /// <summary>
    /// Устанавливает состояние блокирования юнита.
    /// Используется в QTE системе при успешном блоке.
    /// </summary>
    /// <param name="blocking">true - юнит блокирует, false - не блокирует</param>
    public void SetBlocking(bool blocking)
    {
        isBlocking = blocking;
        // Здесь можно добавить визуальный эффект блокирования (например, свечение щита)
    }
    
    /// <summary>
    /// Проверяет, блокирует ли юнит в данный момент.
    /// </summary>
    /// <returns>true если юнит блокирует, false если нет</returns>
    public bool IsBlocking() => isBlocking;
    
    // ========== БОЕВЫЕ ЗВУКИ ==========
    
    /// <summary>
    /// Получает AudioSource для боевых звуков (для использования в WeaponCollider)
    /// </summary>
    public AudioSource GetCombatAudioSource()
    {
        if (unitAudioSource == null)
        {
            InitializeAudioSources();
        }
        return unitAudioSource;
    }
    
    /// <summary>
    /// Воспроизводит звук получения урона с небольшой задержкой после атаки
    /// </summary>
    private void PlayTakeDamageSound()
    {
        if (takeDamageSound != null)
        {
            // Инициализируем AudioSource, если его нет
            if (unitAudioSource == null)
            {
                InitializeAudioSources();
            }
            
            if (unitAudioSource != null)
            {
                // Добавляем небольшую задержку, чтобы звук атаки успел проиграться
                StartCoroutine(PlayTakeDamageSoundDelayed(0.1f));
            }
        }
    }
    
    private System.Collections.IEnumerator PlayTakeDamageSoundDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (unitAudioSource != null && takeDamageSound != null)
        {
            float volume = AudioManager.Instance != null ? AudioManager.Instance.SFXVolume : 1f;
            unitAudioSource.PlayOneShot(takeDamageSound, volume);
        }
    }
    
    /// <summary>
    /// Воспроизводит звук смерти юнита через временный объект, чтобы звук проигрался даже после уничтожения юнита
    /// </summary>
    private void PlayDeathSound()
    {
        if (unitDeathSound == null)
        {
            return;
        }
        
        // Останавливаем все другие звуки перед воспроизведением звука смерти
        if (unitAudioSource != null) unitAudioSource.Stop();
        
        // Создаем временный объект для воспроизведения звука смерти
        // Этот объект не будет уничтожен вместе с юнитом
        GameObject tempSoundObject = new GameObject("TempDeathSound");
        tempSoundObject.transform.position = transform.position;
        AudioSource tempAudioSource = tempSoundObject.AddComponent<AudioSource>();
        
        // Настраиваем AudioSource
        tempAudioSource.spatialBlend = 1f; // 3D звук
        tempAudioSource.minDistance = 5f;
        tempAudioSource.maxDistance = 20f;
        tempAudioSource.priority = 0; // Высокий приоритет
        
        float volume = PlayerPrefs.HasKey("SFXVolume") ? PlayerPrefs.GetFloat("SFXVolume") : 1f;
        tempAudioSource.PlayOneShot(unitDeathSound, volume);
        
        // Уничтожаем временный объект после проигрывания звука
        Destroy(tempSoundObject, unitDeathSound.length + 0.1f);
    }
    
}