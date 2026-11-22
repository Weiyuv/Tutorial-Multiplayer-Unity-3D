
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
public class CrystalSwordAttack : NetworkBehaviour
{
    [Header("Animação")]
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkAnimator networkAnimator;
    [SerializeField] private string attackTrigger = "Attack";

    [Header("Ataque")]
    [SerializeField] private float attackCooldown = 0.7f;
    [SerializeField] private float hitDelay = 0.25f;
    [SerializeField] private float hitboxActiveTime = 0.25f;

    private bool attacking;

    private float localNextAttackTime;
    private float serverNextAttackTime;

    private Coroutine attackCoroutine;
    private SwordDamage currentSwordDamage;

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

        if (Time.time < localNextAttackTime)
            return;

        if (FindHeldCrystal() == null)
        {
            Debug.Log("[SWORD ATTACK] Nenhuma espada equipada.");
            return;
        }

        localNextAttackTime = Time.time + attackCooldown;

        RequestAttackServerRpc();
    }

    private Crystal FindHeldCrystal()
    {
        Crystal[] crystals =
            FindObjectsByType<Crystal>(FindObjectsSortMode.None);

        foreach (Crystal crystal in crystals)
        {
            if (crystal == null || !crystal.IsSpawned)
                continue;

            if (!crystal.IsHeld)
                continue;

            if (crystal.HolderClientId == OwnerClientId)
                return crystal;
        }

        return null;
    }

    [ServerRpc]
    private void RequestAttackServerRpc(
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (attacking || Time.time < serverNextAttackTime)
            return;

        Crystal crystal = FindHeldCrystal();

        if (crystal == null)
            return;

        SwordDamage swordDamage =
            crystal.GetComponentInChildren<SwordDamage>(true);

        if (swordDamage == null)
        {
            Debug.LogError("[SWORD ATTACK] SwordDamage não encontrado.");
            return;
        }

        attacking = true;
        serverNextAttackTime = Time.time + attackCooldown;
        currentSwordDamage = swordDamage;

        currentSwordDamage.DisableAttack();

        if (networkAnimator != null)
            networkAnimator.SetTrigger(attackTrigger);
        else if (animator != null)
            animator.SetTrigger(attackTrigger);

        attackCoroutine = StartCoroutine(
            AttackRoutine(crystal, swordDamage)
        );
    }

    private IEnumerator AttackRoutine(
        Crystal crystal,
        SwordDamage swordDamage)
    {
        yield return new WaitForSeconds(hitDelay);

        if (crystal == null ||
            !crystal.IsHeld ||
            crystal.HolderClientId != OwnerClientId ||
            swordDamage == null)
        {
            FinishAttack();
            yield break;
        }

        // Ativa o collider e permite causar dano.
        swordDamage.SetAttacking(true);

        yield return new WaitForSeconds(hitboxActiveTime);

        FinishAttack();
    }

    private void FinishAttack()
    {
        if (currentSwordDamage != null)
            currentSwordDamage.DisableAttack();

        currentSwordDamage = null;
        attacking = false;
        attackCoroutine = null;
    }

    public override void OnNetworkDespawn()
    {
        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        FinishAttack();

        localNextAttackTime = 0f;
        serverNextAttackTime = 0f;
    }
}
