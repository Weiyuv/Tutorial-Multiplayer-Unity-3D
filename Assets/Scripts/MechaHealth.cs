
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

    public float CurrentHealth => currentHealth.Value;
    public float MaxHealth => maxHealth;
    public bool IsDead => currentHealth.Value <= 0f;

    public event Action<float, float> HealthChanged;
    public event Action Death;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            currentHealth.Value = maxHealth;

        currentHealth.OnValueChanged += OnHealthChanged;

        HealthChanged?.Invoke(CurrentHealth, MaxHealth);

        if (IsDead)
            Death?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
    }

    public void TakeDamageServer(float damage)
    {
        if (!IsServer || IsDead || damage <= 0f)
            return;

        currentHealth.Value = Mathf.Max(
            0f,
            currentHealth.Value - damage
        );

        Debug.Log(
            "[MECHA] " + name +
            " | Vida: " + currentHealth.Value +
            "/" + maxHealth
        );

        if (IsDead)
            OnDeath();
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
        Debug.Log(
            "[MECHA] " + name +
            " | Vida atualizada: " + current
        );

        HealthChanged?.Invoke(current, maxHealth);

        if (previous > 0f && current <= 0f)
            Death?.Invoke();
    }

    private void OnDeath()
    {
        Debug.Log("[MECHA] " + name + " foi derrotado.");

        // Adicione aqui a logica de morte ou respawn.
    }
}
