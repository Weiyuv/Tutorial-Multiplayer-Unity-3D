using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Crystal : NetworkBehaviour
{
    [Header("Respawn Settings")]
    [SerializeField] private float respawnDelay = 1f;
    [SerializeField] private int maxBounces = 3;

    [Header("Bounce Settings")]
    [SerializeField, Range(0f, 1.5f)] private float bounceStrength = 0.8f;
    [SerializeField, Range(0f, 1f)] private float bounceUpwardFactor = 0.15f;
    [SerializeField] private float minimumBounceSpeed = 3f;

    [Header("Throw Settings")]
    [SerializeField] private float throwForce = 15f;
    [SerializeField] private float upwardForce = 2f;

    private readonly NetworkVariable<bool> isHeld =
        new NetworkVariable<bool>(false);

    private readonly NetworkVariable<ulong> holderClientId =
        new NetworkVariable<ulong>(ulong.MaxValue);

    private readonly NetworkVariable<bool> isRespawning =
        new NetworkVariable<bool>(false);

    private Rigidbody rb;
    private Collider[] colliders;
    private CrystalCollector localCollector;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private bool hasBeenThrown;
    private bool respawnInProgress;
    private int bounceCount;
    private float lastBounceTime;

    public bool IsHeld => isHeld.Value;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        colliders = GetComponentsInChildren<Collider>(true);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        isHeld.OnValueChanged += OnHeldChanged;
        holderClientId.OnValueChanged += OnHolderChanged;
        isRespawning.OnValueChanged += OnRespawningChanged;

        ApplyHeldState();
    }

    public override void OnNetworkDespawn()
    {
        isHeld.OnValueChanged -= OnHeldChanged;
        holderClientId.OnValueChanged -= OnHolderChanged;
        isRespawning.OnValueChanged -= OnRespawningChanged;
    }

    private void Update()
    {
        if (!IsClient || !isHeld.Value ||
            NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.LocalClientId != holderClientId.Value)
            return;

        if (localCollector == null)
            FindLocalCollector();

        if (localCollector == null)
            return;

        Transform point = localCollector.GetHoldPoint();

        if (point != null)
        {
            transform.position = point.position;
            transform.rotation = point.rotation;
        }
    }

    public void PickupServer(ulong clientId)
    {
        if (!IsServer || isHeld.Value || isRespawning.Value ||
            NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
            return;

        NetworkObject playerObject = null;

        foreach (NetworkObject obj in
                 NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            if (obj.OwnerClientId == clientId && obj.CompareTag("Player"))
            {
                playerObject = obj;
                break;
            }
        }

        if (playerObject == null)
        {
            Debug.LogWarning("[CRYSTAL] Jogador não encontrado.");
            return;
        }

        if (Vector3.Distance(
                playerObject.transform.position,
                transform.position) > 3.5f)
        {
            Debug.LogWarning("[CRYSTAL] Jogador longe demais.");
            return;
        }

        hasBeenThrown = false;
        bounceCount = 0;
        holderClientId.Value = clientId;
        isHeld.Value = true;

        ApplyHeldState();
        NotifyPickupClientRpc(clientId);

        Debug.Log("[CRYSTAL] Bola coletada.");
    }

    [ClientRpc]
    private void NotifyPickupClientRpc(ulong clientId)
    {
        if (NetworkManager.Singleton == null ||
            NetworkManager.Singleton.LocalClientId != clientId)
            return;

        FindLocalCollector();

        if (localCollector == null)
        {
            Debug.LogError("[CRYSTAL] CrystalCollector não encontrado.");
            return;
        }

        localCollector.SetHeldCrystal(this);

        DodgeballThrow thrower =
            localCollector.GetComponent<DodgeballThrow>();

        if (thrower == null)
            thrower = localCollector.GetComponentInParent<DodgeballThrow>();

        if (thrower != null)
            thrower.SetHeldBall(this);

        Transform point = localCollector.GetHoldPoint();

        if (point != null)
        {
            transform.position = point.position;
            transform.rotation = point.rotation;
        }

        ApplyHeldState();

        Debug.Log("[CRYSTAL] Bola pronta para arremessar.");
    }

    public void ThrowFromPlayer(Vector3 direction, float force, float upForce)
    {
        if (!IsClient || !isHeld.Value ||
            NetworkManager.Singleton == null)
            return;

        if (holderClientId.Value != NetworkManager.Singleton.LocalClientId)
            return;

        ThrowServerRpc(direction.normalized, force, upForce);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ThrowServerRpc(
        Vector3 direction,
        float force,
        float upForce,
        ServerRpcParams rpcParams = default)
    {
        if (!isHeld.Value ||
            isRespawning.Value ||
            rpcParams.Receive.SenderClientId != holderClientId.Value)
            return;

        ulong clientId = holderClientId.Value;

        force = Mathf.Clamp(force, 0f, 40f);
        upForce = Mathf.Clamp(upForce, 0f, 15f);

        isHeld.Value = false;
        holderClientId.Value = ulong.MaxValue;
        hasBeenThrown = true;
        bounceCount = 0;
        lastBounceTime = -1f;

        ApplyHeldState();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.AddForce(
                direction * force + Vector3.up * upForce,
                ForceMode.Impulse
            );
        }

        ThrowClientRpc(direction, force, upForce, clientId);

        Debug.Log("[CRYSTAL] Bola arremessada.");
    }

    [ClientRpc]
    private void ThrowClientRpc(
        Vector3 direction,
        float force,
        float upForce,
        ulong clientId)
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.LocalClientId == clientId)
        {
            FindLocalCollector();

            if (localCollector != null)
                localCollector.ClearHeldCrystal(this);
        }

        ApplyHeldState();
        localCollector = null;

        if (rb != null && !IsServer)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.AddForce(
                direction * force + Vector3.up * upForce,
                ForceMode.Impulse
            );
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || !hasBeenThrown || respawnInProgress)
            return;

        if (Time.time - lastBounceTime < 0.08f)
            return;

        lastBounceTime = Time.time;
        bounceCount++;

        if (bounceCount > maxBounces)
        {
            Debug.Log("[CRYSTAL] Limite de rebotes atingido. Respawn.");
            StartCoroutine(RespawnAfterCollision());
            return;
        }

        Vector3 normal = collision.contactCount > 0
            ? collision.GetContact(0).normal
            : -rb.linearVelocity.normalized;

        Vector3 incomingVelocity = rb.linearVelocity;
        Vector3 reflectedVelocity = Vector3.Reflect(incomingVelocity, normal);

        if (reflectedVelocity.sqrMagnitude > 0.01f)
        {
            reflectedVelocity *= bounceStrength;
            reflectedVelocity += Vector3.up *
                                 (reflectedVelocity.magnitude * bounceUpwardFactor);

            if (reflectedVelocity.magnitude < minimumBounceSpeed)
                reflectedVelocity = reflectedVelocity.normalized * minimumBounceSpeed;

            rb.linearVelocity = reflectedVelocity;
        }

        Debug.Log("[CRYSTAL] Rebote " + bounceCount + "/" + maxBounces);
    }

    private IEnumerator RespawnAfterCollision()
    {
        respawnInProgress = true;
        isRespawning.Value = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        ApplyHeldState();

        yield return new WaitForSeconds(respawnDelay);

        transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
        }

        hasBeenThrown = false;
        bounceCount = 0;
        isRespawning.Value = false;

        ApplyHeldState();

        respawnInProgress = false;

        Debug.Log("[CRYSTAL] Bola respawnada.");
    }

    private void OnHeldChanged(bool previous, bool current)
    {
        ApplyHeldState();
    }

    private void OnHolderChanged(ulong previous, ulong current)
    {
        if (current == ulong.MaxValue)
            localCollector = null;
    }

    private void OnRespawningChanged(bool previous, bool current)
    {
        ApplyHeldState();
    }

    private void ApplyHeldState()
    {
        bool disablePhysics = isHeld.Value || isRespawning.Value;

        if (rb != null)
        {
            rb.isKinematic = disablePhysics;

            if (disablePhysics)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (colliders != null)
        {
            foreach (Collider col in colliders)
            {
                if (col != null)
                    col.enabled = !disablePhysics;
            }
        }
    }

    private void FindLocalCollector()
    {
        localCollector = null;

        if (NetworkManager.Singleton == null)
            return;

        foreach (NetworkClient client in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != NetworkManager.Singleton.LocalClientId)
                continue;

            if (client.PlayerObject != null)
            {
                localCollector =
                    client.PlayerObject.GetComponentInChildren<CrystalCollector>(true);

                if (localCollector != null)
                    return;
            }
        }

        foreach (CrystalCollector collector in
                 FindObjectsByType<CrystalCollector>(FindObjectsSortMode.None))
        {
            NetworkObject obj =
                collector.GetComponentInParent<NetworkObject>();

            if (obj != null && obj.IsOwner)
            {
                localCollector = collector;
                return;
            }
        }
    }
}