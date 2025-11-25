using UnityEngine;
using TMPro;

[RequireComponent(typeof(Camera))]
public class CubeSphereCellPickerMath : MonoBehaviour
{
    [Header("Planet")]
    [SerializeField] private CubeSphereBlockMesh planet;

    [Tooltip("Extra radius offset used for ray-sphere picking. 0 is usually fine.")]
    [SerializeField] private float pickRadiusOffset = 0f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI cellLocationText; // "(X, Y)"
    [SerializeField] private TextMeshProUGUI cellFaceText;     // "Face"

    [Header("Raycast")]
    [SerializeField] private float maxRayDistance = 100000f;

    private Camera cam;

    // Last hit info (so managers can read from this if they want)
    public int LastFaceIndex { get; private set; }    // 0..5
    public int LastCellX { get; private set; }    // 0..steps-1
    public int LastCellY { get; private set; }    // 0..steps-1
    public int LastCellIndex { get; private set; }    // flattened index

    // Must match CubeSphereBlockMesh.faceDirections order:
    // 0: +Y, 1: -Y, 2: -X, 3: +X, 4: +Z, 5: -Z
    private static readonly Vector3[] FaceDirections =
    {
        Vector3.up,       // 0
        Vector3.down,     // 1
        Vector3.left,     // 2
        Vector3.right,    // 3
        Vector3.forward,  // 4
        Vector3.back      // 5
    };

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        if (planet == null)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            HandleClick(Input.mousePosition);
        }
    }

    private void HandleClick(Vector3 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (!RaySphereHit(ray, out Vector3 hitWorld))
            return;

        // Direction from planet center to the hit point
        Vector3 centerPos = planet.planetCenter != null
            ? planet.planetCenter.position
            : planet.transform.position;

        Vector3 dir = (hitWorld - centerPos).normalized;

        // Convert that direction to face + (x,y)
        if (DirectionToCell(dir, planet.cellsPerFace,
                            out int faceIndex, out int x, out int y, out int cellIndex))
        {
            LastFaceIndex = faceIndex;
            LastCellX = x;
            LastCellY = y;
            LastCellIndex = cellIndex;

            // Simple debug UI
            if (cellLocationText)
                cellLocationText.text = $"Cell: ({x}, {y})";

            if (cellFaceText)
                cellFaceText.text = $"Face: {faceIndex + 1}";
        }
    }

    /// <summary>
    /// Ray-sphere intersection against the planet sphere (math only, no collider).
    /// </summary>
    private bool RaySphereHit(Ray ray, out Vector3 hitPoint)
    {
        Vector3 centerPos = planet.planetCenter != null
            ? planet.planetCenter.position
            : planet.transform.position;

        // Radius we use for picking. You can tweak pickRadiusOffset in the inspector.
        float pickRadius = Mathf.Max(0.01f, planet.radius + pickRadiusOffset);

        Vector3 o = ray.origin;
        Vector3 d = ray.direction.normalized;
        Vector3 m = o - centerPos;

        float b = Vector3.Dot(m, d);
        float c = Vector3.Dot(m, m) - pickRadius * pickRadius;

        // If ray origin is outside sphere (c > 0) and ray is pointing away (b > 0) -> no hit
        if (c > 0f && b > 0f)
        {
            hitPoint = Vector3.zero;
            return false;
        }

        float discriminant = b * b - c;
        if (discriminant < 0f)
        {
            hitPoint = Vector3.zero;
            return false;
        }

        // Closest intersection along the ray
        float t = -b - Mathf.Sqrt(discriminant);
        if (t < 0f)
            t = -b + Mathf.Sqrt(discriminant);

        if (t < 0f || t > maxRayDistance)
        {
            hitPoint = Vector3.zero;
            return false;
        }

        hitPoint = o + t * d;
        return true;
    }

    /// <summary>
    /// Convert a direction on the spherified cube to (face, x, y, cellIndex).
    /// </summary>
    private static bool DirectionToCell(
        Vector3 dir,
        int cellsPerFace,
        out int faceIndex,
        out int x,
        out int y,
        out int cellIndex)
    {
        dir.Normalize();

        float ax = Mathf.Abs(dir.x);
        float ay = Mathf.Abs(dir.y);
        float az = Mathf.Abs(dir.z);

        // Decide which cube face (major axis)
        if (ay >= ax && ay >= az)
        {
            // ±Y
            if (dir.y > 0f) faceIndex = 0; // +Y
            else faceIndex = 1; // -Y
        }
        else if (ax >= ay && ax >= az)
        {
            // ±X
            if (dir.x > 0f) faceIndex = 3; // +X (right)
            else faceIndex = 2; // -X (left)
        }
        else
        {
            // ±Z
            if (dir.z > 0f) faceIndex = 4; // +Z (forward)
            else faceIndex = 5; // -Z (back)
        }

        Vector3 localUp = FaceDirections[faceIndex];
        Vector3 axisA = new Vector3(localUp.y, localUp.z, localUp.x);
        Vector3 axisB = Vector3.Cross(localUp, axisA);

        // Project direction into this face's local basis
        float du = Vector3.Dot(dir, axisA);
        float dv = Vector3.Dot(dir, axisB);
        float dw = Vector3.Dot(dir, localUp);
        float absW = Mathf.Abs(dw);

        // Shouldn't really happen unless dir is exactly tangent
        if (absW < 1e-6f)
        {
            x = y = cellIndex = 0;
            return false;
        }

        // Project onto the face plane in [-1,1]x[-1,1] space
        float uPlane = du / absW; // [-1,1]
        float vPlane = dv / absW; // [-1,1]

        // Map plane coords -> [0,1]
        float u = 0.5f * (uPlane + 1f);
        float v = 0.5f * (vPlane + 1f);

        int steps = Mathf.Max(1, cellsPerFace);

        // Convert to cell indices; clamp to safety
        x = Mathf.Clamp(Mathf.FloorToInt(u * steps), 0, steps - 1);
        y = Mathf.Clamp(Mathf.FloorToInt(v * steps), 0, steps - 1);

        int cellsPerFaceTotal = steps * steps;
        cellIndex = faceIndex * cellsPerFaceTotal + y * steps + x;

        return true;
    }
}
