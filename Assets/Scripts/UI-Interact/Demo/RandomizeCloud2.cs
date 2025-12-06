using UnityEngine;

public class RandomizeCloud2 : MonoBehaviour
{
    [Header("Reference to CloudManager")]
    public CloudManager cloudManager;

    /// <summary>
    /// Call from a UI Button to randomize the shader cloud seed.
    /// </summary>
    public void Randomize()
    {
        if (cloudManager == null)
        {
            Debug.LogWarning("RandomizeCloud2: No CloudManager assigned!");
            return;
        }

        // Assign a random seed for the shader
        cloudManager.cloudSeed = Random.Range(0, 999999);
    }
}
