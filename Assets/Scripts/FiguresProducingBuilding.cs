using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class FiguresProducingBuilding : MonoBehaviour, IResourceReceiver
{
    [SerializeField]
    [FormerlySerializedAs("figuresToBuild")]
    private FiguresToBuild[] figureRecipes;
    private GameObject mapManager;
    private List<string> storedResources = new();
    public string[] Inputs
    {
        get
        {
            return storedResources.ToArray();
        }
    }

    public void ApplyInputs(string[] receivedResources)
    {
        storedResources = new List<string>(receivedResources ?? Array.Empty<string>());
    }

    public IEnumerable<GameObject> Prefabs
    {
        get
        {
            return figureRecipes == null ? Enumerable.Empty<GameObject>() : figureRecipes.Select(recipe => recipe.buildingPrefab);
        }
    }

    public void AddResource(string resourceName)
    {
        if (!LanGameState.CanSimulate)
        {
            return;
        }

        storedResources.Add(resourceName);
        TryProduce();
    }

    private void Update()
    {
        if (LanGameState.CanSimulate && storedResources.Count > 0)
        {
            TryProduce();
        }
    }

    private void TryProduce()
    {
        if (figureRecipes == null)
        {
            return;
        }

        foreach (var recipe in figureRecipes)
        {
            if (!LanWorldReplicator.CanSpawn(recipe.buildingPrefab)
                || !LanWorldReplicator.HasResources(storedResources, recipe.resourcesNeeded))
            {
                continue;
            }

            var tilesController = FindFirstObjectByType<TilesController>();
            var tilesGenerator = FindFirstObjectByType<TilesGenerator>();
            if (tilesController == null || tilesGenerator == null)
            {
                return;
            }

            Vector2Int buildingCoordinates = tilesController.PositionToCordinats(transform.position);
            Vector2Int[] directions =
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            foreach (Vector2Int direction in directions)
            {
                Vector3 spawnPosition = tilesController.CoordinatesToPosition(buildingCoordinates + direction);
                if (!tilesGenerator.TryGetWorldTile(spawnPosition, out TileConfig tile) || tile.normalSpeedOn <= 0)
                {
                    continue;
                }

                bool cellOccupied = FindObjectsByType<TilesMovement>(FindObjectsSortMode.None)
                    .Any(existingFigure => (existingFigure.transform.position - spawnPosition).sqrMagnitude < .1f);
                if (cellOccupied)
                {
                    continue;
                }

                if (LanWorldReplicator.Spawn(recipe.buildingPrefab, spawnPosition, LanWorldReplicator.TeamOf(gameObject)) == null)
                {
                    return;
                }

                foreach (string recipeResource in recipe.resourcesNeeded)
                {
                    storedResources.Remove(recipeResource);
                }

                return;
            }
        }
    }

    [Serializable]
    private class FiguresToBuild
    {
        public GameObject buildingPrefab;
        public string[] resourcesNeeded;
    }
}
