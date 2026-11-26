
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

    [Header("Damage Flash")]
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField, Min(1)] private int flashCount = 3;
    [SerializeField, Min(0.01f)] private float flashInterval = 0.08f;

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

    private readonly List<Collider> enemyColliders =
        new List<Collider>();

    private class RendererFlashData
    {
        public Renderer renderer;
        public int materialIndex;
        public MaterialPropertyBlock originalBlock;
        public MaterialPropertyBlock flashBlock;
        public string colorProperty;
        public Color originalColor;
    }

    private readonly List<RendererFlashData> flashData =
        new List<RendererFlashData>();

    private Coroutine flashCoroutine;

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

        HealthChanged?.Invoke(CurrentHealth, CurrentHealth);
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;

        StopDamageFlash();
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

        DamageFlashClientRpc();

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

    [ClientRpc]
    private void DamageFlashClientRpc()
    {
        StartDamageFlash();
    }

    private void StartDamageFlash()
    {
        StopDamageFlash();

        flashCoroutine = StartCoroutine(DamageFlashRoutine());
    }

    private IEnumerator DamageFlashRoutine()
    {
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>(true);

        flashData.Clear();

        foreach (Renderer rend in renderers)
        {
            if (rend == null)
                continue;

            Material[] materials = rend.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];

                if (material == null)
                    continue;

                string colorProperty = null;

                if (material.HasProperty("_BaseColor"))
                    colorProperty = "_BaseColor";
                else if (material.HasProperty("_Color"))
                    colorProperty = "_Color";

                if (string.IsNullOrEmpty(colorProperty))
                    continue;

                RendererFlashData data = new RendererFlashData
                {
                    renderer = rend,
                    materialIndex = i,
                    colorProperty = colorProperty,
                    originalColor = material.GetColor(colorProperty),
                    originalBlock = new MaterialPropertyBlock(),
                    flashBlock = new MaterialPropertyBlock()
                };

                rend.GetPropertyBlock(
                    data.originalBlock,
                    i
                );

                rend.GetPropertyBlock(
                    data.flashBlock,
                    i
                );

                flashData.Add(data);
            }
        }

        for (int i = 0; i < flashCount; i++)
        {
            ApplyFlashColor();

            yield return new WaitForSecondsRealtime(
                flashInterval
            );

            RestoreFlashColor();

            if (i < flashCount - 1)
            {
                yield return new WaitForSecondsRealtime(
                    flashInterval
                );
            }
        }

        RestoreFlashColor();

        flashCoroutine = null;
        flashData.Clear();
    }

    private void ApplyFlashColor()
    {
        foreach (RendererFlashData data in flashData)
        {
            if (data.renderer == null)
                continue;

            data.flashBlock.SetColor(
                data.colorProperty,
                flashColor
            );

            data.renderer.SetPropertyBlock(
                data.flashBlock,
                data.materialIndex
            );
        }
    }

    private void RestoreFlashColor()
    {
        foreach (RendererFlashData data in flashData)
        {
            if (data.renderer == null)
                continue;

            data.renderer.SetPropertyBlock(
                data.originalBlock,
                data.materialIndex
            );
        }
    }

    private void StopDamageFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        RestoreFlashColor();
        flashData.Clear();
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
        HealthChanged?.Invoke(previous, current);

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

        DisableEnemyCollidersClientRpc();

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

    [ClientRpc]
    private void DisableEnemyCollidersClientRpc()
    {
        if (!CompareTag("Enemy"))
            return;

        enemyColliders.Clear();

        enemyColliders.AddRange(
            GetComponentsInChildren<Collider>(true)
        );

        foreach (Collider enemyCollider in enemyColliders)
        {
            if (enemyCollider != null)
                enemyCollider.enabled = false;
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
