using UnityEngine;

public class SimpleRotate2 : MonoBehaviour
{
    [SerializeField] private float strength = 15f;   // How far to rotate (degrees)
    [SerializeField] private float speed = 2f;       // How fast to oscillate

    private float baseZ;

    void Start()
    {
        // Store the starting Z rotation
        baseZ = transform.localEulerAngles.z;
    }

    void Update()
    {
        float zOffset = Mathf.Sin(Time.time * speed) * strength;
        Vector3 currentRotation = transform.localEulerAngles;
        transform.localEulerAngles = new Vector3(currentRotation.x, currentRotation.y, baseZ + zOffset);
    }
}
