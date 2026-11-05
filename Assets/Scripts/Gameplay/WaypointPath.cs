using UnityEngine;

public sealed class WaypointPath : MonoBehaviour
{
    [Tooltip("In travel order: spawn, road center points, shared road, Home.")]
    public Transform[] points;
    public Color routeColor = Color.cyan;
    [Tooltip("Optional parent route for tracking spawns on a temporary section.")]
    public WaypointPath counterRoute;

    public Vector3[] GetPositions()
    {
        if (points == null || points.Length < 2) return System.Array.Empty<Vector3>();
        var result = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) return System.Array.Empty<Vector3>();
            result[i] = points[i].position;
        }
        return result;
    }

    private void OnDrawGizmos()
    {
        if (points == null) return;
        Gizmos.color = routeColor;
        Transform previous = null;
        foreach (Transform point in points)
        {
            if (point == null) continue;
            Gizmos.DrawWireSphere(point.position, 0.075f);
            if (previous != null) Gizmos.DrawLine(previous.position, point.position);
            previous = point;
        }
    }
}
