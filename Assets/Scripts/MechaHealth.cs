
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class MechaHealth : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Header("Death")]
    [SerializeField] private bool hidePlayerOnDeath = true;

    [Tooltip("Componentes de movimento e combate que serão desativados ao morrer.")]
    [SerializeField] private Behaviour[] disableOnDeath;

    private readonly NetworkVariable<float> currentHealth =
        new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly List<Renderer> playerRenderers =
        new List<Renderer>();

    private readonly List<Collider> playerColliders =
        new List<Collider>();

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool hasSpawnPosition;
    private bool deathHandled;
    private bool playerDisabled;

    public float CurrentHealth => currentHealth.Value;
    public float MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth <= 0f;

    public event Action<float, float> HealthChanged;
    public event Action Death;

    public override void OnNetworkSpawn()
    {
        currentHealth.OnValueChanged += OnHealthChanged;

        if (IsServer)
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            hasSpawnPosition = true;

            deathHandled = false;
            currentHealth.Value = maxHealth;
        }

        if (CompareTag("Player"))
            CachePlayerComponents();

        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void CachePlayerComponents()
    {
        playerRenderers.Clear();
        playerColliders.Clear();

        playerRenderers.AddRange(
            GetComponentsInChildren<Renderer>(true)
        );

        playerColliders.AddRange(
            GetComponentsInChildren<Collider>(true)
        );
    }

    public void TakeDamageServer(float damage)
    {
        if (!IsServer || IsDead || deathHandled || damage <= 0f)
            return;

        currentHealth.Value = Mathf.Max(
            0f,
            currentHealth.Value - damage
        );

        Debug.Log(
            $"[MECHA] {name} | Vida: {CurrentHealth}/{MaxHealth}"
        );

        if (IsDead)
            HandleDeathServer();
    }

    public void HealServer(float amount)
    {
        if (!IsServer || IsDead || deathHandled || amount <= 0f)
            return;

        currentHealth.Value = Mathf.Min(
            maxHealth,
            currentHealth.Value + amount
        );
    }

    private void OnHealthChanged(float previous, float current)
    {
        HealthChanged?.Invoke(current, maxHealth);

        if (previous > 0f && current <= 0f)
            Death?.Invoke();

        if (CompareTag("Player"))
        {
            if (current <= 0f)
                DisablePlayerLocally();
            else if (previous <= 0f && current > 0f)
                EnablePlayerLocally();
        }

        Debug.Log($"[MECHA] {name} | Vida atualizada: {current}/{maxHealth}");
    }

    private void HandleDeathServer()
    {
        if (!IsServer || deathHandled)
            return;

        deathHandled = true;

        if (CompareTag("Enemy"))
        {
            Debug.Log($"[MECHA] Inimigo destruído: {name}");

            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);

            return;
        }

        if (CompareTag("Player"))
        {
            Debug.Log($"[MECHA] Jogador derrotado: {name}");

            DisablePlayerClientRpc();
            ShowRespawnScreenClientRpc(CreateOwnerClientRpcParams());
            return;
        }

        Debug.LogWarning(
            $"[MECHA] {name} morreu, mas a tag precisa ser Player ou Enemy."
        );
    }

    private void DisablePlayerLocally()
    {
        if (playerDisabled)
            return;

        playerDisabled = true;

        if (playerRenderers.Count == 0 || playerColliders.Count == 0)
            CachePlayerComponents();

        if (hidePlayerOnDeath)
        {
            foreach (Renderer playerRenderer in playerRenderers)
            {
                if (playerRenderer != null)
                    playerRenderer.enabled = false;
            }
        }

        foreach (Collider playerCollider in playerColliders)
        {
            if (playerCollider != null)
                playerCollider.enabled = false;
        }

        CharacterController characterController =
            GetComponent<CharacterController>();

        if (characterController != null)
            characterController.enabled = false;

        if (disableOnDeath != null)
        {
            foreach (Behaviour component in disableOnDeath)
            {
                if (component != null && component != this)
                    component.enabled = false;
            }
        }
    }

    private void EnablePlayerLocally()
    {
        if (!playerDisabled)
            return;

        playerDisabled = false;

        foreach (Renderer playerRenderer in playerRenderers)
        {
            if (playerRenderer != null)
                playerRenderer.enabled = true;
        }

        foreach (Collider playerCollider in playerColliders)
        {
            if (playerCollider != null)
                playerCollider.enabled = true;
        }

        if (disableOnDeath != null)
        {
            foreach (Behaviour component in disableOnDeath)
            {
                if (component != null && component != this)
                    component.enabled = true;
            }
        }

        CharacterController characterController =
            GetComponent<CharacterController>();

        if (characterController != null)
            characterController.enabled = true;
    }

    [ClientRpc]
    private void DisablePlayerClientRpc()
    {
        if (CompareTag("Player"))
            DisablePlayerLocally();
    }

    public void RequestRespawn()
    {
        if (!IsOwner || !IsSpawned)
        {
            Debug.LogWarning(
                "[RESPAWN] O jogador local não possui este objeto."
            );
            return;
        }

        RequestRespawnServerRpc();
    }

    [ServerRpc]
    private void RequestRespawnServerRpc(
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (!CompareTag("Player"))
            return;

        if (!deathHandled || !IsDead || !hasSpawnPosition)
        {
            Debug.LogWarning(
                "[RESPAWN] Solicitação recusada: estado inválido."
            );
            return;
        }

        RespawnPlayerServer();
    }

    private void RespawnPlayerServer()
    {
        if (!IsServer)
            return;

        transform.SetPositionAndRotation(
            spawnPosition,
            spawnRotation
        );

        currentHealth.Value = maxHealth;
        deathHandled = false;

        Debug.Log(
            $"[RESPAWN] {name} reapareceu com {maxHealth} de vida."
        );

        RespawnedClientRpc(CreateOwnerClientRpcParams());
    }

    private ClientRpcParams CreateOwnerClientRpcParams()
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };
    }

    [ClientRpc]
    private void ShowRespawnScreenClientRpc(
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner)
            return;

        RespawnScreen screen =
            FindFirstObjectByType<RespawnScreen>();

        if (screen != null)
            screen.Show();
        else
            Debug.LogError("[RESPAWN] RespawnScreen não encontrado na cena.");
    }

    [ClientRpc]
    private void RespawnedClientRpc(
        ClientRpcParams clientRpcParams = default)
    {
        if (CompareTag("Player"))
            EnablePlayerLocally();

        if (!IsOwner)
            return;

        RespawnScreen screen =
            FindFirstObjectByType<RespawnScreen>();

        if (screen != null)
        {
            screen.Hide();
            screen.EnableRespawnButton();
        }
    }
}
