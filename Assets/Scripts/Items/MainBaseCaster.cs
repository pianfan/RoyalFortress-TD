using UnityEngine;

public sealed class MainBaseCaster : MonoBehaviour
{
    [Tooltip("Explicitly placed at this map's base; independent of camera position and inactive old markers.")]
    public Transform castOrigin;
    public Vector3 Position => castOrigin ? castOrigin.position : transform.position;
}
