
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CameraPOV : NetworkBehaviour
{
    [Header("Camera")]
    [SerializeField] private UnityEngine.Camera playerCamera;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;

    private CharacterController characterController;
    private float yaw;
    private float pitch;
    private float verticalVelocity;
    private bool cursorLocked = true;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            UnityEngine.Camera[] cameras =
                GetComponentsInChildren<UnityEngine.Camera>(true);

            foreach (UnityEngine.Camera cam in cameras)
            {
                if (cam != null)
                {
                    playerCamera = cam;
                    break;
                }
            }
        }

        if (playerCamera == null)
        {
            GameObject cameraObject = new GameObject("FirstPersonCamera");
            cameraObject.transform.SetParent(transform);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;

            playerCamera = cameraObject.AddComponent<UnityEngine.Camera>();
            cameraObject.AddComponent<AudioListener>();
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
        pitch = 0f;

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
        if (!IsOwner || playerCamera == null)
            return;

        playerCamera.transform.position =
            transform.TransformPoint(cameraOffset);

        playerCamera.transform.rotation =
            Quaternion.Euler(pitch, yaw, 0f);
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

        Vector3 moveDirection =
            Quaternion.Euler(0f, yaw, 0f) * inputDirection;

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

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

        UnityEngine.Cursor.lockState = locked
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        UnityEngine.Cursor.visible = !locked;
    }
}
