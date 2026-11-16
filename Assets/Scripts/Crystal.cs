
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Crystal : NetworkBehaviour
{
    private readonly NetworkVariable<bool> isHeld =
        new NetworkVariable<bool>(false);

    private readonly NetworkVariable<ulong> holderClientId =
        new NetworkVariable<ulong>(ulong.MaxValue);

    private Rigidbody rb;
    private Collider[] colliders;
    private CrystalCollector localCollector;

    private readonly HashSet<Collider> attackColliders =
        new HashSet<Collider>();

    public bool IsHeld => isHeld.Value;
    public ulong HolderClientId => holderClientId.Value;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        colliders = GetComponentsInChildren<Collider>(true);

        SwordDamage[] damages =
            GetComponentsInChildren<SwordDamage>(true);

        foreach (SwordDamage damage in damages)
        {
            if (damage == null)
                continue;

            Collider attackCollider =
                damage.GetComponent<Collider>();

            if (attackCollider == null)
            {
                Debug.LogError("[CRYSTAL] Collider de ataque ausente.");
                continue;
            }

            attackColliders.Add(attackCollider);
            attackCollider.enabled = false;
        }

        ApplyHeldState();
    }

    public override void OnNetworkSpawn()
    {
        isHeld.OnValueChanged += OnHeldChanged;
        holderClientId.OnValueChanged += OnHolderChanged;

        ApplyHeldState();
    }

    public override void OnNetworkDespawn()
    {
        isHeld.OnValueChanged -= OnHeldChanged;
        holderClientId.OnValueChanged -= OnHolderChanged;
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

    public bool PickupServer(
        ulong clientId,
        NetworkObject playerObject,
        float maxDistance)
    {
        if (!IsServer || isHeld.Value)
            return false;

        if (playerObject == null || !playerObject.IsSpawned)
            return false;

        if (playerObject.OwnerClientId != clientId)
            return false;

        float distance = Vector3.Distance(
            playerObject.transform.position,
            transform.position
        );

        if (distance > maxDistance)
            return false;

        holderClientId.Value = clientId;
        isHeld.Value = true;

        ApplyHeldState();

        NotifyPickupClientRpc(
            clientId,
            playerObject.NetworkObjectId
        );

        return true;
    }

    [ClientRpc]
    private void NotifyPickupClientRpc(
        ulong clientId,
        ulong playerNetworkObjectId)
    {
        if (NetworkManager.Singleton == null ||
            NetworkManager.Singleton.LocalClientId != clientId)
            return;

        FindLocalCollector(playerNetworkObjectId);

        if (localCollector == null)
        {
            Debug.LogError("[CRYSTAL] Collector não encontrado.");
            return;
        }

        localCollector.SetHeldCrystal(this);

        Transform point = localCollector.GetHoldPoint();

        if (point != null)
        {
            transform.position = point.position;
            transform.rotation = point.rotation;
        }

        ApplyHeldState();
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

    private void ApplyHeldState()
    {
        bool held = isHeld.Value;

        if (rb != null)
        {
            rb.isKinematic = held;

            if (held)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (colliders == null)
            return;

        foreach (Collider col in colliders)
        {
            if (col == null)
                continue;

            if (attackColliders.Contains(col))
                continue;

            col.enabled = !held;
        }
    }

    private void FindLocalCollector(
        ulong playerNetworkObjectId = ulong.MaxValue)
    {
        localCollector = null;

        if (NetworkManager.Singleton == null)
            return;

        if (playerNetworkObjectId != ulong.MaxValue &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                playerNetworkObjectId,
                out NetworkObject playerObject))
        {
            localCollector =
                playerObject.GetComponentInChildren<CrystalCollector>(true);

            if (localCollector != null)
                return;
        }

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
                 FindObjectsByType<CrystalCollector>(
                     FindObjectsSortMode.None))
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
