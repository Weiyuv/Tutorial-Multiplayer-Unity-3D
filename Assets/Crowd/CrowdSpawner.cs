
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CrowdSpawner : MonoBehaviour
{
    [Header("Mesh da Arquibancada")]
    [SerializeField] private MeshFilter seatingMesh;

    [Header("Centro do Estadio")]
    [SerializeField] private Transform crowdCenter;

    [Header("Personagens")]
    [SerializeField] private GameObject[] crowdPrefabs;

    [Header("Quantidade")]
    [SerializeField] private int spectatorCount = 1000;
    [SerializeField] private float minimumSpacing = 1.2f;

    [Header("Spawn Gradual")]
    [SerializeField] private int attemptsPerFrame = 100;

    [Header("Margem das Bordas")]
    [SerializeField] private float edgeMargin = 0.5f;

    [Header("Posicionamento")]
    [SerializeField] private float surfaceOffset = 0.05f;
    [SerializeField] private float randomRotation = 8f;

    [SerializeField]
    private Vector2 randomScale = new Vector2(0.95f, 1.05f);

    [Header("Superficie")]
    [SerializeField] private float minimumUpwardNormal = 0.75f;

    [Header("Cores")]
    [SerializeField]
    private Color[] crowdColors =
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.magenta,
        Color.cyan,
        new Color(1f, 0.5f, 0f),
        Color.white,
        new Color(0.5f, 0.2f, 0.8f)
    };

    [SerializeField] private string colorProperty = "_BaseColor";

    [Header("Organizacao")]
    [SerializeField] private Transform crowdParent;

    private bool hasSpawned;
    private bool isSpawning;

    private int currentSpawned;

    private readonly List<Vector3> spawnedPositions =
        new List<Vector3>();

    private readonly List<Vector3> boundaryA =
        new List<Vector3>();

    private readonly List<Vector3> boundaryB =
        new List<Vector3>();

    private struct EdgeKey
    {
        public Vector3Int a;
        public Vector3Int b;

        public EdgeKey(Vector3 p1, Vector3 p2)
        {
            Vector3Int v1 = Quantize(p1);
            Vector3Int v2 = Quantize(p2);

            if (Compare(v1, v2) <= 0)
            {
                a = v1;
                b = v2;
            }
            else
            {
                a = v2;
                b = v1;
            }
        }

        private static Vector3Int Quantize(Vector3 p)
        {
            return new Vector3Int(
                Mathf.RoundToInt(p.x * 1000f),
                Mathf.RoundToInt(p.y * 1000f),
                Mathf.RoundToInt(p.z * 1000f)
            );
        }

        private static int Compare(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x)
                return a.x.CompareTo(b.x);

            if (a.y != b.y)
                return a.y.CompareTo(b.y);

            return a.z.CompareTo(b.z);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is EdgeKey))
                return false;

            EdgeKey other = (EdgeKey)obj;

            return a.Equals(other.a) && b.Equals(other.b);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (a.GetHashCode() * 397) ^ b.GetHashCode();
            }
        }
    }

    private class EdgeData
    {
        public Vector3 a;
        public Vector3 b;
        public int count;
    }

    private void Start()
    {
        SpawnCrowd();
    }

    [ContextMenu("Spawn Crowd")]
    public void SpawnCrowd()
    {
        if (hasSpawned || isSpawning)
            return;

        if (!ValidateSettings())
            return;

        StartCoroutine(SpawnCrowdRoutine());
    }

    private bool ValidateSettings()
    {
        if (seatingMesh == null ||
            seatingMesh.sharedMesh == null)
        {
            Debug.LogError(
                "CrowdSpawner: Mesh da arquibancada nao configurado!"
            );
            return false;
        }

        if (crowdCenter == null)
        {
            Debug.LogError(
                "CrowdSpawner: Centro do estadio nao configurado!"
            );
            return false;
        }

        if (crowdPrefabs == null ||
            crowdPrefabs.Length == 0)
        {
            Debug.LogError(
                "CrowdSpawner: Adicione os prefabs da torcida!"
            );
            return false;
        }

        if (spectatorCount <= 0)
        {
            Debug.LogError(
                "CrowdSpawner: Quantidade de espectadores invalida!"
            );
            return false;
        }

        return true;
    }

    private IEnumerator SpawnCrowdRoutine()
    {
        isSpawning = true;

        if (crowdParent == null)
            crowdParent = transform;

        Mesh mesh = seatingMesh.sharedMesh;

        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;

        Transform meshTransform = seatingMesh.transform;

        List<int> validTriangles = new List<int>();

        Dictionary<EdgeKey, EdgeData> edges =
            new Dictionary<EdgeKey, EdgeData>();

        // Identifica as superficies superiores.

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = meshTransform.TransformPoint(
                vertices[triangles[i]]
            );

            Vector3 b = meshTransform.TransformPoint(
                vertices[triangles[i + 1]]
            );

            Vector3 c = meshTransform.TransformPoint(
                vertices[triangles[i + 2]]
            );

            Vector3 normal = Vector3.Cross(
                b - a,
                c - a
            ).normalized;

            if (normal.y < minimumUpwardNormal)
                continue;

            validTriangles.Add(triangles[i]);
            validTriangles.Add(triangles[i + 1]);
            validTriangles.Add(triangles[i + 2]);

            AddEdge(edges, a, b);
            AddEdge(edges, b, c);
            AddEdge(edges, c, a);
        }

        if (validTriangles.Count == 0)
        {
            Debug.LogError(
                "CrowdSpawner: Nenhuma superficie superior encontrada!"
            );

            isSpawning = false;
            yield break;
        }

        // Identifica as bordas externas.

        boundaryA.Clear();
        boundaryB.Clear();

        foreach (KeyValuePair<EdgeKey, EdgeData> pair in edges)
        {
            EdgeData edge = pair.Value;

            if (edge.count == 1)
            {
                boundaryA.Add(edge.a);
                boundaryB.Add(edge.b);
            }
        }

        spawnedPositions.Clear();

        currentSpawned = 0;

        int attempts = 0;
        int maxAttempts = spectatorCount * 30;

        int attemptsThisFrame = 0;

        while (currentSpawned < spectatorCount &&
               attempts < maxAttempts)
        {
            attempts++;
            attemptsThisFrame++;

            int triangleIndex = Random.Range(
                0,
                validTriangles.Count / 3
            ) * 3;

            Vector3 a = meshTransform.TransformPoint(
                vertices[validTriangles[triangleIndex]]
            );

            Vector3 b = meshTransform.TransformPoint(
                vertices[validTriangles[triangleIndex + 1]]
            );

            Vector3 c = meshTransform.TransformPoint(
                vertices[validTriangles[triangleIndex + 2]]
            );

            float r1 = Random.value;
            float r2 = Random.value;

            if (r1 + r2 > 1f)
            {
                r1 = 1f - r1;
                r2 = 1f - r2;
            }

            Vector3 position =
                a + r1 * (b - a) + r2 * (c - a);

            Vector3 normal = Vector3.Cross(
                b - a,
                c - a
            ).normalized;

            if (normal.y < minimumUpwardNormal)
                goto CheckFrame;

            position += normal * surfaceOffset;

            if (IsNearEdge(position))
                goto CheckFrame;

            bool tooClose = false;

            foreach (Vector3 existing in spawnedPositions)
            {
                if ((position - existing).sqrMagnitude <
                    minimumSpacing * minimumSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                goto CheckFrame;

            GameObject prefab = crowdPrefabs[
                Random.Range(0, crowdPrefabs.Length)
            ];

            if (prefab == null)
                goto CheckFrame;

            Vector3 forward = Vector3.ProjectOnPlane(
                crowdCenter.position - position,
                normal
            ).normalized;

            if (forward.sqrMagnitude < 0.001f)
                goto CheckFrame;

            Quaternion rotation = Quaternion.LookRotation(
                forward,
                normal
            );

            rotation *= Quaternion.Euler(
                0f,
                Random.Range(-randomRotation, randomRotation),
                0f
            );

            GameObject spectator = Instantiate(
                prefab,
                position,
                rotation,
                crowdParent
            );

            float scale = Random.Range(
                randomScale.x,
                randomScale.y
            );

            spectator.transform.localScale *= scale;

            spectator.name = "Spectator_" + currentSpawned;

            ApplyRandomColor(spectator);

            spawnedPositions.Add(position);

            currentSpawned++;

        CheckFrame:

            if (attemptsThisFrame >= Mathf.Max(1, attemptsPerFrame))
            {
                attemptsThisFrame = 0;

                yield return null;
            }
        }

        hasSpawned = true;
        isSpawning = false;

        Debug.Log(
            "CrowdSpawner: " + currentSpawned +
            " de " + spectatorCount +
            " espectadores criados. Tentativas: " + attempts
        );
    }

    private void AddEdge(
        Dictionary<EdgeKey, EdgeData> edges,
        Vector3 a,
        Vector3 b
    )
    {
        EdgeKey key = new EdgeKey(a, b);

        if (edges.TryGetValue(key, out EdgeData edge))
        {
            edge.count++;
        }
        else
        {
            edges.Add(key, new EdgeData
            {
                a = a,
                b = b,
                count = 1
            });
        }
    }

    private bool IsNearEdge(Vector3 position)
    {
        if (edgeMargin <= 0f)
            return false;

        float marginSqr = edgeMargin * edgeMargin;

        for (int i = 0; i < boundaryA.Count; i++)
        {
            float distanceSqr = DistanceToSegmentSqr(
                position,
                boundaryA[i],
                boundaryB[i]
            );

            if (distanceSqr < marginSqr)
                return true;
        }

        return false;
    }

    private float DistanceToSegmentSqr(
        Vector3 point,
        Vector3 a,
        Vector3 b
    )
    {
        Vector3 segment = b - a;

        float lengthSqr = segment.sqrMagnitude;

        if (lengthSqr < 0.0001f)
            return (point - a).sqrMagnitude;

        float t = Vector3.Dot(
            point - a,
            segment
        ) / lengthSqr;

        t = Mathf.Clamp01(t);

        Vector3 closestPoint = a + segment * t;

        return (point - closestPoint).sqrMagnitude;
    }

    private void ApplyRandomColor(GameObject spectator)
    {
        if (crowdColors == null ||
            crowdColors.Length == 0)
            return;

        Color color = crowdColors[
            Random.Range(0, crowdColors.Length)
        ];

        Renderer[] renderers =
            spectator.GetComponentsInChildren<Renderer>();

        MaterialPropertyBlock block = new MaterialPropertyBlock();

        foreach (Renderer rend in renderers)
        {
            rend.GetPropertyBlock(block);

            block.SetColor(colorProperty, color);

            rend.SetPropertyBlock(block);
        }
    }
}
