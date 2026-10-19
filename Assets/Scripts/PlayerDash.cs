using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerDash : NetworkBehaviour
{
    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 1f;

    private CharacterController controller;
    private ThirdPersonCameraController cameraController;

    private float dashTimer;
    private float cooldownTimer;
    private Vector3 dashDirection;
    private bool isDashing;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cameraController = GetComponent<ThirdPersonCameraController>();
    }

    private void Update()
    {
        if (!IsOwner || Keyboard.current == null)
            return;

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (Keyboard.current.qKey.wasPressedThisFrame &&
            cooldownTimer <= 0f &&
            !isDashing)
        {
            StartDash();
        }

        if (isDashing)
        {
            controller.Move(dashDirection * dashSpeed * Time.deltaTime);

            dashTimer -= Time.deltaTime;

            if (dashTimer <= 0f)
                isDashing = false;
        }
    }

    private void StartDash()
    {
        Vector3 direction = Vector3.zero;

        if (Keyboard.current.wKey.isPressed)
            direction.z += 1f;

        if (Keyboard.current.sKey.isPressed)
            direction.z -= 1f;

        if (Keyboard.current.aKey.isPressed)
            direction.x -= 1f;

        if (Keyboard.current.dKey.isPressed)
            direction.x += 1f;

        if (direction.sqrMagnitude > 0.01f)
        {
            direction.Normalize();

            float yaw = cameraController != null
                ? cameraController.transform.eulerAngles.y
                : transform.eulerAngles.y;

            dashDirection = Quaternion.Euler(0f, yaw, 0f) * direction;
        }
        else
        {
            dashDirection = transform.forward;
        }

        dashDirection.y = 0f;
        dashDirection.Normalize();

        isDashing = true;
        dashTimer = dashDuration;
        cooldownTimer = dashCooldown;
    }
}