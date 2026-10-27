
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
    [SerializeField] private LayerMask targetLayers = ~0;

    [Header("Attack Collider")]
    [SerializeField] private Collider attackCollider;

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

        if (attackCollider != null)
        {
            attackCollider.isTrigger = true;
            attackCollider.enabled = false;
        }
        else
        {
            Debug.LogError(
                "[SWORD ATTACK] Arraste o Collider da área de ataque para Attack Collider.",
                this
            );
        }
    }

    public override void OnNetworkSpawn()
    {
        if (attackCollider != null)
            attackCollider.enabled = false;
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
                return true;
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
            networkAnimator.SetTrigger(attackTrigger);
        else if (animator != null)
            animator.SetTrigger(attackTrigger);

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(hitDelay);

        if (IsServer && HasSwordEquipped())
        {
            PerformHit();
        }

        yield return new WaitForSeconds(
            Mathf.Max(0.05f, attackCooldown - hitDelay)
        );

        if (attackCollider != null)
            attackCollider.enabled = false;

        attacking = false;
    }

    private void PerformHit()
    {
        if (!IsServer || !HasSwordEquipped())
            return;

        if (attackCollider == null)
        {
            Debug.LogError("[SWORD ATTACK] Attack Collider não configurado.");
            return;
        }

        attackCollider.enabled = true;

        Bounds bounds = attackCollider.bounds;

        Collider[] hits = Physics.OverlapBox(
            bounds.center,
            bounds.extents,
            attackCollider.transform.rotation,
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
                target.GetComponentInParent<MechaHealth>();

            if (health != null && !health.IsDead)
            {
                health.TakeDamageServer(damage);

                Debug.Log(
                    "[SWORD ATTACK] " + target.name +
                    " recebeu " + damage + " de dano."
                );
            }
        }

        attackCollider.enabled = false;
    }
}
