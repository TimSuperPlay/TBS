using UnityEngine;

public class TerraformingBuilding : MonoBehaviour
{
    [SerializeField] TileConfig tileConfig;
    private TilesGenerator tilesGenerator;
    private TilesController tilesController;
    void Awake()
    {
        tilesGenerator = GameObject.FindWithTag("MapManager").GetComponent<TilesGenerator>();
        tilesController = GameObject.FindWithTag("MapManager").GetComponent<TilesController>();
        tilesGenerator.ReplaceTile(tilesController.PositionToCordinats(transform.position), tileConfig);
        //Destroy(gameObject);
    }
}
