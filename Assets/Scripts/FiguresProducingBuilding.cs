using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

public class FiguresProducingBuilding : MonoBehaviour, IResourceReceiver
{
    [SerializeField] FiguresToBuild[] figuresToBuild;
    private GameObject mapManager;
    private List<string> resourcesIn = new();

    public void AddResource(string resource)
    {
        resourcesIn.Add(resource);
        FiguresToBuild toBuild = null;
        foreach (FiguresToBuild buildingToBuild in figuresToBuild)
        {
            if (buildingToBuild.resourcesNeeded != null && buildingToBuild.resourcesNeeded.SequenceEqual(resourcesIn))
            {
                toBuild = buildingToBuild;
                break;
            }
        }
        if (toBuild == null) return;

        mapManager = GameObject.FindGameObjectWithTag("MapManager");
        TilesGenerator tilesGenerator = mapManager.GetComponent<TilesGenerator>();
        TilesController tilesController = mapManager.GetComponent<TilesController>();

        Vector2Int[] directions =
            {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
            };

        Vector2Int point = tilesController.PositionToCordinats(transform.position);
        foreach (Vector2Int direction in directions)
        {
            Vector2Int position = tilesController.PositionToCordinats((Vector2)transform.position + direction);
            tilesGenerator.SpawnedTiles.TryGetValue(position, out TileConfig tile);
            if (tile == null) continue;
            if (tile.normalSpeedOn <= 0) continue;
            else
            {
                point = position;
                break;
            }
        }

        Instantiate(toBuild.buildingPrefab, tilesController.CoordinatesToPosition(point), Quaternion.identity);
    }

    [Serializable]
    private class FiguresToBuild
    {
        public GameObject buildingPrefab;
        public string[] resourcesNeeded;
    }
}
