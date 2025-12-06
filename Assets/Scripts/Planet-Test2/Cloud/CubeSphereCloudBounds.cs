using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static cloud "bounds" shell around a cube-sphere planet.
/// This builds a blocky shell (like the ground blocks) with a fixed
/// inner/outer radius per column. The idea is that a shader will later
/// "paint" actual cloud density/height inside this volume, instead of
/// the CPU rebuilding the mesh based on noise.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CubeSphereCloudBounds : MonoBehaviour
{
    [Header("Planet Link")]
    [Tooltip("Center of the planet. If null, uses this GameObject's transform.")]
    public Transform planetCenter;

    [Tooltip("Base planet radius (same as ground radius).")]
    public float radius = 50f;

    [Header("Grid")]
    [Range(1, 128)]
    [Tooltip("Number of cloud columns per face side.")]
    public int cellsPerFace = 32;

    [Header("Cloud Shell")]
    [Tooltip("Offset from planet radius where the middle of the cloud layer sits (red line).")]
    public float cloudShellOffset = 3f;

    [Tooltip("Total radial thickness of the cloud shell.")]
    public float cloudShellThickness = 2f;

    [Header("Debug")]
    [Tooltip("Regenerate automatically in edit mode when values change.")]
    public bool autoRebuildInEditor = true;

    private static readonly Vector3[] faceDirections =
    {
        Vector3.up, Vector3.down,
        Vector3.left, Vector3.right,
        Vector3.forward, Vector3.back
    };

    private MeshFilter meshFilter;
    private Mesh meshInstance;

    private const int VertsPerCell = 8;

    private void Awake()
    {
        EnsureMesh();
        Generate();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!autoRebuildInEditor)
            return;

        EnsureMesh();
        Generate();
    }
#endif

    private void EnsureMesh()
    {
        if (!meshFilter)
            meshFilter = GetComponent<MeshFilter>();

        meshInstance = meshFilter.sharedMesh;
        if (!meshInstance)
        {
            meshInstance = new Mesh();
            meshInstance.name = "CubeSphereCloudBounds";
            meshFilter.sharedMesh = meshInstance;
        }
    }

    [ContextMenu("Generate Cloud Bounds")]
    public void Generate()
    {
        EnsureMesh();

        Transform center = planetCenter != null ? planetCenter : transform;

        int steps = Mathf.Max(1, cellsPerFace);
        int totalCells = 6 * steps * steps;

        int vertCount = totalCells * VertsPerCell;

        var vertices = new Vector3[vertCount];
        var normals = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];
        var uv2 = new Vector2[vertCount]; // y = height factor [0,1]

        var triangles = new List<int>(totalCells * 6 * 3);

        float midRadius = radius + cloudShellOffset;              // your red line
        float halfThickness = cloudShellThickness * 0.5f;
        float innerRadius = midRadius - halfThickness;
        float outerRadius = midRadius + halfThickness;

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

                    // corners on cube face
                    Vector3 c00 = FacePoint(localUp, axisA, axisB, u0, v0);
                    Vector3 c10 = FacePoint(localUp, axisA, axisB, u1, v0);
                    Vector3 c11 = FacePoint(localUp, axisA, axisB, u1, v1);
                    Vector3 c01 = FacePoint(localUp, axisA, axisB, u0, v1);

                    // spherified directions
                    Vector3 d00 = CubeToSphere(c00);
                    Vector3 d10 = CubeToSphere(c10);
                    Vector3 d11 = CubeToSphere(c11);
                    Vector3 d01 = CubeToSphere(c01);

                    // bottom / top radii are constant for all cells
                    Vector3 b00 = center.position + d00 * innerRadius;
                    Vector3 b10 = center.position + d10 * innerRadius;
                    Vector3 b11 = center.position + d11 * innerRadius;
                    Vector3 b01 = center.position + d01 * innerRadius;

                    Vector3 t00 = center.position + d00 * outerRadius;
                    Vector3 t10 = center.position + d10 * outerRadius;
                    Vector3 t11 = center.position + d11 * outerRadius;
                    Vector3 t01 = center.position + d01 * outerRadius;

                    int vStart = cellIndex * VertsPerCell;

                    // vertices
                    vertices[vStart + 0] = b00;
                    vertices[vStart + 1] = b10;
                    vertices[vStart + 2] = b11;
                    vertices[vStart + 3] = b01;

                    vertices[vStart + 4] = t00;
                    vertices[vStart + 5] = t10;
                    vertices[vStart + 6] = t11;
                    vertices[vStart + 7] = t01;

                    // normals (radial)
                    normals[vStart + 0] = (b00 - center.position).normalized;
                    normals[vStart + 1] = (b10 - center.position).normalized;
                    normals[vStart + 2] = (b11 - center.position).normalized;
                    normals[vStart + 3] = (b01 - center.position).normalized;

                    normals[vStart + 4] = (t00 - center.position).normalized;
                    normals[vStart + 5] = (t10 - center.position).normalized;
                    normals[vStart + 6] = (t11 - center.position).normalized;
                    normals[vStart + 7] = (t01 - center.position).normalized;

                    // UVs per cell (for 2D noise sampling in shader)
                    uvs[vStart + 0] = new Vector2(u0, v0);
                    uvs[vStart + 1] = new Vector2(u1, v0);
                    uvs[vStart + 2] = new Vector2(u1, v1);
                    uvs[vStart + 3] = new Vector2(u0, v1);

                    uvs[vStart + 4] = new Vector2(u0, v0);
                    uvs[vStart + 5] = new Vector2(u1, v0);
                    uvs[vStart + 6] = new Vector2(u1, v1);
                    uvs[vStart + 7] = new Vector2(u0, v1);

                    // UV2: height factor (0 bottom, 1 top) – handy for vertical falloff in shader
                    uv2[vStart + 0] = new Vector2(0f, 0f);
                    uv2[vStart + 1] = new Vector2(0f, 0f);
                    uv2[vStart + 2] = new Vector2(0f, 0f);
                    uv2[vStart + 3] = new Vector2(0f, 0f);

                    uv2[vStart + 4] = new Vector2(0f, 1f);
                    uv2[vStart + 5] = new Vector2(0f, 1f);
                    uv2[vStart + 6] = new Vector2(0f, 1f);
                    uv2[vStart + 7] = new Vector2(0f, 1f);

                    // triangles (same block topology as your other scripts)
                    // bottom
                    triangles.Add(vStart + 0);
                    triangles.Add(vStart + 1);
                    triangles.Add(vStart + 2);
                    triangles.Add(vStart + 0);
                    triangles.Add(vStart + 2);
                    triangles.Add(vStart + 3);

                    // top
                    triangles.Add(vStart + 4);
                    triangles.Add(vStart + 6);
                    triangles.Add(vStart + 5);
                    triangles.Add(vStart + 4);
                    triangles.Add(vStart + 7);
                    triangles.Add(vStart + 6);

                    // sides
                    triangles.Add(vStart + 0);
                    triangles.Add(vStart + 5);
                    triangles.Add(vStart + 1);
                    triangles.Add(vStart + 0);
                    triangles.Add(vStart + 4);
                    triangles.Add(vStart + 5);

                    triangles.Add(vStart + 1);
                    triangles.Add(vStart + 6);
                    triangles.Add(vStart + 2);
                    triangles.Add(vStart + 1);
                    triangles.Add(vStart + 5);
                    triangles.Add(vStart + 6);

                    triangles.Add(vStart + 2);
                    triangles.Add(vStart + 7);
                    triangles.Add(vStart + 3);
                    triangles.Add(vStart + 2);
                    triangles.Add(vStart + 6);
                    triangles.Add(vStart + 7);

                    triangles.Add(vStart + 3);
                    triangles.Add(vStart + 4);
                    triangles.Add(vStart + 0);
                    triangles.Add(vStart + 3);
                    triangles.Add(vStart + 7);
                    triangles.Add(vStart + 4);

                    cellIndex++;
                }
            }
        }

        meshInstance.Clear();
        meshInstance.vertices = vertices;
        meshInstance.normals = normals;
        meshInstance.uv = uvs;
        meshInstance.uv2 = uv2;
        meshInstance.triangles = triangles.ToArray();
        meshInstance.RecalculateBounds();
    }

    // --- Helpers ---

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
