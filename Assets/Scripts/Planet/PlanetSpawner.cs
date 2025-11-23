using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// Spawns a hollow voxel shell with heightmap, grouped into 16x16x16 (configurable) chunk containers.
public class PlanetSpawner : MonoBehaviour
{
    [Header("Base Sphere")]
    [Min(0.1f)] public float radius = 20f;
    [Min(0.05f)] public float cubeSize = 1f;
    [Range(0.1f, 3f)] public float shellThicknessInCubes = 0.75f;
    [Min(0.5f)] public float spacingMultiplier = 1.0f;

    [Header("Height from Spherical Noise")]
    public bool useHeightMap = true;
    public float heightAmp = 3.0f;
    public float heightFreq = 0.06f;
    [Range(1, 6)] public int octaves = 4;
    public float lacunarity = 2.0f;
    public float gain = 0.5f;
    public int seed = 1337;

    [Header("Look Tweaks")]
    public float surfaceJitter = 0.0f;
    public bool randomizeRotation = false;

    [Header("Instancing / Hierarchy")]
    public GameObject cubePrefab;

    [Tooltip("Parent for all chunks (created if null).")]
    public Transform container;

    [Header("Chunking")]
    [Tooltip("World size of a chunk on each axis.")]
    public float chunkSize = 16f;

    [Tooltip("If true, shifts the chunk grid so all indices are >= 0 (useful for editor sanity).")]
    public bool offsetToPositiveSpace = false;

    // Computed when generating if offsetToPositiveSpace==true
    Vector3 chunkOriginOffset = Vector3.zero;

    [Header("Safety")]
    [Min(100)] public int maxCubes = 50000;
    public bool autoRegenerateInEditor = false;

    // Internals
    private readonly List<Transform> spawned = new();
    private readonly Dictionary<Vector3Int, Transform> chunkParents = new();
    private Vector3 noiseOffset;

    [ContextMenu("Generate Hollow Height Sphere")]
    public void Generate()
    {
        ClearImmediate();

        if (!container)
        {
            var go = new GameObject("_Chunks");
            go.transform.SetParent(transform, false);
            container = go.transform;
        }

        Random.InitState(seed);
        noiseOffset = new Vector3(Random.value * 1000f, Random.value * 1000f, Random.value * 1000f);

        float step = cubeSize * Mathf.Max(0.5f, spacingMultiplier);
        float thick = shellThicknessInCubes * cubeSize;

        float bound = radius + (useHeightMap ? Mathf.Abs(heightAmp) : 0f) + thick + step;
        int n = Mathf.CeilToInt(bound / step);

        // If we want all-positive chunk indices, shift the grid so min corner maps to ~0
        if (offsetToPositiveSpace)
            chunkOriginOffset = Vector3.one * bound;
        else
            chunkOriginOffset = Vector3.zero;

        int count = 0;

        for (int ix = -n; ix <= n; ix++)
        {
            float x = ix * step;
            for (int iy = -n; iy <= n; iy++)
            {
                float y = iy * step;
                for (int iz = -n; iz <= n; iz++)
                {
                    float z = iz * step;
                    Vector3 p = new Vector3(x, y, z);
                    float r = p.magnitude;
                    if (r < 1e-4f) continue;

                    Vector3 dir = p / r;

                    float targetR = radius;
                    if (useHeightMap && heightAmp != 0f)
                    {
                        float h01 = FBM3D(dir * heightFreq + noiseOffset, octaves, lacunarity, gain);
                        float h = (h01 * 2f - 1f) * heightAmp;
                        targetR += h;
                    }

                    float halfThick = 0.5f * thick;
                    if (r < targetR - halfThick || r > targetR + halfThick)
                        continue;

                    if (count >= maxCubes)
                    {
                        Debug.LogWarning($"[PlanetSpawner] Reached maxCubes ({maxCubes}). Stopping early.");
                        return;
                    }

                    Vector3 pos = (surfaceJitter > 0f)
                        ? p + dir * (Random.value - 0.5f) * surfaceJitter
                        : p;

                    // --- Chunking: find/create the chunk container and parent the cube there ---
                    Vector3Int chunkIdx = WorldToChunk(pos);
                    Transform parentChunk = GetOrCreateChunkParent(chunkIdx);

                    Transform t = SpawnOne(pos, parentChunk);
                    spawned.Add(t);
                    count++;
                }
            }
        }

        Debug.Log($"[PlanetSpawner] Spawned {count} cubes (radius={radius}, heightAmp={heightAmp}, step={cubeSize * spacingMultiplier}).");
    }

    [ContextMenu("Clear Spawned")]
    public void ClearSpawned() => ClearImmediate();

    // ---------- Chunk helpers ----------

    Vector3Int WorldToChunk(Vector3 localPos)
    {
        // Apply optional origin shift so indices can be positive-only
        Vector3 shifted = localPos + chunkOriginOffset;

        int cx = Mathf.FloorToInt(shifted.x / chunkSize);
        int cy = Mathf.FloorToInt(shifted.y / chunkSize);
        int cz = Mathf.FloorToInt(shifted.z / chunkSize);
        return new Vector3Int(cx, cy, cz);
    }

    Transform GetOrCreateChunkParent(Vector3Int idx)
    {
        if (chunkParents.TryGetValue(idx, out var tr)) return tr;

        var go = new GameObject($"Chunk_{idx.x}_{idx.y}_{idx.z}");
        go.transform.SetParent(container, false);

        // Optional: place chunk object at its local-space origin (nice for gizmos/debug)
        Vector3 chunkLocalOrigin = new Vector3(idx.x * chunkSize, idx.y * chunkSize, idx.z * chunkSize) - chunkOriginOffset;
        go.transform.localPosition = chunkLocalOrigin;

        tr = go.transform;
        chunkParents[idx] = tr;
        return tr;
    }

    // ---------- Spawning / clearing ----------

    private Transform SpawnOne(Vector3 localPos, Transform parent)
    {
        GameObject g;
        if (cubePrefab)
        {
            g = Instantiate(cubePrefab, parent);
            g.transform.localPosition = localPos - parent.localPosition; // keep correct local offset within chunk
            g.transform.localRotation = randomizeRotation ? Random.rotationUniform : Quaternion.identity;
            g.transform.localScale = Vector3.one * cubeSize;
        }
        else
        {
            g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos - parent.localPosition;
            g.transform.localRotation = randomizeRotation ? Random.rotationUniform : Quaternion.identity;
            g.transform.localScale = Vector3.one * cubeSize;
            var col = g.GetComponent<Collider>();
            if (col) DestroyImmediate(col);
        }
        g.name = "Cube";
        return g.transform;
    }

    private void ClearImmediate()
    {
        spawned.Clear();
        chunkParents.Clear();

        if (!container)
        {
            var child = transform.Find("_Chunks");
            if (child) container = child;
        }

        if (container)
        {
            var toDelete = new List<GameObject>();
            foreach (Transform t in container) toDelete.Add(t.gameObject);
            foreach (var go in toDelete)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(go);
                else Destroy(go);
#else
                Destroy(go);
#endif
            }
        }
    }

    // -------- Noise helpers --------

    static float Noise3D(Vector3 p)
    {
        float a = Mathf.PerlinNoise(p.x, p.y);
        float b = Mathf.PerlinNoise(p.y, p.z);
        float c = Mathf.PerlinNoise(p.z, p.x);
        return (a + b + c) / 3f;
    }

    static float FBM3D(Vector3 p, int oct, float lac, float gain)
    {
        float amp = 0.5f, sum = 0f, freq = 1f;
        for (int i = 0; i < oct; i++)
        {
            sum += Noise3D(p * freq) * amp;
            freq *= lac;
            amp *= gain;
        }
        return Mathf.Clamp01(sum);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!autoRegenerateInEditor) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                Generate();
            };
        }
    }
#endif
}
