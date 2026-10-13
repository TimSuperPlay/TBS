using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

public class BuildingFoundation : MonoBehaviour, IResourceReceiver
{
    [SerializeField] BuildingToBuild[] buildingsToBuild;
    private GameObject mapManager;
    private List<string> resourcesIn = new();

    public void AddResource(string resource)
    {
        Debug.Log("added resource to foundation");
        resourcesIn.Add(resource);
        BuildingToBuild toBuild = null;
        foreach (BuildingToBuild buildingToBuild in buildingsToBuild)
        {
            if (buildingToBuild.resourcesNeeded != null && buildingToBuild.resourcesNeeded.SequenceEqual(resourcesIn))
            {
                toBuild = buildingToBuild;
                break;
            }
        }
        if (toBuild == null) return;

        if (toBuild.tileName == null || toBuild.tileName == "")
        {
            Instantiate(toBuild.buildingPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }

        mapManager = GameObject.FindGameObjectWithTag("MapManager");
        TilesGenerator tilesGenerator = mapManager.GetComponent<TilesGenerator>();
        TilesController tilesController = mapManager.GetComponent<TilesController>();

        Vector2Int Cordinats = (tilesController.PositionToCordinats(transform.position.x, transform.position.y));
        TileConfig tileOn = tilesGenerator.SpawnedTiles[Cordinats];

        if (tileOn.tileName != toBuild.tileName) return;

        Instantiate(toBuild.buildingPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    [Serializable] private class BuildingToBuild
    {
        [Description("If empty can be build anywhere")] public string tileName;
        public GameObject buildingPrefab;
        public string[] resourcesNeeded;
    }
}
