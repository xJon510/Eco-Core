//using System.Collections.Generic;
//using UnityEngine;

//#if UNITY_EDITOR
//using UnityEditor;
//#endif

///// Generates a hollow sphere shell using a spherical heightmap,
///// stored as voxels in 16x16x16 chunks, one mesh per chunk.
//public class PlanetVoxelWorld : MonoBehaviour
//{
//    [Header("Voxel & Chunk")]
//    public int chunkSize = 16;             // 16x16x16
//    [Min(0.1f)] public float voxelSize = 1f; // world units per voxel cell edge
//    public Material chunkMaterial;

//    [Header("Base Sphere")]
//    [Min(0.1f)] public float radius = 40f;   // base planet radius
//    [Range(0.1f, 3f)] public float shellThicknessInVoxels = 0.8f; // ~thickness in voxel units

//    [Header("Height from Spherical Noise")]
//    public bool useHeightMap = true;
//    public float heightAmp = 3.0f;     // world units
//    public float heightFreq = 0.06f;   // cycles per world unit
//    [Range(1, 6)] public int octaves = 4;
//    public float lacunarity = 2.0f;
//    public float gain = 0.5f;
//    public int seed = 1337;

//    [Header("Generation Bounds / Safety")]
//    public bool offsetToPositiveSpace = false; // shifts chunk grid so indices are >= 0
//    [Min(100)] public int maxVoxels = 1_500_000; // safety cap

//    [Header("Editor")]
//    public bool autoRegenerateInEditor = false;

//    [Header("Runtime")]
//    public bool generateOnPlay = true;
//    public bool verboseLogs = true;

//    [Header("Shading")]
//    public bool useRadialNormals = true;

//    // World containers
//    public Transform chunksRoot;
//    readonly Dictionary<Vector3Int, VoxelChunk> chunks = new();

//    // Precompute offset for positive-only indexing
//    Vector3 worldOriginOffset = Vector3.zero;
//    Vector3 noiseOffset;
//    public Vector3 PlanetCenterWorld => transform.position;

//    void Start()
//    {
//        if (generateOnPlay)
//        {
//            if (verboseLogs) Debug.Log("[PlanetVoxelWorld] Start() -> Generate()");
//            Generate();
//        }
//    }

//    [ContextMenu("Generate Planet Voxels")]
//    public void Generate()
//    {
//        if (verboseLogs) Debug.Log("[PlanetVoxelWorld] Generate BEGIN");

//        ClearAll();


//        if (!chunksRoot)
//        {
//            var go = new GameObject("_Chunks");
//            go.transform.SetParent(transform, false);
//            chunksRoot = go.transform;
//        }

//        Random.InitState(seed);
//        noiseOffset = new Vector3(Random.value * 1000f, Random.value * 1000f, Random.value * 1000f);

//        // Thickness in world units
//        float shellThicknessWU = shellThicknessInVoxels * voxelSize;

//        // Conservative bound
//        float bound = radius + (useHeightMap ? Mathf.Abs(heightAmp) : 0f) + shellThicknessWU + voxelSize * 2f;

//        if (offsetToPositiveSpace)
//            worldOriginOffset = Vector3.one * bound; // shift so min corner becomes ~0
//        else
//            worldOriginOffset = Vector3.zero;

//        // Iterate a 3D grid at voxelSize spacing
//        int steps = Mathf.CeilToInt((bound * 2f) / voxelSize);
//        int half = steps / 2;

//        int written = 0;

//        for (int ix = -half; ix <= half; ix++)
//        {
//            float x = ix * voxelSize;
//            for (int iy = -half; iy <= half; iy++)
//            {
//                float y = iy * voxelSize;
//                for (int iz = -half; iz <= half; iz++)
//                {
//                    float z = iz * voxelSize;
//                    Vector3 p = new Vector3(x, y, z);
//                    float r = p.magnitude;
//                    if (r < 1e-4f) continue;

//                    // Direction on sphere
//                    Vector3 dir = p / r;

//                    // Target radius at this direction (spherical heightmap)
//                    float targetR = radius;
//                    if (useHeightMap && heightAmp != 0f)
//                    {
//                        float h01 = FBM3D(dir * heightFreq + noiseOffset, octaves, lacunarity, gain);
//                        float h = (h01 * 2f - 1f) * heightAmp; // [-amp, +amp]
//                        targetR += h;
//                    }

//                    // Keep cells within a thin band around targetR
//                    float halfThick = 0.5f * shellThicknessWU;
//                    if (r < targetR - halfThick || r > targetR + halfThick)
//                        continue;

//                    // Safety
//                    if (++written > maxVoxels)
//                    {
//                        Debug.LogWarning($"[PlanetVoxelWorld] Hit maxVoxels={maxVoxels}. Stopping fill.");
//                        AssignMaterialsAndBuildAll(); // build what we have
//                        return;
//                    }

//                    // Write voxel = solid
//                    WriteVoxel(p, true);
//                }
//            }
//        }

//        AssignMaterialsAndBuildAll();
//        Debug.Log($"[PlanetVoxelWorld] Wrote {written} voxels across {chunks.Count} chunks.");
//    }

//    void AssignMaterialsAndBuildAll()
//    {
//        // Fallback material if none provided (URP Lit if available, else Standard)
//        if (!chunkMaterial)
//        {
//            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
//            var std = Shader.Find("Standard");
//            var sh = urpLit ? urpLit : std;
//            chunkMaterial = new Material(sh);
//            if (verboseLogs) Debug.Log("[PlanetVoxelWorld] No material set; assigned fallback material.");
//        }

//        foreach (var kv in chunks)
//        {
//            var c = kv.Value;
//            if (chunkMaterial) c.SetMaterial(chunkMaterial);
//            c.RebuildAllLODs(useRadialNormals, PlanetCenterWorld); // pass toggle + center
//        }
//    }

//    // --- World <-> Chunk mapping & editing ---

//    void WriteVoxel(Vector3 localPos, bool solid)
//    {
//        // Apply offset if requested
//        Vector3 shifted = localPos + worldOriginOffset;

//        // Chunk index in local space
//        Vector3Int ci = new(
//            Mathf.FloorToInt(shifted.x / (chunkSize * voxelSize)),
//            Mathf.FloorToInt(shifted.y / (chunkSize * voxelSize)),
//            Mathf.FloorToInt(shifted.z / (chunkSize * voxelSize)));

//        // Local position inside that chunk (0..chunkSize-1)
//        int vx = FloorToIntMod(shifted.x / voxelSize, chunkSize);
//        int vy = FloorToIntMod(shifted.y / voxelSize, chunkSize);
//        int vz = FloorToIntMod(shifted.z / voxelSize, chunkSize);

//        var chunk = GetOrCreateChunk(ci);
//        chunk.SetVoxel(vx, vy, vz, solid ? (byte)1 : (byte)0);
//    }

//    public VoxelChunk GetOrCreateChunk(Vector3Int ci)
//    {
//        if (chunks.TryGetValue(ci, out var c)) return c;

//        var go = new GameObject($"Chunk_{ci.x}_{ci.y}_{ci.z}");
//        go.transform.SetParent(chunksRoot, false);

//        // Place chunk at its local origin (corner), so child mesh verts are small numbers
//        Vector3 chunkOriginLS = new Vector3(
//            ci.x * chunkSize * voxelSize,
//            ci.y * chunkSize * voxelSize,
//            ci.z * chunkSize * voxelSize
//        ) - worldOriginOffset;
//        go.transform.localPosition = chunkOriginLS;

//        c = go.AddComponent<VoxelChunk>();
//        c.Init(this, ci, chunkSize, voxelSize);

//        chunks.Add(ci, c);
//        return c;
//    }

//    public bool TryGetChunk(Vector3Int ci, out VoxelChunk c) => chunks.TryGetValue(ci, out c);

//    public byte SampleVoxelGlobal(Vector3Int globalCell)
//    {
//        // Map global cell index -> chunk index + local voxel index
//        Vector3 cellPosWU = new Vector3(globalCell.x * voxelSize, globalCell.y * voxelSize, globalCell.z * voxelSize);
//        Vector3 shifted = cellPosWU + worldOriginOffset;
//        Vector3Int ci = new(
//            Mathf.FloorToInt(shifted.x / (chunkSize * voxelSize)),
//            Mathf.FloorToInt(shifted.y / (chunkSize * voxelSize)),
//            Mathf.FloorToInt(shifted.z / (chunkSize * voxelSize)));
//        if (!chunks.TryGetValue(ci, out var chunk)) return 0;

//        int vx = FloorToIntMod(shifted.x / voxelSize, chunkSize);
//        int vy = FloorToIntMod(shifted.y / voxelSize, chunkSize);
//        int vz = FloorToIntMod(shifted.z / voxelSize, chunkSize);
//        return chunk.GetVoxel(vx, vy, vz);
//    }

//    static int FloorToIntMod(float value, int mod)
//    {
//        int v = Mathf.FloorToInt(value);
//        int m = v % mod;
//        if (m < 0) m += mod;
//        return m;
//    }

//    // --- Noise helpers ---

//    static float Noise3D(Vector3 p)
//    {
//        float a = Mathf.PerlinNoise(p.x, p.y);
//        float b = Mathf.PerlinNoise(p.y, p.z);
//        float c = Mathf.PerlinNoise(p.z, p.x);
//        return (a + b + c) / 3f;
//    }

//    static float FBM3D(Vector3 p, int oct, float lac, float gain)
//    {
//        float amp = 0.5f, sum = 0f, freq = 1f;
//        for (int i = 0; i < oct; i++)
//        {
//            sum += Noise3D(p * freq) * amp;
//            freq *= lac;
//            amp *= gain;
//        }
//        return Mathf.Clamp01(sum);
//    }

//    // --- Clear ---

//    [ContextMenu("Clear All")]
//    public void ClearAll()
//    {
//        chunks.Clear();
//        if (chunksRoot)
//        {
//            var kill = new List<GameObject>();
//            foreach (Transform t in chunksRoot) kill.Add(t.gameObject);
//            foreach (var go in kill)
//            {
//#if UNITY_EDITOR
//                if (!Application.isPlaying) DestroyImmediate(go);
//                else Destroy(go);
//#else
//                Destroy(go);
//#endif
//            }
//        }
//    }

//#if UNITY_EDITOR
//    void OnValidate()
//    {
//        if (!autoRegenerateInEditor) return;
//        if (!EditorApplication.isPlayingOrWillChangePlaymode)
//        {
//            EditorApplication.delayCall += () =>
//            {
//                if (this == null) return;
//                Generate();
//            };
//        }
//    }
//#endif
//}
