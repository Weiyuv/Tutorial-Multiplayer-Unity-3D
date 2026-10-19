using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonCameraController : NetworkBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float cameraDistance = 3.5f;
    [SerializeField] private float shoulderOffset = 0.65f;
    [SerializeField] private float cameraHeight = 1.5f;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float rotationSmoothness = 12f;

    private CharacterController characterController;
    private float yaw;
    private float pitch = 10f;
    private float verticalVelocity;
    private bool cursorLocked = true;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (cameraPivot == null)
        {
            Transform existingPivot = transform.Find("CameraPivot");

            if (existingPivot != null)
                cameraPivot = existingPivot;
        }

        if (playerCamera == null)
        {
            Camera[] cameras = GetComponentsInChildren<Camera>(true);

            foreach (Camera cam in cameras)
            {
                if (cam != null)
                {
                    playerCamera = cam;
                    break;
                }
            }
        }

        if (cameraPivot == null)
        {
            GameObject pivotObject = new GameObject("CameraPivot");
            cameraPivot = pivotObject.transform;
            cameraPivot.SetParent(transform);
            cameraPivot.localPosition = new Vector3(0f, cameraHeight, 0f);
            cameraPivot.localRotation = Quaternion.identity;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (playerCamera != null)
        {
            playerCamera.enabled = IsOwner;

            AudioListener listener =
                playerCamera.GetComponent<AudioListener>();

            if (listener != null)
                listener.enabled = IsOwner;

            if (IsOwner)
                playerCamera.tag = "MainCamera";
            else if (playerCamera.CompareTag("MainCamera"))
                playerCamera.tag = "Untagged";
        }

        if (!IsOwner)
            return;

        yaw = transform.eulerAngles.y;
        LockCursor(true);
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            LockCursor(false);
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        HandleCursor();
        HandleCameraInput();
        HandleMovement();
    }

    private void LateUpdate()
    {
        if (!IsOwner || playerCamera == null || cameraPivot == null)
            return;

        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 shoulderPosition =
            cameraPivot.position +
            yawRotation * Vector3.right * shoulderOffset;

        playerCamera.transform.position =
            shoulderPosition -
            cameraRotation * Vector3.forward * cameraDistance;

        Vector3 lookTarget =
            cameraPivot.position +
            cameraRotation * Vector3.forward * 10f;

        playerCamera.transform.rotation =
            Quaternion.LookRotation(
                lookTarget - playerCamera.transform.position,
                Vector3.up
            );
    }

    private void HandleCursor()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            LockCursor(false);
        }

        if (!cursorLocked &&
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor(true);
        }
    }

    private void HandleCameraInput()
    {
        if (!cursorLocked || Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null)
            return;

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed)
            horizontal -= 1f;

        if (Keyboard.current.dKey.isPressed)
            horizontal += 1f;

        if (Keyboard.current.wKey.isPressed)
            vertical += 1f;

        if (Keyboard.current.sKey.isPressed)
            vertical -= 1f;

        Vector3 inputDirection =
            new Vector3(horizontal, 0f, vertical).normalized;

        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);

        Vector3 moveDirection =
            yawRotation * inputDirection;

        if (inputDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.Euler(0f, yaw, 0f);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSmoothness * Time.deltaTime
            );
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity =
            moveDirection * moveSpeed +
            Vector3.up * verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }

    private void LockCursor(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !locked;
    }
}