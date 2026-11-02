
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class MechaCameraLook : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minVerticalAngle = -40f;
    [SerializeField] private float maxVerticalAngle = 70f;

    [Header("Player")]
    [SerializeField] private Transform mechaBody;

    private float yaw;
    private float pitch;

    private NetworkObject networkObject;
    private MechaHealth mechaHealth;

    private bool isLocalPlayer;
    private bool respawnScreenOpen;

    private void Awake()
    {
        networkObject = GetComponentInParent<NetworkObject>();

        if (networkObject != null)
        {
            mechaHealth = networkObject.GetComponent<MechaHealth>();
            isLocalPlayer = false;
        }
        else
        {
            isLocalPlayer = true;
        }

        if (mechaBody == null && transform.parent != null)
            mechaBody = transform.parent;

        yaw = mechaBody != null
            ? mechaBody.eulerAngles.y
            : transform.eulerAngles.y;

        pitch = transform.localEulerAngles.x;

        if (pitch > 180f)
            pitch -= 360f;

        pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
    }

    private void Start()
    {
        UpdateLocalPlayerStatus();

        if (isLocalPlayer)
            LockCursor();
    }

    private void Update()
    {
        UpdateLocalPlayerStatus();

        if (!isLocalPlayer || respawnScreenOpen)
            return;

        if (mechaHealth != null && mechaHealth.IsDead)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;

        pitch = Mathf.Clamp(
            pitch,
            minVerticalAngle,
            maxVerticalAngle
        );
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer || respawnScreenOpen)
            return;

        if (mechaHealth != null && mechaHealth.IsDead)
            return;

        if (mechaBody != null)
            mechaBody.rotation = Quaternion.Euler(0f, yaw, 0f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void UpdateLocalPlayerStatus()
    {
        if (networkObject == null)
            return;

        if (!networkObject.IsSpawned)
            return;

        isLocalPlayer = networkObject.IsOwner;

        if (!isLocalPlayer && enabled)
            enabled = false;
    }

    public void SetRespawnScreenOpen(bool isOpen)
    {
        respawnScreenOpen = isOpen;

        if (isOpen)
            UnlockCursor();
        else
            LockCursor();
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDisable()
    {
    }
}
