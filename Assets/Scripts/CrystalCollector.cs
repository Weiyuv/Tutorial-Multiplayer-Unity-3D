
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CrystalCollector : NetworkBehaviour
{
    [Header("Crystal Settings")]
    [SerializeField] private float collectDistance = 3f;
    [SerializeField] private Transform holdPoint;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string hasWeaponParameter = "HasWeapon";

    private Crystal heldCrystal;
    private bool pickupPending;

    private void Update()
    {
        if (!IsOwner)
            return;

        UpdateWeaponAnimation();

        if (Keyboard.current == null ||
            !Keyboard.current.eKey.wasPressedThisFrame)
            return;

        Debug.Log("[COLLECTOR] Tecla E pressionada.");

        if (pickupPending)
        {
            Debug.Log("[COLLECTOR] Coleta já está sendo processada.");
            return;
        }

        if (heldCrystal != null && heldCrystal.IsHeld)
        {
            Debug.Log("[COLLECTOR] Você já está segurando uma espada.");
            return;
        }

        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening)
        {
            Debug.LogError("[COLLECTOR] Netcode não está ativo.");
            return;
        }

        Crystal crystal = FindClosestCrystal();

        if (crystal == null)
        {
            Debug.LogWarning("[COLLECTOR] Nenhuma espada próxima encontrada.");
            return;
        }

        if (!crystal.IsSpawned)
        {
            Debug.LogWarning("[COLLECTOR] Espada não spawnada na rede.");
            return;
        }

        pickupPending = true;

        Debug.Log("[COLLECTOR] Solicitando coleta: " + crystal.name);

        CollectCrystalServerRpc(crystal.NetworkObjectId);
    }

    private void UpdateWeaponAnimation()
    {
        if (animator == null)
            return;

        bool hasWeapon = heldCrystal != null && heldCrystal.IsHeld;

        animator.SetBool(hasWeaponParameter, hasWeapon);
    }

    private Crystal FindClosestCrystal()
    {
        Crystal[] crystals =
            FindObjectsByType<Crystal>(FindObjectsSortMode.None);

        Crystal closest = null;
        float closestDistance = collectDistance;

        foreach (Crystal crystal in crystals)
        {
            if (crystal == null || !crystal.IsSpawned || crystal.IsHeld)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                crystal.transform.position
            );

            Debug.Log(
                "[COLLECTOR] " + crystal.name +
                " | Distância: " + distance.ToString("F2")
            );

            if (distance <= closestDistance)
            {
                closest = crystal;
                closestDistance = distance;
            }
        }

        return closest;
    }

    [ServerRpc]
    private void CollectCrystalServerRpc(
        ulong crystalId,
        ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        Debug.Log("[COLLECTOR] Servidor recebeu pedido de coleta.");

        NetworkObject playerNetworkObject =
            GetComponent<NetworkObject>();

        if (playerNetworkObject == null ||
            !playerNetworkObject.IsSpawned)
        {
            Debug.LogError(
                "[COLLECTOR] RECUSA: PLAYER SEM NETWORKOBJECT SPAWNADO."
            );

            PickupResultClientRpc(false, clientId);
            return;
        }

        if (playerNetworkObject.OwnerClientId != clientId)
        {
            Debug.LogWarning(
                "[COLLECTOR] RECUSA: PROPRIETÁRIO INCORRETO."
            );

            PickupResultClientRpc(false, clientId);
            return;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
            crystalId,
            out NetworkObject crystalObject))
        {
            Debug.LogWarning(
                "[COLLECTOR] RECUSA: ESPADA NÃO ENCONTRADA."
            );

            PickupResultClientRpc(false, clientId);
            return;
        }

        Crystal crystal = crystalObject.GetComponent<Crystal>();

        if (crystal == null)
        {
            Debug.LogWarning(
                "[COLLECTOR] RECUSA: COMPONENTE CRYSTAL AUSENTE."
            );

            PickupResultClientRpc(false, clientId);
            return;
        }

        bool success = crystal.PickupServer(
            clientId,
            playerNetworkObject,
            collectDistance
        );

        if (!success)
        {
            Debug.LogWarning(
                "[COLLECTOR] RECUSA: CRYSTAL NÃO ACEITOU A COLETA."
            );

            PickupResultClientRpc(false, clientId);
            return;
        }

        Debug.Log("[COLLECTOR] Servidor confirmou a coleta.");

        PickupResultClientRpc(true, clientId);
    }

    [ClientRpc]
    private void PickupResultClientRpc(
        bool success,
        ulong clientId)
    {
        if (NetworkManager.Singleton == null ||
            NetworkManager.Singleton.LocalClientId != clientId)
            return;

        pickupPending = false;

        if (!success)
        {
            Debug.LogWarning(
                "[COLLECTOR] O servidor recusou a coleta."
            );

            return;
        }

        Debug.Log("[COLLECTOR] Coleta confirmada pelo servidor.");
    }

    public void SetHeldCrystal(Crystal crystal)
    {
        heldCrystal = crystal;
        pickupPending = false;

        UpdateWeaponAnimation();

        Debug.Log("[COLLECTOR] Espada equipada no jogador.");
    }

    public void ClearHeldCrystal(Crystal crystal)
    {
        if (heldCrystal == crystal)
        {
            heldCrystal = null;
            pickupPending = false;

            UpdateWeaponAnimation();
        }
    }

    public Transform GetHoldPoint()
    {
        if (holdPoint != null)
            return holdPoint;

        holdPoint = transform.Find("HoldPoint");

        if (holdPoint != null)
            return holdPoint;

        if (Camera.main != null)
            return Camera.main.transform;

        return transform;
    }
}
