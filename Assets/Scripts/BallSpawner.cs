using Unity.Netcode;
using UnityEngine;

public class BallSpawner : NetworkBehaviour
{
    public static BallSpawner Instance;

    [SerializeField] private GameObject ballPrefab;

    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 8f;

    private void Awake()
    {
        Instance = this;
    }

    public void SpawnBall(Vector3 playerPosition)
    {
        if (!IsServer)
            return;

        Vector2 direction = Random.insideUnitCircle.normalized;
        float distance = Random.Range(minDistance, maxDistance);

        Vector3 spawnPosition = playerPosition +
            new Vector3(direction.x, 0f, direction.y) * distance;

        GameObject ball = Instantiate(
            ballPrefab,
            spawnPosition,
            Quaternion.identity
        );

        NetworkObject netObj = ball.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[BALL] Ball prefab não tem NetworkObject!");
            Destroy(ball);
            return;
        }

        netObj.Spawn();

        Debug.Log("[BALL] Bola spawnada em " + spawnPosition);
    }
}