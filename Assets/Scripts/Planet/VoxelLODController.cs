//using System.Collections.Generic;
//using UnityEngine;

//public class VoxelLODController : MonoBehaviour
//{
//    public PlanetVoxelWorld world;
//    public Camera cam;

//    [Header("Distance thresholds (world units)")]
//    public float lod0Near = 20f; // use full 16³
//    public float lod1Near = 60f; // use 8³
//    // beyond lod1Near -> use 4³

//    [Header("Hysteresis")]
//    public float hysteresis = 3f; // stickiness to avoid rapid toggles

//    // Cache the active LOD we last chose per chunk
//    readonly Dictionary<VoxelChunk, int> last = new();

//    void LateUpdate()
//    {
//        if (!world || !cam || world.chunksRoot == null) return;

//        foreach (Transform child in world.chunksRoot)
//        {
//            var chunk = child.GetComponent<VoxelChunk>();
//            if (!chunk) continue;

//            float d = Vector3.Distance(cam.transform.position, chunk.WorldCenter());
//            int want =
//                (d <= lod0Near - H(chunk, 0)) ? 0 :
//                (d <= lod1Near - H(chunk, 1)) ? 1 : 2;

//            if (!chunk.HasLOD(want))
//            {
//                if (want == 2 && chunk.HasLOD(1)) want = 1;
//                else if (chunk.HasLOD(0)) want = 0;
//            }

//            if (!last.TryGetValue(chunk, out int prev))
//                prev = -1;

//            // Apply hysteresis: if we’re trying to move to a *farther* LOD, require distance to exceed threshold + hysteresis
//            if (prev == 0 && want > 0 && d < lod0Near + hysteresis) want = 0;
//            if (prev <= 1 && want > 1 && d < lod1Near + hysteresis) want = Mathf.Max(1, prev);

//            if (want != prev)
//            {
//                chunk.SetActiveLOD(want);
//                last[chunk] = want;
//            }
//        }
//    }

//    float H(VoxelChunk ch, int level)
//    {
//        // small bias so we don't instantly jump back when hovering near threshold
//        if (!last.TryGetValue(ch, out int prev)) return 0f;
//        return prev == level ? -hysteresis : 0f;
//    }
//}
