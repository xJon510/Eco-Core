using UnityEngine;

/// <summary>
/// Simple 6-face cube-to-sphere block planet.
/// - 6 faces like a dice
/// - Each face has a cellsPerFace x cellsPerFace grid
/// - One cube per cell, curved around a sphere of given radius
/// </summary>
[ExecuteAlways]
public class CubeSphereBlockPlanet : MonoBehaviour
{
    [Header("Planet")]
    [Tooltip("Center of the planet. If null, uses this GameObject's transform.")]
    public Transform planetCenter;
    public float radius = 50f;          // Distance from center to cube bottoms

    [Header("Grid")]
    [Range(1, 256)]
    public int cellsPerFace = 16;       // Number of cells along one side of each face

    [Header("Blocks")]
    public GameObject blockPrefab;      // 1x1x1 cube (or any block mesh)
    public float blockSize = 1f;        // Base cube size
    public bool alignBlocksToSphere = true;  // Rotate blocks so their top points outwards

    [Header("Generation Control")]
    public bool autoRegenerate = true;  // Regenerate on inspector changes

    // All blocks go under this container
    private Transform blocksContainer;

    // 6 directions for the cube faces (think dice faces)
    private static readonly Vector3[] faceDirections =
    {
        Vector3.up,       // Face 0  (+Y)
        Vector3.down,     // Face 1  (-Y)
        Vector3.left,     // Face 2  (-X)
        Vector3.right,    // Face 3  (+X)
        Vector3.forward,  // Face 4  (+Z)
        Vector3.back      // Face 5  (-Z)
    };

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

    [ContextMenu("Generate Planet")]
    public void Generate()
    {
        if (blockPrefab == null)
        {
            Debug.LogWarning("CubeSphereBlockPlanet: Please assign a blockPrefab.");
            return;
        }

        Transform center = planetCenter != null ? planetCenter : transform;

        EnsureContainer();
        ClearContainer();

        int steps = Mathf.Max(1, cellsPerFace);

        // For each of the 6 cube faces
        for (int face = 0; face < 6; face++)
        {
            Vector3 localUp = faceDirections[face];

            // Build face-local tangent axes
            Vector3 axisA = new Vector3(localUp.y, localUp.z, localUp.x);
            Vector3 axisB = Vector3.Cross(localUp, axisA);

            for (int y = 0; y < steps; y++)
            {
                for (int x = 0; x < steps; x++)
                {
                    // Normalized [0,1] coords for the center of each cell
                    float u = (x + 0.5f) / steps; // center of cell in X
                    float v = (y + 0.5f) / steps; // center of cell in Y

                    // Map to cube face in [-1, 1] space
                    Vector3 pointOnCube =
                        localUp +
                        (u - 0.5f) * 2f * axisA +
                        (v - 0.5f) * 2f * axisB;

                    // Direction from planet center through this cell
                    Vector3 dir = pointOnCube.normalized;

                    // We want the bottom of the block to sit on the sphere at 'radius'
                    // and the top to stick outward. So the block center is at:
                    //   radius + (blockSize / 2)
                    float centerRadius = radius + blockSize * 0.5f;

                    Vector3 worldPos = center.position + dir * centerRadius;

                    GameObject block = Instantiate(
                        blockPrefab,
                        worldPos,
                        Quaternion.identity,
                        blocksContainer
                    );

                    block.transform.localScale = Vector3.one * blockSize;

                    if (alignBlocksToSphere)
                    {
                        // Make block's local +Y (top) point along 'dir' (outwards)
                        block.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                    }

                    // OPTIONAL: you can attach a component here to store logical coords:
                    // var coord = block.AddComponent<BlockCoord>();
                    // coord.faceIndex = face;
                    // coord.x = x;
                    // coord.y = y;
                }
            }
        }
    }

    // Create/find the "Blocks" container as a child
    private void EnsureContainer()
    {
        if (blocksContainer != null) return;

        Transform found = transform.Find("Blocks");
        if (found != null)
        {
            blocksContainer = found;
            return;
        }

        GameObject obj = new GameObject("Blocks");
        obj.transform.SetParent(transform, false);
        blocksContainer = obj.transform;
    }

    // Clear all existing generated blocks
    private void ClearContainer()
    {
        if (blocksContainer == null) return;

        for (int i = blocksContainer.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(blocksContainer.GetChild(i).gameObject);
            else
                Destroy(blocksContainer.GetChild(i).gameObject);
#else
            Destroy(blocksContainer.GetChild(i).gameObject);
#endif
        }
    }
}
