
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
public class MechaHealth : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Header("Animations")]
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkAnimator networkAnimator;
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField] private string deathTrigger = "Death";

    [Header("Enemy Death")]
    [SerializeField, Min(0f)] private float enemyDespawnDelay = 3f;

    [Header("Player Death")]
    [SerializeField] private bool hidePlayerOnDeath = true;

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

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (networkAnimator == null)
            networkAnimator = GetComponent<NetworkAnimator>();

        if (networkAnimator == null)
            networkAnimator = GetComponentInChildren<NetworkAnimator>(true);
    }

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

    private void PlayAnimationServer(string triggerName)
    {
        if (!IsServer || string.IsNullOrEmpty(triggerName))
            return;

        if (networkAnimator != null)
        {
            networkAnimator.SetTrigger(triggerName);
        }
        else if (animator != null)
        {
            animator.SetTrigger(triggerName);
        }
    }

    public void TakeDamageServer(float damage)
    {
        if (!IsServer || IsDead || deathHandled || damage <= 0f)
            return;

        float newHealth = Mathf.Max(
            0f,
            currentHealth.Value - damage
        );

        if (newHealth > 0f)
        {
            PlayAnimationServer(hitTrigger);

            if (CompareTag("Enemy"))
            {
                MechaBotAI botAI = GetComponent<MechaBotAI>();

                if (botAI == null)
                    botAI = GetComponentInChildren<MechaBotAI>();

                if (botAI != null)
                    botAI.StunServer();
            }
        }

        currentHealth.Value = newHealth;

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
    }

    private void HandleDeathServer()
    {
        if (!IsServer || deathHandled)
            return;

        deathHandled = true;

        if (CompareTag("Enemy"))
        {
            HandleEnemyDeathServer();
            return;
        }

        if (CompareTag("Player"))
        {
            HandlePlayerDeathServer();
            return;
        }

        Debug.LogWarning(
            $"[MECHA] {name} morreu, mas precisa ter a tag Player ou Enemy."
        );
    }

    private void HandleEnemyDeathServer()
    {
        StopEnemyMovement();
        DisableDeathComponents();
        PlayAnimationServer(deathTrigger);

        StartCoroutine(DespawnEnemyAfterAnimation());
    }

    private void StopEnemyMovement()
    {
        NavMeshAgent agent = GetComponentInChildren<NavMeshAgent>(true);

        if (agent == null)
            return;

        if (agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            agent.enabled = false;
        }
    }

    private void DisableDeathComponents()
    {
        if (disableOnDeath == null)
            return;

        foreach (Behaviour component in disableOnDeath)
        {
            if (component == null ||
                component == this ||
                component == animator ||
                component == networkAnimator)
                continue;

            component.enabled = false;
        }
    }

    private IEnumerator DespawnEnemyAfterAnimation()
    {
        yield return new WaitForSeconds(enemyDespawnDelay);

        if (!IsServer)
            yield break;

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    private void HandlePlayerDeathServer()
    {
        DisablePlayerClientRpc();

        ShowRespawnScreenClientRpc(
            CreateOwnerClientRpcParams()
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

        DisableDeathComponents();
    }

    private void EnablePlayerLocally()
    {
        if (!playerDisabled)
            return;

        playerDisabled = false;

        if (hidePlayerOnDeath)
        {
            foreach (Renderer playerRenderer in playerRenderers)
            {
                if (playerRenderer != null)
                    playerRenderer.enabled = true;
            }
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
                if (component == null ||
                    component == this ||
                    component == animator ||
                    component == networkAnimator)
                    continue;

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
            return;

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
            return;

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

        RespawnedClientRpc(
            CreateOwnerClientRpcParams()
        );
    }

    private ClientRpcParams CreateOwnerClientRpcParams()
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[]
                {
                    OwnerClientId
                }
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
            Debug.LogError(
                "[RESPAWN] RespawnScreen não encontrado na cena."
            );
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
