using UnityEngine;
using UnityEngine.InputSystem;

public class Unit : MonoBehaviour
{
    public Transform cameraAttachPoint;
    public Player owner;

    [Header("Chess Settings")]
    public ChessUnitType chessType;             // Тип фигуры (задается в Инспекторе)
    public Vector2Int currentGridPosition;      // Текущая логическая координата на доске (A1, C4, ...)
    public bool isFirstMove = true;             // Флаг для пешек или других юнитов
    

    [SerializeField] private int health = 100;
    [SerializeField] private int damage = 20;

    private CharacterController controller;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxVerticalAngle = 80f;
    [SerializeField] private bool isKing = false;

    [Header("Rule Integrity")]
    [SerializeField] public float ruleIntegrityPoints = 100f; // Начальный запас очков целостности
    [SerializeField] private float integrityCostPerSecond = 5f;
    

    private Vector3 playerVelocity;
    private Vector2Int startGridPosition;
    private Vector2Int lastLegalGridPosition;
    private float xRotation = 0f;
    private bool isGrounded;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpInput;
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
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
        if (isControlled && moveInput != Vector2.zero)
        {
            //Debug.Log($"Unit {gameObject.name}: Move Input = {moveInput}");
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        jumpInput = context.performed;
        if (isControlled && jumpInput)
        {
            //Debug.Log($"Unit {gameObject.name}: Jump Input");
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
        if (isControlled && fireInput)
        {
            Debug.Log($"Unit {gameObject.name}: Fire Input");
        }
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
        if (move.magnitude > 0)
        {
            Debug.Log($"Unit {gameObject.name}: Moving with velocity {move}");
        }

        if (jumpInput && isGrounded)
        {
            playerVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            jumpInput = false;
            Debug.Log($"Unit {gameObject.name}: Jumping");
        }

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
        }
    }

    private void Attack()
    {
        if (animator != null)
        {
            // Запускаем триггер анимации.
            // Коллизия (урон) будет активирована Событием Анимации в нужный момент.
            animator.SetTrigger("Attack");
        }
    }

    public void TakeDamage(int amount)
    {
        health -= amount;
        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"Unit {gameObject.name} died!");
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
        // Снимаем очки, только если юнит активно двигается и его движение НЕ легально
        if (moveInput.magnitude > 0 && ChessGrid.Instance != null && ChessRulesManager.Instance != null)
        {
            // Обновляем текущую логическую позицию
            currentGridPosition = ChessGrid.Instance.WorldToGridCoords(transform.position);

            // Проверяем:
            // 1. Юнит покинул стартовую клетку? (startGridPosition != currentGridPosition)
            // 2. Текущее смещение с startGridPosition до currentGridPosition НЕ соответствует правилам фигуры?
            
            bool isCurrentMoveLegal = false;
            
            // Если юнит находится на стартовой клетке, движение всегда легально.
            if (startGridPosition == currentGridPosition)
            {
                isCurrentMoveLegal = true;
            }
            else
            {
                // Проверяем, может ли фигура ТЕОРЕТИЧЕСКИ сделать такой ход (проверка паттерна/дальности)
                isCurrentMoveLegal = ChessRulesManager.Instance.IsMoveValid(
                    chessType, 
                    startGridPosition, 
                    currentGridPosition, 
                    isFirstMove
                );
            }

            if (!isCurrentMoveLegal)
            {
                // Движение является "свободным" (нелегальным), снимаем очки
                ruleIntegrityPoints -= integrityCostPerSecond * Time.deltaTime;
                ruleIntegrityPoints = Mathf.Max(0f, ruleIntegrityPoints);
                
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning($"❌ ILLEGAL MOVEMENT! {chessType}. Start: {ChessGrid.Instance.GridToChessNotation(startGridPosition.x, startGridPosition.y)} -> Current: {ChessGrid.Instance.GridToChessNotation(currentGridPosition.x, currentGridPosition.y)}. Deducting points.");
                }
            }
            else
            {
                // Debug.Log($"LEGAL. Integrity: {ruleIntegrityPoints:F1}");
            }
        }
        
        // Проверка на смерть/штраф
        if (ruleIntegrityPoints <= 0)
        {
            Debug.Log($"Unit {gameObject.name} исчерпал Rule Integrity Points и УМЕР!");
            Die();
        }
    }

    public void ResetAnimation()
    {
    if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.Update(0f);
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
            }
            // Если у юнита были потрачены очки целостности, и он начинает новый ход, 
            // мы можем рассмотреть возможность их частичного восстановления здесь (если бы ты хотел).
        }
        else
        {
            // Конец хода: Просто убеждаемся, что анимация остановлена
            ResetAnimation();
            
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
    public float GetRuleIntegrityPoints()
    {
    return ruleIntegrityPoints;
    }
    public int Damage { get { return damage; } }
}