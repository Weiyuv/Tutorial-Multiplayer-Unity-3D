
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class MechaCombat : NetworkBehaviour
{
    [Header("Animacao")]
    [SerializeField] private Animator animator;

    [Header("Combate")]
    [SerializeField] private float attackCooldown = 0.6f;
    [SerializeField] private float attackDuration = 0.8f;

    private MechaMovement movement;
    private float nextAttackTime;
    private float attackEndTime;
    private bool isAttacking;
    private bool isDefending;

    private Vector3 lockedPosition;
    private Quaternion lockedRotation;

    private static readonly int AttackTrigger =
        Animator.StringToHash("Attack");

    private static readonly int DefendBool =
        Animator.StringToHash("IsDefending");

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        movement = GetComponent<MechaMovement>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    public override void OnNetworkSpawn()
    {
        enabled = IsOwner;
    }

    private void Update()
    {
        if (!IsOwner || Mouse.current == null || animator == null)
            return;

        if (isAttacking)
        {
            if (Time.time >= attackEndTime)
            {
                isAttacking = false;

                if (movement != null)
                    movement.SetMovementLocked(false);
            }
        }

        if (Mouse.current.leftButton.wasPressedThisFrame &&
            !isAttacking &&
            !isDefending &&
            Time.time >= nextAttackTime)
        {
            StartAttack();
        }

        isDefending = Mouse.current.rightButton.isPressed && !isAttacking;
        animator.SetBool(DefendBool, isDefending);
    }

    private void StartAttack()
    {
        isAttacking = true;
        attackEndTime = Time.time + attackDuration;
        nextAttackTime = Time.time + attackCooldown;

        lockedPosition = transform.position;
        lockedRotation = transform.rotation;

        if (movement != null)
            movement.SetMovementLocked(true);

        animator.ResetTrigger(AttackTrigger);
        animator.SetTrigger(AttackTrigger);
    }

    private void LateUpdate()
    {
        if (!IsOwner || !isAttacking)
            return;

        transform.SetPositionAndRotation(lockedPosition, lockedRotation);
    }

    public override void OnNetworkDespawn()
    {
        UnlockMovement();
    }

    private void OnDisable()
    {
        UnlockMovement();
    }

    private void UnlockMovement()
    {
        isAttacking = false;

        if (movement != null)
            movement.SetMovementLocked(false);
    }
}
