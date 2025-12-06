using UnityEngine;

public class TimeScaleButton : MonoBehaviour
{
    public enum TimeSpeed
    {
        x1 = 1,
        x5 = 5,
        x10 = 10,
        x50 = 50
    }

    [Header("Time Speed For This Button")]
    public TimeSpeed speed = TimeSpeed.x1;

    [Header("Reference to SunRotate")]
    public SunRotate sunRotate;

    // Call this from a UI Button's OnClick()
    public void ApplySpeed()
    {
        if (sunRotate == null)
        {
            Debug.LogWarning("TimeScaleButton: SunRotate reference not assigned!");
            return;
        }

        sunRotate.timeScale = (float)speed;
    }
}
