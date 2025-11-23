using UnityEngine;
using System.Collections.Generic;

public class PlanetController : MonoBehaviour
{
    public Camera targetCamera;
    public float radius = 200f;
    [Range(2, 128)] public int patchResolution = 16;  // grid per tile side
    [Range(0, 8)] public int maxLOD = 6;

    [Header("LOD thresholds (pixels)")]
    public float splitSSE = 3.0f;
    public float mergeSSE = 1.8f;
    public int perFrameBudget = 4;

    [Header("References")]
    public Material patchMaterial;

    FaceRoot[] faces;
    readonly List<Tile> visibleTiles = new();

    void Awake()
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!patchMaterial) patchMaterial = Resources.Load<Material>("Mat_PlanetPatch");

        // Build 6 faces
        faces = new FaceRoot[6];
        for (int i = 0; i < 6; i++)
        {
            var go = new GameObject("Face_" + ((CubeFace)i).ToString());
            go.transform.SetParent(transform, false);
            faces[i] = new FaceRoot(go.transform, (CubeFace)i, this);
        }
    }

    void Update()
    {
        if (!targetCamera) return;

        visibleTiles.Clear();
        foreach (var f in faces) f.CollectVisible(visibleTiles, targetCamera);

        int budget = perFrameBudget;

        // Split / Merge pass
        foreach (var t in visibleTiles)
        {
            float sse = t.ComputeScreenSpaceError(targetCamera);
            if (t.CanSplit && sse > splitSSE && budget > 0)
            {
                t.Split();
                budget--;
            }
        }
        foreach (var t in visibleTiles)
        {
            float sse = t.ComputeScreenSpaceError(targetCamera);
            if (t.CanMerge && sse < mergeSSE && budget > 0)
            {
                t.Merge();
                budget--;
            }
        }

        // Optional: neighbor-LOD clamp & seam fix would go here (v2).
    }

    // ----- Inner types -----
    public enum CubeFace { PosX, NegX, PosY, NegY, PosZ, NegZ }

    public class FaceRoot
    {
        public readonly Transform root;
        public readonly CubeFace face;
        public readonly PlanetController planet;
        public Tile rootTile;

        public FaceRoot(Transform root, CubeFace face, PlanetController planet)
        {
            this.root = root; this.face = face; this.planet = planet;
            // Full face covers uv in [-1,1]x[-1,1]
            rootTile = new Tile(this, null, new Rect(-1f, -1f, 2f, 2f), 0);
            rootTile.EnsureMesh();
        }

        // Backface + frustum cull, collect visible tiles
        public void CollectVisible(List<Tile> list, Camera cam)
        {
            rootTile.CollectVisible(list, cam);
        }
    }

    public class Tile
    {
        public readonly FaceRoot faceRoot;
        public readonly Tile parent;
        public readonly PlanetController planet;
        public Rect uvRect;        // on face, in [-1,1] range
        public int lod;            // depth
        public Tile[] children;    // 4 children, null if leaf

        GameObject go; MeshRenderer mr; MeshFilter mf;
        Bounds worldBounds;
        Vector3 centerWS, normalWS;
        float geomError; // world-space width of tile (for SSE)

        public Tile(FaceRoot faceRoot, Tile parent, Rect uvRect, int lod)
        {
            this.faceRoot = faceRoot;
            this.parent = parent;
            this.planet = faceRoot.planet;
            this.uvRect = uvRect;
            this.lod = lod;
        }

        public bool IsLeaf => children == null;
        public bool CanSplit => IsLeaf && lod < planet.maxLOD;
        public bool CanMerge => !IsLeaf && AllChildrenAreLeaves();

        bool AllChildrenAreLeaves()
        {
            if (children == null) return false;
            for (int i = 0; i < 4; i++) if (children[i].children != null) return false;
            return true;
        }

        public void EnsureMesh()
        {
            if (go) return;
            go = new GameObject($"Tile_L{lod}");
            go.transform.SetParent(faceRoot.root, false);
            mf = go.AddComponent<MeshFilter>();
            mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = planet.patchMaterial;

            var mesh = PatchMesher.BuildPatchMesh(faceRoot.face, uvRect, planet.radius, planet.patchResolution);
            mf.sharedMesh = mesh;

            // Bounds/center/normal approx:
            centerWS = PatchMesher.Evaluate(faceRoot.face, uvRect.center, planet.radius);
            normalWS = (centerWS).normalized; // planet at origin
            worldBounds = mesh.bounds;
            worldBounds.center = centerWS; // decent approx
            geomError = PatchMesher.EdgeLength(faceRoot.face, uvRect, planet.radius);
        }

        public void DestroyMesh()
        {
            if (!go) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
            go = null; mf = null; mr = null;
        }

        public void Split()
        {
            if (!CanSplit) return;
            children = new Tile[4];
            float hw = uvRect.width * 0.5f;
            float hh = uvRect.height * 0.5f;
            var a = new Rect(uvRect.x, uvRect.y + hh, hw, hh); // TL
            var b = new Rect(uvRect.x + hw, uvRect.y + hh, hw, hh); // TR
            var c = new Rect(uvRect.x, uvRect.y, hw, hh); // BL
            var d = new Rect(uvRect.x + hw, uvRect.y, hw, hh); // BR
            children[0] = new Tile(faceRoot, this, a, lod + 1);
            children[1] = new Tile(faceRoot, this, b, lod + 1);
            children[2] = new Tile(faceRoot, this, c, lod + 1);
            children[3] = new Tile(faceRoot, this, d, lod + 1);
            foreach (var ch in children) ch.EnsureMesh();
            DestroyMesh();
        }

        public void Merge()
        {
            if (IsLeaf || !CanMerge) return;
            foreach (var ch in children) ch.DestroyMesh();
            children = null;
            EnsureMesh();
        }

        public void CollectVisible(List<Tile> list, Camera cam)
        {
            // Backface cull at tile level
            Vector3 toCam = (cam.transform.position - centerWS).normalized;
            if (Vector3.Dot(normalWS, toCam) <= 0f) return;

            // Frustum: simple distance + facing; (proper: GeometryUtility.TestPlanesAABB)
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            if (!GeometryUtility.TestPlanesAABB(planes, new Bounds(centerWS, Vector3.one * geomError * 2f)))
                return;

            if (IsLeaf) list.Add(this);
            else
                for (int i = 0; i < 4; i++)
                    children[i].CollectVisible(list, cam);
        }

        public float ComputeScreenSpaceError(Camera cam)
        {
            float dist = Vector3.Distance(cam.transform.position, centerWS);
            if (dist < 1e-3f) dist = 1e-3f;
            float projScale = cam.pixelHeight / (2f * Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad));
            // geometric error ~ projected size of tile edge
            float sse = (geomError * projScale) / dist;
            return sse;
        }
    }
}
