using UnityEngine;

public sealed class BuildSite : MonoBehaviour
{
    public string siteLabel;
    public Transform buildAnchor;
    public Collider2D clickArea;
    public GameObject emptyIndicators;
    public LineRenderer selectionRing;
    private GameObject builtTower;
    private bool building;
    private bool selected;
    private bool hovered;
    private bool placementHint;
    public bool IsOccupied => builtTower != null || building;
    public GameObject BuiltTower => builtTower;
    public int PaidCost { get; private set; }

    public void Select(bool value) { selected = value; RefreshIndicator(); }
    public void Hover(bool value) { hovered = value; RefreshIndicator(); }
    public void ShowPlacementHint(bool value) { placementHint = value; RefreshIndicator(); }

    private void RefreshIndicator()
    {
        if (emptyIndicators != null) emptyIndicators.SetActive(!IsOccupied);
        if (selectionRing == null) return;
        selectionRing.enabled = selected || (!IsOccupied && (hovered || placementHint));
        Color color = placementHint ? (hovered ? new Color32(216, 255, 213, 255) : new Color32(111, 224, 157, 210))
            : selected ? new Color32(190, 153, 255, 255) : new Color32(245, 205, 116, 220);
        selectionRing.startColor = selectionRing.endColor = color;
    }

    public bool TryBuild(GameObject prefab, Transform towerRoot, KingdomBorder.TwinTowers.TowerDefinition settings = null, int paidCost = 0)
    {
        if (IsOccupied || prefab == null) return false;
        building = true;
        try
        {
            Vector3 position = buildAnchor != null ? buildAnchor.position : transform.position;
            builtTower = Instantiate(prefab, position, Quaternion.identity, towerRoot);
            if (settings && builtTower.TryGetComponent<KingdomBorder.TwinTowers.TowerBehaviour>(out var tower)) tower.definition = settings;
            builtTower.name = prefab.name + " - " + siteLabel;
            PaidCost = Mathf.Max(0, paidCost);
            if (clickArea != null) clickArea.enabled = false;
            selected = hovered = false;
            RefreshIndicator();
            return true;
        }
        finally { building = false; }
    }

    public bool TryRemoveTower()
    {
        if (building || !builtTower) return false;
        var tower = builtTower;
        builtTower = null;
        PaidCost = 0;
        selected = hovered = placementHint = false;
        // OnDisable immediately removes Frost aura effects before end-of-frame destruction.
        tower.SetActive(false);
        Destroy(tower);
        if (clickArea) clickArea.enabled = true;
        RefreshIndicator();
        return true;
    }
}

