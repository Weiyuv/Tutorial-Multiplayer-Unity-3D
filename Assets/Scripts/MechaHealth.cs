
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
            Debug.LogWarning(
                "[MECHA] NetworkAnimator não encontrado em " + name +
                ". A animação será executada apenas localmente."
            );

            animator.SetTrigger(triggerName);
        }
        else
        {
            Debug.LogWarning(
                "[MECHA] Animator não encontrado em " + name
            );
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
            PlayAnimationServer(hitTrigger);

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

        Debug.Log(
            $"[MECHA] {name} | Vida atualizada: {current}/{maxHealth}"
        );
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
            $"[MECHA] {name} morreu, mas a tag precisa ser Player ou Enemy."
        );
    }

    private void HandleEnemyDeathServer()
    {
        Debug.Log(
            $"[MECHA] Inimigo derrotado: {name}. " +
            $"Despawn em {enemyDespawnDelay} segundos."
        );

        StopEnemyMovement();

        DisableDeathComponents();

        // Mantém Animator e NetworkAnimator ativos.
        PlayAnimationServer(deathTrigger);

        StartCoroutine(DespawnEnemyAfterAnimation());
    }

    private void StopEnemyMovement()
    {
        // Procura o NavMeshAgent no objeto ou em seus filhos.
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
            if (component == null)
                continue;

            if (component == this)
                continue;

            // Não desativa a animação de morte.
            if (component == animator)
                continue;

            if (component == networkAnimator)
                continue;

            component.enabled = false;

            Debug.Log(
                $"[MECHA] Componente desativado pela morte: {component.GetType().Name}"
            );
        }
    }

    private IEnumerator DespawnEnemyAfterAnimation()
    {
        yield return new WaitForSeconds(enemyDespawnDelay);

        if (!IsServer)
            yield break;

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            Debug.Log(
                $"[MECHA] Despawn do inimigo: {name}"
            );

            NetworkObject.Despawn(true);
        }
    }

    private void HandlePlayerDeathServer()
    {
        Debug.Log(
            $"[MECHA] Jogador derrotado: {name}"
        );

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
                if (component == null || component == this)
                    continue;

                if (component == animator || component == networkAnimator)
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
