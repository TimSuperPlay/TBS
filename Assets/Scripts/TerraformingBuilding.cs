using UnityEngine;

public class TerraformingBuilding : MonoBehaviour
{
    [SerializeField]
    private TileConfig tileConfig;
    private TilesGenerator tilesGenerator;
    private bool tileChanged;
    public TileConfig Config
    {
        get
        {
            return tileConfig;
        }
    }

    private void Update()
    {
        if (tileChanged || !LanGameState.CanSimulate)
        {
            return;
        }

        tilesGenerator = FindFirstObjectByType<TilesGenerator>();
        if (tilesGenerator == null || tileConfig == null)
        {
            return;
        }

        tilesGenerator.ReplaceWorldTile(transform.position, tileConfig);
        tileChanged = true;
    }
}
