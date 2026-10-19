using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CrystalCollector : NetworkBehaviour
{
    [Header("Crystal Settings")]
    [SerializeField] private float collectDistance = 3f;
    [SerializeField] private Transform holdPoint;

    private Crystal heldCrystal;
    private bool pickupPending;

    private void Update()
    {
        if (!IsOwner)
            return;

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
            Debug.Log("[COLLECTOR] Você já está segurando uma bola.");
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
            Debug.LogWarning("[COLLECTOR] Nenhuma bola próxima encontrada.");
            return;
        }

        if (!crystal.IsSpawned)
        {
            Debug.LogWarning("[COLLECTOR] Bola não spawnada na rede.");
            return;
        }

        pickupPending = true;

        Debug.Log("[COLLECTOR] Solicitando coleta: " + crystal.name);

        CollectCrystalServerRpc(crystal.NetworkObjectId);
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
            crystalId, out NetworkObject crystalObject))
        {
            Debug.LogWarning(
                "[COLLECTOR] RECUSA: BOLA NÃO ENCONTRADA."
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

        if (crystal.IsHeld)
        {
            Debug.LogWarning(
                "[COLLECTOR] RECUSA: BOLA JÁ COLETADA."
            );

            PickupResultClientRpc(false, clientId);
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            crystal.transform.position
        );

        Debug.Log(
            "[COLLECTOR] Distância no servidor: " +
            distance.ToString("F2")
        );

        if (distance > collectDistance)
        {
            Debug.LogWarning(
                "[COLLECTOR] RECUSA: DISTÂNCIA EXCEDIDA."
            );

            PickupResultClientRpc(false, clientId);
            return;
        }

        crystal.PickupServer(clientId);

        Debug.Log("[COLLECTOR] Coleta solicitada com sucesso.");

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

        Debug.Log("[COLLECTOR] Pedido de coleta aceito.");

        if (heldCrystal == null)
        {
            Crystal[] crystals =
                FindObjectsByType<Crystal>(FindObjectsSortMode.None);

            foreach (Crystal crystal in crystals)
            {
                if (crystal != null &&
                    crystal.IsSpawned &&
                    crystal.IsHeld)
                {
                    heldCrystal = crystal;
                    break;
                }
            }
        }

        if (heldCrystal == null)
        {
            Debug.LogWarning(
                "[COLLECTOR] Aguardando confirmação da bola."
            );
        }
    }

    public void SetHeldCrystal(Crystal crystal)
    {
        heldCrystal = crystal;
        pickupPending = false;
    }

    public void ClearHeldCrystal(Crystal crystal)
    {
        if (heldCrystal == crystal)
            heldCrystal = null;
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