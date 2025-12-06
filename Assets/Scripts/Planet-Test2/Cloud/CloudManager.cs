using UnityEngine;

/// <summary>
/// Drives the EcoCore/CloudShell shader: sets planet center, camera position,
/// noise scroll, band speeds/limits, etc. No mesh rebuilding involved.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class CloudManager : MonoBehaviour
{
    [Header("Links")]
    public Transform planetCenter;
    public Transform cameraTransform;

    public SunRotate sunRotate;

    [Header("Noise / Coverage")]
    [Tooltip("Lower = larger cloud blobs.")]
    public float cloudFrequency = 0.25f;

    [Range(0f, 1f)]
    [Tooltip("Bias for coverage. Higher = fewer clouds.")]
    public float coverageBias = 0.4f;

    public int cloudSeed = 42;

    [Header("Animation / Scrolling")]
    public bool animate = true;

    [Tooltip("Direction in which the base noise field scrolls.")]
    public Vector3 noiseScrollDirection = new Vector3(1f, 0f, 0.3f);

    [Tooltip("Speed multiplier for noise scrolling.")]
    public float noiseScrollSpeed = 0.1f;

    [Header("Latitudinal Bands (|sin(lat)|)")]
    [Range(0f, 1f)]
    [Tooltip("Equator band extent in |sin(lat)| (e.g. 0.25 ≈ ±14°).")]
    public float equatorBandLimit = 0.25f;

    [Range(0f, 1f)]
    [Tooltip("Mid-latitude band extent in |sin(lat)| (above equator band).")]
    public float midBandLimit = 0.6f;

    [Tooltip("Relative scroll speed in the equatorial band.")]
    public float equatorBandSpeed = 1.0f;

    [Tooltip("Relative scroll speed in the mid-latitude band.")]
    public float midBandSpeed = 0.7f;

    [Tooltip("Relative scroll speed in the polar band.")]
    public float polarBandSpeed = 0.4f;

    [Header("Camera Clearance")]
    public float clearanceRadius = 15f;  // fully clear
    public float clearanceFade = 5f;   // fade band

    [Header("Visuals")]
    public Color cloudColor = Color.white;
    [Range(0f, 1f)] public float cloudOpacity = 0.9f;

    private Material _mat;
    private Vector3 _noiseScrollOffset;

    [SerializeField, Range(0f, 1f)]
    private float lastTimeOfDay = -1f;

    private void Awake()
    {
        var mr = GetComponent<MeshRenderer>();
        // Use .material so each planet has its own material instance
        _mat = mr.material;
    }

    private void Update()
    {
        if (_mat == null || planetCenter == null || cameraTransform == null)
            return;

        // ----------------------------------------------------------------
        // 1. Update scroll offset (using simulated time if SunRotate exists)
        // ----------------------------------------------------------------
        if (animate)
        {
            Vector3 dir = noiseScrollDirection.sqrMagnitude > 0.0001f
                ? noiseScrollDirection.normalized
                : Vector3.zero;

            if (sunRotate != null)
            {
                // Use SunRotate's time-of-day as the single source of truth,
                // same idea as TemperatureManager.
                float currentDayT = sunRotate.GetTimeOfDay(); // 0..1 over a full day

                // First frame: just initialize, don't scroll yet.
                if (lastTimeOfDay < 0f)
                {
                    lastTimeOfDay = currentDayT;
                }
                else
                {
                    // Fraction of a day that has passed since last frame (handles wrap).
                    float deltaDay = currentDayT - lastTimeOfDay;
                    if (deltaDay < 0f)
                        deltaDay += 1f;

                    // Scroll proportional to in-game day fraction.
                    // noiseScrollSpeed is "offset units per in-game day".
                    _noiseScrollOffset += dir * (noiseScrollSpeed * deltaDay);

                    lastTimeOfDay = currentDayT;
                }
            }
            else
            {
                // Fallback: use real-time seconds if no SunRotate is wired up.
                // Here noiseScrollSpeed is "offset units per real-time second".
                _noiseScrollOffset += dir * (noiseScrollSpeed * Time.deltaTime);
            }
        }

        // ----------------------------------------------------------------
        // 2. Push parameters to the material
        // ----------------------------------------------------------------

        // --- Core noise/coverage ---
        _mat.SetFloat("_CloudFrequency", cloudFrequency);
        _mat.SetFloat("_CoverageBias", coverageBias);
        _mat.SetFloat("_CloudSeed", cloudSeed);

        _mat.SetVector("_NoiseOffset", _noiseScrollOffset);
        _mat.SetVector("_PlanetCenter", planetCenter.position);

        // --- Band parameters ---
        _mat.SetFloat("_EqBandLimit", equatorBandLimit);
        _mat.SetFloat("_MidBandLimit", midBandLimit);

        _mat.SetFloat("_EqBandSpeed", equatorBandSpeed);
        _mat.SetFloat("_MidBandSpeed", midBandSpeed);
        _mat.SetFloat("_PolarBandSpeed", polarBandSpeed);

        // --- Camera clearance + visuals ---
        _mat.SetFloat("_ClearanceRadius", clearanceRadius);
        _mat.SetFloat("_ClearanceFade", clearanceFade);

        _mat.SetColor("_CloudColor", cloudColor);
        _mat.SetFloat("_CloudOpacity", cloudOpacity);
    }
}
