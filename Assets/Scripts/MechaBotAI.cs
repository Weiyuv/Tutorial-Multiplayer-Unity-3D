
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NavMeshAgent))]
public class MechaBotAI : NetworkBehaviour
{
    [Header("Referencias")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkAnimator networkAnimator;

    [Header("Stun")]
    [SerializeField, Min(0.1f)] private float stunDuration = 1.2f;

    private bool isStunned;
    private Coroutine stunCoroutine;

    public bool IsStunned => isStunned;

    [Header("Animacao Strafe")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string strafeXParameter = "MoveX";
    [SerializeField] private string strafeYParameter = "MoveY";
    [SerializeField] private float animationDampTime = 0.1f;

    [Header("Combate")]
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float attackStartDelay = 0.2f;
    [SerializeField] private float attackRecovery = 0.3f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float attackAngle = 70f;
    [SerializeField] private int maxSimultaneousAttackers = 1;

    [Header("Perseguicao")]
    [SerializeField] private float targetUpdateInterval = 0.3f;
    [SerializeField] private float predictionTime = 0.5f;
    [SerializeField] private float stoppingDistance = 2f;
    [SerializeField] private float loseTargetDistance = 35f;

    [Header("Taticas de Flanqueamento")]
    [SerializeField] private float flankDistance = 4.5f;
    [SerializeField] private float flankUpdateInterval = 1.5f;
    [SerializeField] private float preferredAngleWeight = 2f;
    [SerializeField] private int flankPositionSamples = 12;

    [Header("Separacao entre Bots")]
    [SerializeField] private float separationRadius = 3.5f;
    [SerializeField] private float separationWeight = 2f;

    [Header("Hit and Run")]
    [SerializeField] private float retreatDistance = 4f;
    [SerializeField] private float disengageDuration = 1.5f;
    [SerializeField] private float retreatSpeed = 4.5f;

    [Header("Velocidade")]
    [SerializeField] private float combatMovementSpeed = 3f;

    private Transform currentTarget;
    private MechaHealth targetHealth;

    private Vector3 lastTargetPosition;
    private Vector3 targetVelocity;

    private MechaBotAI[] nearbyBots;

    private float configuredSpeed = 3.5f;
    private float configuredCooldown = 1.5f;

    private float nextTargetUpdate;
    private float nextBotRefresh;
    private float nextAttackTime;
    private float nextFlankUpdate;
    private float disengageUntil;
    private float preferredAngle;

    private Vector3 flankDestination;

    private bool isAttacking;
    private bool isDisengaging;

    private Coroutine attackCoroutine;

    private enum TacticalState
    {
        Pursuit,
        Flanking,
        Attacking,
        Disengaging
    }

    private TacticalState currentState;

    public void ConfigureServer(bool armed, float movementSpeed, float cooldown)
    {
        configuredSpeed = movementSpeed;
        configuredCooldown = cooldown;
    }

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (networkAnimator == null)
            networkAnimator = GetComponentInChildren<NetworkAnimator>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            agent.enabled = true;
            agent.speed = configuredSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.updateRotation = false;
            agent.isStopped = false;

            preferredAngle = Random.Range(0f, 360f);
            nextAttackTime = Time.time + Random.Range(0.5f, 2f);
            nextFlankUpdate = Time.time + Random.Range(0f, 1f);

            currentState = TacticalState.Pursuit;
        }
        else
        {
            agent.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer)
            return;

        if (isStunned)
        {
            StopAgentMovement();
            return;
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (Time.time >= nextTargetUpdate)
        {
            FindTarget();
            nextTargetUpdate = Time.time + targetUpdateInterval;
        }

        if (Time.time >= nextBotRefresh)
        {
            RefreshNearbyBots();
            nextBotRefresh = Time.time + 0.75f;
        }

        if (currentTarget == null || targetHealth == null || targetHealth.IsDead)
        {
            StopMovement();
            UpdateAnimation();
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            currentTarget.position
        );

        if (distance > loseTargetDistance)
        {
            StopMovement();
            UpdateAnimation();
            return;
        }

        UpdateTargetVelocity();
        RotateTowardsTarget();
        UpdateTactics(distance);
        UpdateAnimation();
    }

    public void StunServer()
    {
        StunServer(stunDuration);
    }

    public void StunServer(float duration)
    {
        if (!IsServer || !IsSpawned || duration <= 0f)
            return;

        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
            stunCoroutine = null;
        }

        isStunned = true;

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        isAttacking = false;
        isDisengaging = false;
        currentState = TacticalState.Pursuit;

        StopAgentMovement();

        if (animator != null)
        {
            animator.SetFloat(speedParameter, 0f);
            animator.SetFloat(strafeXParameter, 0f);
            animator.SetFloat(strafeYParameter, 0f);
        }

        stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (!IsServer || !IsSpawned)
            yield break;

        stunCoroutine = null;

        MechaHealth health = GetComponent<MechaHealth>();

        if (health != null && health.IsDead)
            yield break;

        isStunned = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;

        nextTargetUpdate = 0f;
        nextFlankUpdate = 0f;
    }

    private void StopAgentMovement()
    {
        if (agent == null || !agent.enabled)
            return;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    private void FindTarget()
    {
        if (NetworkManager.Singleton == null)
            return;

        float closestDistance = float.MaxValue;

        Transform closestTarget = null;
        MechaHealth closestHealth = null;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            MechaHealth health = client.PlayerObject.GetComponent<MechaHealth>();

            if (health == null || health.IsDead)
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

        if (currentTarget != closestTarget)
        {
            currentTarget = closestTarget;
            targetHealth = closestHealth;
            targetVelocity = Vector3.zero;

            if (currentTarget != null)
                lastTargetPosition = currentTarget.position;

            isDisengaging = false;
        }
    }

    private void UpdateTargetVelocity()
    {
        if (currentTarget == null)
            return;

        Vector3 newPosition = currentTarget.position;

        targetVelocity = (newPosition - lastTargetPosition) /
                         Mathf.Max(Time.deltaTime, 0.001f);

        targetVelocity = Vector3.ClampMagnitude(targetVelocity, 10f);
        lastTargetPosition = newPosition;
    }

    private void RefreshNearbyBots()
    {
        nearbyBots = FindObjectsByType<MechaBotAI>(
            FindObjectsSortMode.None
        );
    }

    private void UpdateTactics(float distance)
    {
        if (isAttacking)
        {
            currentState = TacticalState.Attacking;
            agent.isStopped = true;
            return;
        }

        if (isDisengaging)
        {
            if (Time.time < disengageUntil)
            {
                currentState = TacticalState.Disengaging;
                PerformRetreat();
                return;
            }

            isDisengaging = false;
            nextFlankUpdate = 0f;
        }

        if (distance > flankDistance + 2f)
        {
            currentState = TacticalState.Pursuit;
            ChaseTarget();
            return;
        }

        currentState = TacticalState.Flanking;

        if (Time.time >= nextFlankUpdate)
        {
            CalculateFlankPosition();
            nextFlankUpdate = Time.time + flankUpdateInterval;
        }

        if (distance <= attackRange &&
            Time.time >= nextAttackTime &&
            CanAttack())
        {
            currentState = TacticalState.Attacking;
            attackCoroutine = StartCoroutine(AttackRoutine());
            return;
        }

        MoveToFlankPosition();
    }

    private void ChaseTarget()
    {
        Vector3 predictedPosition = currentTarget.position +
                                    targetVelocity * predictionTime;

        Vector3 destination = predictedPosition + CalculateSeparation();

        agent.speed = configuredSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    private void CalculateFlankPosition()
    {
        if (currentTarget == null)
            return;

        float bestScore = float.MinValue;
        Vector3 bestPosition = transform.position;

        int samples = Mathf.Max(4, flankPositionSamples);

        for (int i = 0; i < samples; i++)
        {
            float angle = preferredAngle + (360f / samples) * i;
            float radians = angle * Mathf.Deg2Rad;

            Vector3 direction = new Vector3(
                Mathf.Sin(radians),
                0f,
                Mathf.Cos(radians)
            );

            Vector3 candidate = currentTarget.position +
                                direction * flankDistance;

            if (!NavMesh.SamplePosition(
                candidate,
                out NavMeshHit hit,
                2f,
                NavMesh.AllAreas))
                continue;

            float score = EvaluateFlankPosition(hit.position);

            if (score > bestScore)
            {
                bestScore = score;
                bestPosition = hit.position;
            }
        }

        flankDestination = bestPosition;
    }

    private float EvaluateFlankPosition(Vector3 position)
    {
        float score = 0f;
        float nearestBotDistance = separationRadius * 2f;

        if (nearbyBots != null)
        {
            foreach (MechaBotAI bot in nearbyBots)
            {
                if (bot == null || bot == this || !bot.IsSpawned)
                    continue;

                float distance = Vector3.Distance(
                    position,
                    bot.transform.position
                );

                if (distance < nearestBotDistance)
                    nearestBotDistance = distance;
            }
        }

        score += nearestBotDistance * 3f;

        float distanceToTarget = Vector3.Distance(
            position,
            currentTarget.position
        );

        score -= Mathf.Abs(distanceToTarget - flankDistance) * 2f;

        Vector3 candidateDirection = position - currentTarget.position;
        candidateDirection.y = 0f;

        Vector3 preferredDirection = new Vector3(
            Mathf.Sin(preferredAngle * Mathf.Deg2Rad),
            0f,
            Mathf.Cos(preferredAngle * Mathf.Deg2Rad)
        );

        score += Vector3.Dot(
            candidateDirection.normalized,
            preferredDirection
        ) * preferredAngleWeight;

        return score;
    }

    private void MoveToFlankPosition()
    {
        Vector3 destination = flankDestination + CalculateSeparation();

        if (NavMesh.SamplePosition(
            destination,
            out NavMeshHit hit,
            1.5f,
            NavMesh.AllAreas))
        {
            destination = hit.position;
        }
        else
        {
            destination = flankDestination;
        }

        agent.speed = combatMovementSpeed;
        agent.stoppingDistance = 0.5f;
        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    private Vector3 CalculateSeparation()
    {
        Vector3 separation = Vector3.zero;

        if (nearbyBots == null)
            return separation;

        int neighbors = 0;

        foreach (MechaBotAI bot in nearbyBots)
        {
            if (bot == null || bot == this || !bot.IsSpawned)
                continue;

            Vector3 difference = transform.position - bot.transform.position;
            difference.y = 0f;

            float distance = difference.magnitude;

            if (distance <= 0.01f || distance >= separationRadius)
                continue;

            separation += difference.normalized / distance;
            neighbors++;
        }

        if (neighbors > 0)
        {
            separation /= neighbors;
            separation = Vector3.ClampMagnitude(separation, 1f);
        }

        return separation * separationWeight;
    }

    private bool CanAttack()
    {
        if (nearbyBots == null)
            return true;

        int attackers = 0;

        foreach (MechaBotAI bot in nearbyBots)
        {
            if (bot == null || bot == this || !bot.IsSpawned)
                continue;

            if (bot.currentTarget != currentTarget)
                continue;

            if (bot.isAttacking)
                attackers++;
        }

        return attackers < maxSimultaneousAttackers;
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        nextAttackTime = Time.time + configuredCooldown;

        agent.isStopped = true;

        if (networkAnimator != null)
            networkAnimator.SetTrigger("Attack");
        else if (animator != null)
            animator.SetTrigger("Attack");

        yield return new WaitForSeconds(attackStartDelay);

        if (isStunned)
            yield break;

        if (currentTarget != null &&
            targetHealth != null &&
            !targetHealth.IsDead)
        {
            float distance = Vector3.Distance(
                transform.position,
                currentTarget.position
            );

            Vector3 direction = currentTarget.position - transform.position;
            direction.y = 0f;

            float angle = Vector3.Angle(transform.forward, direction);

            if (distance <= attackRange + 0.5f && angle <= attackAngle)
                targetHealth.TakeDamageServer(attackDamage);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, attackRecovery));

        if (isStunned)
            yield break;

        isAttacking = false;
        attackCoroutine = null;

        isDisengaging = true;
        disengageUntil = Time.time + disengageDuration;

        preferredAngle += Random.Range(90f, 180f);
        preferredAngle %= 360f;

        nextFlankUpdate = 0f;
    }

    private void PerformRetreat()
    {
        if (currentTarget == null)
            return;

        Vector3 awayDirection = transform.position - currentTarget.position;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude < 0.01f)
        {
            awayDirection = -transform.forward;
            awayDirection.y = 0f;
        }

        awayDirection.Normalize();

        Vector3 lateral = Vector3.Cross(Vector3.up, awayDirection);
        float side = preferredAngle % 2f > 1f ? 1f : -1f;

        Vector3 retreatDirection =
            (awayDirection + lateral * side * 0.5f).normalized;

        Vector3 destination = transform.position +
                              retreatDirection * retreatDistance;

        if (NavMesh.SamplePosition(
            destination,
            out NavMeshHit hit,
            3f,
            NavMesh.AllAreas))
        {
            destination = hit.position;
        }

        agent.speed = retreatSpeed;
        agent.stoppingDistance = 0f;
        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    private void RotateTowardsTarget()
    {
        if (currentTarget == null)
            return;

        Vector3 direction = currentTarget.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void UpdateAnimation()
    {
        if (animator == null)
            return;

        Vector3 localVelocity = Vector3.zero;

        if (agent != null &&
            agent.enabled &&
            agent.isOnNavMesh &&
            !agent.isStopped)
        {
            localVelocity = transform.InverseTransformDirection(agent.velocity);
        }

        float currentSpeed = localVelocity.magnitude;
        float referenceSpeed = Mathf.Max(agent.speed, 0.01f);

        float normalizedX = Mathf.Clamp(
            localVelocity.x / referenceSpeed,
            -1f,
            1f
        );

        float normalizedY = Mathf.Clamp(
            localVelocity.z / referenceSpeed,
            -1f,
            1f
        );

        animator.SetFloat(
            speedParameter,
            currentSpeed,
            animationDampTime,
            Time.deltaTime
        );

        animator.SetFloat(
            strafeXParameter,
            normalizedX,
            animationDampTime,
            Time.deltaTime
        );

        animator.SetFloat(
            strafeYParameter,
            normalizedY,
            animationDampTime,
            Time.deltaTime
        );
    }

    private void StopMovement()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        if (stunCoroutine != null)
            StopCoroutine(stunCoroutine);

        attackCoroutine = null;
        stunCoroutine = null;

        isAttacking = false;
        isDisengaging = false;
        isStunned = false;
    }

    private void OnDisable()
    {
        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        if (stunCoroutine != null)
            StopCoroutine(stunCoroutine);

        attackCoroutine = null;
        stunCoroutine = null;

        isAttacking = false;
        isDisengaging = false;
        isStunned = false;
    }
}
