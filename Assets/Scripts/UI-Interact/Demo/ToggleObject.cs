using UnityEngine;

public class ToggleObject : MonoBehaviour
{
    [Header("Primary Object To Toggle")]
    public GameObject target;

    [Header("Force-Off Object (Optional)")]
    public GameObject forceOffObject;

    /// <summary>
    /// Toggles the target object. If a forceOffObject is assigned,
    /// it will always be disabled regardless of toggle state.
    /// </summary>
    public void Toggle()
    {
        if (target == null)
        {
            Debug.LogWarning("ToggleObject: No target assigned!");
            return;
        }

        // Toggle the main target
        target.SetActive(!target.activeSelf);

        // Force disable the other object
        if (forceOffObject != null)
            forceOffObject.SetActive(false);
    }
}
