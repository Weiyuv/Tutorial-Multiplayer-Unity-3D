using Unity.Netcode;
using UnityEngine;

public class Crystal : NetworkBehaviour
{
    private bool collected;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("[CRYSTAL] Trigger: " + other.name);

        if (!other.CompareTag("Player"))
            return;

        Debug.Log("[CRYSTAL] PLAYER PEGOU O CRYSTAL!");

        if (collected)
            return;

        // Multiplayer ainda não iniciou
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[CRYSTAL] NetworkManager não iniciou. Removendo localmente.");
            collected = true;
            Destroy(gameObject);
            return;
        }

        // Multiplayer iniciou
        CollectServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void CollectServerRpc()
    {
        if (collected)
            return;

        collected = true;

        Debug.Log("[CRYSTAL] SERVIDOR: removendo Crystal!");

        NetworkObject.Despawn();
    }
}