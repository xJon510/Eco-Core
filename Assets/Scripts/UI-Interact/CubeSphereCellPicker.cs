using UnityEngine;
using TMPro;

[RequireComponent(typeof(Camera))]
public class CubeSphereCellPicker : MonoBehaviour
{
    [Header("Planet")]
    [SerializeField] private CubeSphereBlockMesh planet;
    [SerializeField] private MeshCollider planetCollider;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI cellLocationText;
    [SerializeField] private TextMeshProUGUI cellFaceText;

    [Header("Raycast")]
    [SerializeField] private float maxDistance = 10000f;
    [SerializeField] private LayerMask raycastMask = ~0;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        // auto-find collider if not wired
        if (!planetCollider && planet)
            planetCollider = planet.GetComponent<MeshCollider>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandleClick(Input.mousePosition);
    }

    private void HandleClick(Vector3 screenPos)
    {
        if (!planet || !planetCollider) return;

        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, raycastMask))
        {
            if (hit.collider != planetCollider)
                return;

            Mesh mesh = planetCollider.sharedMesh;
            if (!mesh) return;

            // --- FIX: Use main triangles only (submesh = 0 always) ---
            int[] tris = mesh.triangles;

            int triStart = hit.triangleIndex * 3;
            int v0 = tris[triStart + 0];
            int v1 = tris[triStart + 1];
            int v2 = tris[triStart + 2];

            // Each cell uses 8 vertices in order -> we can decode cell directly
            int cellVertexIndex = Mathf.Min(v0, Mathf.Min(v1, v2));
            int cellIndex = cellVertexIndex / 8;

            // Decode back into face + grid coords
            int steps = Mathf.Max(1, planet.cellsPerFace);
            int cellsPerFace = steps * steps;

            int faceIndex = cellIndex / cellsPerFace;
            int indexInFace = cellIndex % cellsPerFace;

            int y = indexInFace / steps;
            int x = indexInFace % steps;

            if (cellLocationText)
                cellLocationText.text = $"Cell: ({x}, {y})";

            if (cellFaceText)
                cellFaceText.text = $"Face: {faceIndex + 1}";
        }
    }
}
