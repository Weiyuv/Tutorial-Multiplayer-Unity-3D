
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NavMeshAgent))]
public class MechaBotAI : NetworkBehaviour
{
    [Header("Referências")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkAnimator networkAnimator;
    [SerializeField] private SwordDamage swordDamage;
    [SerializeField] private GameObject swordVisual;

    [Header("Combate")]
    [SerializeField] private float attackRange = 2.2f;
    [SerializeField] private float attackDuration = 0.4f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Alvo")]
    [SerializeField] private float targetUpdateInterval = 0.5f;

    public NetworkVariable<bool> HasSword = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Transform currentTarget;
    private MechaHealth targetHealth;

    private bool configured;
    private bool armedConfiguration;
    private float configuredSpeed = 3.5f;
    private float configuredCooldown = 1.1f;

    private float nextTargetUpdate;
    private float nextAttackTime;
    private bool isAttacking;

    public void ConfigureServer(
        bool armed,
        float movementSpeed,
        float attackCooldown)
    {
        armedConfiguration = armed;
        configuredSpeed = movementSpeed;
        configuredCooldown = attackCooldown;
        configured = true;
    }

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (networkAnimator == null)
            networkAnimator = GetComponentInChildren<NetworkAnimator>();

        if (swordDamage == null)
            swordDamage = GetComponentInChildren<SwordDamage>();

        if (swordVisual == null && swordDamage != null)
            swordVisual = swordDamage.gameObject;

        if (animator != null)
            animator.applyRootMotion = false;
    }

    public override void OnNetworkSpawn()
    {
        HasSword.OnValueChanged += OnSwordChanged;

        if (IsServer)
        {
            if (!configured)
            {
                armedConfiguration = swordVisual != null;
            }

            HasSword.Value = armedConfiguration;

            if (agent != null)
            {
                agent.enabled = true;
                agent.speed = configuredSpeed;
                agent.isStopped = false;
                agent.updateRotation = false;
            }

            nextAttackTime = Time.time + 1f;
        }
        else
        {
            if (agent != null)
                agent.enabled = false;
        }

        ApplySwordState(HasSword.Value);
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer)
            return;

        if (agent == null || !agent.enabled)
            return;

        if (Time.time >= nextTargetUpdate)
        {
            FindTarget();
            nextTargetUpdate = Time.time + targetUpdateInterval;
        }

        if (currentTarget == null || targetHealth == null ||
            targetHealth.IsDead)
        {
            agent.isStopped = true;
            UpdateAnimation();
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            currentTarget.position
        );

        RotateTowardsTarget();

        if (HasSword.Value && distance <= attackRange)
        {
            agent.isStopped = true;

            if (!isAttacking && Time.time >= nextAttackTime)
                StartCoroutine(AttackRoutine());
        }
        else
        {
            if (!HasSword.Value && distance <= attackRange)
                agent.isStopped = true;
            else
            {
                agent.isStopped = false;
                agent.SetDestination(currentTarget.position);
            }
        }

        UpdateAnimation();
    }

    private void FindTarget()
    {
        if (NetworkManager.Singleton == null)
            return;

        float closestDistance = float.MaxValue;
        Transform closestTarget = null;
        MechaHealth closestHealth = null;

        foreach (NetworkClient client in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            MechaHealth health =
                client.PlayerObject.GetComponent<MechaHealth>();

            if (health != null && health.IsDead)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                client.PlayerObject.transform.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = client.PlayerObject.transform;
                closestHealth = health;
            }
        }

        currentTarget = closestTarget;
        targetHealth = closestHealth;
    }

    private void RotateTowardsTarget()
    {
        if (currentTarget == null)
            return;

        Vector3 direction =
            currentTarget.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        nextAttackTime = Time.time + configuredCooldown;

        if (networkAnimator != null)
            networkAnimator.SetTrigger("Attack");
        else if (animator != null)
            animator.SetTrigger("Attack");

        if (swordDamage != null && HasSword.Value)
            swordDamage.SetAttacking(true);

        yield return new WaitForSeconds(attackDuration);

        if (swordDamage != null)
            swordDamage.SetAttacking(false);

        isAttacking = false;
    }

    private void UpdateAnimation()
    {
        if (animator == null)
            return;

        float speed = 0f;

        if (agent != null && agent.enabled && !agent.isStopped)
            speed = agent.velocity.magnitude;

        animator.SetFloat("Speed", speed);
    }

    private void OnSwordChanged(bool previous, bool current)
    {
        ApplySwordState(current);
    }

    private void ApplySwordState(bool armed)
    {
        if (swordVisual != null)
            swordVisual.SetActive(armed);

        if (swordDamage != null && !IsServer)
            swordDamage.SetAttacking(false);
    }

    public override void OnNetworkDespawn()
    {
        HasSword.OnValueChanged -= OnSwordChanged;

        if (swordDamage != null && IsServer)
            swordDamage.SetAttacking(false);
    }

    private void OnDisable()
    {
        if (swordDamage != null)
            swordDamage.SetAttacking(false);
    }
}
