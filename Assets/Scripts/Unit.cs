using UnityEngine;
using UnityEngine.InputSystem;

public class Unit : MonoBehaviour
{
    public Transform cameraAttachPoint;
    public Player owner;

    [SerializeField] private int health = 100;
    [SerializeField] private int damage = 20;
    [SerializeField] private float attackRange = 10f;

    private CharacterController controller;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxVerticalAngle = 80f;

    private Vector3 playerVelocity;
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
            Debug.LogError($"Unit {gameObject.name}: CharacterController missing!");
            return;
        }
        if (cameraAttachPoint == null)
        {
            Debug.LogError($"Unit {gameObject.name}: CameraAttachPoint not assigned!");
        }
        if (gameObject.layer != LayerMask.NameToLayer("Units"))
        {
            Debug.LogWarning($"Unit {gameObject.name}: Ensure layer is set to 'Units'!");
        }
        animator = GetComponent<Animator>();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
        if (isControlled && moveInput != Vector2.zero)
        {
            Debug.Log($"Unit {gameObject.name}: Move Input = {moveInput}");
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        jumpInput = context.performed;
        if (isControlled && jumpInput)
        {
            Debug.Log($"Unit {gameObject.name}: Jump Input");
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
        if (isControlled && lookInput != Vector2.zero)
        {
            Debug.Log($"Unit {gameObject.name}: Look Input = {lookInput}");
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
            animator.SetTrigger("Attack");
        }
        Camera actionCamera = Camera.main; 
        Ray ray = new Ray(actionCamera.transform.position, actionCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, attackRange, LayerMask.GetMask("Units")))
        {
            Unit target = hit.collider.GetComponent<Unit>();
            if (target != null && target.owner != owner)
            {
                target.TakeDamage(damage);
                Debug.Log($"Unit {gameObject.name} attacked {target.gameObject.name} for {damage} damage. Target HP: {target.health}");
            }
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
        Destroy(gameObject);
    }
    public void ResetAnimation()
    {
    if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.Update(0f);
        }
    }

    public void SetControlled(bool controlled)
    {
        isControlled = controlled;
        if (!controlled)
        {
            ResetAnimation();
        }
        Debug.Log($"Unit {gameObject.name}: Controlled = {isControlled}");
    }

    public int GetHealth()
    {
        return health;
    }
}