
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class DestroyPlayerTest : NetworkBehaviour
{
    [Header("Teste de Morte")]
    [SerializeField] private Key testKey = Key.K;
    [SerializeField] private float testDamage = 10000f;

    private MechaHealth mechaHealth;

    public override void OnNetworkSpawn()
    {
        mechaHealth = GetComponent<MechaHealth>();

        if (mechaHealth == null)
        {
            Debug.LogError(
                "[TESTE] MechaHealth não encontrado no jogador."
            );
        }
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current[testKey].wasPressedThisFrame)
            RequestTestDeath();
    }

    private void RequestTestDeath()
    {
        if (mechaHealth == null)
            return;

        if (mechaHealth.IsDead)
        {
            Debug.Log("[TESTE] O jogador já está derrotado.");
            return;
        }

        if (IsServer)
        {
            ApplyTestDamage();
        }
        else
        {
            TestDeathServerRpc();
        }
    }

    [ServerRpc]
    private void TestDeathServerRpc(
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        ApplyTestDamage();
    }

    private void ApplyTestDamage()
    {
        if (!IsServer || mechaHealth == null || mechaHealth.IsDead)
            return;

        mechaHealth.TakeDamageServer(testDamage);

        Debug.Log("[TESTE] Dano de teste aplicado ao jogador.");
    }
}
