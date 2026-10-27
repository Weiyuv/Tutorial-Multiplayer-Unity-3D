
using UnityEngine;

public class MechaTargetHUD : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private HUDController hudController;

    [Header("Detecção")]
    [SerializeField] private float maxTargetDistance = 50f;

    private readonly RaycastHit[] hits = new RaycastHit[32];

    private void Awake()
    {
        if (hudController == null)
            hudController = FindFirstObjectByType<HUDController>();

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void Update()
    {
        if (hudController == null || playerCamera == null)
            return;

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        int hitCount = Physics.RaycastNonAlloc(
            ray,
            hits,
            maxTargetDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = float.MaxValue;
        MechaHealth targetHealth = null;

        for (int i = 0; i < hitCount; i++)
        {
            if (hits[i].collider == null)
                continue;

            if (hits[i].distance >= nearestDistance)
                continue;

            MechaHealth health =
                hits[i].collider.GetComponentInParent<MechaHealth>();

            if (health == null)
                continue;

            if (!health.gameObject.CompareTag("Enemy"))
                continue;

            if (health.IsDead)
                continue;

            nearestDistance = hits[i].distance;
            targetHealth = health;
        }

        if (targetHealth != null)
        {
            hudController.SetEnemyHealth(
                targetHealth.CurrentHealth,
                targetHealth.MaxHealth
            );
        }
        else
        {
            hudController.HideEnemyHealth();
        }
    }
}
