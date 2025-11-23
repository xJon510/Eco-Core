using UnityEngine;

public class SimpleOrbitCamera : MonoBehaviour
{
    public Transform target;

    [Header("Distance Settings")]
    public float FarMax = 500f;
    public float CloseMin = 10f;
    public float Distance = 200f;

    [Header("Sensitivity")]
    public float xSpeed = 90f;
    public float ySpeed = 60f;
    public float zoomSpeed = 400f;

    private float yaw, pitch = 20f;
    void LateUpdate()
    {
        if (Input.GetMouseButton(0))
        {
            yaw += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
            pitch -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -85f, 85f);
        }

        Distance = Mathf.Clamp(
            Distance - Input.GetAxis("Mouse ScrollWheel") * zoomSpeed * Time.deltaTime,
            CloseMin, FarMax
        );

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 fwd = rot * Vector3.forward;

        Vector3 pos = target
            ? target.position - fwd * Distance
            : -(fwd * Distance);

        transform.SetPositionAndRotation(pos, rot);
    }
}
