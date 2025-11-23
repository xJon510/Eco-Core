using UnityEngine;

[ExecuteAlways]
public class CubeSpherePlanet : MonoBehaviour
{
    [Range(4, 256)]
    public int resolution = 32;

    [Header("Radius / Height")]
    public float baseRadius = 50f;        // Average planet radius
    public float heightAmplitude = 5f;    // +/- height around base radius

    [Header("Noise Settings")]
    public float noiseFrequency = 1.0f;   // Lower = larger, smoother continents
    public int noiseSeed = 0;             // Change to get different planets

    public Material planetMaterial;

    private MeshFilter[] meshFilters;
    private Mesh[] meshes;

    // The 6 cube face directions
    private static readonly Vector3[] faceDirections =
    {
        Vector3.up,    // +Y
        Vector3.down,  // -Y
        Vector3.left,  // -X
        Vector3.right, // +X
        Vector3.forward, // +Z
        Vector3.back     // -Z
    };

    private static readonly Vector3[] faceNoiseOffsets =
    {
        new Vector3(13.1f, 7.4f, 5.9f),   // +Y
        new Vector3(2.7f,  19.3f, 4.6f),  // -Y
        new Vector3(11.5f, 3.2f,  17.8f), // -X
        new Vector3(9.8f,  14.7f, 6.1f),  // +X
        new Vector3(5.4f,  12.2f, 9.9f),  // +Z
        new Vector3(8.3f,  2.5f,  21.7f)  // -Z
    };

    // ==========================
    //  DEBUG GRID (NEW)
    // ==========================
    [Header("Debug Grid")]
    public bool drawGridGizmos = true;     // toggle in inspector
    public Color gridColor = Color.yellow; // gizmo color
    public float gizmoSize = 0.4f;         // size of the little boxes
    [Range(1, 16)]
    public int gizmoStep = 4;              // draw every Nth tile (1 = all)

    private void OnValidate()
    {
        // Regenerate when values change in the inspector
        Generate();
    }

    private void Start()
    {
        Generate();
    }

    public void Generate()
    {
        if (planetMaterial == null)
        {
            Debug.LogWarning("CubeSpherePlanet: Please assign a planetMaterial.");
            return;
        }

        if (meshFilters == null || meshFilters.Length != 6)
        {
            meshFilters = new MeshFilter[6];
            meshes = new Mesh[6];

            // Create 6 child objects, one per face
            for (int i = 0; i < 6; i++)
            {
                GameObject faceObj;

                if (transform.childCount > i)
                {
                    faceObj = transform.GetChild(i).gameObject;
                }
                else
                {
                    faceObj = new GameObject("Face_" + i);
                    faceObj.transform.SetParent(transform, false);
                }

                var mf = faceObj.GetComponent<MeshFilter>();
                var mr = faceObj.GetComponent<MeshRenderer>();

                if (mf == null) mf = faceObj.AddComponent<MeshFilter>();
                if (mr == null) mr = faceObj.AddComponent<MeshRenderer>();

                mr.sharedMaterial = planetMaterial;

                meshFilters[i] = mf;

                if (meshes[i] == null)
                {
                    meshes[i] = new Mesh();
                    meshes[i].name = "FaceMesh_" + i;
                }

                mf.sharedMesh = meshes[i];
            }
        }

        // Generate each face mesh
        for (int i = 0; i < 6; i++)
        {
            GenerateFace(meshes[i], faceDirections[i], faceNoiseOffsets[i]);
        }
    }

    private void GenerateFace(Mesh mesh, Vector3 localUp, Vector3 noiseOffset)
    {
        mesh.Clear();

        // Build local axes on this face
        Vector3 axisA = new Vector3(localUp.y, localUp.z, localUp.x);
        Vector3 axisB = Vector3.Cross(localUp, axisA);

        int vertCount = resolution * resolution;
        Vector3[] vertices = new Vector3[vertCount];
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];

        int triIndex = 0;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = x + y * resolution;

                // Percent across this face in [0,1]
                float percentX = x / (resolution - 1f);
                float percentY = y / (resolution - 1f);

                // Map to cube face in [-1, 1] space
                Vector3 pointOnCube =
                    localUp +
                    (percentX - 0.5f) * 2f * axisA +
                    (percentY - 0.5f) * 2f * axisB;

                // Direction on the unit sphere
                Vector3 dir = pointOnCube.normalized;

                // Sample height noise
                float elevation = CalculateElevation(dir, noiseOffset);

                // Final vertex position (base radius + height)
                Vector3 pointOnSphere = dir * (baseRadius + elevation);
                vertices[i] = pointOnSphere;

                // Make triangles (two per quad)
                if (x != resolution - 1 && y != resolution - 1)
                {
                    int a = i;
                    int b = i + resolution;
                    int c = i + resolution + 1;
                    int d = i + 1;

                    // First tri (a, c, b)
                    triangles[triIndex++] = a;
                    triangles[triIndex++] = c;
                    triangles[triIndex++] = b;

                    // Second tri (a, d, c)
                    triangles[triIndex++] = a;
                    triangles[triIndex++] = d;
                    triangles[triIndex++] = c;
                }
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // --- Height / noise ---

    private float CalculateElevation(Vector3 dir, Vector3 offset)
    {
        // dir is a unit vector on the sphere
        // We'll use its components as coordinates into 2D PerlinNoise
        float f = noiseFrequency;

        // NEW: incorporate per-face noise offset
        float nx = dir.x * f + noiseSeed * 0.123f + offset.x;
        float ny = dir.y * f + noiseSeed * 0.456f + offset.y;
        float nz = dir.z * f + noiseSeed * 0.789f + offset.z;

        // Same blending as before
        float n1 = Mathf.PerlinNoise(nx, ny);
        float n2 = Mathf.PerlinNoise(ny, nz);

        float n = n1 * 0.6f + n2 * 0.4f;

        n = (n - 0.5f) * 2f;   // [-1,1]
        return n * heightAmplitude;
    }

    // ==========================
    //  GIZMO GRID DRAWING (NEW)
    // ==========================
    private void OnDrawGizmosSelected()
    {
        if (!drawGridGizmos) return;

        Gizmos.color = gridColor;

        // For each face, re-use the same math we used to build the mesh
        for (int f = 0; f < 6; f++)
        {
            Vector3 localUp = faceDirections[f];
            Vector3 axisA = new Vector3(localUp.y, localUp.z, localUp.x);
            Vector3 axisB = Vector3.Cross(localUp, axisA);
            Vector3 noiseOffset = faceNoiseOffsets[f];

            for (int y = 0; y < resolution; y += gizmoStep)
            {
                for (int x = 0; x < resolution; x += gizmoStep)
                {
                    float percentX = x / (resolution - 1f);
                    float percentY = y / (resolution - 1f);

                    Vector3 pointOnCube =
                        localUp +
                        (percentX - 0.5f) * 2f * axisA +
                        (percentY - 0.5f) * 2f * axisB;

                    Vector3 dir = pointOnCube.normalized;

                    float elevation = CalculateElevation(dir, noiseOffset);
                    Vector3 pointOnSphere = dir * (baseRadius + elevation);

                    // Draw a little wire cube at this tile center
                    Gizmos.DrawWireCube(transform.position + pointOnSphere,
                                        Vector3.one * gizmoSize);
                }
            }
        }
    }
}
