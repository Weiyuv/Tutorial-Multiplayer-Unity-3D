using Unity.Netcode;
using UnityEngine;

public class BallSpawner : NetworkBehaviour
{
    public static BallSpawner Instance;

    [Header("Ball")]
    [SerializeField] private GameObject ballPrefab;

    [Header("Distance From Player")]
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 8f;

    [Header("Ground")]
    [SerializeField] private float raycastHeight = 50f;
    [SerializeField] private float raycastDistance = 100f;
    [SerializeField] private float groundOffset = 0.5f;

    private void Awake()
    {
        Instance = this;
    }

    public void SpawnBallNearPlayer(Vector3 playerPosition)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[BALL] Não é o servidor!");
            return;
        }

        if (ballPrefab == null)
        {
            Debug.LogError("[BALL] Ball Prefab não foi configurado!");
            return;
        }

        // Escolhe uma direção aleatória no plano 3D (X/Z)
        Vector2 randomDirection = Random.insideUnitCircle.normalized;

        float distance = Random.Range(
            minDistance,
            maxDistance
        );

        // Posição aleatória ao redor do Player
        Vector3 randomPosition =
            playerPosition +
            new Vector3(
                randomDirection.x,
                0f,
                randomDirection.y
            ) * distance;

        // Começa o Raycast bem acima da posição escolhida
        Vector3 rayOrigin =
            randomPosition +
            Vector3.up * raycastHeight;

        // Procura o chão para baixo
        if (!Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            raycastDistance))
        {
            Debug.LogWarning(
                "[BALL] Não encontrou o chão em " +
                randomPosition
            );

            return;
        }

        // Coloca a bola sobre a superfície
        Vector3 spawnPosition =
            hit.point +
            Vector3.up * groundOffset;

        Debug.Log(
            "[BALL] Spawn position: " +
            spawnPosition
        );

        // Cria a bola
        GameObject ball = Instantiate(
            ballPrefab,
            spawnPosition,
            Quaternion.identity
        );

        // Pega o NetworkObject
        NetworkObject networkObject =
            ball.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError(
                "[BALL] O Ball Prefab não possui NetworkObject!"
            );

            Destroy(ball);
            return;
        }

        // Sincroniza a bola com todos os jogadores
        networkObject.Spawn();

        Debug.Log(
            "[BALL] BOLA SPAWNADA COM SUCESSO!"
        );
    }
}