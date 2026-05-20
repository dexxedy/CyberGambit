using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Collections;
using System;

/// <summary>Роль юнита для таргетинга ИИ (танк отдельно от пехоты в PvBot / Mission2).</summary>
public enum UnitCombatRole
{
    Standard = 0,
    Tank = 1
}

public class Unit : MonoBehaviour
{
    public static event Action<Unit, Vector2Int, Vector2Int> OnGridPositionChanged;

    public Transform cameraAttachPoint;
    public Player owner;

    [Header("Chess Settings")]
    public ChessUnitType chessType;             // Тип фигуры (задается в Инспекторе)
    public Vector2Int currentGridPosition;      // Текущая логическая координата на доске (A1, C4, ...)
    public bool isFirstMove = true;             // Флаг для пешек или других юнитов

    [Header("AI / Mission targeting")]
    [Tooltip("Tank: обычные враги PvBot не выбирают этот юнит как цель; только Guardian (ракетчик) может бить танк. Если на объекте есть Mission2.TankController, танк определяется и так.")]
    [SerializeField] private UnitCombatRole combatRole = UnitCombatRole.Standard;

    /// <summary>Танк для правил выбора цели ботом. Учитывает роль и наличие Mission2.TankController.</summary>
    public bool IsTankUnit => combatRole == UnitCombatRole.Tank || GetComponent<Mission2.TankController>() != null;

    [SerializeField] private int health = 100;
    [SerializeField] private int maxHealth = 100;   // Максимальное здоровье (для хилла)
    [SerializeField] private int damage = 20;

    private CharacterController controller;
    [SerializeField] private float moveSpeed = 5f;
    public float MoveSpeed => moveSpeed; // Публичное свойство для доступа к скорости
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxVerticalAngle = 80f;
    [SerializeField] private bool isKing = false;
    
    [Header("Animation")]
    [SerializeField] private float baseMoveSpeed = 5f; // Базовая скорость, для которой анимация настроена
    [SerializeField] private float minAnimationSpeed = 0.6f; // Минимальная скорость анимации
    [SerializeField] private float maxAnimationSpeed = 1.2f; // Максимальная скорость анимации

    [Header("Death")]
    [Tooltip("Если в Animator есть такой Trigger (например Any State → Death), перед Destroy проигрывается клип смерти.")]
    [SerializeField] private bool useDeathAnimation = true;
    [SerializeField] private string deathAnimatorTrigger = "Die";
    [Tooltip("Секунды до Destroy после смерти. Подгони под длину клипа Death в Animator (без loop на последнем кадре).")]
    [SerializeField] private float deathDestroyDelaySeconds = 3f;
    
    [Header("Movement Steps (Dice)")]
    [SerializeField] private int remainingSteps = 0;
    [SerializeField] private float remainingMoveMeters = 0f;
    [SerializeField] private bool constrainActionMovementToNavMesh = true;
    [SerializeField] private float navMeshSampleRadius = 1.25f;
    
    public bool hubExploreNoCombat = false;

    [Header("Grid Sync (Tactical Layer)")]
    [Tooltip("Если включено, юнит будет принудительно привязываться к ChessGrid в Start(). Отключи в mission1, если юниты улетают/падают.")]
    [SerializeField] private bool snapToGridOnStart = true;
    [Tooltip("После центра клетки по XZ — луч вниз, Y ставится по коллайдеру пола/платформы (иначе GridToWorld всегда давал бы Y сетки).")]
    [SerializeField] private bool snapRaycastDownForGroundY = true;
    [Tooltip("На сколько метров выше max(Y сетки, текущая Y) начинается луч (чтобы не задеть свой коллайдер).")]
    [SerializeField] private float snapGroundRaycastStartAbove = 5f;
    [Tooltip("Максимальная длина луча вниз.")]
    [SerializeField] private float snapGroundRaycastMaxDistance = 100f;
    [SerializeField] private LayerMask snapGroundRaycastLayers = ~0;
    [Tooltip("Небольшой зазор над поверхностью, чтобы не застревать в коллайдере.")]
    [SerializeField] private float snapGroundYClearance = 0.08f;

    // Был ли юнит хоть раз сдвинут за всю игру (для логики "первый шаг только вперед")
    private bool hasMovedAtLeastOnce = false;
    
    [Header("QTE & Combat")]
    private bool isBlocking = false; // Флаг блокирования (снижает входящий урон)
    
    [Header("Combat Sounds")]
    [SerializeField] private AudioClip takeDamageSound; // Звук получения урона
    [SerializeField] private AudioClip unitDeathSound; // Звук смерти юнита
    
    
    [Header("Audio Source (Optional)")]
    [SerializeField] private AudioSource unitAudioSource; // Можно назначить вручную в Inspector, или создастся автоматически
    
    [Header("Tactical icon card (BF-style)")]
    [SerializeField] private string unitDisplayNameOverride;
    [SerializeField] private Sprite tacticalPortrait;

    [Header("Equipped Weapon (Prefab)")]
    [Tooltip("Если задано — юнит инстанцирует это оружие в Start() и будет стрелять им по ЛКМ.")]
    [SerializeField] private Weapon startingWeaponPrefab;
    [Tooltip("Куда прикреплять оружие (кость руки/пустышка). Если null — к корню юнита.")]
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private Weapon equippedWeapon;

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
    private Vector2Int lastCheckedGridPosition; // Последняя зафиксированная клетка (для списания шагов)
    private Vector3 lastCheckedCellWorldPosition; // Центр последней зафиксированной клетки
    private float xRotation = 0f;

    private bool isGrounded;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool fireInput;
    private bool isControlled = false;
    private const float MinMoveBudgetEpsilon = 0.01f;

    private bool isDeadOrDying;
    private Animator animator;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            return;
        }
        if (snapToGridOnStart && ChessGrid.Instance != null)
        {
            SnapToGrid();
        }
        
        // Инициализируем AudioSource для звуков юнита
        InitializeAudioSources();

        if (startingWeaponPrefab != null)
        {
            EquipWeaponPrefab(startingWeaponPrefab);
        }
    }
    
    void Awake()
    {
        // Инициализируем AudioSource в Awake, чтобы он был готов до Start
        InitializeAudioSources();
        if (GetComponent<TacticalUnitPresentation>() == null)
            gameObject.AddComponent<TacticalUnitPresentation>();

        animator = GetComponent<Animator>();

        // Auto-bind camera attach point if not set (prevents runtime exceptions).
        if (cameraAttachPoint == null)
        {
            // Common child names used for FPS camera pivots.
            Transform t = transform.Find("CameraAttachPoint");
            if (t == null) t = transform.Find("cameraAttachPoint");
            if (t == null) t = transform.Find("CameraPivot");
            if (t == null) t = transform.Find("CameraPivotPoint");
            cameraAttachPoint = t;
        }
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
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        fireInput = context.performed;
    }

    void Update()
    {
        if (isDeadOrDying) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused()) return;
        if (MissionSelectUI.Instance != null && MissionSelectUI.Instance.IsOpen) return;
        if (controller == null) return;
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
            return;
        }
        // Обновляем позицию для тактической карты даже в экшен-режиме.
        if (ChessGrid.Instance != null)
        {
            SetCurrentGridPosition(ChessGrid.Instance.WorldToGridCoords(transform.position));
        }

        Mission2.TankController tankDrive = GetComponent<Mission2.TankController>();
        bool tankHandledMoveLook = tankDrive != null && tankDrive.enabled &&
            tankDrive.ApplyPlayerTankFrame(this, controller, moveInput, lookInput, moveSpeed, mouseSensitivity);

        if (!tankHandledMoveLook)
        {
            Vector3 rawMove = transform.right * moveInput.x + transform.forward * moveInput.y;
            Vector3 moveDirection = rawMove.sqrMagnitude > 0f ? rawMove.normalized : Vector3.zero;
            float requestedDistance = moveSpeed * moveInput.magnitude * Time.deltaTime;
            float allowedDistance = Mathf.Min(requestedDistance, remainingMoveMeters);
            Vector3 moveVector = moveDirection * allowedDistance;
            moveVector = ConstrainMoveVectorToNavMesh(moveVector);
            if (moveVector.sqrMagnitude > 0f)
            {
                float actualDistance = moveVector.magnitude;
                controller.Move(moveVector);
                ConsumeMoveMeters(actualDistance);
                if (ChessGrid.Instance != null)
                {
                    SetCurrentGridPosition(ChessGrid.Instance.WorldToGridCoords(transform.position));
                }
            }

            float mouseY = lookInput.y * mouseSensitivity;
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -maxVerticalAngle, maxVerticalAngle);
            if (cameraAttachPoint != null)
            {
                cameraAttachPoint.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            }

            float mouseX = lookInput.x * mouseSensitivity;
            transform.Rotate(Vector3.up * mouseX);
        }

        HandleMovementSteps();
        if (!isControlled) return;

        // Перезарядка оружия по R должна работать даже без выстрела.
        if (!hubExploreNoCombat && equippedWeapon != null && equippedWeapon.Config != null)
        {
            equippedWeapon.TickReload();
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                equippedWeapon.TryStartReload();
            }
        }

        if (animator != null)
        {
            float speed = moveInput.magnitude;
            animator.SetFloat("Speed", speed);

            bool combatStance = !hubExploreNoCombat && equippedWeapon != null && Mouse.current != null && Mouse.current.rightButton.isPressed;
            TrySetAnimatorCombat(combatStance);

            if (baseMoveSpeed > 0)
            {
                float animationSpeedMultiplier = moveSpeed / baseMoveSpeed;
                animationSpeedMultiplier = Mathf.Clamp(animationSpeedMultiplier, minAnimationSpeed, maxAnimationSpeed);
                animator.speed = animationSpeedMultiplier;
            }
        }

        if (fireInput)
        {
            fireInput = false;
            Attack();
        }
    }

    /// <summary>
    /// Выполняет атаку (публичный метод для использования ботом и игроком)
    /// </summary>
    public void Attack()
    {
        if (hubExploreNoCombat) return;
        if (equippedWeapon != null && equippedWeapon.Config != null)
        {
            PerformEquippedWeaponFire();
            return;
        }

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

    private void PerformEquippedWeaponFire()
    {
        Camera actionCam = CameraManager.Instance != null ? CameraManager.Instance.GetActionCamera() : null;
        Vector3 origin;
        Vector3 direction;

        if (actionCam != null)
        {
            // Hitscan и урон — строго из камеры; трассер рисуется из дула в Weapon.TryFire.
            origin = actionCam.transform.position;
            direction = actionCam.transform.forward;
        }
        else
        {
            origin = equippedWeapon.Muzzle != null ? equippedWeapon.Muzzle.position : (transform.position + Vector3.up * 1.2f);
            direction = transform.forward;
        }

        if (equippedWeapon.TryFire(this, origin, direction))
        {
            if (animator != null)
            {
                animator.SetTrigger("Shoot");
            }
        }
    }

    public void EquipWeapon(Weapon weaponInstance)
    {
        if (equippedWeapon != null)
        {
            Destroy(equippedWeapon.gameObject);
            equippedWeapon = null;
        }

        equippedWeapon = weaponInstance;
        if (equippedWeapon == null) return;

        Transform parent = weaponSocket != null ? weaponSocket : transform;
        equippedWeapon.transform.SetParent(parent, worldPositionStays: false);
        equippedWeapon.transform.localPosition = Vector3.zero;
        equippedWeapon.transform.localRotation = Quaternion.identity;
        equippedWeapon.InitializeFromConfigIfNeeded();
    }

    public void EquipWeaponPrefab(Weapon weaponPrefab)
    {
        if (weaponPrefab == null) return;
        Weapon instance = Instantiate(weaponPrefab);
        EquipWeapon(instance);
    }

    public Weapon GetEquippedWeapon()
    {
        return equippedWeapon;
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
        if (isDeadOrDying) return;

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
        if (isDeadOrDying) return;
        isDeadOrDying = true;
        health = 0;

        PlayDeathSound();

        if (isControlled && CameraManager.Instance != null)
        {
            CameraManager.Instance.ForceSwitchToTacticalMode();
            SetControlled(false);
        }

        if (isKing && GameManager.Instance != null)
            GameManager.Instance.EndGame(owner);

        DisableGameplayForDeath();

        if (useDeathAnimation && TryPlayDeathAnimation())
        {
            StartCoroutine(DestroyAfterDeathPresentation());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void DisableGameplayForDeath()
    {
        if (controller != null)
            controller.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;

        Mission2.TankController tankCtrl = GetComponent<Mission2.TankController>();
        if (tankCtrl != null)
            tankCtrl.enabled = false;

        if (isControlled)
            SetControlled(false);

        // Не отключаем сам MonoBehaviour Unit — иначе корутина Destroy не выполнится.
    }

    private bool TryPlayDeathAnimation()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            return false;
        if (!AnimatorHasTriggerParameter(animator, deathAnimatorTrigger))
            return false;

        animator.speed = 1f;
        animator.ResetTrigger(deathAnimatorTrigger);
        animator.SetTrigger(deathAnimatorTrigger);
        return true;
    }

    private static bool AnimatorHasTriggerParameter(Animator anim, string triggerName)
    {
        if (anim == null || string.IsNullOrEmpty(triggerName)) return false;
        foreach (AnimatorControllerParameter p in anim.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerName)
                return true;
        }
        return false;
    }

    private IEnumerator DestroyAfterDeathPresentation()
    {
        float delay = Mathf.Max(0.05f, deathDestroyDelaySeconds);
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
    public void SnapToGrid()
    {
        if (ChessGrid.Instance == null) return;
        SetCurrentGridPosition(ChessGrid.Instance.WorldToGridCoords(transform.position));
        Vector3 baseSnap = ChessGrid.Instance.GridToWorldPosition(currentGridPosition.x, currentGridPosition.y);
        Vector3 snapPosition = ApplyGroundYFromRaycast(baseSnap);

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

    private Vector3 ApplyGroundYFromRaycast(Vector3 baseSnapXZ)
    {
        if (!snapRaycastDownForGroundY)
            return baseSnapXZ;

        float startY = Mathf.Max(baseSnapXZ.y, transform.position.y) + Mathf.Max(0.1f, snapGroundRaycastStartAbove);
        Vector3 rayOrigin = new Vector3(baseSnapXZ.x, startY, baseSnapXZ.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, snapGroundRaycastMaxDistance, snapGroundRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            float y = hit.point.y + Mathf.Max(0f, snapGroundYClearance);
            return new Vector3(baseSnapXZ.x, y, baseSnapXZ.z);
        }

        return new Vector3(baseSnapXZ.x, transform.position.y, baseSnapXZ.z);
    }

    public void SetRemainingSteps(int steps)
    {
        remainingSteps = Mathf.Max(0, steps);
        float fallbackMetersPerStep = ChessGrid.Instance != null ? ChessGrid.Instance.cellSize : 1f;
        float metersPerStep = GameManager.Instance != null ? GameManager.Instance.GetMetersPerDicePoint() : fallbackMetersPerStep;
        remainingMoveMeters = remainingSteps * Mathf.Max(0f, metersPerStep);
        
        if (ChessGrid.Instance != null)
        {
            lastCheckedGridPosition = ChessGrid.Instance.WorldToGridCoords(transform.position);
            lastCheckedCellWorldPosition = ChessGrid.Instance.GridToWorldPosition(lastCheckedGridPosition.x, lastCheckedGridPosition.y);
        }
    }
    
    public int GetRemainingSteps() => remainingSteps;
    
    public void SetRemainingMoveMeters(float meters)
    {
        remainingMoveMeters = Mathf.Max(0f, meters);
        float fallbackMetersPerStep = ChessGrid.Instance != null ? ChessGrid.Instance.cellSize : 1f;
        float metersPerStep = GameManager.Instance != null ? GameManager.Instance.GetMetersPerDicePoint() : fallbackMetersPerStep;
        if (metersPerStep > 0f)
        {
            remainingSteps = Mathf.CeilToInt(remainingMoveMeters / metersPerStep);
        }
        else
        {
            remainingSteps = 0;
        }
    }
    
    public float GetRemainingMoveMeters() => remainingMoveMeters;
    
    public void ConsumeMoveMeters(float meters)
    {
        if (meters <= 0f) return;
        remainingMoveMeters = Mathf.Max(0f, remainingMoveMeters - meters);

        float fallbackMetersPerStep = ChessGrid.Instance != null ? ChessGrid.Instance.cellSize : 1f;
        float metersPerStep = GameManager.Instance != null ? GameManager.Instance.GetMetersPerDicePoint() : fallbackMetersPerStep;
        if (metersPerStep > 0f)
        {
            remainingSteps = Mathf.CeilToInt(remainingMoveMeters / metersPerStep);
        }
        else
        {
            remainingSteps = 0;
        }
    }

    public bool HasMovedAtLeastOnce() => hasMovedAtLeastOnce;

    public bool HasMovedThisTurn()
    {
        if (!isControlled) return false;
        if (ChessGrid.Instance == null) return false;
        Vector2Int pos = ChessGrid.Instance.WorldToGridCoords(transform.position);
        return pos != startGridPosition;
    }
    
    private void HandleMovementSteps()
    {
        if (ChessGrid.Instance != null)
        {
            SetCurrentGridPosition(ChessGrid.Instance.WorldToGridCoords(transform.position));
            if (currentGridPosition != lastCheckedGridPosition)
            {
                hasMovedAtLeastOnce = true;
                lastCheckedGridPosition = currentGridPosition;
                lastCheckedCellWorldPosition = ChessGrid.Instance.GridToWorldPosition(currentGridPosition.x, currentGridPosition.y);
                if (GridHighlighter.Instance != null)
                {
                    GridHighlighter.Instance.ShowAllowedMoves(this);
                }
            }
        }

        TryEndActionTurnWhenMoveBudgetEmpty();
    }

    /// <summary>Срабатывает и без ChessGrid (уровень только по NavMesh).</summary>
    private void TryEndActionTurnWhenMoveBudgetEmpty()
    {
        if (remainingMoveMeters > MinMoveBudgetEpsilon) return;
        if (CameraManager.Instance == null || !CameraManager.Instance.IsActionMode()) return;
        if (CameraManager.Instance.GetCurrentControlledUnit() != this) return;

        remainingMoveMeters = 0f;
        remainingSteps = 0;
        CameraManager.Instance.SwitchToTacticalMode();
    }
    
    private void SnapToCellWorldPosition(Vector3 worldPos)
    {
        if (controller != null)
        {
            controller.enabled = false;
            transform.position = worldPos;
            controller.enabled = true;
        }
        else
        {
            transform.position = worldPos;
        }
    }

    public void ResetAnimation()
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.speed = 1.0f; // Сбрасываем скорость анимации
            TrySetAnimatorCombat(false);
            animator.Update(0f);
        }
        // Очищаем список пораженных целей при сбросе анимации
        WeaponCollider weaponCollider = GetComponentInChildren<WeaponCollider>();
        if (weaponCollider != null)
        {
            weaponCollider.ClearHitTargets();
        }
    }

    /// <summary>
    /// Выставляет bool Combat, если он есть в Animator Controller (например BishopController).
    /// Стойка прицела: зажата ПКМ при экипированном оружии.
    /// </summary>
    private void TrySetAnimatorCombat(bool combatStance)
    {
        if (animator == null) return;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == "Combat")
            {
                animator.SetBool("Combat", combatStance);
                return;
            }
        }
    }

    /// <summary>
    /// Внешний контроль анимации движения (когда юнит двигается корутинами бота, не через input).
    /// </summary>
    public void SetExternalMoveAnimation(bool moving)
    {
        if (animator == null) return;
        animator.SetFloat("Speed", moving ? 1f : 0f);

        if (moving && baseMoveSpeed > 0f)
        {
            float animationSpeedMultiplier = moveSpeed / baseMoveSpeed;
            animationSpeedMultiplier = Mathf.Clamp(animationSpeedMultiplier, minAnimationSpeed, maxAnimationSpeed);
            animator.speed = animationSpeedMultiplier;
        }
        else if (!moving)
        {
            animator.speed = 1.0f;
        }
    }

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
                lastCheckedGridPosition = startGridPosition; // Инициализируем отслеживание переходов
                lastCheckedCellWorldPosition = ChessGrid.Instance.GridToWorldPosition(startGridPosition.x, startGridPosition.y);
            }
        }
        else
        {
            Mission2.TankController tankCtrl = GetComponent<Mission2.TankController>();
            if (tankCtrl != null)
                tankCtrl.NotifyTankControlEnded();

            // Сбрасываем ввод, чтобы не "залипала" анимация бега после завершения хода
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            fireInput = false;

            // Конец хода: Просто убеждаемся, что анимация остановлена
            ResetAnimation();
            // isAttacking уже сброшен в ResetAnimation()
            
            // Если юнит завершает ход, и он стоит не на стартовой клетке
            if (ChessGrid.Instance != null)
            {
                SetCurrentGridPosition(ChessGrid.Instance.WorldToGridCoords(transform.position));
                // Если ход был легален (isMoveLegal == true в последнем Update), 
                // можно пометить isFirstMove = false для пешек.
                if (chessType == ChessUnitType.Pawn && startGridPosition != currentGridPosition)
                {
                    // Для пешки, если она сдвинулась, это был первый ход
                    isFirstMove = false;
                }
            }
        }
    }

    private void SetCurrentGridPosition(Vector2Int newPosition)
    {
        Vector2Int oldPosition = currentGridPosition;
        currentGridPosition = newPosition;
        if (oldPosition != newPosition)
        {
            OnGridPositionChanged?.Invoke(this, oldPosition, newPosition);
        }
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

    /// <summary>Пустая строка = использовать дефолт по типу фигуры на UI.</summary>
    public string GetUnitDisplayName()
    {
        return unitDisplayNameOverride ?? string.Empty;
    }

    public Sprite GetTacticalPortrait()
    {
        return tacticalPortrait;
    }
    
    public float GetRuleIntegrityPoints()
    {
        // Backwards-compat: заменено на шаги. План: удалить вызовы с UI/бота.
        return remainingMoveMeters;
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
        if (isDeadOrDying) return;
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
    
    /// <summary>
    /// Перемещает юнита к указанной позиции сетки плавно (для использования ботом)
    /// </summary>
    /// <param name="targetPos">Целевая позиция на сетке</param>
    public void MoveToGridPosition(Vector2Int targetPos)
    {
        if (ChessGrid.Instance == null) return;
        
        Vector3 targetWorldPos = ChessGrid.Instance.GridToWorldPosition(targetPos.x, targetPos.y);
        
        // Запускаем корутину для плавного перемещения
        StartCoroutine(MoveToPositionCoroutine(targetWorldPos, targetPos));
    }
    
    /// <summary>
    /// Корутина для плавного перемещения юнита к целевой позиции
    /// </summary>
    private IEnumerator MoveToPositionCoroutine(Vector3 targetWorldPos, Vector2Int targetGridPos)
    {
        Vector3 startPos = transform.position;
        float distance = Vector3.Distance(startPos, targetWorldPos);
        // Получаем скорость движения (используем публичное свойство или поле)
        float unitMoveSpeed = moveSpeed;
        float duration = distance / unitMoveSpeed; // Время движения зависит от расстояния и скорости
        duration = Mathf.Clamp(duration, 0.5f, 3f); // Увеличиваем максимальное время для плавности
        
        float elapsedTime = 0f;
        
        // Включаем анимацию движения
        if (animator != null)
        {
            animator.SetFloat("Speed", 1f);
        }
        
        // Поворачиваем юнита к цели
        Vector3 direction = (targetWorldPos - startPos).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
        
        // Плавное перемещение
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            
            // Используем CharacterController для перемещения
            if (controller != null && controller.enabled)
            {
                Vector3 currentPos = Vector3.Lerp(startPos, targetWorldPos, t);
                Vector3 moveVector = currentPos - transform.position;
                moveVector.y = 0; // Игнорируем вертикальное движение
                controller.Move(moveVector);
            }
            else
            {
                transform.position = Vector3.Lerp(startPos, targetWorldPos, t);
            }
            
            yield return null;
        }
        
        // Финальная позиция (точно на сетке)
        if (controller != null)
        {
            controller.enabled = false;
            transform.position = targetWorldPos;
            controller.enabled = true;
        }
        else
        {
            transform.position = targetWorldPos;
        }
        
        // Обновляем позицию на сетке
        SetCurrentGridPosition(targetGridPos);
        SnapToGrid();
        ConsumeMoveMeters(distance);
        
        // Останавливаем анимацию движения
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }
    }
    
    public void RefreshGridPositionFromWorld()
    {
        if (ChessGrid.Instance == null) return;
        SetCurrentGridPosition(ChessGrid.Instance.WorldToGridCoords(transform.position));
    }

    /// <summary>Ограничение шага движения в экшене по NavMesh (танк и др.).</summary>
    public Vector3 ConstrainActionMoveToNavMesh(Vector3 moveVector) => ConstrainMoveVectorToNavMesh(moveVector);

    /// <summary>Вертикальный наклон камеры экшена (танк вызывает вместе с наклоном ствола).</summary>
    public void ApplyActionLookPitchDelta(float pitchDeltaDeg)
    {
        xRotation -= pitchDeltaDeg;
        xRotation = Mathf.Clamp(xRotation, -maxVerticalAngle, maxVerticalAngle);
        if (cameraAttachPoint != null)
            cameraAttachPoint.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    /// <summary>Текущий pitch камеры экшена (локальный X на cameraAttachPoint), градусы.</summary>
    public float GetActionLookPitchDegrees() => xRotation;

    /// <summary>Жёсткий clamp вертикали прицела (танк и др.), в тех же градусах, что <see cref="GetActionLookPitchDegrees"/>.</summary>
    public void ClampActionLookPitchAbsolute(float minDeg, float maxDeg)
    {
        if (minDeg > maxDeg)
            (minDeg, maxDeg) = (maxDeg, minDeg);
        xRotation = Mathf.Clamp(xRotation, minDeg, maxDeg);
        if (cameraAttachPoint != null)
            cameraAttachPoint.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
    
    public float CalculateNavMeshPathLength(Vector3 targetWorldPos)
    {
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, targetWorldPos, NavMesh.AllAreas, path) ||
            path.corners == null || path.corners.Length < 2)
        {
            return 0f;
        }
        
        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++)
        {
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }
        return length;
    }
    
    private Vector3 ConstrainMoveVectorToNavMesh(Vector3 moveVector)
    {
        if (!constrainActionMovementToNavMesh || moveVector.sqrMagnitude <= 0f)
        {
            return moveVector;
        }

        Vector3 targetPos = transform.position + moveVector;
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, Mathf.Max(0.1f, navMeshSampleRadius), NavMesh.AllAreas))
        {
            Vector3 adjusted = hit.position - transform.position;
            adjusted.y = 0f;
            return adjusted;
        }

        return Vector3.zero;
    }
    
    /// <summary>
    /// Находит ближайшего вражеского юнита (для использования ботом)
    /// </summary>
    /// <returns>Ближайший вражеский юнит или null, если не найден</returns>
    public Unit GetNearestEnemy()
    {
        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        Unit nearestEnemy = null;
        float nearestDistance = float.MaxValue;
        
        foreach (Unit enemy in allUnits)
        {
            if (enemy == null || enemy.owner == this.owner || enemy.GetHealth() <= 0) continue;
            
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestEnemy = enemy;
            }
        }
        
        return nearestEnemy;
    }
    
    
}