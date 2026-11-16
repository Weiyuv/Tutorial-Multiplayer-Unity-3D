
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerCameraTarget : NetworkBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Transform cameraTarget;

    [SerializeField] private float searchInterval = 0.2f;
    [SerializeField] private float maxSearchTime = 10f;

    private Coroutine cameraRoutine;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (cameraRoutine != null)
            StopCoroutine(cameraRoutine);

        cameraRoutine = StartCoroutine(ConfigureCamera());
    }

    public override void OnNetworkDespawn()
    {
        if (cameraRoutine != null)
        {
            StopCoroutine(cameraRoutine);
            cameraRoutine = null;
        }
    }

    private IEnumerator ConfigureCamera()
    {
        if (cameraTarget == null)
            cameraTarget = transform.Find("CameraTarget");

        if (cameraTarget == null)
        {
            Debug.LogWarning(
                "[CAMERA] CameraTarget não encontrado. Usando o jogador."
            );

            cameraTarget = transform;
        }

        float elapsed = 0f;

        while (elapsed < maxSearchTime)
        {
            CinemachineCamera[] cameras =
                FindObjectsByType<CinemachineCamera>(
                    FindObjectsSortMode.None
                );

            foreach (CinemachineCamera cam in cameras)
            {
                if (cam == null || !cam.isActiveAndEnabled)
                    continue;

                CameraTarget target = cam.Target;
                target.TrackingTarget = cameraTarget;
                cam.Target = target;

                Debug.Log(
                    "[CAMERA] Câmera configurada para: " + gameObject.name
                );

                cameraRoutine = null;
                yield break;
            }

            elapsed += searchInterval;
            yield return new WaitForSeconds(searchInterval);
        }

        Debug.LogError(
            "[CAMERA] Nenhuma Cinemachine Camera encontrada na cena."
        );

        cameraRoutine = null;
    }
}
