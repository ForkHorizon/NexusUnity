using UnityEngine;

/// <summary>
/// Lab fixture component. The benchmark reads and writes its serialized fields.
/// </summary>
public class LabMarker : MonoBehaviour
{
    public string markerName = "readable";
    public float intensity = 1.25f;
    public int revision = 7;
}
