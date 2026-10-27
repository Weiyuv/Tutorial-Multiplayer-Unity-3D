
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SwordDamage : NetworkBehaviour
{
    [Header("Dano")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float hitCooldown = 0.5f;

    [Header("Configuração")]
    [SerializeField] private bool damageOnlyWhileAttacking = true;

    private Collider swordCollider;
    private bool isAttacking;

    private readonly Dictionary<ulong, float> lastHitTimes =
        new Dictionary<ulong, float>();

    private void Awake()
    {
        swordCollider = GetComponent<Collider>();
        swordCollider.isTrigger = true;
    }

    public override void OnNetworkSpawn()
    {
        if (swordCollider != null)
            swordCollider.enabled = false;
    }

    public override void OnNetworkDespawn()
    {
        lastHitTimes.Clear();
    }

    public void SetAttacking(bool attacking)
    {
        isAttacking = attacking;

        if (swordCollider != null)
            swordCollider.enabled = attacking;
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
        if (!IsSpawned || !IsServer)
            return;

        if (damageOnlyWhileAttacking && !isAttacking)
            return;

        if (damage <= 0f)
            return;

        MechaHealth health =
            other.GetComponentInParent<MechaHealth>();

        if (health == null || health.IsDead)
            return;

        if (health.NetworkObject == NetworkObject)
            return;

        ulong targetId = health.NetworkObjectId;

        if (lastHitTimes.TryGetValue(targetId, out float lastHit))
        {
            if (Time.time - lastHit < hitCooldown)
                return;
        }

        lastHitTimes[targetId] = Time.time;

        health.TakeDamageServer(damage);

        Debug.Log(
            "[ESPADA] " + health.name +
            " recebeu " + damage +
            " de dano. Vida restante: " +
            health.CurrentHealth + "/" +
            health.MaxHealth
        );
    }
}
