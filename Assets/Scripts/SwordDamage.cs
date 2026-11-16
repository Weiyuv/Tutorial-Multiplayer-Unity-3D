
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SwordDamage : MonoBehaviour
{
    [Header("Dano")]
    [SerializeField] private float damage = 25f;

    private Collider attackCollider;
    private Crystal crystal;

    private bool isAttacking;

    private readonly HashSet<ulong> hitTargets =
        new HashSet<ulong>();

    private void Awake()
    {
        attackCollider = GetComponent<Collider>();
        crystal = GetComponentInParent<Crystal>();

        attackCollider.isTrigger = true;

        DisableAttack();
    }

    private void OnEnable()
    {
        if (attackCollider == null)
            attackCollider = GetComponent<Collider>();

        DisableAttack();
    }

    public void SetAttacking(bool attacking)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            DisableAttack();
            return;
        }

        if (attacking)
        {
            // Não permite atacar sem possuir a espada.
            if (crystal == null || !crystal.IsHeld)
            {
                DisableAttack();
                return;
            }

            isAttacking = true;
            hitTargets.Clear();

            attackCollider.enabled = true;

            Debug.Log("[SWORD DAMAGE] Ataque autorizado.");
        }
        else
        {
            DisableAttack();
        }
    }

    public void DisableAttack()
    {
        isAttacking = false;

        if (attackCollider != null)
            attackCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryDamage(other);
    }

    private void TryDamage(Collider other)
    {
        // Bloqueio absoluto fora da janela de ataque.
        if (!isAttacking)
            return;

        if (attackCollider == null || !attackCollider.enabled)
            return;

        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
            return;

        if (crystal == null || !crystal.IsHeld)
        {
            DisableAttack();
            return;
        }

        if (other == null)
            return;

        MechaHealth health =
            other.GetComponentInParent<MechaHealth>();

        if (health == null || health.IsDead)
            return;

        // Impede dano ao próprio jogador.
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(
            crystal.HolderClientId,
            out NetworkClient holderClient))
        {
            if (holderClient.PlayerObject != null &&
                health.NetworkObjectId ==
                holderClient.PlayerObject.NetworkObjectId)
                return;
        }

        ulong targetId = health.NetworkObjectId;

        // Cada inimigo só recebe dano uma vez por golpe.
        if (hitTargets.Contains(targetId))
            return;

        hitTargets.Add(targetId);

        health.TakeDamageServer(damage);

        Debug.Log(
            "[SWORD DAMAGE] ACERTOU " +
            health.name + " | Dano: " + damage
        );
    }

    private void OnDisable()
    {
        DisableAttack();
    }
}
