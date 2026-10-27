
using System;
using Unity.Netcode;
using UnityEngine;

public class MechaHealth : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    private readonly NetworkVariable<float> currentHealth =
        new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private bool deathHandled;

    public float CurrentHealth => currentHealth.Value;
    public float MaxHealth => maxHealth;
    public bool IsDead => currentHealth.Value <= 0f;

    public event Action<float, float> HealthChanged;
    public event Action Death;

    public override void OnNetworkSpawn()
    {
        currentHealth.OnValueChanged += OnHealthChanged;

        if (IsServer)
            currentHealth.Value = maxHealth;

        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
    }

    public void TakeDamageServer(float damage)
    {
        if (!IsServer || IsDead || damage <= 0f || deathHandled)
            return;

        currentHealth.Value = Mathf.Max(
            0f,
            currentHealth.Value - damage
        );

        Debug.Log(
            "[MECHA] " + name +
            " | Vida: " + CurrentHealth +
            "/" + MaxHealth
        );

        if (IsDead)
            HandleDeathServer();
    }

    public void HealServer(float amount)
    {
        if (!IsServer || IsDead || amount <= 0f)
            return;

        currentHealth.Value = Mathf.Min(
            maxHealth,
            currentHealth.Value + amount
        );
    }

    private void OnHealthChanged(float previous, float current)
    {
        HealthChanged?.Invoke(current, maxHealth);

        Debug.Log(
            "[MECHA] " + name +
            " | Vida atualizada: " + current
        );

        if (previous > 0f && current <= 0f)
            Death?.Invoke();
    }

    private void HandleDeathServer()
    {
        if (!IsServer || deathHandled)
            return;

        deathHandled = true;

        Debug.Log("[MECHA] " + name + " foi derrotado.");

        if (CompareTag("Enemy"))
        {
            Debug.Log("[MECHA] Inimigo destruído: " + name);

            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);

            return;
        }

        if (CompareTag("Player"))
        {
            Debug.Log("[MECHA] Jogador derrotado: " + name);

            ShowRespawnScreenClientRpc(
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { OwnerClientId }
                    }
                }
            );

            return;
        }

        Debug.LogWarning(
            "[MECHA] " + name +
            " morreu, mas sua tag não é Enemy nem Player."
        );
    }

    [ClientRpc]
    private void ShowRespawnScreenClientRpc(
        ClientRpcParams clientRpcParams = default)
    {
        RespawnScreen screen =
            FindFirstObjectByType<RespawnScreen>();

        if (screen != null)
        {
            screen.Show();
        }
        else
        {
            Debug.LogWarning(
                "[MECHA] RespawnScreen não encontrado na cena."
            );
        }
    }
}
