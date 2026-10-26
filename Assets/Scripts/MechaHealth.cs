
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

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            currentHealth.Value = maxHealth;

        currentHealth.OnValueChanged += OnHealthChanged;
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

    private void OnHealthChanged(float previous, float current)
    {
        Debug.Log(
            "[MECHA] " + name +
            " | Vida atualizada: " + current
        );
    }

    private void OnDeath()
    {
        Debug.Log("[MECHA] " + name + " foi derrotado.");

        // Adicione aqui a lógica de morte ou respawn.
    }
}
