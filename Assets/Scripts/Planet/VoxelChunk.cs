//using System;
//using System.Collections.Generic;
//using UnityEngine;
//using static UnityEngine.UI.GridLayoutGroup;

///// Holds a 16x16x16 voxel array (configurable) and builds one mesh per chunk.
//[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
//public class VoxelChunk : MonoBehaviour
//{
//    public PlanetVoxelWorld world { get; private set; }
//    public Vector3Int chunkIndex { get; private set; }

//    [SerializeField, Range(0f, 1f)] private float normalBlend = 0.4f; // 0=flat, 1=full radial

//    int size;               // 16
//    float voxelSize;        // world units
//    byte[,,] vox;           // 0=air, >0=solid

//    MeshFilter mf;
//    MeshCollider mc; // optional
//    Mesh[] lodMeshes = new Mesh[3];   // 0: 16³, 1: 8³, 2: 4³
//    int currentLOD = -1;

//    public void Init(PlanetVoxelWorld w, Vector3Int ci, int size, float voxelSize)
//    {
//        this.world = w;
//        this.chunkIndex = ci;
//        this.size = size;
//        this.voxelSize = voxelSize;
//        vox = new byte[size, size, size];

//        mf = GetComponent<MeshFilter>();
//        if (!TryGetComponent<MeshRenderer>(out _)) gameObject.AddComponent<MeshRenderer>();
//        // Uncomment if you want physics:
//        // mc = GetComponent<MeshCollider>() ?? gameObject.AddComponent<MeshCollider>();
//    }

//    public void SetMaterial(Material m) => GetComponent<MeshRenderer>().sharedMaterial = m;

//    public void SetVoxel(int x, int y, int z, byte v)
//    {
//        vox[x, y, z] = v;
//    }

//    public byte GetVoxel(int x, int y, int z) => vox[x, y, z];

//    public void RebuildMesh()
//    {
//        var verts = new List<Vector3>(size * size * 6);
//        var norms = new List<Vector3>(size * size * 6);
//        var tris = new List<int>(size * size * 12);
//        var uvs = new List<Vector2>(size * size * 6);

//        // For neighbor sampling across chunk boundaries,
//        // we query the world when on edges.
//        // Local->world position helper:
//        Vector3 Base = transform.localPosition;

//        // Directions
//        Vector3Int[] dirs = {
//            new Vector3Int( 1, 0, 0),
//            new Vector3Int(-1, 0, 0),
//            new Vector3Int( 0, 1, 0),
//            new Vector3Int( 0,-1, 0),
//            new Vector3Int( 0, 0, 1),
//            new Vector3Int( 0, 0,-1),
//        };
//        Vector3[] faceNormals = {
//            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
//        };
//        Vector3[,] faceCorners = {
//            // +X
//            { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
//            // -X
//            { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) },
//            // +Y
//            { new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(0,1,1) },
//            // -Y
//            { new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,0,0), new Vector3(0,0,0) },
//            // +Z
//            { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,0,1) },
//            // -Z
//            { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(0,1,0), new Vector3(0,0,0) },
//        };

//        // Build faces where solid borders air
//        for (int z = 0; z < size; z++)
//            for (int y = 0; y < size; y++)
//                for (int x = 0; x < size; x++)
//                {
//                    if (vox[x, y, z] == 0) continue;

//                    for (int d = 0; d < 6; d++)
//                    {
//                        int nx = x + dirs[d].x;
//                        int ny = y + dirs[d].y;
//                        int nz = z + dirs[d].z;

//                        bool neighborSolid;
//                        if (nx >= 0 && nx < size && ny >= 0 && ny < size && nz >= 0 && nz < size)
//                        {
//                            neighborSolid = vox[nx, ny, nz] != 0;
//                        }
//                        else
//                        {
//                            // Query neighbor chunk
//                            Vector3Int globalCell = LocalCellToGlobalCell(new Vector3Int(x, y, z)) + dirs[d];
//                            neighborSolid = world.SampleVoxelGlobal(globalCell) != 0;
//                        }

//                        if (neighborSolid) continue; // internal face -> skip

//                        // Emit this face
//                        int vStart = verts.Count;

//                        // Corner positions in local voxel coords -> world space
//                        for (int c = 0; c < 4; c++)
//                        {
//                            Vector3 corner = (new Vector3(x, y, z) + faceCorners[d, c]) * voxelSize;
//                            verts.Add(corner);
//                            norms.Add(faceNormals[d]);
//                            uvs.Add(c == 0 ? new Vector2(0, 0) :
//                                    c == 1 ? new Vector2(0, 1) :
//                                    c == 2 ? new Vector2(1, 1) : new Vector2(1, 0));
//                        }

//                        tris.Add(vStart + 0); tris.Add(vStart + 1); tris.Add(vStart + 2);
//                        tris.Add(vStart + 0); tris.Add(vStart + 2); tris.Add(vStart + 3);
//                    }
//                }

//        var mesh = new Mesh();
//        mesh.indexFormat = (verts.Count > 65000) ?
//            UnityEngine.Rendering.IndexFormat.UInt32 :
//            UnityEngine.Rendering.IndexFormat.UInt16;

//        mesh.SetVertices(verts);
//        mesh.SetNormals(norms);
//        mesh.SetUVs(0, uvs);
//        mesh.SetTriangles(tris, 0);
//        mesh.RecalculateBounds();

//        mf.sharedMesh = mesh;
//        if (mc) mc.sharedMesh = mesh;
//    }

//    // Convert a local voxel cell index to a global cell index (in world grid units)
//    Vector3Int LocalCellToGlobalCell(Vector3Int localCell)
//    {
//        // Global cell index = chunkIndex*chunkSize + localCell, BUT we also need to consider voxel size and world offset.
//        // PlanetVoxelWorld.SampleVoxelGlobal does mapping via worldOriginOffset and voxelSize,
//        // so here we compute the global cell by using our chunk's origin (in world units).
//        int gx = Mathf.FloorToInt((transform.localPosition.x / voxelSize)) + localCell.x;
//        int gy = Mathf.FloorToInt((transform.localPosition.y / voxelSize)) + localCell.y;
//        int gz = Mathf.FloorToInt((transform.localPosition.z / voxelSize)) + localCell.z;
//        return new Vector3Int(gx, gy, gz);
//    }

//    // Call after voxels are filled or edited:
//    public void RebuildAllLODs(bool useRadialNormals, Vector3 planetCenterWorld)
//    {
//        lodMeshes[0] = BuildMeshFromGrid(vox, size, voxelSize, useRadialNormals, planetCenterWorld);

//        byte[,,] v8 = Downsample(vox, size, 2);
//        lodMeshes[1] = BuildMeshFromGrid(v8, size / 2, voxelSize * 2f, useRadialNormals, planetCenterWorld);

//        byte[,,] v4 = Downsample(v8, size / 2, 2);
//        lodMeshes[2] = BuildMeshFromGrid(v4, size / 4, voxelSize * 4f, useRadialNormals, planetCenterWorld);

//        Debug.Log($"{name}: LOD0={(lodMeshes[0] ? "OK" : "NULL")}, LOD1={(lodMeshes[1] ? "OK" : "NULL")}, LOD2={(lodMeshes[2] ? "OK" : "NULL")}");
//        SetActiveLOD(1);
//    }

//    public void SetActiveLOD(int lod)
//    {
//        lod = Mathf.Clamp(lod, 0, 2);

//        if (!HasLOD(lod))
//        {
//            if (lod == 2 && HasLOD(1)) lod = 1;
//            else if (HasLOD(0)) lod = 0;
//            else
//            {
//                Debug.LogWarning($"{name}: All LODs empty — nothing to render.");
//                return;
//            }
//        }

//        if (lod == currentLOD) return;
//        currentLOD = lod;
//        if (mf == null) mf = GetComponent<MeshFilter>();
//        mf.sharedMesh = lodMeshes[lod];
//        if (mc) mc.sharedMesh = lodMeshes[lod];
//    }

//    // Simple downsample: a dst cell is solid if ANY of its 2x2x2 src cells are solid
//    static byte[,,] Downsample(byte[,,] src, int srcSize, int factor)
//    {
//        int dstSize = srcSize / factor;
//        var dst = new byte[dstSize, dstSize, dstSize];
//        for (int z = 0; z < dstSize; z++)
//            for (int y = 0; y < dstSize; y++)
//                for (int x = 0; x < dstSize; x++)
//                {
//                    byte solid = 0;
//                    for (int dz = 0; dz < factor; dz++)
//                        for (int dy = 0; dy < factor; dy++)
//                            for (int dx = 0; dx < factor; dx++)
//                            {
//                                if (src[x * factor + dx, y * factor + dy, z * factor + dz] != 0) { solid = 1; goto WRITE; }
//                            }
//                        WRITE:
//                    dst[x, y, z] = solid;
//                }
//        return dst;
//    }

//    // Factor your existing “face mesher” into a function that takes an arbitrary grid & cell size:
//    Mesh BuildMeshFromGrid(byte[,,] grid, int gridSize, float cellSize, bool useRadialNormals, Vector3 planetCenterWorld)
//    {
//        var verts = new List<Vector3>();
//        var norms = new List<Vector3>();
//        var tris = new List<int>();
//        var uvs = new List<Vector2>();

//        var l2w = transform.localToWorldMatrix;
//        var w2l = transform.worldToLocalMatrix;

//        Vector3Int[] dirs = {
//        new Vector3Int( 1, 0, 0),
//        new Vector3Int(-1, 0, 0),
//        new Vector3Int( 0, 1, 0),
//        new Vector3Int( 0,-1, 0),
//        new Vector3Int( 0, 0, 1),
//        new Vector3Int( 0, 0,-1),
//    };
//        Vector3[] faceNormals = {
//        Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
//    };
//        Vector3[,] faceCorners = {
//        { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
//        { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) },
//        { new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(0,1,1) },
//        { new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,0,0), new Vector3(0,0,0) },
//        { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,0,1) },
//        { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(0,1,0), new Vector3(0,0,0) },
//    };

//        for (int z = 0; z < gridSize; z++)
//            for (int y = 0; y < gridSize; y++)
//                for (int x = 0; x < gridSize; x++)
//                {
//                    if (grid[x, y, z] == 0) continue;

//                    for (int d = 0; d < 6; d++)
//                    {
//                        int nx = x + dirs[d].x;
//                        int ny = y + dirs[d].y;
//                        int nz = z + dirs[d].z;

//                        bool neighborSolid =
//                            (nx >= 0 && nx < gridSize &&
//                             ny >= 0 && ny < gridSize &&
//                             nz >= 0 && nz < gridSize)
//                            ? grid[nx, ny, nz] != 0
//                            : false;

//                        if (neighborSolid) continue;

//                        int vStart = verts.Count;
//                        for (int c = 0; c < 4; c++)
//                        {
//                            Vector3 cornerLS = (new Vector3(x, y, z) + faceCorners[d, c]) * cellSize;
//                            verts.Add(cornerLS);

//                            // --- NORMALS ---
//                            // flat per-face normal
//                            Vector3 flat = faceNormals[d];

//                            // radial (planet) normal
//                            Vector3 smooth = flat;
//                            if (useRadialNormals)
//                            {
//                                Vector3 cornerWS = l2w.MultiplyPoint3x4(cornerLS);
//                                Vector3 nWS = (cornerWS - planetCenterWorld).normalized;
//                                smooth = w2l.MultiplyVector(nWS).normalized;
//                            }

//                            // blend (0 = flat cubes, 1 = smooth sphere)
//                            Vector3 nLocal = Vector3.Normalize(Vector3.Lerp(flat, smooth, normalBlend));
//                            norms.Add(nLocal);
//                            // ---------------

//                            uvs.Add(c switch
//                            {
//                                0 => new Vector2(0, 0),
//                                1 => new Vector2(0, 1),
//                                2 => new Vector2(1, 1),
//                                _ => new Vector2(1, 0),
//                            });
//                        }

//                        tris.Add(vStart + 0); tris.Add(vStart + 1); tris.Add(vStart + 2);
//                        tris.Add(vStart + 0); tris.Add(vStart + 2); tris.Add(vStart + 3);
//                    }
//                }

//        var mesh = new Mesh();
//        mesh.indexFormat = verts.Count > 65000
//            ? UnityEngine.Rendering.IndexFormat.UInt32
//            : UnityEngine.Rendering.IndexFormat.UInt16;

//        mesh.SetVertices(verts);
//        mesh.SetNormals(norms);
//        mesh.SetUVs(0, uvs);
//        mesh.SetTriangles(tris, 0);
//        mesh.RecalculateBounds();
//        return mesh;
//    }

//    // Handy center for distance testing:
//    public Vector3 WorldCenter()
//    {
//        // chunk origin is localPosition; center is + half-extent
//        float extent = size * voxelSize * 0.5f;
//        return transform.TransformPoint(new Vector3(extent, extent, extent));
//    }

//    public bool HasLOD(int lod)
//    {
//        return lod >= 0 && lod < lodMeshes.Length
//            && lodMeshes[lod] != null
//            && lodMeshes[lod].vertexCount > 0;
//    }

//    // Render-only warp: shrink vertices toward planet center by factor t (0..1).
//    // t=0 -> no change, t=0.5 -> half the distance to center, t=1 -> all the way (don't do 1).
//    public void ApplyRadialShrink(float t, Vector3 planetCenterWorld)
//    {
//        // If we don't have a valid active LOD yet, bail.
//        if (!HasLOD(currentLOD))
//            return;

//        // When t~0, ensure we are showing the *base* LOD mesh (undo any previous warp).
//        if (t <= 0.0001f)
//        {
//            if (mf == null) mf = GetComponent<MeshFilter>();
//            if (mf.sharedMesh != lodMeshes[currentLOD])
//            {
//                mf.sharedMesh = lodMeshes[currentLOD];
//                if (mc) mc.sharedMesh = lodMeshes[currentLOD];
//            }
//            return;
//        }

//        // Always warp from the *base* LOD mesh (no accumulation).
//        var src = lodMeshes[currentLOD];
//        if (!src || src.vertexCount == 0)
//            return;

//        // Clone once per call; for perf we’ll only call when t meaningfully changes (see controller).
//        var dst = Instantiate(src);

//        var verts = dst.vertices;
//        var l2w = transform.localToWorldMatrix;
//        var w2l = transform.worldToLocalMatrix;

//        for (int i = 0; i < verts.Length; i++)
//        {
//            Vector3 w = l2w.MultiplyPoint3x4(verts[i]);
//            Vector3 dir = (w - planetCenterWorld);
//            float r = dir.magnitude;
//            if (r > 1e-4f)
//            {
//                Vector3 wShrunk = planetCenterWorld + dir * (1f - t);
//                verts[i] = w2l.MultiplyPoint3x4(wShrunk);
//            }
//        }

//        dst.vertices = verts;
//        dst.RecalculateBounds();

//        if (mf == null) mf = GetComponent<MeshFilter>();
//        mf.sharedMesh = dst;
//        if (mc) mc.sharedMesh = dst;
//    }
//}
