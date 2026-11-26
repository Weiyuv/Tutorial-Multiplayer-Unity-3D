
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(Camera))]
public class TrailerFreeCamera : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float fastSpeed = 30f;
    [SerializeField] private float slowSpeed = 3f;
    [SerializeField] private float acceleration = 3f;
    [SerializeField] private float deceleration = 2f;

    [Header("Cinematic Rotation")]
    [SerializeField] private float mouseSensitivity = 0.10f;
    [SerializeField] private float rotationSmoothTime = 0.12f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Cinematic Roll")]
    [SerializeField] private bool enableRoll = true;
    [SerializeField] private float rollAmount = 3f;
    [SerializeField] private float rollSmoothness = 3f;

    [Header("Speed Adjustment")]
    [SerializeField] private float scrollSensitivity = 2f;
    [SerializeField] private float minSpeed = 1f;
    [SerializeField] private float maxSpeed = 100f;

    [Header("Camera Focus")]
    [SerializeField] private Transform focusTarget;
    [SerializeField] private float orbitDistance = 8f;
    [SerializeField] private float orbitSensitivity = 0.15f;
    [SerializeField] private float orbitSmoothTime = 0.15f;

    [Header("Camera Toggle")]
    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private int playerCameraPriority = 20;
    [SerializeField] private int inactiveCameraPriority = 0;

    private Camera freeCamera;

    private float yaw;
    private float pitch;
    private float targetYaw;
    private float targetPitch;

    private float yawVelocity;
    private float pitchVelocity;

    private Vector3 currentVelocity;

    private float currentRoll;
    private float targetRoll;

    private bool orbitMode;
    private bool playerCameraMode;

    private void Start()
    {
        freeCamera = GetComponent<Camera>();

        Vector3 angles = transform.eulerAngles;

        yaw = angles.y;
        pitch = angles.x;

        if (pitch > 180f)
            pitch -= 360f;

        targetYaw = yaw;
        targetPitch = pitch;

        if (playerCamera != null)
            playerCamera.Priority = inactiveCameraPriority;

        freeCamera.enabled = true;

        LockCursor();
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null)
            return;

        HandleCursor();

        if (Keyboard.current.rKey.wasPressedThisFrame)
            ToggleCamera();

        if (playerCameraMode)
            return;

        HandleModeSwitch();

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            HandleMouseLook();
            HandleMovement();
            HandleSpeedAdjustment();
        }
        else
        {
            currentVelocity = Vector3.Lerp(
                currentVelocity,
                Vector3.zero,
                deceleration * Time.deltaTime
            );
        }
    }

    private void LateUpdate()
    {
        if (playerCameraMode)
            return;

        ApplyCinematicRotation();

        if (orbitMode && focusTarget != null)
            ApplyOrbit();
        else
            transform.position += currentVelocity * Time.deltaTime;
    }

    private void ToggleCamera()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning("Configure a Player Camera no Inspector.");
            return;
        }

        playerCameraMode = !playerCameraMode;

        if (playerCameraMode)
        {
            playerCamera.Priority = playerCameraPriority;
            freeCamera.enabled = false;

            currentVelocity = Vector3.zero;

            Debug.Log("Câmera do jogador ativada.");
        }
        else
        {
            playerCamera.Priority = inactiveCameraPriority;
            freeCamera.enabled = true;

            Vector3 angles = transform.eulerAngles;

            yaw = angles.y;
            pitch = angles.x;

            if (pitch > 180f)
                pitch -= 360f;

            targetYaw = yaw;
            targetPitch = pitch;

            yawVelocity = 0f;
            pitchVelocity = 0f;

            currentVelocity = Vector3.zero;

            Debug.Log("Câmera livre ativada.");
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HandleCursor()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
            LockCursor();
    }

    private void HandleModeSwitch()
    {
        if (Keyboard.current.fKey.wasPressedThisFrame &&
            focusTarget != null)
        {
            orbitMode = !orbitMode;
            currentVelocity = Vector3.zero;

            if (orbitMode)
            {
                Vector3 direction =
                    transform.position - focusTarget.position;

                orbitDistance = Mathf.Max(1f, direction.magnitude);

                Vector3 angles = Quaternion.LookRotation(
                    focusTarget.position - transform.position
                ).eulerAngles;

                targetYaw = angles.y;
                targetPitch = angles.x;

                if (targetPitch > 180f)
                    targetPitch -= 360f;

                yaw = targetYaw;
                pitch = targetPitch;
            }
        }
    }

    private void HandleMouseLook()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float sensitivity = orbitMode
            ? orbitSensitivity
            : mouseSensitivity;

        targetYaw += mouseDelta.x * sensitivity;
        targetPitch -= mouseDelta.y * sensitivity;

        targetPitch = Mathf.Clamp(
            targetPitch,
            minPitch,
            maxPitch
        );
    }

    private void ApplyCinematicRotation()
    {
        yaw = Mathf.SmoothDampAngle(
            yaw,
            targetYaw,
            ref yawVelocity,
            rotationSmoothTime
        );

        pitch = Mathf.SmoothDampAngle(
            pitch,
            targetPitch,
            ref pitchVelocity,
            rotationSmoothTime
        );

        if (enableRoll && !orbitMode)
        {
            float horizontalInput = 0f;

            if (Keyboard.current.aKey.isPressed)
                horizontalInput = 1f;

            if (Keyboard.current.dKey.isPressed)
                horizontalInput = -1f;

            targetRoll = horizontalInput * rollAmount;

            currentRoll = Mathf.Lerp(
                currentRoll,
                targetRoll,
                rollSmoothness * Time.deltaTime
            );
        }
        else
        {
            targetRoll = 0f;

            currentRoll = Mathf.Lerp(
                currentRoll,
                0f,
                rollSmoothness * Time.deltaTime
            );
        }

        transform.rotation = Quaternion.Euler(
            pitch,
            yaw,
            currentRoll
        );
    }

    private void HandleMovement()
    {
        Vector3 input = Vector3.zero;

        if (Keyboard.current.wKey.isPressed)
            input += Vector3.forward;

        if (Keyboard.current.sKey.isPressed)
            input += Vector3.back;

        if (Keyboard.current.aKey.isPressed)
            input += Vector3.left;

        if (Keyboard.current.dKey.isPressed)
            input += Vector3.right;

        input = Vector3.ClampMagnitude(input, 1f);

        Vector3 direction = transform.TransformDirection(input);

        if (Keyboard.current.spaceKey.isPressed)
            direction += Vector3.up;

        if (Keyboard.current.qKey.isPressed)
            direction += Vector3.down;

        direction = Vector3.ClampMagnitude(direction, 1f);

        float targetSpeed = moveSpeed;

        if (Keyboard.current.leftShiftKey.isPressed)
            targetSpeed = fastSpeed;

        if (Keyboard.current.leftCtrlKey.isPressed)
            targetSpeed = slowSpeed;

        Vector3 targetVelocity = direction * targetSpeed;

        float smooth = direction.sqrMagnitude > 0f
            ? acceleration
            : deceleration;

        currentVelocity = Vector3.Lerp(
            currentVelocity,
            targetVelocity,
            smooth * Time.deltaTime
        );
    }

    private void HandleSpeedAdjustment()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0f)
        {
            moveSpeed += scroll * scrollSensitivity * 0.01f;

            moveSpeed = Mathf.Clamp(
                moveSpeed,
                minSpeed,
                maxSpeed
            );
        }
    }

    private void ApplyOrbit()
    {
        Quaternion rotation = Quaternion.Euler(
            pitch,
            yaw,
            currentRoll
        );

        Vector3 offset =
            rotation * Vector3.back * orbitDistance;

        Vector3 desiredPosition =
            focusTarget.position + offset;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            Time.deltaTime / Mathf.Max(0.01f, orbitSmoothTime)
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (focusTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(focusTarget.position, 0.3f);
            Gizmos.DrawLine(transform.position, focusTarget.position);
        }
    }
}
