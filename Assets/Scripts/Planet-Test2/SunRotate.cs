using UnityEngine;

/// <summary>
/// Controls a simple sun system:
/// - YearlyPivot tilts up/down over a "year" to simulate seasons (sun arc angle).
/// - DailyPivot rotates around the planet once per "day" to simulate day/night.
/// 
/// Expected hierarchy (recommended):
/// YearlyPivot
///   └── DailyPivot
///       └── Sun (Directional Light)
/// 
/// Attach this script to anything (commonly the YearlyPivot).
/// Then assign both pivots in the inspector.
/// </summary>
public class SunRotate : MonoBehaviour
{
    [Header("Pivots")]
    [Tooltip("Pivot that controls the daily rotation around the planet (spins once per day).")]
    public Transform dailyPivot;

    [Tooltip("Pivot that controls the seasonal arc / declination (tilts over the year).")]
    public Transform yearlyPivot;

    [Header("Time Settings")]
    [Tooltip("Real-time seconds for one full day-night cycle (timeOfDay from 0 → 1).")]
    public float dayLengthSeconds = 120f; // e.g. 2 minutes per day

    [Tooltip("Number of in-game days that make up one full year (season cycle).")]
    public float daysPerYear = 60f; // e.g. 60 in-game days for a year

    [Tooltip("Global speed multiplier for time (1 = normal, 2 = 2x faster, etc.).")]
    public float timeScale = 1f;

    [Header("Sun Geometry")]
    [Tooltip("Maximum axial tilt in degrees (Earth ≈ 23.5). This controls the seasonal arc height.")]
    public float axialTiltDegrees = 23.5f;

    [Tooltip("Offset for where 'day 0' is in the year (0–1). 0.25 = start at summer solstice.")]
    [Range(0f, 1f)]
    public float yearPhaseOffset = 0f;

    [Tooltip("Start of the day as a fraction (0–1). 0 = midnight, 0.25 = 6am, 0.5 = noon, etc.")]
    [Range(0f, 1f)]
    public float dayPhaseOffset = 0.25f; // default: start at sunrise-ish

    [Header("Debug (read-only)")]
    [SerializeField, Range(0f, 1f)]
    private float timeOfDay = 0f;     // 0–1 across a day

    [SerializeField, Range(0f, 1f)]
    private float timeOfYear = 0f;    // 0–1 across a year

    private const float TwoPi = Mathf.PI * 2f;

    private void Reset()
    {
        // Try to auto-guess pivots if not assigned (optional convenience).
        if (dailyPivot == null)
            dailyPivot = transform;

        if (yearlyPivot == null)
            yearlyPivot = transform.parent != null ? transform.parent : transform;
    }

    private void Update()
    {
        if (dayLengthSeconds <= 0f || daysPerYear <= 0f)
            return;

        float dt = Time.deltaTime * Mathf.Max(timeScale, 0f);

        // --- 1. Advance time of day (0–1) ---
        float dayDelta = dt / dayLengthSeconds;
        timeOfDay += dayDelta;
        if (timeOfDay > 1f)
            timeOfDay -= Mathf.Floor(timeOfDay); // wrap around

        // --- 2. Advance time of year (0–1) ---
        // 1 year = daysPerYear * dayLengthSeconds in real seconds.
        float yearLengthSeconds = dayLengthSeconds * daysPerYear;
        float yearDelta = dt / yearLengthSeconds;
        timeOfYear += yearDelta;
        if (timeOfYear > 1f)
            timeOfYear -= Mathf.Floor(timeOfYear); // wrap around

        // --- 3. Compute angles ---

        // Daily rotation: one full spin per day.
        // timeOfDay (0 -> 1) maps to 0° -> 360°.
        float dayT = Mathf.Repeat(timeOfDay + dayPhaseOffset, 1f);
        float dailyAngle = dayT * 360f;

        // Yearly "declination": how high the sun arc is.
        // timeOfYear (0 -> 1) maps to a sine wave between -axialTilt and +axialTilt.
        float yearT = Mathf.Repeat(timeOfYear + yearPhaseOffset, 1f);
        float declination = axialTiltDegrees * Mathf.Sin(yearT * TwoPi);

        // --- 4. Apply rotations ---

        // Daily: rotate around local Y by daily angle.
        // You can change axis if your planet is oriented differently.
        if (dailyPivot != null)
        {
            // We only affect rotation around one axis, so use Euler.
            dailyPivot.localRotation = Quaternion.Euler(0f, dailyAngle, 0f);
        }

        // Yearly: tilt the sun's orbital plane by declination around X.
        // Again, change axis to taste depending on how your world is oriented.
        if (yearlyPivot != null)
        {
            yearlyPivot.localRotation = Quaternion.Euler(declination, 0f, 0f);
        }
    }

    /// <summary>
    /// Manually set the normalized time of day (0–1).
    /// 0 = midnight, 0.25 = 6am, 0.5 = noon, 0.75 = 6pm, 1 = midnight.
    /// </summary>
    public void SetTimeOfDay(float t)
    {
        timeOfDay = Mathf.Repeat(t, 1f);
    }

    /// <summary>
    /// Manually set the normalized time of year (0–1).
    /// 0 and 1 = same point in season cycle (e.g. spring equinox).
    /// </summary>
    public void SetTimeOfYear(float t)
    {
        timeOfYear = Mathf.Repeat(t, 1f);
    }

    /// <summary>
    /// Get current normalized time of day (0–1).
    /// </summary>
    public float GetTimeOfDay()
    {
        return timeOfDay;
    }

    /// <summary>
    /// Get current normalized time of year (0–1).
    /// </summary>
    public float GetTimeOfYear()
    {
        return timeOfYear;
    }
}
