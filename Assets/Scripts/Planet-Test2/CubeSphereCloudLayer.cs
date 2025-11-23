using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CubeSphereCloudLayer : MonoBehaviour
{
    [Header("Planet Link")]
    public Transform planetCenter;
    public float radius = 50f;

    [Header("Grid")]
    [Range(1, 128)]
    public int cellsPerFace = 32;

    [Header("Cloud Shape")]
    [Tooltip("Base height above ground where clouds start.")]
    public float cloudBaseOffset = 3f;   // above ground
    [Tooltip("Max additional puff height for the tallest clouds.")]
    public float cloudMaxHeight = 2f;    // max puff height

    [Header("Cloud Noise (Continental)")]
    [Tooltip("Lower = larger cloud blobs.")]
    public float cloudFrequency = 0.25f;
    public int cloudSeed = 42;
    [Range(0f, 1f)]
    [Tooltip("Bias for coverage. Higher = fewer clouds.")]
    public float coverageBias = 0.4f;

    [Header("Animation / Scrolling")]
    [Tooltip("If enabled, the cloud noise scrolls and the mesh is rebuilt over time.")]
    public bool animate = true;

    [Tooltip("Seconds between rebuilds. Larger = cheaper but more 'steppy'.")]
    public float rebuildInterval = 5f;

    [Tooltip("Direction in which the noise field scrolls.")]
    public Vector3 noiseScrollDirection = new Vector3(1f, 0f, 0.3f);

    [Tooltip("Speed multiplier for noise scrolling.")]
    public float noiseScrollSpeed = 0.1f;

    [Header("Camera Clearance")]
    public Transform cameraTransform;
    public float clearanceRadius = 15f;      // inner radius: fully clear
    public float clearanceFade = 5f;         // fade band outside inner radius

    private static readonly Vector3[] faceDirections =
    {
        Vector3.up, Vector3.down,
        Vector3.left, Vector3.right,
        Vector3.forward, Vector3.back
    };

    private MeshFilter meshFilter;
    private Mesh meshInstance;

    // Cached buffers
    private Vector3[] vertices;
    private Vector3[] normals;
    private Vector2[] uvs;
    private Color[] colors;
    private int[] triangles;

    private int steps;
    private int totalCells;
    private const int VertsPerCell = 8;

    // runtime animation state
    private float rebuildTimer = 0f;
    private Vector3 noiseScrollOffset = Vector3.zero;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshInstance = meshFilter.sharedMesh;

        if (meshInstance == null)
        {
            meshInstance = new Mesh();
            meshInstance.name = "CubeSphereCloudLayer";
            meshFilter.sharedMesh = meshInstance;
        }

        InitBuffers();
        BuildCloudMesh();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (!animate) return;

        // Smoothly advance scroll offset
        Vector3 dir = noiseScrollDirection.sqrMagnitude > 0.0001f
            ? noiseScrollDirection.normalized
            : Vector3.zero;

        noiseScrollOffset += dir * (noiseScrollSpeed * Time.deltaTime);

        // Timed rebuild
        rebuildTimer += Time.deltaTime;
        if (rebuildTimer >= rebuildInterval)
        {
            rebuildTimer = 0f;
            BuildCloudMesh();
        }
    }

    /// <summary>
    /// Allocate and initialize buffers once, including fixed triangle topology.
    /// </summary>
    private void InitBuffers()
    {
        steps = Mathf.Max(1, cellsPerFace);
        totalCells = 6 * steps * steps;

        int vertCount = totalCells * VertsPerCell;

        vertices = new Vector3[vertCount];
        normals = new Vector3[vertCount];
        uvs = new Vector2[vertCount];
        colors = new Color[vertCount];

        // Build triangle indices once
        List<int> triList = new List<int>(totalCells * 6 * 3);

        for (int cellIndex = 0; cellIndex < totalCells; cellIndex++)
        {
            int vStart = cellIndex * VertsPerCell;

            // Bottom
            triList.Add(vStart + 0);
            triList.Add(vStart + 1);
            triList.Add(vStart + 2);
            triList.Add(vStart + 0);
            triList.Add(vStart + 2);
            triList.Add(vStart + 3);

            // Top
            triList.Add(vStart + 4);
            triList.Add(vStart + 6);
            triList.Add(vStart + 5);
            triList.Add(vStart + 4);
            triList.Add(vStart + 7);
            triList.Add(vStart + 6);

            // Sides (same pattern as before)
            triList.Add(vStart + 0);
            triList.Add(vStart + 5);
            triList.Add(vStart + 1);
            triList.Add(vStart + 0);
            triList.Add(vStart + 4);
            triList.Add(vStart + 5);

            triList.Add(vStart + 1);
            triList.Add(vStart + 6);
            triList.Add(vStart + 2);
            triList.Add(vStart + 1);
            triList.Add(vStart + 5);
            triList.Add(vStart + 6);

            triList.Add(vStart + 2);
            triList.Add(vStart + 7);
            triList.Add(vStart + 3);
            triList.Add(vStart + 2);
            triList.Add(vStart + 6);
            triList.Add(vStart + 7);

            triList.Add(vStart + 3);
            triList.Add(vStart + 4);
            triList.Add(vStart + 0);
            triList.Add(vStart + 3);
            triList.Add(vStart + 7);
            triList.Add(vStart + 4);
        }

        triangles = triList.ToArray();

        meshInstance.Clear();
        meshInstance.vertices = vertices;
        meshInstance.normals = normals;
        meshInstance.uv = uvs;
        meshInstance.colors = colors;
        meshInstance.triangles = triangles;
        meshInstance.RecalculateBounds();
    }

    [ContextMenu("Rebuild Cloud Layer Now")]
    public void BuildCloudMesh()
    {
        if (meshInstance == null)
        {
            meshInstance = new Mesh();
            meshInstance.name = "CubeSphereCloudLayer";
            meshFilter.sharedMesh = meshInstance;
            InitBuffers();
        }

        Transform center = planetCenter != null ? planetCenter : transform;

        float baseRadius = radius + cloudBaseOffset;

        int cellIndex = 0;

        for (int face = 0; face < 6; face++)
        {
            Vector3 localUp = faceDirections[face];
            Vector3 axisA = new Vector3(localUp.y, localUp.z, localUp.x);
            Vector3 axisB = Vector3.Cross(localUp, axisA);

            for (int y = 0; y < steps; y++)
            {
                for (int x = 0; x < steps; x++)
                {
                    float u0 = (float)x / steps;
                    float v0 = (float)y / steps;
                    float u1 = (float)(x + 1) / steps;
                    float v1 = (float)(y + 1) / steps;

                    Vector3 c00 = FacePoint(localUp, axisA, axisB, u0, v0);
                    Vector3 c10 = FacePoint(localUp, axisA, axisB, u1, v0);
                    Vector3 c11 = FacePoint(localUp, axisA, axisB, u1, v1);
                    Vector3 c01 = FacePoint(localUp, axisA, axisB, u0, v1);

                    Vector3 d00 = CubeToSphere(c00);
                    Vector3 d10 = CubeToSphere(c10);
                    Vector3 d11 = CubeToSphere(c11);
                    Vector3 d01 = CubeToSphere(c01);

                    Vector3 dCenter = (d00 + d10 + d11 + d01) * 0.25f;
                    dCenter.Normalize();

                    // --- 1. noise -> base density ---
                    float n = SampleSphereNoiseScrolled(dCenter, cloudFrequency, cloudSeed, noiseScrollOffset);

                    float density;
                    if (n < coverageBias)
                        density = 0f;
                    else
                        density = Mathf.Clamp01((n - coverageBias) / (1f - coverageBias));

                    // --- 2. camera clearance bubble in *local* space ---
                    if (cameraTransform != null && density > 0f)
                    {
                        // Approximate cell center on the shell at baseRadius in LOCAL space
                        // (center.position is world, so bring it into local too)
                        Vector3 centerLocal = transform.InverseTransformPoint(center.position);
                        Vector3 cellCenterLocal = centerLocal + dCenter * baseRadius;

                        Vector3 camLocal = transform.InverseTransformPoint(cameraTransform.position);

                        float inner = clearanceRadius;
                        float outer = clearanceRadius + clearanceFade;

                        float dist = Vector3.Distance(camLocal, cellCenterLocal);

                        if (dist <= inner)
                        {
                            density = 0f; // fully cleared
                        }
                        else if (dist <= outer)
                        {
                            float t = Mathf.InverseLerp(outer, inner, dist);
                            density *= t;
                        }
                    }

                    // --- 3. center the cloud column around the baseRadius ---
                    float height = cloudMaxHeight * density;
                    float halfHeight = height * 0.5f;

                    float midRadius = baseRadius;              // your red line (cloudBaseOffset radius)
                    float bottomRadius = midRadius - halfHeight;
                    float topRadius = midRadius + halfHeight;

                    // world-space positions for this column (same as before)
                    Vector3 b00 = center.position + d00 * bottomRadius;
                    Vector3 b10 = center.position + d10 * bottomRadius;
                    Vector3 b11 = center.position + d11 * bottomRadius;
                    Vector3 b01 = center.position + d01 * bottomRadius;

                    Vector3 t00 = center.position + d00 * topRadius;
                    Vector3 t10 = center.position + d10 * topRadius;
                    Vector3 t11 = center.position + d11 * topRadius;
                    Vector3 t01 = center.position + d01 * topRadius;

                    int vStart = cellIndex * VertsPerCell;

                    vertices[vStart + 0] = b00;
                    vertices[vStart + 1] = b10;
                    vertices[vStart + 2] = b11;
                    vertices[vStart + 3] = b01;

                    vertices[vStart + 4] = t00;
                    vertices[vStart + 5] = t10;
                    vertices[vStart + 6] = t11;
                    vertices[vStart + 7] = t01;

                    // UVs per face
                    uvs[vStart + 0] = new Vector2(u0, v0);
                    uvs[vStart + 1] = new Vector2(u1, v0);
                    uvs[vStart + 2] = new Vector2(u1, v1);
                    uvs[vStart + 3] = new Vector2(u0, v1);

                    uvs[vStart + 4] = new Vector2(u0, v0);
                    uvs[vStart + 5] = new Vector2(u1, v0);
                    uvs[vStart + 6] = new Vector2(u1, v1);
                    uvs[vStart + 7] = new Vector2(u0, v1);

                    // Radial normals
                    normals[vStart + 0] = (b00 - center.position).normalized;
                    normals[vStart + 1] = (b10 - center.position).normalized;
                    normals[vStart + 2] = (b11 - center.position).normalized;
                    normals[vStart + 3] = (b01 - center.position).normalized;

                    normals[vStart + 4] = (t00 - center.position).normalized;
                    normals[vStart + 5] = (t10 - center.position).normalized;
                    normals[vStart + 6] = (t11 - center.position).normalized;
                    normals[vStart + 7] = (t01 - center.position).normalized;

                    // store final density in vertex alpha (for shader cutout)
                    Color c = new Color(1f, 1f, 1f, density);
                    for (int i = 0; i < 8; i++)
                        colors[vStart + i] = c;

                    cellIndex++;
                }
            }
        }

        meshInstance.vertices = vertices;
        meshInstance.normals = normals;
        meshInstance.uv = uvs;
        meshInstance.colors = colors;
        meshInstance.RecalculateBounds();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Re-init if grid size changed in editor
        if (!Application.isPlaying)
        {
            meshFilter = GetComponent<MeshFilter>();
            meshInstance = meshFilter.sharedMesh;
            if (meshInstance == null)
            {
                meshInstance = new Mesh();
                meshInstance.name = "CubeSphereCloudLayer";
                meshFilter.sharedMesh = meshInstance;
            }

            InitBuffers();
            noiseScrollOffset = Vector3.zero;
            rebuildTimer = 0f;
            BuildCloudMesh();
        }
    }
#endif

    // --- helpers ---

    /// <summary>
    /// Spherical-ish Perlin noise with a scrolling offset.
    /// </summary>
    private float SampleSphereNoiseScrolled(Vector3 dir, float frequency, int seed, Vector3 scroll)
    {
        float f = frequency;
        float sx = dir.x * f + seed * 0.123f + scroll.x;
        float sy = dir.y * f + seed * 0.456f + scroll.y;
        float sz = dir.z * f + seed * 0.789f + scroll.z;

        float n1 = Mathf.PerlinNoise(sx, sy);
        float n2 = Mathf.PerlinNoise(sy, sz);
        float n3 = Mathf.PerlinNoise(sz, sx);

        return (n1 + n2 + n3) / 3f;
    }

    private static Vector3 CubeToSphere(Vector3 p)
    {
        float x = p.x, y = p.y, z = p.z;
        float x2 = x * x, y2 = y * y, z2 = z * z;

        float sx = x * Mathf.Sqrt(1f - 0.5f * (y2 + z2) + (y2 * z2) / 3f);
        float sy = y * Mathf.Sqrt(1f - 0.5f * (z2 + x2) + (z2 * x2) / 3f);
        float sz = z * Mathf.Sqrt(1f - 0.5f * (x2 + y2) + (x2 * y2) / 3f);

        return new Vector3(sx, sy, sz).normalized;
    }

    private static Vector3 FacePoint(Vector3 localUp, Vector3 axisA, Vector3 axisB, float u, float v)
    {
        return localUp +
               (u - 0.5f) * 2f * axisA +
               (v - 0.5f) * 2f * axisB;
    }
}
