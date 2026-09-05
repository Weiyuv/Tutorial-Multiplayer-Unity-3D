using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CrystalCollector : NetworkBehaviour
{
    [Header("Crystal Settings")]
    [SerializeField] private float collectDistance = 2.5f;

    private void Update()
    {
        if (!IsOwner)
            return;

        Debug.Log("[COLLECTOR] ESTÁ RODANDO");

        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            Debug.Log("[COLLECTOR] E APERTADO!");

            Crystal crystal = FindClosestCrystal();

            if (crystal == null)
            {
                Debug.Log("[COLLECTOR] NENHUM CRYSTAL PRÓXIMO!");
                return;
            }

            Debug.Log(
                "[COLLECTOR] CRYSTAL ENCONTRADO: " +
                crystal.gameObject.name
            );

            if (!NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning(
                    "[COLLECTOR] NetworkManager não está rodando!"
                );

                return;
            }

            CollectCrystalServerRpc(
                crystal.NetworkObjectId
            );
        }
    }

    private Crystal FindClosestCrystal()
    {
        Crystal[] crystals =
            FindObjectsByType<Crystal>(
                FindObjectsSortMode.None
            );

        Debug.Log(
            "[COLLECTOR] Cristais encontrados na cena: " +
            crystals.Length
        );

        Crystal closest = null;
        float closestDistance = collectDistance;

        foreach (Crystal crystal in crystals)
        {
            float distance = Vector3.Distance(
                transform.position,
                crystal.transform.position
            );

            Debug.Log(
                "[COLLECTOR] " +
                crystal.gameObject.name +
                " distância: " +
                distance.ToString("F2")
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
    private void CollectCrystalServerRpc(ulong crystalId)
    {
        Debug.Log(
            "[COLLECTOR] SERVER RPC RECEBIDO!"
        );

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(
                crystalId,
                out NetworkObject crystalObject
            ))
        {
            Debug.LogWarning(
                "[COLLECTOR] CRYSTAL NÃO ENCONTRADO NO SERVIDOR!"
            );

            return;
        }

        Crystal crystal =
            crystalObject.GetComponent<Crystal>();

        if (crystal == null)
        {
            Debug.LogWarning(
                "[COLLECTOR] NetworkObject não possui Crystal!"
            );

            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            crystal.transform.position
        );

        Debug.Log(
            "[COLLECTOR] Distância verificada pelo servidor: " +
            distance.ToString("F2")
        );

        if (distance > collectDistance)
        {
            Debug.LogWarning(
                "[COLLECTOR] PLAYER ESTÁ LONGE DEMAIS!"
            );

            return;
        }

        Debug.Log(
            "[COLLECTOR] SERVIDOR: CRYSTAL COLETADO!"
        );

        crystalObject.Despawn();
    }
}