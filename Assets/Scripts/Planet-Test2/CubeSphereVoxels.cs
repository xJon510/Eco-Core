using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class CubeSphereVoxels : MonoBehaviour
{
    [Header("Grid / Planet")]
    [Range(4, 128)]
    public int resolution = 32;         // tiles per face in one dimension
    public float baseRadius = 50f;      // base radius of the planet
    public float cubeSize = 1f;         // size of each voxel cube

    [Header("Height (Blocky)")]
    public int maxHeightSteps = 3;      // how many "extra" blocks above base
    public float heightAmplitude = 3f;  // controls noise strength
    public float noiseFrequency = 0.8f; // lower = bigger continents
    public int noiseSeed = 0;

    [Header("Rendering")]
    public GameObject cubePrefab;       // 1x1x1 cube (or any voxel mesh)
    public bool alignCubesToSphere = false; // if true, cubes "stand up" on the surface

    // internal bookkeeping so we can regen cleanly
    private readonly List<GameObject> spawnedCubes = new List<GameObject>();

    // The 6 cube face directions
    private static readonly Vector3[] faceDirections =
    {
        Vector3.up,       // +Y
        Vector3.down,     // -Y
        Vector3.left,     // -X
        Vector3.right,    // +X
        Vector3.forward,  // +Z
        Vector3.back      // -Z
    };

    private void OnValidate()
    {
        Generate();
    }

    private void Start()
    {
        Generate();
    }

    public void Generate()
    {
        if (cubePrefab == null)
        {
            Debug.LogWarning("CubeSphereVoxels: Please assign a cubePrefab.");
            return;
        }

        ClearCubes();

        // spawn cubes for each face
        for (int face = 0; face < 6; face++)
        {
            GenerateFace(faceDirections[face]);
        }
    }

    private void ClearCubes()
    {
        // Remove ALL children (all spawned cubes)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject);
#else
        Destroy(child.gameObject);
#endif
        }
    }

    private void GenerateFace(Vector3 localUp)
    {
        Vector3 axisA = new Vector3(localUp.y, localUp.z, localUp.x);
        Vector3 axisB = Vector3.Cross(localUp, axisA);

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float percentX = x / (resolution - 1f);
                float percentY = y / (resolution - 1f);

                // Point on cube in [-1,1] around this face
                Vector3 pointOnCube =
                    localUp +
                    (percentX - 0.5f) * 2f * axisA +
                    (percentY - 0.5f) * 2f * axisB;

                // Direction on unit sphere
                Vector3 dir = pointOnCube.normalized;

                // Sample noise and get a block height step
                int heightStep = GetHeightStep(dir);

                // Final radius (center of cube)
                float r = baseRadius + heightStep * cubeSize;

                Vector3 worldPos = dir * r + transform.position;

                // Spawn cube
                GameObject cube = Instantiate(cubePrefab, worldPos, Quaternion.identity, transform);

                // Ensure consistent size
                cube.transform.localScale = Vector3.one * cubeSize;

                // Optional: align cube so its "up" points away from planet center
                if (alignCubesToSphere)
                {
                    cube.transform.rotation = Quaternion.FromToRotation(Vector3.up, -dir);
                }

                spawnedCubes.Add(cube);
            }
        }
    }

    // --- Noise & height quantization ---

    private int GetHeightStep(Vector3 dir)
    {
        float f = noiseFrequency;

        float nx = dir.x * f + noiseSeed * 0.123f;
        float ny = dir.y * f + noiseSeed * 0.456f;
        float nz = dir.z * f + noiseSeed * 0.789f;

        float n1 = Mathf.PerlinNoise(nx, ny);
        float n2 = Mathf.PerlinNoise(ny, nz);
        float n = n1 * 0.6f + n2 * 0.4f; // combine noises

        // Map [0,1] -> [-1,1]
        n = (n - 0.5f) * 2f;

        // Scale by amplitude
        float h = n * heightAmplitude;

        // Turn that into a 0..maxHeightSteps integer
        // First normalize back to [0,1] roughly
        float normalized = Mathf.InverseLerp(-heightAmplitude, heightAmplitude, h);

        float stepF = normalized * maxHeightSteps;
        int step = Mathf.FloorToInt(stepF);

        step = Mathf.Clamp(step, 0, maxHeightSteps);

        return step;
    }
}
