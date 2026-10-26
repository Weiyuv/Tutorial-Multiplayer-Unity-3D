
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
public class CrystalSwordAttack : NetworkBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkAnimator networkAnimator;
    [SerializeField] private string attackTrigger = "Attack";

    [Header("Attack")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float attackCooldown = 0.7f;
    [SerializeField] private float hitDelay = 0.25f;
    [SerializeField] private float hitRadius = 1.2f;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask targetLayers = ~0;

    private bool attacking;
    private float nextAttackTime;

    private readonly HashSet<ulong> hitTargets =
        new HashSet<ulong>();

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (networkAnimator == null)
            networkAnimator = GetComponent<NetworkAnimator>();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (attacking || Time.time < nextAttackTime)
            return;

        if (!HasSwordEquipped())
        {
            Debug.Log("[SWORD ATTACK] Nenhuma espada equipada.");
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        RequestAttackServerRpc();
    }

    private bool HasSwordEquipped()
    {
        Crystal[] crystals =
            FindObjectsByType<Crystal>(FindObjectsSortMode.None);

        foreach (Crystal crystal in crystals)
        {
            if (crystal == null ||
                !crystal.IsSpawned ||
                !crystal.IsHeld)
                continue;

            if (crystal.transform.IsChildOf(transform))
                return true;

            CrystalCollector collector =
                GetComponentInChildren<CrystalCollector>(true);

            if (collector != null &&
                crystal.transform.position != Vector3.zero)
            {
                return true;
            }
        }

        return false;
    }

    [ServerRpc]
    private void RequestAttackServerRpc(
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (attacking || !HasSwordEquipped())
            return;

        attacking = true;
        hitTargets.Clear();

        if (networkAnimator != null)
            networkAnimator.SetTrigger("Attack");
        else if (animator != null)
            animator.SetTrigger("Attack");

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(hitDelay);

        if (IsServer)
            PerformHit();

        yield return new WaitForSeconds(
            Mathf.Max(0.05f, attackCooldown - hitDelay)
        );

        attacking = false;
    }

    private void PerformHit()
    {
        if (!IsServer || !HasSwordEquipped())
            return;

        Vector3 center = attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * 1.2f
              + Vector3.up;

        Collider[] hits = Physics.OverlapSphere(
            center,
            hitRadius,
            targetLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            if (hit == null || hit.transform.IsChildOf(transform))
                continue;

            NetworkObject target =
                hit.GetComponentInParent<NetworkObject>();

            if (target == null ||
                target == NetworkObject ||
                !target.IsSpawned)
                continue;

            if (!hitTargets.Add(target.NetworkObjectId))
                continue;

            MechaHealth health =
                target.GetComponent<MechaHealth>();

            if (health != null)
                health.TakeDamageServer(damage);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * 1.2f
              + Vector3.up;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, hitRadius);
    }
}
