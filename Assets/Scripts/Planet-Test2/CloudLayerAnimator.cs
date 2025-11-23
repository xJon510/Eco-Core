using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class CloudLayerAnimator : MonoBehaviour
{
    [Header("Rotation")]
    public float rotationSpeedDegPerSec = 3f;
    public Vector3 rotationAxis = Vector3.up;

    [Header("UV Scrolling")]
    public Vector2 uvScrollSpeed = new Vector2(0.02f, 0.0f); // in UV units / second

    private MeshRenderer _renderer;
    private Material _matInstance;
    private Vector2 _uvOffset;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();

        // Make sure we don't edit the shared material asset
        _matInstance = _renderer.material;
    }

    private void Update()
    {
        // Rotate the entire cloud shell
        if (rotationSpeedDegPerSec != 0f)
        {
            transform.Rotate(rotationAxis * rotationSpeedDegPerSec * Time.deltaTime, Space.World);
        }

        // Scroll UVs
        _uvOffset += uvScrollSpeed * Time.deltaTime;
        _uvOffset.x = Mathf.Repeat(_uvOffset.x, 1f);
        _uvOffset.y = Mathf.Repeat(_uvOffset.y, 1f);

        _matInstance.SetVector("_UVOffset", _uvOffset);
    }
}
