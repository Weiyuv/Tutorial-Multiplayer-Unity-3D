
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class MechaCombat : NetworkBehaviour
{
    [Header("Animacao")]
    [SerializeField] private Animator animator;

    [Header("Combate")]
    [SerializeField] private float attackCooldown = 0.6f;

    private float nextAttackTime;
    private bool isDefending;

    private static readonly int AttackTrigger =
        Animator.StringToHash("Attack");

    private static readonly int DefendBool =
        Animator.StringToHash("IsDefending");

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        enabled = IsOwner;
    }

    private void Update()
    {
        if (!IsOwner || Mouse.current == null || animator == null)
            return;

        HandleAttack();
        HandleDefense();
    }

    private void HandleAttack()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (Time.time < nextAttackTime || isDefending)
            return;

        nextAttackTime = Time.time + attackCooldown;

        animator.SetTrigger(AttackTrigger);
    }

    private void HandleDefense()
    {
        isDefending = Mouse.current.rightButton.isPressed;

        animator.SetBool(DefendBool, isDefending);
    }
}
