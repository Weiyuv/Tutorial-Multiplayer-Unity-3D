using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class DodgeballThrow : NetworkBehaviour
{
    [Header("Throw Settings")]
    public Transform throwPoint;
    public float throwForce = 15f;
    public float upwardForce = 2f;
    public float throwCooldown = 0.5f;

    private float lastThrowTime;
    private Crystal heldBall;
    private CrystalCollector collector;

    private void Update()
    {
        if (!IsOwner)
            return;

        if (Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (Time.time < lastThrowTime + throwCooldown)
            return;

        if (collector == null)
            collector = GetComponentInChildren<CrystalCollector>(true);

        if (heldBall == null && collector != null)
            heldBall = GetHeldBallFromCollector();

        if (heldBall == null)
        {
            Debug.LogWarning("[THROW] Nenhuma bola está sendo segurada.");
            return;
        }

        Camera playerCamera = Camera.main;

        Vector3 direction = playerCamera != null
            ? playerCamera.transform.forward
            : transform.forward;

        if (throwPoint != null)
            direction = (throwPoint.position + direction * 10f - throwPoint.position).normalized;

        Debug.Log("[THROW] Arremessando bola: " + heldBall.name);

        heldBall.ThrowFromPlayer(direction, throwForce, upwardForce);

        heldBall = null;
        lastThrowTime = Time.time;
    }

    private Crystal GetHeldBallFromCollector()
    {
        if (collector == null)
            return null;

        Crystal[] crystals =
            FindObjectsByType<Crystal>(FindObjectsSortMode.None);

        foreach (Crystal crystal in crystals)
        {
            if (crystal != null &&
                crystal.IsSpawned &&
                crystal.IsHeld)
            {
                return crystal;
            }
        }

        return null;
    }

    public void SetHeldBall(Crystal ball)
    {
        if (!IsOwner)
            return;

        heldBall = ball;
    }
}