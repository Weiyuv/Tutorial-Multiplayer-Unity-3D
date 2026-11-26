
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
public class RoundManager : NetworkBehaviour
{
    [Header("Bot")]
    [SerializeField] private GameObject botPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnInterval = 0.8f;
    [SerializeField] private float spawnRadius = 2f;
    [SerializeField] private float navMeshSampleDistance = 5f;
    [SerializeField, Range(0f, 1f)] private float armedChance = 0.65f;

    [Header("Rounds")]
    [SerializeField] private int maxRounds = 10;
    [SerializeField] private float firstRoundDelay = 3f;
    [SerializeField] private float intermissionDuration = 10f;

    [Header("Enemies Per Round")]
    [SerializeField]
    private List<int> enemiesPerRound = new List<int>
    {
        3, 4, 5, 6, 8, 9, 11, 13, 15, 18
    };

    public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> EnemiesRemaining = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsIntermission = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> IntermissionRemaining = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> MatchFinished = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly List<ulong> activeBotIds = new List<ulong>();

    private Coroutine roundCoroutine;

    private void OnValidate()
    {
        maxRounds = Mathf.Max(1, maxRounds);

        if (enemiesPerRound == null)
            enemiesPerRound = new List<int>();

        maxRounds = Mathf.Min(maxRounds, enemiesPerRound.Count);

        for (int i = 0; i < enemiesPerRound.Count; i++)
        {
            enemiesPerRound[i] = Mathf.Max(0, enemiesPerRound[i]);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        if (botPrefab == null)
        {
            Debug.LogError("RoundManager: Bot Prefab não foi configurado ou sua referência foi destruída.");
            return;
        }

        NetworkObject prefabNetworkObject = botPrefab.GetComponent<NetworkObject>();

        if (prefabNetworkObject == null)
        {
            Debug.LogError("RoundManager: O Bot Prefab precisa ter um NetworkObject na raiz.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("RoundManager: Nenhum Spawn Point foi configurado.");
            return;
        }

        if (enemiesPerRound == null || enemiesPerRound.Count == 0)
        {
            Debug.LogError("RoundManager: Configure a quantidade de inimigos por round.");
            return;
        }

        maxRounds = Mathf.Clamp(maxRounds, 1, enemiesPerRound.Count);

        roundCoroutine = StartCoroutine(RunRounds());
    }

    public override void OnNetworkDespawn()
    {
        if (roundCoroutine != null)
        {
            StopCoroutine(roundCoroutine);
            roundCoroutine = null;
        }
    }

    private IEnumerator RunRounds()
    {
        yield return new WaitForSeconds(firstRoundDelay);

        while (CurrentRound.Value < maxRounds && !MatchFinished.Value)
        {
            if (!AnyAlivePlayers())
            {
                FinishMatch();
                yield break;
            }

            int round = CurrentRound.Value + 1;
            CurrentRound.Value = round;

            int enemyCount = enemiesPerRound[round - 1];

            Debug.Log("Iniciando Round " + round + " com " + enemyCount + " inimigos.");

            for (int i = 0; i < enemyCount; i++)
            {
                if (!AnyAlivePlayers())
                {
                    FinishMatch();
                    yield break;
                }

                if (!SpawnBot(round))
                {
                    Debug.LogError("RoundManager: Não foi possível criar um bot. Partida interrompida.");
                    FinishMatch();
                    yield break;
                }

                EnemiesRemaining.Value = GetAliveBotCount();

                yield return new WaitForSeconds(spawnInterval);
            }

            while (GetAliveBotCount() > 0)
            {
                EnemiesRemaining.Value = GetAliveBotCount();

                if (!AnyAlivePlayers())
                {
                    FinishMatch();
                    yield break;
                }

                yield return new WaitForSeconds(0.5f);
            }

            EnemiesRemaining.Value = 0;

            Debug.Log("Round " + round + " concluído.");

            if (round < maxRounds)
            {
                IsIntermission.Value = true;
                float remaining = intermissionDuration;

                while (remaining > 0f)
                {
                    if (!AnyAlivePlayers())
                    {
                        FinishMatch();
                        yield break;
                    }

                    IntermissionRemaining.Value = remaining;
                    remaining -= 1f;

                    yield return new WaitForSeconds(1f);
                }

                IntermissionRemaining.Value = 0f;
                IsIntermission.Value = false;
            }
        }

        FinishMatch();
    }

    private bool SpawnBot(int round)
    {
        if (botPrefab == null)
        {
            Debug.LogError("RoundManager: Bot Prefab está nulo ou foi destruído.");
            return false;
        }

        NetworkObject prefabNetworkObject = botPrefab.GetComponent<NetworkObject>();

        if (prefabNetworkObject == null)
        {
            Debug.LogError("RoundManager: Bot Prefab não possui NetworkObject na raiz.");
            return false;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("RoundManager: NetworkManager não encontrado.");
            return false;
        }

        if (!NetworkManager.Singleton.IsServer)
            return false;

        Vector3 spawnPosition = GetSpawnPosition();

        GameObject bot = Instantiate(
            botPrefab,
            spawnPosition,
            Quaternion.identity
        );

        if (bot == null)
        {
            Debug.LogError("RoundManager: Falha ao instanciar o bot.");
            return false;
        }

        MechaBotAI botAI = bot.GetComponent<MechaBotAI>();

        if (botAI == null)
        {
            Debug.LogError("RoundManager: O Bot Prefab não possui MechaBotAI.");
            Destroy(bot);
            return false;
        }

        MechaHealth health = bot.GetComponent<MechaHealth>();

        if (health == null)
        {
            Debug.LogError("RoundManager: O Bot Prefab não possui MechaHealth.");
            Destroy(bot);
            return false;
        }

        NetworkObject networkObject = bot.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError("RoundManager: O bot instanciado não possui NetworkObject.");
            Destroy(bot);
            return false;
        }

        networkObject.Spawn(true);

        activeBotIds.Add(networkObject.NetworkObjectId);

        Debug.Log("Bot criado no Round " + round + ". ID: " + networkObject.NetworkObjectId);

        return true;
    }

    private Vector3 GetSpawnPosition()
    {
        List<Transform> validSpawnPoints = new List<Transform>();

        foreach (Transform point in spawnPoints)
        {
            if (point != null)
                validSpawnPoints.Add(point);
        }

        if (validSpawnPoints.Count == 0)
        {
            Debug.LogWarning("RoundManager: Nenhum Spawn Point válido.");
            return Vector3.zero;
        }

        Transform selectedPoint = validSpawnPoints[
            Random.Range(0, validSpawnPoints.Count)
        ];

        Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
        randomOffset.y = 0f;

        Vector3 candidatePosition = selectedPoint.position + randomOffset;

        if (NavMesh.SamplePosition(
            candidatePosition,
            out NavMeshHit hit,
            navMeshSampleDistance,
            NavMesh.AllAreas))
        {
            return hit.position;
        }

        if (NavMesh.SamplePosition(
            selectedPoint.position,
            out hit,
            navMeshSampleDistance,
            NavMesh.AllAreas))
        {
            return hit.position;
        }

        Debug.LogWarning("RoundManager: Não foi possível encontrar uma posição válida na NavMesh.");
        return selectedPoint.position;
    }

    private int GetAliveBotCount()
    {
        if (NetworkManager.Singleton == null ||
            NetworkManager.Singleton.SpawnManager == null)
        {
            return 0;
        }

        activeBotIds.RemoveAll(id =>
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                id,
                out NetworkObject botObject))
            {
                return true;
            }

            if (botObject == null || !botObject.IsSpawned)
                return true;

            MechaHealth health = botObject.GetComponent<MechaHealth>();

            if (health == null || health.IsDead)
                return true;

            return false;
        });

        return activeBotIds.Count;
    }

    private bool AnyAlivePlayers()
    {
        if (NetworkManager.Singleton == null ||
            NetworkManager.Singleton.SpawnManager == null)
        {
            return false;
        }

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            MechaHealth health = client.PlayerObject.GetComponent<MechaHealth>();

            if (health != null && !health.IsDead)
                return true;
        }

        return false;
    }

    private void FinishMatch()
    {
        if (MatchFinished.Value)
            return;

        MatchFinished.Value = true;
        IsIntermission.Value = false;
        IntermissionRemaining.Value = 0f;
        EnemiesRemaining.Value = GetAliveBotCount();

        Debug.Log("Partida finalizada.");
    }
}
