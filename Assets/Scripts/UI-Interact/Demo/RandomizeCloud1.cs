using UnityEngine;

public class RandomizeCloud1 : MonoBehaviour
{
    [Header("Reference to Cloud Layer")]
    public CubeSphereCloudLayer cloudLayer;

    /// <summary>
    /// Call from a UI Button to randomize the cloud seed
    /// and rebuild the cloud layer.
    /// </summary>
    public void Randomize()
    {
        if (cloudLayer == null)
        {
            Debug.LogWarning("RandomizeCloud1: No cloudLayer assigned!");
            return;
        }

        // Assign a new random seed
        cloudLayer.cloudSeed = Random.Range(0, 999999);

        // Immediately rebuild the mesh
        cloudLayer.BuildCloudMesh();
    }
}
