using UnityEngine;

public class RandomizePlanetSeed : MonoBehaviour
{
    public enum SeedTarget
    {
        TerrainNoise,   // affects noiseSeed
        Continents      // affects continentSeed
    }

    [Header("Planet Mesh Reference")]
    public CubeSphereBlockMesh cubeSphere;

    [Header("Which Seed Should This Button Change?")]
    public SeedTarget target = SeedTarget.TerrainNoise;

    [Header("Random Seed Range (inclusive)")]
    public int minSeed = 0;
    public int maxSeed = 999999;

    /// <summary>
    /// Call this from a UI Button's OnClick().
    /// Randomizes the chosen seed and regenerates the planet mesh.
    /// </summary>
    public void Randomize()
    {
        if (cubeSphere == null)
        {
            Debug.LogWarning("RandomizePlanetSeed: No CubeSphereBlockMesh assigned!");
            return;
        }

        if (minSeed > maxSeed)
        {
            int tmp = minSeed;
            minSeed = maxSeed;
            maxSeed = tmp;
        }

        int newSeed = Random.Range(minSeed, maxSeed + 1);

        switch (target)
        {
            case SeedTarget.TerrainNoise:
                cubeSphere.noiseSeed = newSeed;
                break;

            case SeedTarget.Continents:
                cubeSphere.continentSeed = newSeed;
                break;
        }

        // Rebuild the planet mesh with the new seed
        cubeSphere.Generate();
    }
}
