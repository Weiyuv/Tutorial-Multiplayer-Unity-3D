
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

    [Header("Animacao")]
    [SerializeField] private Animator animator;

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private Vector3 dashDirection;

    private float dashTimer;
    private float nextDashTime;

    private bool isDashing;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        enabled = IsOwner;

        if (IsOwner)
            Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (!IsOwner || Keyboard.current == null)
            return;

        HandleMovement();
        HandleDash();
        ApplyGravity();
        UpdateAnimations();
    }

    private void HandleMovement()
    {
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

        Vector3 direction = forward * input.y + right * input.x;
        direction.Normalize();

        if (isDashing)
        {
            controller.Move(
                dashDirection * dashSpeed * Time.deltaTime);

            dashTimer -= Time.deltaTime;

            if (dashTimer <= 0f)
                isDashing = false;

            return;
        }

        controller.Move(direction * moveSpeed * Time.deltaTime);

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }
    }

    private void HandleDash()
    {
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
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        verticalVelocity.y += gravity * Time.deltaTime;

        controller.Move(
            verticalVelocity * Time.deltaTime);
    }

    private void UpdateAnimations()
    {
        if (animator == null || Keyboard.current == null)
            return;

        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;

        animator.SetFloat("Speed", input.magnitude);
        animator.SetBool("IsDashing", isDashing);
        animator.SetBool("IsGrounded", controller.isGrounded);
    }

    private void OnDisable()
    {
        if (IsOwner)
            Cursor.lockState = CursorLockMode.None;
    }
}
