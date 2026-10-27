
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class MechaMovement : NetworkBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float gravity = -25f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("HUD")]
    [SerializeField] private HUDController hudController;

    [Header("Animacao")]
    [SerializeField] private Animator animator;

    private CharacterController controller;
    private MechaHealth mechaHealth;

    private Vector3 verticalVelocity;
    private Vector3 dashDirection;

    private float dashTimer;
    private float nextDashTime;

    private bool isDashing;
    private bool movementLocked;

    public bool IsMovementLocked => movementLocked;
    public bool IsDashing => isDashing;
    public float DashCooldown => dashCooldown;
    public float DashCooldownRemaining =>
        Mathf.Max(0f, nextDashTime - Time.time);

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        mechaHealth = GetComponent<MechaHealth>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (hudController == null)
            hudController = FindFirstObjectByType<HUDController>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        enabled = true;

        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (mechaHealth == null)
            mechaHealth = GetComponent<MechaHealth>();

        if (hudController == null)
            hudController = FindFirstObjectByType<HUDController>();

        verticalVelocity = Vector3.zero;
        isDashing = false;
        movementLocked = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;

        if (locked)
        {
            isDashing = false;
            dashTimer = 0f;
            dashDirection = Vector3.zero;
        }
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (controller == null)
            return;

        // A morte bloqueia todos os comandos de movimento.
        if (mechaHealth != null && mechaHealth.IsDead)
        {
            StopMovement();
            UpdateAnimations();
            return;
        }

        // Nunca chama Move enquanto o CharacterController estiver inativo.
        if (!CanMove())
        {
            StopMovement();
            UpdateAnimations();
            return;
        }

        if (movementLocked)
        {
            isDashing = false;
            dashTimer = 0f;
            dashDirection = Vector3.zero;

            ApplyGravity();
            UpdateAnimations();
            return;
        }

        if (Keyboard.current == null)
        {
            ApplyGravity();
            UpdateAnimations();
            return;
        }

        HandleMovement();
        HandleDash();
        ApplyGravity();
        UpdateAnimations();
    }

    private bool CanMove()
    {
        return controller != null &&
               controller.enabled &&
               controller.gameObject.activeInHierarchy &&
               isActiveAndEnabled;
    }

    private void StopMovement()
    {
        verticalVelocity = Vector3.zero;
        isDashing = false;
        dashTimer = 0f;
        dashDirection = Vector3.zero;
    }

    private void HandleMovement()
    {
        if (!CanMove())
            return;

        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;

        input = Vector2.ClampMagnitude(input, 1f);

        Transform cam = Camera.main != null
            ? Camera.main.transform
            : transform;

        Vector3 forward = Vector3.ProjectOnPlane(
            cam.forward, Vector3.up).normalized;

        Vector3 right = Vector3.ProjectOnPlane(
            cam.right, Vector3.up).normalized;

        Vector3 direction =
            (forward * input.y + right * input.x).normalized;

        if (isDashing)
        {
            if (!CanMove())
                return;

            controller.Move(dashDirection * dashSpeed * Time.deltaTime);

            dashTimer -= Time.deltaTime;

            if (dashTimer <= 0f)
                isDashing = false;

            return;
        }

        if (!CanMove())
            return;

        controller.Move(direction * moveSpeed * Time.deltaTime);

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
        }
    }

    private void HandleDash()
    {
        if (!CanMove() || Keyboard.current == null)
            return;

        if (!Keyboard.current.leftShiftKey.wasPressedThisFrame)
            return;

        if (Time.time < nextDashTime || isDashing)
            return;

        Vector3 direction = Vector3.zero;

        if (Keyboard.current.wKey.isPressed)
            direction += transform.forward;

        if (Keyboard.current.sKey.isPressed)
            direction -= transform.forward;

        if (Keyboard.current.dKey.isPressed)
            direction += transform.right;

        if (Keyboard.current.aKey.isPressed)
            direction -= transform.right;

        if (direction.sqrMagnitude < 0.01f)
            direction = transform.forward;

        dashDirection = direction.normalized;
        isDashing = true;
        dashTimer = dashDuration;
        nextDashTime = Time.time + dashCooldown;

        if (hudController == null)
            hudController = FindFirstObjectByType<HUDController>();

        if (hudController != null)
            hudController.StartDashCooldown(dashCooldown);
    }

    private void ApplyGravity()
    {
        if (!CanMove())
            return;

        if (controller.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        verticalVelocity.y += gravity * Time.deltaTime;

        // Verificação imediatamente antes de Move.
        if (CanMove())
            controller.Move(verticalVelocity * Time.deltaTime);
    }

    private void UpdateAnimations()
    {
        if (animator == null)
            return;

        Vector2 input = Vector2.zero;

        if (!movementLocked &&
            (mechaHealth == null || !mechaHealth.IsDead) &&
            Keyboard.current != null &&
            CanMove())
        {
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        }

        animator.SetFloat("Speed", input.magnitude);
        animator.SetBool("IsDashing", isDashing);
    }

    private void OnDisable()
    {
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
