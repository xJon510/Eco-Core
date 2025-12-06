using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    public TMP_Text fpsText;   // for TextMeshPro

    public float updateInterval = 0.5f;

    private float accumulated = 0f;
    private int frames = 0;
    private float timer = 0f;

    private void Update()
    {
        accumulated += Time.unscaledDeltaTime;
        frames++;
        timer += Time.unscaledDeltaTime;

        if (timer >= updateInterval)
        {
            float fps = frames / accumulated;

            if (fpsText != null)
                fpsText.text = Mathf.RoundToInt(fps) + " FPS";

            // reset
            frames = 0;
            accumulated = 0f;
            timer = 0f;
        }
    }
}
