
using UnityEngine;
using System.Collections.Generic;

public class CrowdCameraCulling : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private bool findCameraAutomatically = true;

    [Header("Culling")]
    [SerializeField] private float updateInterval = 0.15f;
    [SerializeField] private float extraBoundsSize = 0.5f;

    [Header("Fallback Bounds")]
    [SerializeField]
    private Vector3 fallbackBoundsSize =
        new Vector3(1.5f, 2.5f, 1.5f);

    private class CrowdCharacter
    {
        public Animator animator;
        public Renderer[] renderers;
        public Transform root;
        public bool isVisible = true;
    }

    private readonly List<CrowdCharacter> characters =
        new List<CrowdCharacter>();

    private Plane[] cameraPlanes;
    private float timer;

    private void Start()
    {
        FindPlayerCamera();
        CollectCharacters();
    }

    private void FindPlayerCamera()
    {
        if (playerCamera == null && findCameraAutomatically)
            playerCamera = Camera.main;
    }

    private void CollectCharacters()
    {
        characters.Clear();

        Animator[] animators =
            GetComponentsInChildren<Animator>(true);

        foreach (Animator animator in animators)
        {
            if (animator == null)
                continue;

            CrowdCharacter character = new CrowdCharacter
            {
                animator = animator,
                root = animator.transform,
                renderers = animator.GetComponentsInChildren<Renderer>(true),
                isVisible = true
            };

            animator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;

            characters.Add(character);
        }

        Debug.Log("Crowd Culling: " + characters.Count + " personagens.");
    }

    private void LateUpdate()
    {
        if (playerCamera == null)
        {
            FindPlayerCamera();
            return;
        }

        timer += Time.unscaledDeltaTime;

        if (timer < updateInterval)
            return;

        timer = 0f;

        cameraPlanes =
            GeometryUtility.CalculateFrustumPlanes(playerCamera);

        UpdateCharacters();
    }

    private void UpdateCharacters()
    {
        foreach (CrowdCharacter character in characters)
        {
            if (character.animator == null)
                continue;

            bool visible = IsCharacterVisible(character);

            if (visible == character.isVisible)
                continue;

            character.isVisible = visible;

            character.animator.enabled = visible;

            foreach (Renderer renderer in character.renderers)
            {
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }
    }

    private bool IsCharacterVisible(CrowdCharacter character)
    {
        bool foundRenderer = false;

        foreach (Renderer renderer in character.renderers)
        {
            if (renderer == null)
                continue;

            foundRenderer = true;

            Bounds bounds = renderer.bounds;

            if (bounds.size.sqrMagnitude < 0.01f)
            {
                bounds = new Bounds(
                    renderer.transform.position,
                    fallbackBoundsSize
                );
            }

            bounds.Expand(extraBoundsSize);

            if (GeometryUtility.TestPlanesAABB(
                cameraPlanes,
                bounds))
            {
                return true;
            }
        }

        if (!foundRenderer)
        {
            Bounds bounds = new Bounds(
                character.root.position,
                fallbackBoundsSize
            );

            return GeometryUtility.TestPlanesAABB(
                cameraPlanes,
                bounds
            );
        }

        return false;
    }

    public void SetPlayerCamera(Camera camera)
    {
        playerCamera = camera;
    }

    private void OnDisable()
    {
        foreach (CrowdCharacter character in characters)
        {
            if (character.animator != null)
                character.animator.enabled = true;

            if (character.renderers == null)
                continue;

            foreach (Renderer renderer in character.renderers)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }
        }
    }
}
