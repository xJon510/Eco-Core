using UnityEngine;

[ExecuteAlways]
public class VoxelPlanetPatch : MonoBehaviour
{
    [Header("Planet")]
    public Transform planetCenter;      // If null, uses this.transform
    public float radius = 50f;

    [Header("Patch Grid")]
    [Range(1, 256)]
    public int xCount = 8;
    [Range(1, 256)]
    public int yCount = 8;

    public float angularWidth = 60f;   // degrees horizontally
    public float angularHeight = 60f;  // degrees vertically

    [Header("Patch Orientation")]
    public Vector3 patchRotationEuler = Vector3.zero;

    [Header("Voxels")]
    public GameObject cubePrefab;
    public float cubeBaseSize = 1f;
    public float heightMultiplier = 1f;
    public bool alignToSphere = true;

    [Header("Debug / Control")]
    public bool autoRegenerate = true;

    // NEW - container for spawned cubes
    private Transform cubesContainer;

    private void OnValidate()
    {
        if (autoRegenerate)
            Generate();
    }

    private void Start()
    {
        if (autoRegenerate)
            Generate();
    }

    [ContextMenu("Generate Patch")]
    public void Generate()
    {
        if (cubePrefab == null)
        {
            Debug.LogWarning("VoxelPlanetPatch: Please assign cubePrefab.");
            return;
        }

        CreateOrFindContainer();
        ClearContainer();

        Transform center = planetCenter != null ? planetCenter : transform;

        // Patch rotation on sphere
        Quaternion patchRotation = Quaternion.Euler(patchRotationEuler);
        Vector3 baseNormal = patchRotation * Vector3.up;
        Vector3 axisRight = patchRotation * Vector3.right;
        Vector3 axisUp = patchRotation * Vector3.forward;

        int xSteps = Mathf.Max(1, xCount);
        int ySteps = Mathf.Max(1, yCount);

        for (int y = 0; y < ySteps; y++)
        {
            for (int x = 0; x < xSteps; x++)
            {
                float u = (x + 0.5f) / xSteps - 0.5f;
                float v = (y + 0.5f) / ySteps - 0.5f;

                float angleX = u * angularWidth;
                float angleY = v * angularHeight;

                Quaternion rotX = Quaternion.AngleAxis(angleX, axisRight);
                Quaternion rotY = Quaternion.AngleAxis(angleY, axisUp);

                Vector3 dir = (rotY * rotX * baseNormal).normalized;

                // bottom of cube sits on the sphere; top extends outward
                float baseHeight = cubeBaseSize;
                float height = cubeBaseSize * Mathf.Max(0f, heightMultiplier);
                float extra = height - baseHeight;

                float centerRadius = radius + extra * 0.5f;
                Vector3 worldPos = center.position + dir * centerRadius;

                GameObject cube = Instantiate(
                    cubePrefab,
                    worldPos,
                    Quaternion.identity,
                    cubesContainer   // NEW - parent into container
                );

                cube.transform.localScale = new Vector3(cubeBaseSize, height, cubeBaseSize);

                if (alignToSphere)
                    cube.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Container helpers
    // ──────────────────────────────────────────────────────────────

    private void CreateOrFindContainer()
    {
        if (cubesContainer != null) return;

        Transform found = transform.Find("Cubes");
        if (found != null)
        {
            cubesContainer = found;
            return;
        }

        GameObject obj = new GameObject("Cubes");
        obj.transform.SetParent(transform, false);
        cubesContainer = obj.transform;
    }

    private void ClearContainer()
    {
        for (int i = cubesContainer.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(cubesContainer.GetChild(i).gameObject);
            else
                Destroy(cubesContainer.GetChild(i).gameObject);
#else
            Destroy(cubesContainer.GetChild(i).gameObject);
#endif
        }
    }
}
