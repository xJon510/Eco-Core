using UnityEngine;
using TMPro;

public class PlanetTimeDisplay : MonoBehaviour
{
    [Header("References")]
    public SunRotate sun;               // drag your SunRotate here

    [Header("UI Outputs")]
    public TMP_Text dayText;
    public TMP_Text yearText;
    public TMP_Text timeScaleText;

    // internal counters (sync with SunRotate's looping behavior)
    private int currentDay = 0;
    private int currentYear = 0;

    private float dayAccumulator = 0f;

    private void Update()
    {
        if (sun == null) return;

        // --- 1. Compute REAL day progress based on timeOfDay ---
        // SunRotate wraps timeOfDay when a day completes.
        float dayLength = sun.dayLengthSeconds;        // seconds per day
        float dt = Time.deltaTime * sun.timeScale;

        dayAccumulator += dt;

        if (dayAccumulator >= dayLength)
        {
            dayAccumulator -= dayLength;
            currentDay++;

            // wrap day -> increase year
            if (currentDay >= sun.daysPerYear)
            {
                currentDay = 0;
                currentYear++;
            }
        }

        // --- 2. Update UI text fields ---
        if (dayText != null)
            dayText.text = $"Day: {currentDay}";

        if (yearText != null)
            yearText.text = $"Year: {currentYear}";

        if (timeScaleText != null)
            timeScaleText.text = $"Time Scale: {sun.timeScale}x";
    }
}
